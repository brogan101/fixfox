# Functionality Matrix (Capability Audit)

Generated from `scripts/capability_audit.py` in `C:\Users\btheobald\Desktop\IT Core`.
Counts: total=140 ok=140 unsupported=0 failing=0

## Core Feature Health

Feature | Health
---|---
tools | working
fixes | working
script tasks | working
runbooks | working
exports | working
masking | working
validator | working
evidence collection | working

## Capability Status Table

Capability | Reachable in UI | ToolRunner | Artifacts | Errors handled | Dry-run | Status | Notes
---|---|---|---|---|---|---|---
`quick_check` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`large_file_radar` | Y | N | N | Y | unsupported | ok | ok; no safe dry-run path
`downloads_cleanup_assistant` | Y | N | N | Y | unsupported | ok | ok; no safe dry-run path
`storage_ranked_view` | Y | N | N | Y | unsupported | ok | ok; no safe dry-run path
`duplicate_finder_hash` | Y | N | N | Y | unsupported | ok | ok; no safe dry-run path
`uninstall_helper` | Y | N | N | Y | unsupported | ok | ok; no safe dry-run path
`disk_health_snapshot` | Y | N | N | Y | unsupported | ok | ok; no safe dry-run path
`battery_report` | Y | N | N | Y | unsupported | ok | ok; no safe dry-run path
`reliability_snapshot` | Y | N | N | Y | unsupported | ok | ok; no safe dry-run path
`problem_devices` | Y | N | N | Y | unsupported | ok | ok; no safe dry-run path
`proxy_hosts_alert` | Y | N | N | Y | unsupported | ok | ok; no safe dry-run path
`weekly_check_reminder` | Y | N | N | Y | unsupported | ok | ok; no safe dry-run path
`fix_flush_dns` | Y | N | N | Y | unsupported | ok | ok; no safe dry-run path
`fix_restart_spooler` | Y | N | N | Y | unsupported | ok | ok; no safe dry-run path
`fix_startup_toggle` | Y | N | N | Y | unsupported | ok | ok; no safe dry-run path
`export_home_share_pack` | Y | N | Y | Y | unsupported | ok | ok; no safe dry-run path
`export_ticket_pack` | Y | N | Y | Y | unsupported | ok | ok; no safe dry-run path
`export_full_pack` | Y | N | Y | Y | unsupported | ok | ok; no safe dry-run path
`script_task.task_systeminfo_export` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_hotfixes_export` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_drivers_export` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_services_export` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_scheduled_tasks_export` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_startup_items_export` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_evtx_application` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_evtx_system` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_evtx_setup` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_evtx_windows_update` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_evtx_printservice` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_evtx_devicesetup` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_update_services_status` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_get_windows_update_log` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_pending_reboot_sources` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_ipconfig_all` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_route_print` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_proxy_show` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_dns_timing` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_hosts_check` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_wlan_report` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_wifi_report_fix_wizard` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_ping_1_1_1_1` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_ping_8_8_8_8` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_dns_flush` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_ip_release_renew` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_browser_rescue` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_app_crash_helper` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_audio_mic_helper` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_camera_privacy_check` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_onedrive_sync_helper` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_usb_bt_disconnect_helper` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_winsock_reset` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_tcpip_reset` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_service_health_snapshot` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_driver_device_inventory_pack` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_firewall_profile_summary` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_wmi_repair_helper` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_office_outlook_helper` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_eventlog_exporter_pack` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_system_profile_belarc_lite` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_startup_autostart_pack` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_update_repair_evidence_pack` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_network_evidence_pack` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_crash_evidence_pack` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_network_stack_repair_tool` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_windows_update_reset_tool` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_sfc_dism_integrity_tool` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_printer_full_reset_tool` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_smart_snapshot` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_thermal_hints` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_printer_status` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_restart_spooler` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_clear_spool_folder` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_sfc_scannow` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_dism_restorehealth` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_reset_update_components` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_reliability_snapshot` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_minidumps_collect` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_arp_a` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_netstat` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_tasklist` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_whoami` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_systeminfo` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_driverquery` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_sc_query` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_powercfg_a` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_powercfg_battery` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_wevtutil_system` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_wevtutil_application` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_nslookup_microsoft` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_print_queue` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_proxy_settings` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_hosts_preview` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_large_file_radar` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_storage_radar` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_downloads_cleanup_buckets` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_fast_file_search` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_appdata_bloat_scanner` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_storage_ranked_view` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_duplicate_hash_scan` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`script_task.task_performance_sample` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`fix_action.open_storage` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`fix_action.flush_dns` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`fix_action.open_network` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`fix_action.open_gethelp` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`fix_action.restart_spooler` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`fix_action.startup_enable` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`fix_action.startup_disable` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`runbook.home_fix_wifi_safe` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`runbook.home_free_up_space_safe` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`runbook.home_speed_up_pc_safe` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`runbook.home_printer_rescue` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`runbook.home_browser_problems` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`runbook.home_no_audio_mic` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`runbook.home_onedrive_not_syncing` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`runbook.home_usb_bt_disconnects` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`runbook.it_ticket_triage_pack` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`runbook.it_app_crash_triage_pack` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`runbook.it_usb_bt_disconnect_triage` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`runbook.it_office_outlook_triage` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`runbook.it_windows_update_repair` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`runbook.it_network_stack_repair` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`runbook.it_system_integrity_check` | Y | Y | Y | Y | ok | ok | ok; dry-run ok
`tool.tool_storage` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`tool.tool_network` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`tool.tool_windows_update` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`tool.tool_apps` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`tool.tool_device_manager` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`tool.tool_event_viewer` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`tool.tool_services` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`tool.tool_reliability` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`tool.tool_get_help` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`tool.tool_feedback` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`tool.tool_print_mgmt` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`tool.tool_cmd_admin` | Y | Y | N | Y | unsupported | ok | ok; no safe dry-run path
`export_preset.home_share` | Y | Y | Y | Y | ok | ok | ok; export ok
`export_preset.ticket` | Y | Y | Y | Y | ok | ok | ok; export ok
`export_preset.full` | Y | Y | Y | Y | ok | ok | ok; export ok
`kb.kb_disk_space` | Y | N | N | Y | unsupported | ok | ok; no safe dry-run path
`kb.kb_proxy_hosts` | Y | N | N | Y | unsupported | ok | ok; no safe dry-run path
`kb.kb_memory` | Y | N | N | Y | unsupported | ok | ok; no safe dry-run path
