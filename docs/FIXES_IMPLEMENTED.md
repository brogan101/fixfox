# Fix Fox Fixes Implemented

Date: 2026-03-03

## 1) Logging/Event Streaming (Top Priority)

- Added authoritative run event layer:
  - `src/core/run_events.py`
  - Event types: `START`, `PROGRESS`, `STDOUT`, `STDERR`, `ARTIFACT`, `WARNING`, `ERROR`, `END`
  - Per-run ring buffering with replay via cursor (`events_since`)

- Integrated runner publishing:
  - `src/core/command_runner.py`
  - `src/core/script_tasks.py`
  - `src/core/runbooks.py`

- ToolRunner now subscribes to buffered bus stream:
  - `src/ui/components/tool_runner.py`
  - Polls bus on timer, replays early events, and updates live output/status/progress.

- Main window now routes worker signals into run events and keeps ToolRunner as execution surface:
  - `src/ui/main_window.py`
  - Added immediate run toast (`running...`) and deterministic error publishing.

## 2) ToolRunner Reliability/UX

- `_start_task` now returns and tracks `run_id` and binds each run to event bus context.
- Script task and runbook execution now use stable pre-created `run_id` (race removed).
- Added `Copy Technical Appendix` in ToolRunner `More` menu.
- Standardized primary action label to `Export Pack`.

## 3) UI Consistency and Layout

- Added themed scroll-area hooks:
  - `PageScroll` and `PageViewport` object names in `src/ui/main_window.py`
- Strengthened themed QSS for scrollbars/splitter/focus states in:
  - `src/ui/app_qss.py`
- Unified rail row construction for nav and settings:
  - shared `_rail_row_widget` in `src/ui/main_window.py`
- Reduced narrow-width crowding by splitting Playbooks and Settings toolbars into two-row shells.

## 4) Runtime/Path Stability

- Enforced branded app data/log root with best-effort legacy migration:
  - `src/core/paths.py`
  - Active logs/crash path now resolves to `%LOCALAPPDATA%\\FixFox\\...`

## 5) Tests and QA Updates

- Extended smoke test with run-event validation:
  - UI module page switching
  - ToolRunner script task run event assertions (`START/PROGRESS/STDOUT|STDERR/END`)
  - Runbook dry-run event assertions
  - Command runner event publishing assertion
  - File: `src/tests/smoke.py`

## 6) Docs Added/Updated

- Full audit first: `docs/FULL_AUDIT_REPORT.md`
- Style guide: `docs/UI_STYLE_GUIDE.md`
- This implementation log: `docs/FIXES_IMPLEMENTED.md`
- QA checklist: `docs/RELEASE_QA_CHECKLIST.md`
