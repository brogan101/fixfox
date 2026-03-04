# UI Polish Audit and Remediation (2026-03-04)

## Scope
Pages and shared surfaces reviewed:
- Home, Playbooks, Diagnose, Fixes, Reports, History, Settings
- ToolRunner runtime window
- Top-bar run status card
- Global theming/QSS pipeline

## Pre-fix issues found
1. Typography unit mismatch:
- Global app QSS used `px` font sizes, conflicting with Qt point-size paths.

2. ToolRunner live log surface:
- Used `QTextEdit` for streaming logs (heavier and less stable under rapid appends).

3. Event semantics for run visibility:
- No dedicated `STATUS` event in the run bus for step/state transitions.

4. Top-bar run status behavior:
- Needed guaranteed live last-line updates from the same event stream used by ToolRunner.

5. Surface consistency:
- `QPlainTextEdit` lacked explicit shared input styling after moving ToolRunner output to plain-text.

## Fixes applied

### A) Typography and shared style consistency
- File: `src/ui/app_qss.py`
- Converted all QSS typography units from `px` to `pt`.
- Added `QPlainTextEdit` to focused control styling and shared input/background/border selectors.
- Added `QPlainTextEdit` padding to match `QTextEdit`.

### B) ToolRunner streaming reliability and readability
- File: `src/ui/components/tool_runner.py`
- Replaced live output widget with `QPlainTextEdit`.
- Added signal-based append pipeline (`append_output_requested`) to keep UI-thread-safe updates.
- Added scroll-follow behavior that only auto-scrolls when user is at bottom.
- Added no-output running placeholder with elapsed indicator.
- Overview summary template now always uses:
  - What we checked
  - What we found
  - What changed
  - What to do next
- Copy actions aligned to user-facing wording:
  - Copy Simple Summary
  - Copy Ticket Summary
  - Copy Raw Logs

### C) Event stream unification (ToolRunner + top bar)
- File: `src/core/run_events.py`
- Added event type: `STATUS`.
- Added `subscribe_global(...)` to drive top-bar updates from the shared bus.

- Files publishing `STATUS`:
  - `src/core/command_runner.py`
  - `src/core/script_tasks.py`
  - `src/core/runbooks.py`
  - `src/ui/main_window.py`

### D) Top-bar run status polish
- File: `src/ui/main_window.py`
- Run status card widened (`min-width: 660`) and icon enlarged (`28px`).
- Top bar now tracks and renders two-line status model from run bus events:
  - Line 1: running/ready/failed state
  - Line 2: latest log line (ellipsized) + elapsed time
- Clicking the status card opens/focuses ToolRunner.

## Theme override audit
Command used:
- `rg -n "setStyleSheet\\(" src`

Result:
- Only global stylesheet application points remain:
  - `src/app.py`
  - `src/ui/main_window.py` (theme refresh)
- No per-page ad-hoc `setStyleSheet(...)` overrides found.

## Validation evidence
- Smoke test: `.\\.venv\\Scripts\\python.exe -m src.tests.smoke` -> passed.
- Startup check: offscreen app boot script -> `APP_STARTUP_CHECK_OK` and no QFont warning spam.

## Release-quality outcome for this pass
The font warning, run-log visibility, top-bar run status behavior, and shared styling consistency blockers from this pass were fixed without breaking core diagnostics/fixes/runbook/export/session flows.
