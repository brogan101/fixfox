# Stabilization Repair Report

Generated: 2026-03-06 12:13:00 -05:00

## Root Causes Found
- Settings sidebar overlap was current and reproducible.
  - Cause: `src/ui/pages/settings_page.py` used `QListWidget.setItemWidget(...)` row widgets while still keeping item text/icon metadata, which produced duplicate/clipped rendering under resize/scaling.
- Run status updates were timer-polled and repolished continuously.
  - Cause: `src/ui/main_window_impl.py` ran a 250ms status timer at all times and forced `unpolish/polish` on every status refresh.
- Resize/fullscreen handling mutated layout directly inside `resizeEvent`.
  - Cause: responsive concierge/header work ran on every resize step instead of using a short debounce.
- Walkthrough/verifier gates were too weak.
  - Cause: they only looked at clipping/manifest success and did not fail on font sanity issues, runtime icon failures, or Qt warning output.
- Generated artifacts were tracked in git.
  - Cause: screenshot folders and logs were not ignored, so proof runs dirtied the repo immediately.
- Offscreen walkthroughs emitted large volumes of Qt paint-effect warnings.
  - Cause: opacity animations were still active in offscreen/minimal test environments where Qt graphics effects are unstable.

## Fixes Applied
- Rebuilt Settings nav with native `QListWidgetItem` rows.
  - Added icon size, text elide, uniform item sizing, and disabled horizontal scrolling.
  - Removed `setItemWidget(...)` usage from the Settings sidebar path.
- Reworked run status handling to be event-driven.
  - Status now updates immediately from run bus events.
  - Spinner animation runs only while a task is active.
  - Status chip repolish occurs only when the semantic kind changes.
- Debounced responsive layout work.
  - `resizeEvent` now schedules a short single-shot layout refresh instead of mutating the shell on every resize tick.
- Hardened font/bootstrap behavior.
  - Real windowed Windows runs prefer the system UI font (`Segoe UI`).
  - Offscreen/minimal runs use the bundled Noto font and explicitly set `QT_QPA_FONTDIR`.
  - Added a dedicated font sanity test that fails on missing basic Latin glyph support or font/QSS warnings.
- Tightened QA gates.
  - `scripts/ui_walkthrough.py` now records `qt_warnings.txt`, checks font sanity, validates runtime icons, checks settings-nav geometry, and records maximize-growth evidence.
  - `scripts/verify_requirements.py` now gates on font sanity, walkthrough Qt warning cleanliness, native settings-nav usage, runtime icon validity, and settings-save debounce responsiveness.
  - `scripts/qss_sanity_check.py` now fails on font warnings in addition to QSS parse/property issues.
- Disabled widget fade animations in offscreen/minimal environments.
  - This removed the `QPainter`/`QWidgetEffectSourcePrivate::pixmap` warning storm from walkthrough/verifier runs.
- Cleaned generated artifacts from version control.
  - Added ignore rules for screenshots/logs and retained `docs/screenshots/.keep` as the tracked placeholder.

## Proofs
- Failing historical screenshot reviewed:
  - `docs/screenshots/20260306_104950/1024x768_1_home.png`
  - `docs/screenshots/20260306_104950/1024x768_7_settings.png`
- Reproduced current settings failure before the fix:
  - `docs/screenshots/20260306_114142/1024x768_7_settings.png`
- Verified current repaired walkthrough output after the fix:
  - `docs/screenshots/20260306_115443/1024x768_7_settings.png`
- Verifier status:
  - `docs/REBUILD_VERIFICATION.md` now reports `Final verdict: PASS`

## Notes
- The `python` and `pip` shell shims on PATH are broken in this environment.
- All actual proof runs were executed with:
  - `C:\Users\btheobald\AppData\Local\Python\pythoncore-3.14-64\python.exe`
