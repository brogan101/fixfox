# Fix Fox Release Polish Checklist

Date: 2026-03-03

## Automated Gates

1. Run `.venv\Scripts\python.exe -m src.tests.smoke`
- Pass criteria:
  - app modules initialize
  - pages switch without exceptions
  - one ToolRunner dry-run task completes
  - Home/Ticket export + manifest/hash validation pass

2. Run marker scan:
- `rg -n "<pending-work-markers>" src docs scripts packaging`
- Pass criteria: no unresolved markers in implemented scope.

## Manual UI Gates

1. Resize checks
- Minimum window (`1100x700`): no critical controls clipped or unreachable.
- 1080p windowed (`1920x1080`): section spacing and card alignment remain consistent.
- Wide (`>=1600`): right detail pane and center content remain balanced.

2. Scrollbar consistency
- Verify themed scrollbars appear in:
  - nav/list directories
  - reports preview trees
  - long content pages
- Pass criteria: no default Qt scrollbar style appears.

3. Tool output routing
- Trigger tool run from Playbooks and Fixes.
- Pass criteria: execution opens ToolRunner; no embedded page-level raw run output widget appears.

4. Export flow
- Run Home Share export and Ticket export.
- Pass criteria:
  - validator status displayed
  - report folder + summaries open/copy actions work
  - partial export path remains available on failures.

5. History flow
- Reopen prior session, compare, and re-export.
- Pass criteria: all three actions function from History detail.

6. Dead-button sweep
- Click all header CTAs, help icons, key list context menus, and settings actions.
- Pass criteria: no inert controls and no unhandled exception dialogs.

## Hidden Dev Check

Run `Ctrl+Alt+T` (UI self-check).
- Pass criteria:
  - report file written under `%LOCALAPPDATA%\FixFox\logs\ui_self_check_*.log`
  - page switch/widget existence checks pass
  - tool/runbook directory checks pass.
