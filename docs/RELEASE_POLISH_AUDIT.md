# Fix Fox Release Polish Audit

Date: 2026-03-03  
Scope: Runtime stability, UI/UX consistency, theme fidelity, ToolRunner routing

## 1) Runtime and Bug Audit

### Commands run
- `.venv\Scripts\python.exe -m src.app` (timed out after ~16s because GUI event loop stayed active, expected)
- `$env:QT_QPA_PLATFORM='offscreen'; .venv\Scripts\python.exe -c "from PySide6.QtWidgets import QApplication; from src.ui.main_window import MainWindow; app=QApplication([]); w=MainWindow(); print('main_window_init_ok'); w.close(); app.quit()"`
- `.venv\Scripts\python.exe -m src.tests.smoke`
- `rg -n "<pending-markers>|placeholder|\bpass\b" src docs`

### Results
- App startup path initializes successfully (`main_window_init_ok`).
- Smoke test passes.
- No blocking import/runtime crash reproduced in initialization.
- Marker scan found only expected `pass` usages and documentation text; no active pending-work implementation blockers in release paths.

### Runtime issues found and fixed
1. Reports evidence status text rendering quality.
- File: `src/ui/main_window.py` (`_refresh_evidence_items`)
- Fix: replaced symbol output with deterministic ASCII statuses (`Collected [OK]`, `Optional [I]`, `Missing [!]`).

2. Missing hidden UI self-check flow.
- File: `src/ui/main_window.py`
- Fix: added `Ctrl+Alt+T` shortcut and `_run_ui_self_check()` report writer to `%LOCALAPPDATA%\FixFox\logs\ui_self_check_*.log`.

3. Tool failure messaging not fully normalized.
- Files: `src/ui/main_window.py`, `src/ui/components/tool_runner.py`
- Fix: standardized failure handling to always render what failed, why, and deterministic next steps (including partial export path).

4. Log root path not always branded when legacy folder existed.
- File: `src/core/paths.py`
- Fix: active path now resolves to `%LOCALAPPDATA%\FixFox`; best-effort migration from `PCConcierge` runs automatically.

## 2) UI/UX Audit by Page

## Home
- Controls observed:
  - Header CTA: `Start Quick Check`
  - Goal cards: `Speed Up PC`, `Free Up Space`, `Fix Wi-Fi` (`Start` + `Learn More`)
  - Quick Actions feed + `Manage Favorites`
  - Recent Sessions + `Export Last Pack`
- Implemented in:
  - `src/ui/main_window.py::_build_home`
- Notes:
  - Uses shared card/row components and scroll container.

## Playbooks
- Controls observed:
  - Search, segment switch (`Tools`/`Runbooks`), advanced toggle
  - Tool filters and feeds, runbooks feeds, detail actions
- Implemented in:
  - `src/ui/main_window.py::_build_toolbox`
- Issues fixed:
  - Crowded controls row at narrow widths reduced by moving controls into a two-row shell.

## Diagnose
- Controls observed:
  - Search, severity filter, `Recommended only`
  - Grouped findings accordion feed
- Implemented in:
  - `src/ui/main_window.py::_build_diagnose`, `_apply_diagnose_filters`
- Notes:
  - Detail context remains in right panel and is actionable.

## Fixes
- Controls observed:
  - Scope selector, risk chips, fix directory
  - Detail panel with `Preview` + `Run`, rollback center
- Implemented in:
  - `src/ui/main_window.py::_build_fixes`
- Notes:
  - Master-detail behavior remains stable with ToolRunner execution.

## Reports
- Controls observed:
  - 3-step export flow
  - Redaction preview, evidence checklist/status, generate actions
- Implemented in:
  - `src/ui/main_window.py::_build_reports`
- Issues fixed:
  - Evidence status text rendering normalized.

## History
- Controls observed:
  - Session search/list, case summary actions, compare drawer, run center
- Implemented in:
  - `src/ui/main_window.py::_build_history`
- Notes:
  - Reopen/compare/re-export remains wired and active.

## Settings
- Controls observed:
  - Search + reset/export/help actions
  - Left nav + section stack
- Implemented in:
  - `src/ui/main_window.py::_build_settings`
- Issues fixed:
  - Toolbar row crowding reduced with two-row shell.
  - Settings nav row sizing/icon sizing unified with main nav rail density behavior.

## 3) Layout and Interaction Issues (Resolved)

1. Scrollbar theming.
- File: `src/ui/app_qss.py`
- Fix: added themed `QScrollBar` styles (vertical/horizontal, handle, page/add/sub segments) with density-aware thickness.

2. Splitter handle theming.
- File: `src/ui/app_qss.py`
- Fix: added consistent handle thickness, radius, and hover states.

3. Scroll area background consistency.
- Files: `src/ui/main_window.py` (`_scroll`) + `src/ui/app_qss.py`
- Fix: added `PageScroll` and `PageViewport` object hooks and themed background policy.

4. Controls-row clipping risk in narrow layouts.
- File: `src/ui/main_window.py`
- Fix: split Playbooks and Settings top controls into two-row shells.

## 4) Theme Audit

### Hardcoded colors outside theme tokens
- `src/core/report.py` includes hardcoded hex values for exported HTML report styling.
- In-app widget theme source remains centralized in `src/ui/theme.py` + `src/ui/app_qss.py`.

### Widget-specific QSS overrides
- No ad-hoc widget-level `setStyleSheet(...)` overrides found in UI pages/components.
- Global stylesheet is applied through:
  - `src/app.py`
  - `src/ui/main_window.py::_apply_theme`

### Scrollbar style variance
- Resolved by explicit themed scrollbar rules in `src/ui/app_qss.py`.

## 5) Output/ToolRunner Audit

- Tool/fix/runbook/script-task execution routes through `_start_task(...)` and opens `ToolRunnerWindow`.
- No page-level embedded raw execution outputs on Home/Playbooks/Diagnose/Fixes/Reports/History/Settings.
- Allowed text editors currently in app:
  - Help content reader
  - Reports redaction preview
  - Feedback input
  - ToolRunner tabs (intended execution surface)

## 6) Resolver Pass Completed

1. Added hidden `Ctrl+Alt+T` UI self-check + log output.
2. Normalized ToolRunner/task failure messaging.
3. Fixed Reports evidence status rendering quality.
4. Added and applied style guidance (`docs/UI_STYLE_GUIDE.md`) with unified nav/settings rail sizing.
5. Added full themed scrollbar and splitter handle styles.
6. Added scroll-area object hooks for background consistency.
7. Reduced top-row crowding in Playbooks and Settings.
