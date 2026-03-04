# Testing Guide

## Prerequisites
- Windows environment with required dependencies installed.
- Run from repository root.

## Automated Tests

### Smoke
```powershell
python -m src.tests.smoke
```

Smoke now validates:
- Home runbook dry-run: `home_fix_wifi_safe`.
- Home tool run: `task_storage_radar` (checks artifact creation, including `storage_radar.csv`).
- Evidence collectors: `system` + `network` bundles create files.
- Ticket export validation (manifest + hashes + required structure).
- Share-safe masking in exported summary/session content.

### Unit
```powershell
python -m unittest src.tests.test_unit
```

Unit coverage includes:
- masking patterns (`C:\Users\...`, `DESKTOP-...`, SSID token, IP masking)
- export validator contract
- runbook dry-run checkpoint basics
- error mapping includes user message + next steps
- layout guardrail collapse threshold logic
- duplicate scan report creation in test mode

## Manual UI Verification

### Launch
```powershell
python -m src.app
```

### ToolRunner Everywhere
1. Run one safe fix in `Fixes`.
2. Run one script task in `Playbooks -> Tools`.
3. Run one Home runbook and one IT runbook from `Playbooks -> Runbooks`.
4. Confirm each opens ToolRunner with progress, logs, summary, and next steps.

### Evidence + Exports
1. Generate core evidence (`Collect Core Evidence` action or runbook path).
2. Confirm `Reports -> Evidence Items` shows files.
3. Export `ticket` preset.
4. Verify export structure:
   - `report/`, `data/`, `logs/`, `evidence/`, `manifest/`
5. Open `report/report.html` and inspect `manifest/manifest.json` + `manifest/hashes.txt`.

### UI Reliability
1. Resize app down to minimum window size.
2. Confirm primary actions are not clipped.
3. Confirm right panel auto-collapses on narrow width.
4. Toggle layout debug overlay with `Ctrl+Alt+L`.

## Notes
- Direct run guard: `python src/app.py` prints a friendly message and exits.
- Canonical run path: `python -m src.app`.
