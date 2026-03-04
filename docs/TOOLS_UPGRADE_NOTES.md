# Tools Upgrade Notes (Fix Fox)

Date: 2026-03-03

## Scope Completed

This pass completes end-to-end integration for the tool expansion request:
- ToolRunner execution surface across fixes, script tasks, runbooks, and evidence collection.
- Expanded script-task catalog with Home, IT/MSP, storage power, repair, and hardware helper tools.
- Evidence bundles wired into exports with stable report/data/log/manifest structure.
- UI reliability guardrails with minimum sizes, responsive right-panel collapse, and layout debug overlay.
- Error mapping and deterministic next-step guidance in task and runbook results.
- Capability registry expanded and catalog regenerated.

## New/Expanded Tools

All tools below are discoverable in:
- `Playbooks -> Tools -> Script Tasks`
- Global search
- `Ctrl+K` command palette
- `docs/CAPABILITY_CATALOG.md`

### Home tools
- `task_wifi_report_fix_wizard` -> `wifi_summary.txt`, `wifi_interfaces.txt`, dns/hosts artifacts
- `task_browser_rescue` -> `browser_rescue_summary.txt`
- `task_storage_radar` -> `storage_radar.csv`, `storage_radar_summary.txt`, `storage_radar_bars.txt`
- `task_downloads_cleanup_buckets` -> `downloads_plan.csv`, `downloads_summary.txt`
- `task_performance_sample` -> `perf_sample.csv`, `perf_summary.txt`, raw capture
- `task_printer_status` / printer rescue tasks -> printer status artifacts
- `task_app_crash_helper` -> `crash_summary.txt`
- `task_onedrive_sync_helper` -> `onedrive_summary.txt`
- `task_usb_bt_disconnect_helper` -> `usb_bt_summary.txt`
- `task_audio_mic_helper` -> `audio_summary.txt`

### IT/MSP tools
- `task_eventlog_exporter_pack`
- `task_system_profile_belarc_lite` -> `system_profile.json`, summary
- `task_startup_autostart_pack` -> `startup_inventory.csv` (+ optional `autorunsc.csv`)
- `task_driver_device_inventory_pack`
- `task_update_repair_evidence_pack`
- `task_network_evidence_pack`
- `task_crash_evidence_pack`
- `task_service_health_snapshot`
- `task_firewall_profile_summary`
- `task_wmi_repair_helper`
- `task_office_outlook_helper`

### Storage/repair/hardware power tools
- `task_duplicate_hash_scan` -> `duplicates_report.csv`, `duplicates_summary.txt`
- `task_large_file_radar` -> `large_files.csv`, `large_files_summary.txt`
- `task_fast_file_search` -> `file_index.json`, `search_results.csv`
- `task_appdata_bloat_scanner` -> `appdata_bloat.csv`, `appdata_summary.txt`
- `task_network_stack_repair_tool` -> `network_repair_log.txt`
- `task_windows_update_reset_tool` -> `update_reset_log.txt`
- `task_sfc_dism_integrity_tool` -> integrity chain logs + summary
- `task_printer_full_reset_tool` -> `printer_reset_log.txt`
- `task_smart_snapshot` -> `smart_summary.txt`
- `task_thermal_hints` -> `thermal_summary.txt`

## Runbook Updates

### Home runbooks
- `home_fix_wifi_safe`
- `home_free_up_space_safe`
- `home_speed_up_pc_safe`
- `home_printer_rescue`
- `home_browser_problems`
- `home_onedrive_not_syncing`
- `home_no_audio_mic`
- `home_usb_bt_disconnects`

### IT runbooks
- `it_ticket_triage_pack`
- `it_app_crash_triage_pack`
- `it_windows_update_repair`
- `it_network_stack_repair`
- `it_system_integrity_check`
- `it_usb_bt_disconnect_triage`
- `it_office_outlook_triage`

All runbooks execute through ToolRunner, support dry-run, keep checkpoint metadata, and return deterministic summaries.

## UI Reliability + UX

- `src/ui/layout_guardrails.py` is active in main window sizing and responsive right-panel behavior.
- `Ctrl+Alt+L` toggles layout debug overlay.
- Added Run Center in History (last 20 runs from session actions) with rerun/copy/export shortcuts.
- Tool/task filtering expanded to include `storage`, `repair`, and `hardware` categories.

## Error Handling

- `src/core/errors.py` provides structured user-facing errors and deterministic next steps.
- `src/core/script_tasks.py` maps non-zero codes via `classify_exit` and returns `user_message`, `technical_message`, and `next_steps_list`.
- UI does not surface raw stack traces for task failures.

## Capability Catalog

Regenerated:
- `docs/CAPABILITY_CATALOG.md`

Now includes kinds for script tasks, fix actions, runbooks, tools, export presets, and KB articles.
