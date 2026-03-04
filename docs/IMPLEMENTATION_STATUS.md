# Fix Fox Implementation Status

Date: 2026-03-03

## Scope Completed

### Phase A: ToolRunner Everywhere
- `src/core/command_runner.py`
  - `run_command(...)` now streams stdout/stderr line-by-line via callbacks.
  - Timeout and cancellation are always enforced.
  - Process stream handles are closed to avoid resource leaks.
- `src/core/fixes.py`
  - `run_fix(...)` accepts `log_cb` and `cancel_event`.
  - Command-backed fixes stream output into ToolRunner.
- `src/core/script_tasks.py`
  - `run_script_task(...)` accepts `log_cb` and forwards output streaming.
  - Added/updated long-running task runners with timeout and cancel propagation.
- `src/core/runbooks.py`
  - Runbook execution forwards `progress`, `partial`, `log`, and `cancel` to each step task.
  - Restore point path also supports log streaming.
- `src/ui/main_window.py`
  - Fix actions, script tasks, evidence collection, and runbooks execute through `_start_task(...)` -> ToolRunner surface.

### Phase B: Evidence Collector Bundles
- `src/core/evidence_collector.py`
  - Added bundle APIs:
    - `collect_system_snapshot(...)`
    - `collect_event_logs(...)`
    - `collect_network_bundle(...)`
    - `collect_update_bundle(...)`
    - `collect_printer_bundle(...)`
    - `collect_crash_bundle(...)`
  - Default evidence root is now:
    - `%LOCALAPPDATA%\\FixFox\\sessions\\<session_id>\\evidence\\`
  - Added summary outputs for event logs/printer bundles and generic summary support.

### Phase C: Export + Validation
- `src/core/exporter.py`
  - Export folder layout is now stable:
    - `/report/report.html`
    - `/report/summary.md`
    - `/report/findings.csv`
    - `/data/session.json`
    - `/logs/actions.txt`
    - `/logs/diagnostics.txt`
    - `/evidence/**`
    - `/manifest/manifest.json`
    - `/manifest/hashes.txt`
  - Ticket/Home/Full presets are enforced via category filtering.
  - Manifest + hash generation rebuilt to align with validator checks.
  - Share-safe scan checks for raw token leakage and common unmasked patterns.

### Phase D/E: Runbook and Scanner Upgrades
- `src/core/runbooks.py`
  - Home runbooks and IT runbooks now include deeper network/update/printer/storage steps.
  - Runbook result now includes deterministic fields:
    - `summary_text`
    - `next_steps`
    - `recommended_export_preset`
- `src/core/script_tasks.py`
  - Added real task implementations:
    - `task_dns_timing`
    - `task_hosts_check`
    - `task_pending_reboot_sources`
    - `task_printer_status`
    - `task_storage_ranked_view`
    - `task_duplicate_hash_scan`

### Phase F: UX Polish and Reporting Surface
- `src/ui/main_window.py`
  - Reports page now includes an evidence checklist summary line by category.
  - Core evidence collection action now runs system + network bundle collectors through ToolRunner.

## Additional Fixes
- `src/core/brand.py`
  - Normalized report title string to `Fix Fox - Support Pack`.
- `src/core/diagnostics.py`
  - Updated legacy battery report filename to `battery_report_fixfox.html`.

## Test Status
- `python -m src.tests.smoke`: pass
- `python -m unittest src.tests.test_unit`: pass

