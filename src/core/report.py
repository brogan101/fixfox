"""Compatibility wrappers for export pipeline."""

from __future__ import annotations

import html
import json
from pathlib import Path
from typing import Any

from .brand import APP_TAGLINE, REPORT_TITLE


def _normalize_user_path(text: str) -> str:
    out = str(text or "")
    out = out.replace("C:\\Users\\<user>", "<user-path>")
    out = out.replace("C:\\Users\\", "<user-path>\\")
    return out


def render_html(session: dict[str, Any], icon_rel_path: str = "brand/fixfox_mark.png") -> str:
    summary = str(session.get("symptom", "Quick Check"))
    session_id = str(session.get("session_id", "unknown"))
    findings = session.get("findings", []) if isinstance(session.get("findings", []), list) else []
    actions = session.get("actions", []) if isinstance(session.get("actions", []), list) else []
    evidence_rows = session.get("evidence", {}).get("files", []) if isinstance(session.get("evidence", {}), dict) else []
    finding_count = len(findings)
    categories = sorted({str(row.get("category", "General")) for row in findings if isinstance(row, dict)})
    summary_lines = [f"Session {session_id}: {summary}. Findings: {finding_count}."]
    for row in findings[:6]:
        if not isinstance(row, dict):
            continue
        summary_lines.append(
            f"- {str(row.get('status', 'INFO')).upper()}: {str(row.get('title', ''))} - {str(row.get('plain', row.get('detail', '')))}"
        )
    ticket_summary = _normalize_user_path("\n".join(summary_lines))
    payload = {
        "session_id": session_id,
        "summary": summary,
        "findings": findings,
        "actions": actions,
        "evidence": evidence_rows,
        "categories": categories,
        "ticket_summary": ticket_summary,
    }
    payload_json = json.dumps(payload).replace("</", "<\\/")
    category_options = "".join([f"<option value=\"{html.escape(cat)}\">{html.escape(cat)}</option>" for cat in categories])
    return f"""<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <title>{REPORT_TITLE}</title>
    <style>
      body {{
        font-family: "Segoe UI", Arial, sans-serif;
        margin: 0;
        padding: 24px;
        background: #0E111A;
        color: #E9EDF5;
      }}
      .header {{
        display: flex;
        align-items: center;
        gap: 14px;
        margin-bottom: 18px;
      }}
      .header img {{
        width: 40px;
        height: 40px;
      }}
      .tag {{
        color: #AAB3C5;
      }}
      .card {{
        border: 1px solid #2A344A;
        border-radius: 12px;
        padding: 16px;
        background: #141A26;
        margin-bottom: 12px;
      }}
      .row {{
        display: flex;
        gap: 8px;
        flex-wrap: wrap;
        align-items: center;
      }}
      input, select, button {{
        background: #1B2333;
        color: #E9EDF5;
        border: 1px solid #2A344A;
        border-radius: 8px;
        padding: 6px 10px;
      }}
      table {{
        width: 100%;
        border-collapse: collapse;
      }}
      th, td {{
        border-bottom: 1px solid #2A344A;
        padding: 8px;
        text-align: left;
        vertical-align: top;
      }}
      .sev-CRIT {{ color: #FF6B6B; font-weight: 700; }}
      .sev-WARN {{ color: #FFB020; font-weight: 700; }}
      .sev-OK {{ color: #4BCB90; font-weight: 700; }}
      .sev-INFO {{ color: #77A7FF; font-weight: 700; }}
      details {{
        border: 1px solid #2A344A;
        border-radius: 10px;
        background: #141A26;
        margin-bottom: 12px;
        padding: 8px 12px;
      }}
      summary {{
        cursor: pointer;
        font-weight: 700;
      }}
      .mono {{
        font-family: Consolas, "Courier New", monospace;
        white-space: pre-wrap;
      }}
    </style>
  </head>
  <body>
    <div class="header">
      <img src="{icon_rel_path}" alt="Fix Fox" />
      <div>
        <h1 style="margin:0;">{REPORT_TITLE}</h1>
        <div class="tag">{APP_TAGLINE}</div>
      </div>
    </div>
    <div class="card">
      <p><strong>Session ID:</strong> {html.escape(_normalize_user_path(session_id))}</p>
      <p><strong>Symptom:</strong> {html.escape(_normalize_user_path(summary))}</p>
      <p><strong>Findings:</strong> <span id="count">{finding_count}</span></p>
      <div class="row">
        <input id="query" placeholder="Search findings" />
        <select id="severity">
          <option value="">All severities</option>
          <option value="CRIT">CRIT</option>
          <option value="WARN">WARN</option>
          <option value="OK">OK</option>
          <option value="INFO">INFO</option>
        </select>
        <select id="category">
          <option value="">All categories</option>
          {category_options}
        </select>
        <button id="copyTicket" type="button">Copy Ticket Summary</button>
      </div>
    </div>
    <details open>
      <summary>Findings</summary>
      <div class="card">
        <table id="findingsTable">
          <thead>
            <tr>
              <th>Severity</th>
              <th>Category</th>
              <th>Title</th>
              <th>Summary</th>
            </tr>
          </thead>
          <tbody></tbody>
        </table>
      </div>
    </details>
    <details>
      <summary>Actions</summary>
      <div class="card mono" id="actions"></div>
    </details>
    <details>
      <summary>Evidence</summary>
      <div class="card mono" id="evidence"></div>
    </details>
    <script id="ff-data" type="application/json">{payload_json}</script>
    <script>
      const DATA = JSON.parse(document.getElementById('ff-data').textContent);
      const tbody = document.querySelector('#findingsTable tbody');
      const count = document.getElementById('count');
      const query = document.getElementById('query');
      const severity = document.getElementById('severity');
      const category = document.getElementById('category');

      function escapeHtml(value) {{
        const span = document.createElement('span');
        span.textContent = String(value ?? '');
        return span.innerHTML;
      }}

      function renderFindings() {{
        const q = query.value.trim().toLowerCase();
        const sev = severity.value;
        const cat = category.value;
        const rows = (DATA.findings || []).filter((row) => {{
          const status = String(row.status || row.severity || 'INFO').toUpperCase();
          const categoryText = String(row.category || 'General');
          const blob = `${{row.title || ''}} ${{row.plain || row.detail || ''}} ${{categoryText}} ${{status}}`.toLowerCase();
          if (sev && status !== sev) return false;
          if (cat && categoryText !== cat) return false;
          if (q && !blob.includes(q)) return false;
          return true;
        }});
        tbody.innerHTML = rows.map((row) => {{
          const status = String(row.status || row.severity || 'INFO').toUpperCase();
          const cat = String(row.category || 'General');
          const title = String(row.title || '');
          const plain = String(row.plain || row.detail || '');
          return `<tr><td class="sev-${{escapeHtml(status)}}">${{escapeHtml(status)}}</td><td>${{escapeHtml(cat)}}</td><td>${{escapeHtml(title)}}</td><td>${{escapeHtml(plain)}}</td></tr>`;
        }}).join('');
        count.textContent = String(rows.length);
      }}

      function renderList(targetId, rows, labelBuilder) {{
        const target = document.getElementById(targetId);
        if (!rows || !rows.length) {{
          target.textContent = 'None.';
          return;
        }}
        target.textContent = rows.map(labelBuilder).join('\\n');
      }}

      renderFindings();
      renderList('actions', DATA.actions || [], (row) => `${{row.title || row.key || 'Action'}} | code=${{row.code ?? '?'}} | risk=${{row.risk || 'Safe'}}`);
      renderList('evidence', DATA.evidence || [], (row) => `${{row.category || 'evidence'}} | ${{row.path || ''}}`);

      query.addEventListener('input', renderFindings);
      severity.addEventListener('change', renderFindings);
      category.addEventListener('change', renderFindings);
      document.getElementById('copyTicket').addEventListener('click', async () => {{
        const text = String(DATA.ticket_summary || '');
        try {{
          await navigator.clipboard.writeText(text);
        }} catch (_err) {{
          const area = document.createElement('textarea');
          area.value = text;
          document.body.appendChild(area);
          area.select();
          document.execCommand('copy');
          area.remove();
        }}
      }});
    </script>
  </body>
</html>"""


def write_support_pack(session: dict[str, Any], export_name: str | None = None) -> Path:
    from .exporter import export_session

    result = export_session(session=session, preset="home_share", share_safe=True, mask_ip=False)
    return result.zip_path
