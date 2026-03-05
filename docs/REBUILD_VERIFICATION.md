# Rebuild Verification (2026-03-05)

## Run context
- Branch: `main`
- Start commit for this run: `ca9fed31502dc2696c92688c65a7d28a6708d677`
- Branding checkpoint commit: `e1ec205...`
- Final walkthrough evidence folder: `docs/screenshots/20260305_144413`

## Commands executed
```text
git fetch --all
git status --porcelain
git status
git rev-parse HEAD
.\.venv\Scripts\python.exe --version
.\.venv\Scripts\python.exe -m pip --version
Get-ChildItem

.\.venv\Scripts\python.exe scripts/ui_audit.py
.\.venv\Scripts\python.exe scripts/ui_smoke_check.py
.\.venv\Scripts\python.exe scripts/ui_walkthrough.py
.\.venv\Scripts\python.exe -m src.tests.smoke
.\.venv\Scripts\python.exe -m src.tests.test_unit
.\.venv\Scripts\python.exe -m src.tests.test_requirements_gate
```

## Walkthrough artifacts
- Manifest: `docs/screenshots/20260305_144413/MANIFEST.json`
- Clipping report: `docs/screenshots/20260305_144413/clipping_report.txt`
- Search persistence: `search_dropdown_visible_ms = 707`
- Clipping issues: `0`
- Failure count: `0`

## Screenshot links
- `docs/screenshots/20260305_144413/1024x768_1_home.png`
- `docs/screenshots/20260305_144413/1024x768_2_playbooks.png`
- `docs/screenshots/20260305_144413/1024x768_3_diagnose.png`
- `docs/screenshots/20260305_144413/1024x768_4_fixes.png`
- `docs/screenshots/20260305_144413/1024x768_5_reports.png`
- `docs/screenshots/20260305_144413/1024x768_6_history.png`
- `docs/screenshots/20260305_144413/1024x768_7_settings.png`
- `docs/screenshots/20260305_144413/1280x720_1_home.png`
- `docs/screenshots/20260305_144413/1280x720_2_playbooks.png`
- `docs/screenshots/20260305_144413/1280x720_3_diagnose.png`
- `docs/screenshots/20260305_144413/1280x720_4_fixes.png`
- `docs/screenshots/20260305_144413/1280x720_5_reports.png`
- `docs/screenshots/20260305_144413/1280x720_6_history.png`
- `docs/screenshots/20260305_144413/1280x720_7_settings.png`
- `docs/screenshots/20260305_144413/1600x900_1_home.png`
- `docs/screenshots/20260305_144413/1600x900_2_playbooks.png`
- `docs/screenshots/20260305_144413/1600x900_3_diagnose.png`
- `docs/screenshots/20260305_144413/1600x900_4_fixes.png`
- `docs/screenshots/20260305_144413/1600x900_5_reports.png`
- `docs/screenshots/20260305_144413/1600x900_6_history.png`
- `docs/screenshots/20260305_144413/1600x900_7_settings.png`
- `docs/screenshots/20260305_144413/search_dropdown_open.png`
- `docs/screenshots/20260305_144413/details_side_sheet_open.png`
- `docs/screenshots/20260305_144413/tool_runner_quick_check.png`

## PASS/FAIL checklist
- [x] Branding source normalized at `src/assets/brand/fixfox_logo_source.png`.
- [x] No double-extension source logo.
- [x] Search dropdown remains visible for >=500ms.
- [x] Details panel opens from visible button and closes correctly.
- [x] No shell `QSplitter`.
- [x] No clipping at 1024x768, 1280x720, 1600x900.
- [x] Tool Runner screenshot captured in walkthrough.
- [x] Smoke tests pass.
- [x] Unit tests pass.
- [x] Requirements gate passes.

## Notes
- `pip --version` may not resolve via shell PATH in this environment; `.venv` and `python -m pip` are used for deterministic command execution.
