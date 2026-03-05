# Fix Fox

Local desktop helpdesk app built with Python and PySide6.

## Run (python-first)

Create and activate a virtual environment, install dependencies, then run:

```powershell
py -m venv .venv
.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
python -m src.app
```

## Tests

```powershell
python -m src.tests.smoke
python -m src.tests.test_unit
```

## Build EXE

```powershell
scripts/build_exe.ps1
```

`scripts/build_exe.ps1` builds `dist/FixFox.exe` with:
- icon: `src/assets/branding/fixfox.ico`
- version metadata generated from `src/core/version.py`
- bundled customer docs in `dist/docs`
- bundled license/disclaimer in `dist/licenses`

## Prepare Seller Release Bundle

```powershell
scripts/prepare_release_bundle.ps1
```

Output:
- `release/FixFox_vX.Y.Z/`
  - `FixFox.exe` (if built)
  - `docs/`
  - `licenses/`
  - `scripts/`
  - `samples/`

## Brand Icons

Generate all runtime/build icons from the source PNG:

```powershell
py tools/make_icons.py
```

Outputs:
- `assets/branding/png/fixfox_*.png` (16/24/32/48/64/128/256/512)
- `assets/branding/fixfox.ico` (multi-layer)
- mirrored runtime assets at `src/assets/branding/`

## Data Paths

- Sessions: `%LOCALAPPDATA%\FixFox\sessions` (legacy `%LOCALAPPDATA%\PCConcierge` is still read if present)
- Exports: `%LOCALAPPDATA%\FixFox\exports`
- Logs: `%LOCALAPPDATA%\FixFox\logs`

## Typography

- App UI bundles `Noto Sans` from `src/assets/fonts/NotoSans-Regular.ttf`.
- License details: `docs/font-license.md` and `src/assets/fonts/LICENSE-Noto.txt`.

## Support Pack Export Flow

- Use Reports stepper:
  1. Configure preset and masking
  2. Preview redaction and evidence checklist
  3. Generate validated export
- Ticket preset includes system/network/updates/crash/eventlogs evidence bundle directories by default.

## Generated Catalogs

```powershell
python -m scripts.generate_catalogs
```
