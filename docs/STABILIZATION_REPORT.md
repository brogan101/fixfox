# FixFox Stabilization Report

Generated: 2026-03-06

## Scope

Recovery pass focused on launch reliability and UI responsiveness:

1. App opens reliably and stays interactive.
2. Search does not block UI and does not rebuild static index per keystroke.
3. QSS parse/asset/font warnings are treated as fatal in gates.
4. Regression gates upgraded (launch, QSS sanity, nonblocking search, smoke walkthrough).

## Reproduction Evidence

- Baseline launch run: `logs/launch_debug_20260306_095048.txt`
- First paint evidence:
  - `startup_first_paint_ms=1968.3`
- Perf artifact:
  - `logs/perf_20260306_095056.json`
- Watchdog artifact:
  - `logs/launch_watchdog_20260306_095048.txt`
- Qt message artifact:
  - `logs/fixfox_qt.log`
- QSS sanity artifact:
  - `docs/qss_sanity_report.txt`

## Root Cause Tree

### Primary Cause 1: Qt warning gate blind spot (false PASS)
- Symptom:
  - Runtime emitted warning for combo arrow icon path resolution while QSS sanity still passed.
- Evidence:
  - Previous warning: `Cannot open file ... chevron_down.svg` from QSS sanity output.
- Fix:
  - `src/ui/style/qss_builder.py`: corrected combo arrow URL path emission.
  - `scripts/qss_sanity_check.py`: fatal-warning policy now enforced.
  - `docs/qt_warnings_policy.md`: explicit fatal warning policy.

### Primary Cause 2: Startup diagnostics and freeze observability were insufficient
- Symptom:
  - Startup watchdog ran but did not provide strict Qt warning policy or dedicated artifacts.
- Fix:
  - `src/core/startup_watchdog.py`:
    - explicit `mark/start/stop` API
    - startup watchdog log file `logs/launch_watchdog_<timestamp>.txt`
    - Qt warning log file `logs/fixfox_qt.log`
    - fatal Qt warning classification and logging
    - no unbounded startup tick spam after first paint
  - `src/core/ui_freeze_detector.py`:
    - 100ms heartbeat + >500ms freeze detection + stack dump markers

### Primary Cause 3: Startup/UI work scheduling caused event-loop stalls
- Symptom:
  - `processEvents()` could block long enough to feel hung during launch interactions.
- Contributing factors:
  - eager startup warmup touching multiple heavy refresh paths
  - synchronous status probe work in startup path
- Fixes:
  - `src/ui/main_window_impl.py`:
    - startup warmup reduced to lightweight non-blocking path
    - heavy page refreshes deferred until page navigation
    - startup status probe moved to worker thread (`TaskWorker`)
    - startup background-loading banner added (`Loading background data...`)
    - eventFilter fast-path + offscreen guard

### Primary Cause 4: Verifier/test reliability gaps
- Symptom:
  - verifier command execution was brittle in Windows alias setups and some checks were permissive.
- Fixes:
  - `scripts/verify_requirements.py`:
    - robust Python interpreter resolver (avoids WindowsApps alias trap)
    - strict sub-gates for:
      - `src.tests.test_app_launch`
      - `src.tests.test_qss_sanity`
      - `src.tests.test_search_nonblocking`
      - `scripts/ui_smoke_walkthrough.py`
    - corrected settings-overlap runtime check logic
    - stricter TTFP threshold (`<= 5000ms`)

## Search Stabilization

- `src/core/search.py` contract remains cached/static-safe:
  - `query_index()` does not rebuild static index.
  - dynamic rows refresh asynchronously.
- `src/ui/main_window_impl.py`:
  - async worker search dispatch with cancellation retained.
  - request timing tracked and written to perf metrics (`search.time_to_results_ms`).
- Added regression test:
  - `src/tests/test_search_nonblocking.py`

## New/Updated QA Gates

- `scripts/qss_sanity_check.py`
- `src/tests/test_qss_sanity.py`
- `src/tests/test_app_launch.py`
- `src/tests/test_search_nonblocking.py`
- `scripts/ui_smoke_walkthrough.py`
- `scripts/verify_requirements.py` strict gate orchestration

## Final Gate Results (this pass)

- `python scripts/qss_sanity_check.py` -> PASS
- `python -m src.tests.test_app_launch` -> PASS
- `python -m src.tests.test_qss_sanity` -> PASS
- `python -m src.tests.test_search_nonblocking` -> PASS
- `python scripts/verify_requirements.py` -> PASS
- `python scripts/ui_walkthrough.py` -> PASS
- `python scripts/ui_smoke_walkthrough.py` -> PASS
- Screenshot evidence:
  - `docs/screenshots/20260306_094846/`
  - `docs/screenshots/stability_20260306_095031/`

## Notes

- Canonical runtime branding path remains `src/assets/brand/*` (README/build/runtime checks aligned).
- Launch and interaction telemetry now emits dedicated artifacts for future regression triage.
