# UI M3 Rebuild Report

Date: 2026-03-04

## Scope completed
- Material 3 Light design-system modules added as canonical source:
  - `src/ui/style/tokens.py`
  - `src/ui/style/theme.py`
  - `src/ui/style/qss_builder.py`
- Legacy runtime entrypoints converted to compatibility shims:
  - `src/ui/theme.py` now re-exports from `src/ui/style/theme.py`
  - `src/ui/app_qss.py` now re-exports `build_qss` from `src/ui/style/qss_builder.py`
- `src/ui/style/__init__.py` now exports theme + qss APIs as single style surface.
- Existing explicit execution model and Basic/Pro layout behavior retained (validated by smoke).

## Before/After checklist
- [x] Theme pipeline split between legacy modules -> canonical `src/ui/style/*` modules
- [x] M3 Light token set available (white surfaces + Fix Fox orange accent)
- [x] Global QSS builder token-driven and pt-font based
- [x] Scrollbar and splitter styling remains themed through canonical builder
- [x] Single-click execution regression prevented (smoke assertion)
- [x] Basic/Pro layout differences preserved (smoke assertions)
- [x] ToolRunner open/focus path preserved (smoke assertions)
- [x] `.smoke_tmp/` and `.unit_tmp/` are ignored and not tracked in git index

## git diff --stat
```text
 src/ui/app_qss.py        | 592 +----------------------------------------------
 src/ui/style/__init__.py |  44 ++++
 src/ui/theme.py          | 202 +---------------
 3 files changed, 49 insertions(+), 789 deletions(-)
```

## Files changed in this pass
- `docs/UI_M3_REBUILD_PLAN.md` (new)
- `docs/UI_M3_REBUILD_REPORT.md` (new)
- `src/ui/style/tokens.py` (new)
- `src/ui/style/theme.py` (new)
- `src/ui/style/qss_builder.py` (new)
- `src/ui/style/__init__.py` (updated exports)
- `src/ui/theme.py` (compat shim to style theme)
- `src/ui/app_qss.py` (compat shim to style qss builder)

## QA executed
Automated:
- `python -m compileall src` -> pass
- `python -m src.tests.smoke` -> pass

Behavior verified by smoke assertions:
- Basic/Pro mode toggles and page-variant visibility checks
- right pane collapse defaults in Basic mode
- single-click on tool row does not execute
- explicit action executes and opens ToolRunner
- run status click opens/focuses ToolRunner
- run-event bus emits START/STATUS/END and live output events

Manual QA steps to run locally (recommended quick pass):
1. Launch `python -m src.app`.
2. Verify default is Light mode with white surfaces and orange accent.
3. Navigate all pages (Home/Playbooks/Diagnose/Fixes/Reports/History/Settings) and confirm scrollbar/splitter theming.
4. Single-click any Playbooks tool row; verify no launch.
5. Click `Open` on a tool; verify ToolRunner opens and top run status updates.
6. Switch Basic <-> Pro; confirm Playbooks/Fixes/Reports surfaces visibly change.
7. Resize to minimum and 1920x1080; verify no clipped primary actions.
