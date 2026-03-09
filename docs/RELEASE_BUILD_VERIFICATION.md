# Release Build Verification

## 2026-03-09

### Build

- Command: `python scripts/build_release.py`
- Result: PASS
- Output: `dist/FixFox/FixFox.exe`

### Packaged launch smoke

- Command: launch `dist/FixFox/FixFox.exe` with `FIXFOX_AUTO_EXIT_MS=4000`
- Result: PASS
- Branding/assets: bundled icon and `assets` tree were present under `dist/FixFox/_internal/assets`
- About/build metadata source: `APP_VERSION_LABEL`, `APP_CHANNEL`, and `APP_BUILD_DATE`

### Remaining packaged-path issue

- Result: NOT CLEAN
- Evidence: packaged run logged `UI FREEZE DETECTED stalled_ms=764.9`
- Perf report: `logs/perf_20260309_133824.json`
- Watchdog log: `logs/launch_watchdog_20260309_133807.txt`

The EXE builds and launches, but the packaged warmup path still needs one stabilization pass before it can be called fully release-clean.

### Packaged warmup stabilization

- Command: rebuild with `python scripts/build_release.py`, then launch `dist/FixFox/FixFox.exe` with `FIXFOX_AUTO_EXIT_MS=4000`
- Result: PASS
- Fixes applied:
  - shutdown settings persistence no longer forces database sync from `closeEvent`
  - startup page refreshes are deferred until warmup drains instead of rendering heavy views during the packaged warmup path
- Proof:
  - Perf report: `logs/perf_20260309_135347.json`
  - Watchdog log: `logs/launch_watchdog_20260309_135332.txt`
- Outcome:
  - `ui_freeze_count = 0`
  - no `UI FREEZE DETECTED` entry in the packaged run
  - packaged launch is now release-clean for this smoke path
