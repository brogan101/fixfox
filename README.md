# FixFox

FixFox is a local-first Windows support desktop app for help-desk and endpoint troubleshooting workflows.

## Run

```powershell
py -m venv .venv
.venv\Scripts\python.exe -m pip install -r requirements.txt
.venv\Scripts\python.exe src\app.py
```

## What Is In Scope

- 200 issue classes across shared support families
- script-backed deep playbooks for the highest-value support scenarios
- support issue search, family drill-down, diagnostics, fixes, reports, history, and bundle/export context
- local-first evidence capture and support-ready summaries

## Proof / Verification

- `scripts\support_audit.py` builds support coverage and execution proof into `docs\support_audit.json`
- `scripts\ui_walkthrough.py` generates screenshots into `docs\screenshots\<timestamp>\`
- `python -m unittest src.tests.test_support_catalog_integrity src.tests.test_search_support_discovery`

## Release Notes

- Current app train: `0.9.0-rc2 (RC)`
- Channel: `release-candidate`
- Build date: `2026-03-09`
