# Rebuild Verification (2026-03-05)

## Run context
- Branch: `main`
- Start commit for this pass: `2edb99d2875b080d0df81683ba59410f41c7ceaa`
- Final walkthrough evidence folder: `docs/screenshots/20260305_153732`

## Commands executed
```text
git fetch --all
git status --porcelain
git status
git rev-parse HEAD
python --version
.\.venv\Scripts\python.exe -m pip --version

.\.venv\Scripts\python.exe scripts/ui_walkthrough.py
.\.venv\Scripts\python.exe -m src.tests.smoke
.\.venv\Scripts\python.exe -m src.tests.test_unit
.\.venv\Scripts\python.exe -m src.tests.test_requirements_gate

python scripts/ui_walkthrough.py
python -m src.tests.smoke
python -m src.tests.test_unit
```

## Walkthrough artifacts
- Manifest: `docs/screenshots/20260305_153732/MANIFEST.json`
- Clipping report: `docs/screenshots/20260305_153732/clipping_report.txt`
- Search persistence: `search_dropdown_visible_ms = 704`
- Clipping issues: `0`
- Failure count: `0`

## PASS/FAIL checklist
- [x] Branding source normalized at `src/assets/brand/fixfox_logo_source.png`.
- [x] No double-extension logo source.
- [x] Search dropdown persists >=500ms.
- [x] Details side sheet opens/closes from visible controls.
- [x] No `QSplitter` in app shell.
- [x] No clipping at 1024x768, 1280x720, 1600x900.
- [x] Tool Runner screenshot generated during walkthrough.
- [x] Smoke tests pass.
- [x] Unit tests pass.
- [x] Requirements gate test passes.
