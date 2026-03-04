# Fix Fox Release Audit

Date: 2026-03-03  
Version target: `0.9.0-beta1`

## 1) Repo Tree (Top Level)
- `.venv/`
- `assets/`
- `docs/`
- `packaging/`
- `scripts/`
- `src/`
- `tools/`
- `README.md`
- `requirements.txt`

## 2) Feature Inventory
- UI pages (7): Home, Playbooks, Diagnose, Fixes, Reports, History, Settings.
- Tool execution surface: ToolRunner (workers, progress, cancel, timeout, summaries).
- Script tasks: 82 tasks (`src/core/script_tasks.py`).
- Runbooks: 15 runbooks (`src/core/runbooks.py`).
- Export presets: `home_share`, `ticket`, `full`.
- Evidence bundles:
  - system snapshot
  - network bundle
  - updates bundle
  - crash bundle
  - event logs bundle (`.evtx` best effort)

## 3) Marker Scan (Pending-Work Tokens)
Scan command:
```powershell
rg -n "<pending-work-markers>" src docs scripts packaging
```
Result:
- No pending-work markers in active product code/docs for release scope.

## 4) Known Bugs and Resolution Plan
- Build/packaging runtime import mode:
  - Status: fixed in `src/app.py` (module + script/frozen-compatible import loading).
- Windows environment command alias instability (`python` / `py`):
  - Status: handled in QA by using `.venv\Scripts\python.exe`.
- EVTX export reliability on locked-down hosts:
  - Status: best-effort by design; summarized in bundle warnings and non-blocking where appropriate.

## 5) UI Consistency Checklist
- Nav count <= 7: pass.
- Master->detail pattern across product surfaces: pass.
- Right panel used as context/detail (not duplicate control wall): pass.
- Reports stepper (3-step): pass.
- ToolRunner is run output surface: pass.
- Context bar session-aware collapse/hint behavior: pass.

## 6) Export Validation Checklist
- Required files generated:
  - `report/report.html`
  - `report/summary.md`
  - `report/findings.csv`
  - `data/session.json`
  - `logs/actions.txt`
  - `logs/diagnostics.txt`
  - `manifest/manifest.json`
  - `manifest/hashes.txt`
- Manifest-vs-disk reconciliation: pass in smoke.
- Hash coverage validation: pass in smoke.
- Share-safe scan enforcement:
  - default path blocks on validation failures.
  - explicit override path added with confirmation (`Generate (Allow Warnings)`).

## 7) QA Gate Status
- `python -m src.tests.smoke`: pass (via `.venv\Scripts\python.exe -m src.tests.smoke`).
- PyInstaller build: pass (`scripts/build_exe.ps1` -> `dist/FixFox.exe`).
- Seller bundle prepared: pass (`release/FixFox_v0.9.0-beta1/`).
- Tooling depth checks:
  - Wi-Fi wizard artifacts: pass
  - Storage radar + downloads plan: pass
  - Performance sample artifacts: pass
  - Browser rescue summary: pass
  - Printer summary + logs: pass
- UI layout checks: pass with scrollable surfaces at min/1080p/wide windows.
