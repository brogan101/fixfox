# Next Level Reports (Offline Interactive Report)

## Module
- `src/core/report.py`

## Design goals
- Keep report output static/offline (`report/report.html`) with no server/runtime dependency.
- Improve readability and triage speed for support handoff.
- Preserve share-safe masking guarantees from exporter pipeline.

## Structure
`render_html(session, icon_rel_path)` now generates:

1. Header and summary card
- Session ID
- Symptom
- Finding count

2. Interactive controls (client-side JS)
- Finding text search
- Severity filter
- Category filter
- Copy Ticket Summary button

3. Collapsible sections (`<details>`)
- Findings table
- Actions list
- Evidence list

4. Embedded JSON payload
- Stored in `<script type="application/json" id="ff-data">...`
- Parsed by inline JS to render/filter without network calls.

## Interactivity details
- Filter logic applies in-browser against embedded findings.
- Finding count updates live with current filter.
- Copy Ticket Summary uses Clipboard API with a fallback textarea copy path.

## Masking and safety
- Export flow masks session content before report generation:
  - `src/core/exporter.py` -> `_safe_session_payload(...)`
- `render_html(...)` consumes already-masked payload.
- No external scripts/styles are fetched.

## Extending safely
When adding new fields:
1. Add masked fields to exporter payload first.
2. Add fields to report embedded payload object.
3. Render only escaped values in HTML (`textContent` or escaped strings).
4. Keep all behavior local/offline.

## Compatibility
- Existing export folder/manifest/hashes flow remains unchanged.
- Report remains a single static HTML file under `report/report.html`.
