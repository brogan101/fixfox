# Fix Fox Full Audit Report

Date: 2026-03-03  
Scope: Runtime, wiring, UI consistency, tool execution, export/validator/masking

## Executive Summary

- Runtime baseline is stable: app initializes and smoke passes.
- Major trust issue remains in architecture: live logging uses direct per-window signal wiring with no global run event bus or late-attach buffer.
- ToolRunner is used for primary execution paths, but logging telemetry is not unified across command/script/runbook layers.
- UI is mostly structured and consistent, but some spacing/component consistency gaps remain and a few dead/unused helpers exist.

## A) Repo Scan

Commands:
- `rg -n "<pending-markers>|\bpass\b" src docs scripts packaging`
- `rg -n "class SkeletonList|def _launch_tool\(|def _selected_task_id\(" src/ui src/core`

Findings:
1. `src/ui/widgets.py:162` `SkeletonList` appears unused.
   - Severity: Low
2. `src/ui/main_window.py:2850` `_launch_tool` is a legacy helper not used by current row activation path.
   - Severity: Low
3. `src/ui/main_window.py:2854` `_selected_task_id` is only used by `_run_selected_script_task`; no bug, but tightly coupled to one list widget.
   - Severity: Low
4. No active pending-work marker blockers in product code paths.
   - Severity: Info

## B) Runtime Scan

Commands:
- `.venv\Scripts\python.exe -m src.app` (times out in shell after ~16s because GUI loop stays active; expected)
- `$env:QT_QPA_PLATFORM='offscreen'; .venv\Scripts\python.exe -c "from PySide6.QtWidgets import QApplication; from src.ui.main_window import MainWindow; app=QApplication([]); w=MainWindow(); print('main_window_init_ok'); w.close(); app.quit()"`
- `.venv\Scripts\python.exe -m src.tests.smoke`

Results:
- `main_window_init_ok` in offscreen startup.
- Smoke test passed.
- No import failures or fatal startup exceptions observed.

## C) UI Scan (Page by Page)

## Home
Controls:
- `Start Quick Check`
- Goal cards (`Start`, `Learn More`)
- Quick Actions feed + `Manage Favorites`
- Recent Sessions + `Export Last Pack`
Status:
- Wired and functional.
Issues:
- Goal card interior spacing and bullet presentation are not fully componentized.

## Playbooks
Controls:
- Search, segment switch, advanced toggle
- Tools filters/directories (`Pinned`, `Top`, `Favorites`, `Tool Directory`)
- Tool detail actions (`Run`, `Dry Run`, `Export Pack`)
- Advanced script tasks (`Run Selected`, `Collect Core Evidence`)
- Runbooks audience filter + run actions
Status:
- Functional; all key actions wired.
Issues:
- Dense controls at narrow widths; visual hierarchy in left directory sections still crowded.

## Diagnose
Controls:
- Findings search, severity filter, `Recommended only`
- Grouped findings feed
Status:
- Functional; right detail pane updates from selection.
Issues:
- Minor spacing differences between summary cards and feed area.

## Fixes
Controls:
- Scope selector + risk chips
- Fix directory
- Detail (`Preview`, `Run`) + commands drawer
- Rollback center
Status:
- Functional; all run actions route through run path.
Issues:
- Risk chip appearance is checkbox-driven and less visually consistent than badge/chip system.

## Reports
Controls:
- Stepper tabs
- Preset/share-safe/mask-ip/include-logs
- Redaction preview, export tree, evidence status/checklist/feed
- Generate/export actions
Status:
- Functional; export and validation are wired.
Issues:
- Action density is high in Step 3 at smaller heights.

## History
Controls:
- Search + sessions feed
- Reopen/Compare/Re-export
- Compare drawer
- Run Center
Status:
- Functional.
Issues:
- Case summary text can become long/raw with verbose findings.

## Settings
Controls:
- Search, reset, export settings, help
- Section nav and pages: Safety, Privacy/Masking, Appearance, Advanced, About, Feedback
Status:
- Functional.
Issues:
- Toolbar still crowded at narrow widths prior to additional polish.
- Nav row styling needed full parity with main nav rail in all densities.

## D) Tool Execution Scan

Primary execution entrypoints found:
- `_start_task(...)` in `src/ui/main_window.py`
- `run_fix_action(...)` -> `_start_task(...)`
- `_run_script_task(...)` -> `_start_task(...)`
- `run_selected_runbook(...)` -> `_start_task(...)`
- `_collect_core_evidence(...)` -> `_start_task(...)`
- `_start_export_task(...)` -> `_start_task(...)`
- Tool launch (`_launch_tool_payload`) -> `_start_task(...)`

ToolRunner routing:
- ToolRunner opens in `_start_task` before worker starts.
- `TaskWorker.signals.progress/log_line/partial/result/error` are connected to ToolRunner handlers.

Logging root-cause findings (main bug area):
1. No authoritative global run stream.
   - Current model is direct signal connection from each worker to current ToolRunner instance.
   - Severity: High
2. No per-run ring buffer for late attach/reopen.
   - If UI rebind timing shifts, there is no guaranteed replay channel.
   - Severity: High
3. No cross-layer run identity in logs (`run_id`) from command/script/runbook layers.
   - Hard to correlate mixed output and status across components.
   - Severity: Medium
4. Worker fallback path can drop `log_cb` when function signature mismatch occurs (`TaskWorker` TypeError fallback).
   - Potentially suppresses live line streaming for non-conforming task callables.
   - Severity: Medium
5. Optional corner logging indicator is progress-toast only; it is not a true unified stream of stdout/stderr.
   - Severity: Low

## E) Export / Validator / Masking Scan

Verification points:
- Presets exist: `home_share`, `ticket`, `full` (`src/core/exporter.py`).
- Manifest + hashes generation present.
- Validator checks required files + manifest + hashes + share-safe scan.
- Smoke verifies ticket export includes required evidence folders and validator pass.

Findings:
1. Export pipeline is structurally complete and smoke-validated.
   - Severity: Info
2. Share-safe masking applies to exported text artifacts and copy actions through masking options.
   - Severity: Info
3. Report HTML uses hardcoded colors in `src/core/report.py`; this is acceptable for export artifact rendering but outside UI token system.
   - Severity: Low

## Severity List (Actionable)

1. Implement unified run event architecture with buffering and ToolRunner subscription.
   - Severity: High
2. Ensure every execution path emits START/PROGRESS/STDOUT/STDERR/END events with run identity.
   - Severity: High
3. Harden live logging delivery so logs appear within 0.5–1.0s and are replayed when ToolRunner attaches.
   - Severity: High
4. Final UI consistency sweep (controls density/layout parity and remaining low-level styling mismatches).
   - Severity: Medium
5. Remove or consolidate low-value dead helpers (e.g., unused `SkeletonList`/legacy helper methods) where safe.
   - Severity: Low
