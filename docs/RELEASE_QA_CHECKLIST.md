# Fix Fox Release QA Checklist

Date: 2026-03-03

## Automated Gates

1. `python -m src.tests.smoke`
- Expected:
  - app modules initialize
  - page switching succeeds
  - ToolRunner opens for script task run
  - run event bus captures `START/PROGRESS/STDOUT|STDERR/END`
  - runbook dry-run completes
  - Ticket export validates manifest/hashes

2. Compile guard:
- `python -m py_compile src/app.py src/ui/main_window.py src/ui/components/tool_runner.py src/core/run_events.py`

3. Marker scan:
- `rg -n "<pending-markers>" src docs scripts packaging`

## Manual Runtime Gates

1. Launch test:
- `python -m src.app`
- Expected: no startup exception dialog; window opens; top search/nav visible.

2. ToolRunner live logging:
- Start one script task from Playbooks.
- Expected within 0.5-1.0s:
  - ToolRunner shows `[start]`/running status
  - progress updates advance
  - live output appends while run is active

3. No-output tool behavior:
- Run one action that emits little/no stdout (e.g., settings URI tool).
- Expected:
  - ToolRunner still shows running/progress state
  - completion status is visible even with minimal logs

4. Runbook flow:
- Run a home runbook dry-run.
- Expected:
  - ToolRunner opens with runbook title
  - step-by-step logs stream live
  - completion updates summary + next steps

5. Export flow:
- Export Home Share and Ticket packs.
- Expected:
  - `report.html`, `summary.md`, `findings.csv`, `manifest.json`, `hashes.txt`
  - validator status displayed
  - share-safe warnings enforce block unless explicit override

## Layout/UI Gates

1. Resize:
- Minimum (`1100x700`), `1920x1080`, wide desktop.
- Expected:
  - no clipped primary CTAs
  - row run/preview actions remain reachable
  - right panel auto-collapse/restore behavior remains stable

2. Scrollbars/splitters:
- Expected:
  - themed scrollbar style on all scrolling surfaces
  - splitter handles themed with hover state

3. Interaction states:
- Expected:
  - focus outlines visible on keyboard traversal
  - hover/pressed states consistent across nav rows and action buttons

4. Dev overlays/checks:
- `Ctrl+Alt+L` toggles layout bounds overlay.
- `Ctrl+Alt+T` runs UI self-check and writes `ui_self_check_*.log` under `%LOCALAPPDATA%\\FixFox\\logs`.

## History/Run Center Gates

1. History reopen/re-export:
- Reopen a prior session and re-export.
- Expected: session context and export path update without rerun.

2. Run Center:
- Verify last 20 runs populate with status + context menu actions.
- Expected: re-run and export shortcuts remain functional.
