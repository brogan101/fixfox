# Launch Recovery Report

Generated: 2026-03-06

## Scope
Recover from "app won't open" startup failures by reproducing the stall, identifying root causes, fixing startup path blocking, and adding guardrails/tests.

## Reproduction Evidence

### Baseline startup failure signature
- Normal startup showed long pre-paint delay and appeared hung from user perspective.
- Baseline instrumented log: `logs/launch_debug_20260306_084930.txt`
  - `startup_first_paint_ms=6333.6`
  - Qt warnings included `Could not parse application stylesheet`
  - MainWindow constructor blocked UI thread for multiple seconds before first paint.

### Post-fix launch evidence
- Recovery launch log: `logs/launch_debug_20260306_090401.txt`
  - `startup_first_paint_ms=1873.5`
  - No stylesheet parse warning markers
  - No `STARTUP STALLED` marker

## Root Cause Tree

### Primary cause
1. **UI-thread blocking startup path in `MainWindow.__init__`**
- Theme/density + initial data refresh logic executed synchronously before first paint.
- This delayed window rendering enough to look like launch failure.

### Contributing causes
1. **Constructor-time theme re-application**
- App stylesheet was already applied in bootstrap, then applied again during shell construction.
2. **Runbook auto-selection opened details panel during warmup**
- Startup list-selection callbacks opened side sheet without user intent.
3. **Runtime QSS parse warning**
- Combo arrow URL in QSS used unquoted `url(...)` with encoded path (`%20`), triggering parse warnings.
4. **No startup watchdog/phase telemetry**
- Stall location was opaque; interrupt stack often landed in unrelated event routing.

## Fixes Applied

1. **Startup watchdog + Qt message handler**
- Added `src/core/startup_watchdog.py`.
- 250ms launch watchdog ticks, startup phase markers, stall marker + thread stack dump.
- Captures Qt warnings/errors, including stylesheet parse failures.

2. **Early startup wiring**
- Hooked watchdog into `src/app.py` before heavy startup phases.
- Added phase markers across DB init, QApplication setup, stylesheet, shell build/show, event-loop entry/exit.

3. **Launch-path non-blocking restructure**
- Deferred onboarding display with `QTimer.singleShot(0, ...)`.
- Deferred constructor-time theme application.
- Added staged post-launch warmup (`_startup_warmup_stage_one/two/three`).

4. **Details panel startup behavior fixed**
- Prevented automatic details opening from startup runbook selection flows.
- Side sheet now remains hidden by default until explicit user action.

5. **eventFilter guardrails**
- Added reentrancy guard to `eventFilter` to prevent recursive handling loops.
- Kept eventFilter logic lightweight.

6. **QSS parse fix**
- In `src/ui/style/qss_builder.py`, changed combo arrow rule to quoted URL:
  - `image: url("file:///...")`
- Removed runtime `Could not parse application stylesheet` warnings.

7. **Search startup hardening**
- Added async static cache warm helper in `src/core/search.py` (`warm_static_index_async`).
- Warmed search caches in startup warmup without keystroke-time rebuilds.

8. **Launch regression test added**
- Added `src/tests/test_app_launch.py`:
  - offscreen launch
  - event processing window
  - fails on stylesheet parse/unknown-property warnings
  - verifies main window survives startup

9. **QSS sanity gate strengthened**
- Updated `scripts/qss_sanity_check.py` to apply stylesheet and create/show probe widgets so runtime parse issues are caught.

10. **Logging target requirement met**
- Updated logging to write rotating logs to:
  - appdata logs (`fixfox.log`)
  - repo-local `logs/fixfox.log`
  - console stream for dev runs

## Verification Results

### Launch timing + watchdog
- `logs/launch_debug_20260306_090401.txt`: first paint 1873.5 ms
- `logs/fixfox.log`: startup phase + watchdog trace present

### UI walkthrough proof
- Launch recovery screenshot bundle:
  - `docs/screenshots/launch_recovery_20260306_091439/`

### Gate/test outcomes
- `py -3 scripts/qss_sanity_check.py` -> PASS
- `py -3 scripts/verify_requirements.py` -> PASS (`TTFP(ms): 4486.5`)
- `py -3 scripts/ui_walkthrough.py` -> PASS
- `py -3 -m src.tests.smoke` -> PASS
- `py -3 -m src.tests.test_unit` -> PASS
- `py -3 -m src.tests.test_app_launch` -> PASS
- `py -3 -m pytest -q` -> skipped (`pytest` not installed)

## Conclusion
Startup reliability is recovered. The app launches and renders quickly (<5s first paint measured), stylesheet parse warnings are eliminated, startup stalls are instrumented with watchdog diagnostics, and launch regression coverage is now enforced.
