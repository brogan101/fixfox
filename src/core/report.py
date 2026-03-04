"""Compatibility wrappers for export pipeline."""

from __future__ import annotations

from pathlib import Path
from typing import Any

from .brand import APP_TAGLINE, REPORT_TITLE


def render_html(session: dict[str, Any], icon_rel_path: str = "brand/fixfox.png") -> str:
    summary = str(session.get("symptom", "Quick Check"))
    session_id = str(session.get("session_id", "unknown"))
    findings = session.get("findings", [])
    finding_count = len(findings) if isinstance(findings, list) else 0
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
      <p><strong>Session ID:</strong> {session_id}</p>
      <p><strong>Symptom:</strong> {summary}</p>
      <p><strong>Findings:</strong> {finding_count}</p>
    </div>
  </body>
</html>"""


def write_support_pack(session: dict[str, Any], export_name: str | None = None) -> Path:
    from .exporter import export_session

    result = export_session(session=session, preset="home_share", share_safe=True, mask_ip=False)
    return result.zip_path
