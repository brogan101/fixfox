# Dev Run Guide

## Setup
```powershell
scripts/setup_venv.ps1
```

## Run (python-first)
```powershell
scripts/run.ps1
```

Direct run:
```powershell
python -m src.app
```

## Tests
```powershell
python -m src.tests.smoke
python -m unittest src.tests.test_unit -v
```

## Data Paths
- Sessions: `%LOCALAPPDATA%\\PCConcierge\\sessions`
- Exports: `%LOCALAPPDATA%\\PCConcierge\\exports`
- Logs: `%LOCALAPPDATA%\\PCConcierge\\logs`

## Catalog generation
```powershell
python scripts/generate_catalogs.py
```
