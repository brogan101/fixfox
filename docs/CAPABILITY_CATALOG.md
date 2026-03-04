# Capability Catalog

| Kind | ID | Title | Risk | Admin | Pro | Category | Contexts | Entry Point |
|---|---|---|---|---|---|---|---|---|
| capability | `quick_check` | Quick Check | Safe | False | False | - | home, diagnose | `core.diagnostics.quick_check` |
| capability | `large_file_radar` | Large File Radar | Safe | False | False | - | diagnose, toolbox | `core.diagnostics.large_file_radar` |
| capability | `downloads_cleanup_assistant` | Downloads Cleanup Assistant | Safe | False | False | - | diagnose, fixes | `core.diagnostics.downloads_cleanup_assistant` |
| capability | `storage_ranked_view` | Storage Ranked View | Safe | False | False | - | diagnose, reports | `core.diagnostics.storage_ranked_view` |
| capability | `duplicate_finder_hash` | Duplicate Finder (Exact Hash) | Advanced | False | True | - | diagnose, fixes | `core.diagnostics.duplicate_finder_exact_hash` |
| capability | `uninstall_helper` | Uninstall Helper (Leftover Folders) | Safe | False | False | - | toolbox, fixes | `core.diagnostics.uninstall_leftover_folders` |
| capability | `disk_health_snapshot` | Disk Health Snapshot | Safe | False | False | - | diagnose | `core.diagnostics.disk_health_snapshot` |
| capability | `battery_report` | Battery Report | Safe | False | True | - | toolbox, reports | `core.diagnostics.battery_report` |
| capability | `reliability_snapshot` | Reliability Snapshot | Safe | False | False | - | diagnose | `core.diagnostics.reliability_snapshot` |
| capability | `problem_devices` | Problem Devices List | Safe | False | False | - | diagnose, toolbox | `core.diagnostics.problem_devices` |
| capability | `proxy_hosts_alert` | Proxy + Hosts Alert | Safe | False | False | - | diagnose, toolbox | `core.diagnostics.proxy_and_hosts_alert` |
| capability | `weekly_check_reminder` | Weekly Check Reminder | Safe | False | False | - | home, settings | `core.diagnostics.weekly_check_status` |
| capability | `fix_flush_dns` | Flush DNS Cache | Safe | False | False | - | fixes, toolbox | `core.fixes.run_fix` |
| capability | `fix_restart_spooler` | Restart Print Spooler | Admin | True | False | - | fixes | `core.fixes.run_fix` |
| capability | `fix_startup_toggle` | Startup Toggle | Safe | False | False | - | fixes, settings | `core.fixes.run_fix` |
| capability | `export_home_share_pack` | Home Share Pack Export | Safe | False | False | - | reports | `core.exporter.export_session` |
| capability | `export_ticket_pack` | Ticket Pack Export | Safe | False | False | - | reports | `core.exporter.export_session` |
| capability | `export_full_pack` | Full Pack Export | Safe | False | False | - | reports | `core.exporter.export_session` |
| script_task | `script_task.task_systeminfo_export` | SystemInfo Export | Safe | False | False | system | playbooks, reports | `core.script_tasks.run_script_task:task_systeminfo_export` |
| script_task | `script_task.task_hotfixes_export` | HotFixes Export | Safe | False | False | system | playbooks, reports | `core.script_tasks.run_script_task:task_hotfixes_export` |
| script_task | `script_task.task_drivers_export` | Drivers Export | Safe | False | False | system | playbooks, reports | `core.script_tasks.run_script_task:task_drivers_export` |
| script_task | `script_task.task_services_export` | Services Export | Safe | False | False | system | playbooks, reports | `core.script_tasks.run_script_task:task_services_export` |
| script_task | `script_task.task_scheduled_tasks_export` | Scheduled Tasks Export | Safe | False | False | system | playbooks, reports | `core.script_tasks.run_script_task:task_scheduled_tasks_export` |
| script_task | `script_task.task_startup_items_export` | Startup Items Export | Safe | False | False | system | playbooks, reports | `core.script_tasks.run_script_task:task_startup_items_export` |
| script_task | `script_task.task_evtx_application` | Export Application Event Log | Safe | False | False | eventlogs | playbooks, reports | `core.script_tasks.run_script_task:task_evtx_application` |
| script_task | `script_task.task_evtx_system` | Export System Event Log | Safe | False | False | eventlogs | playbooks, reports | `core.script_tasks.run_script_task:task_evtx_system` |
| script_task | `script_task.task_evtx_setup` | Export Setup Event Log | Safe | False | False | eventlogs | playbooks, reports | `core.script_tasks.run_script_task:task_evtx_setup` |
| script_task | `script_task.task_evtx_windows_update` | Export WindowsUpdateClient Operational | Safe | False | False | eventlogs | playbooks, reports | `core.script_tasks.run_script_task:task_evtx_windows_update` |
| script_task | `script_task.task_evtx_printservice` | Export PrintService Operational | Safe | False | False | eventlogs | playbooks, reports | `core.script_tasks.run_script_task:task_evtx_printservice` |
| script_task | `script_task.task_evtx_devicesetup` | Export DeviceSetupManager Admin | Safe | False | False | eventlogs | playbooks, reports | `core.script_tasks.run_script_task:task_evtx_devicesetup` |
| script_task | `script_task.task_update_services_status` | Update Services Status | Safe | False | False | updates | playbooks, reports | `core.script_tasks.run_script_task:task_update_services_status` |
| script_task | `script_task.task_get_windows_update_log` | Windows Update Log | Safe | False | False | updates | playbooks, reports | `core.script_tasks.run_script_task:task_get_windows_update_log` |
| script_task | `script_task.task_pending_reboot_sources` | Pending Reboot Sources | Safe | False | False | updates | playbooks, reports | `core.script_tasks.run_script_task:task_pending_reboot_sources` |
| script_task | `script_task.task_ipconfig_all` | IP Config | Safe | False | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_ipconfig_all` |
| script_task | `script_task.task_route_print` | Route Print | Safe | False | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_route_print` |
| script_task | `script_task.task_proxy_show` | WinHTTP Proxy | Safe | False | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_proxy_show` |
| script_task | `script_task.task_dns_timing` | DNS Timing Test | Safe | False | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_dns_timing` |
| script_task | `script_task.task_hosts_check` | Hosts File Check | Safe | False | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_hosts_check` |
| script_task | `script_task.task_wlan_report` | WLAN Report | Safe | False | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_wlan_report` |
| script_task | `script_task.task_wifi_report_fix_wizard` | Wi-Fi Report + Fix Wizard | Safe | False | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_wifi_report_fix_wizard` |
| script_task | `script_task.task_ping_1_1_1_1` | Ping Cloudflare | Safe | False | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_ping_1_1_1_1` |
| script_task | `script_task.task_ping_8_8_8_8` | Ping Google DNS | Safe | False | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_ping_8_8_8_8` |
| script_task | `script_task.task_dns_flush` | Flush DNS | Safe | False | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_dns_flush` |
| script_task | `script_task.task_ip_release_renew` | IP Release/Renew | Safe | False | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_ip_release_renew` |
| script_task | `script_task.task_browser_rescue` | Browser Rescue | Safe | False | False | browser | playbooks, reports | `core.script_tasks.run_script_task:task_browser_rescue` |
| script_task | `script_task.task_app_crash_helper` | App Crash Helper | Safe | False | False | crash | playbooks, reports | `core.script_tasks.run_script_task:task_app_crash_helper` |
| script_task | `script_task.task_audio_mic_helper` | Audio & Mic Fix Helper | Safe | False | False | audio | playbooks, reports | `core.script_tasks.run_script_task:task_audio_mic_helper` |
| script_task | `script_task.task_camera_privacy_check` | Camera/Privacy Quick Check | Safe | False | False | privacy | playbooks, reports | `core.script_tasks.run_script_task:task_camera_privacy_check` |
| script_task | `script_task.task_onedrive_sync_helper` | OneDrive Sync Helper | Safe | False | False | cloud | playbooks, reports | `core.script_tasks.run_script_task:task_onedrive_sync_helper` |
| script_task | `script_task.task_usb_bt_disconnect_helper` | USB/Bluetooth Disconnect Helper | Safe | False | False | devices | playbooks, reports | `core.script_tasks.run_script_task:task_usb_bt_disconnect_helper` |
| script_task | `script_task.task_winsock_reset` | Winsock Reset | Admin | True | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_winsock_reset` |
| script_task | `script_task.task_tcpip_reset` | TCP/IP Reset | Admin | True | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_tcpip_reset` |
| script_task | `script_task.task_service_health_snapshot` | Service Health Snapshot | Safe | False | False | services | playbooks, reports | `core.script_tasks.run_script_task:task_service_health_snapshot` |
| script_task | `script_task.task_driver_device_inventory_pack` | Driver / Device Inventory Pack | Safe | False | False | devices | playbooks, reports | `core.script_tasks.run_script_task:task_driver_device_inventory_pack` |
| script_task | `script_task.task_firewall_profile_summary` | Firewall/Profile Summary | Safe | False | False | security | playbooks, reports | `core.script_tasks.run_script_task:task_firewall_profile_summary` |
| script_task | `script_task.task_wmi_repair_helper` | WMI Repair Helper | Admin | False | False | wmi | playbooks, reports | `core.script_tasks.run_script_task:task_wmi_repair_helper` |
| script_task | `script_task.task_office_outlook_helper` | Office/Outlook Helper | Safe | False | False | office | playbooks, reports | `core.script_tasks.run_script_task:task_office_outlook_helper` |
| script_task | `script_task.task_eventlog_exporter_pack` | Event Log Exporter Pack | Safe | False | False | eventlogs | playbooks, reports | `core.script_tasks.run_script_task:task_eventlog_exporter_pack` |
| script_task | `script_task.task_system_profile_belarc_lite` | Belarc-lite System Profile | Safe | False | False | system | playbooks, reports | `core.script_tasks.run_script_task:task_system_profile_belarc_lite` |
| script_task | `script_task.task_startup_autostart_pack` | Startup/Autostart Pack | Safe | False | False | system | playbooks, reports | `core.script_tasks.run_script_task:task_startup_autostart_pack` |
| script_task | `script_task.task_update_repair_evidence_pack` | Update Repair Evidence Pack | Safe | False | False | updates | playbooks, reports | `core.script_tasks.run_script_task:task_update_repair_evidence_pack` |
| script_task | `script_task.task_network_evidence_pack` | Network Evidence Pack | Safe | False | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_network_evidence_pack` |
| script_task | `script_task.task_crash_evidence_pack` | Crash Evidence Pack | Safe | False | False | crash | playbooks, reports | `core.script_tasks.run_script_task:task_crash_evidence_pack` |
| script_task | `script_task.task_network_stack_repair_tool` | Network Stack Repair Tool | Admin | True | False | repair | playbooks, reports | `core.script_tasks.run_script_task:task_network_stack_repair_tool` |
| script_task | `script_task.task_windows_update_reset_tool` | Windows Update Reset Tool | Admin | True | False | repair | playbooks, reports | `core.script_tasks.run_script_task:task_windows_update_reset_tool` |
| script_task | `script_task.task_sfc_dism_integrity_tool` | SFC/DISM Integrity Tool | Admin | True | False | repair | playbooks, reports | `core.script_tasks.run_script_task:task_sfc_dism_integrity_tool` |
| script_task | `script_task.task_printer_full_reset_tool` | Printer Full Reset Tool | Admin | True | False | repair | playbooks, reports | `core.script_tasks.run_script_task:task_printer_full_reset_tool` |
| script_task | `script_task.task_smart_snapshot` | SMART Snapshot + Warnings | Safe | False | False | hardware | playbooks, reports | `core.script_tasks.run_script_task:task_smart_snapshot` |
| script_task | `script_task.task_thermal_hints` | Thermal/Throttle Hints | Safe | False | False | hardware | playbooks, reports | `core.script_tasks.run_script_task:task_thermal_hints` |
| script_task | `script_task.task_printer_status` | Printer Status | Safe | False | False | printer | playbooks, reports | `core.script_tasks.run_script_task:task_printer_status` |
| script_task | `script_task.task_restart_spooler` | Restart Print Spooler | Admin | True | False | printer | playbooks, reports | `core.script_tasks.run_script_task:task_restart_spooler` |
| script_task | `script_task.task_clear_spool_folder` | Clear Spool Folder | Admin | True | False | printer | playbooks, reports | `core.script_tasks.run_script_task:task_clear_spool_folder` |
| script_task | `script_task.task_sfc_scannow` | SFC Scan | Admin | True | False | integrity | playbooks, reports | `core.script_tasks.run_script_task:task_sfc_scannow` |
| script_task | `script_task.task_dism_restorehealth` | DISM RestoreHealth | Admin | True | False | integrity | playbooks, reports | `core.script_tasks.run_script_task:task_dism_restorehealth` |
| script_task | `script_task.task_reset_update_components` | Reset Update Components | Admin | True | False | updates | playbooks, reports | `core.script_tasks.run_script_task:task_reset_update_components` |
| script_task | `script_task.task_reliability_snapshot` | Reliability Snapshot | Safe | False | False | crash | playbooks, reports | `core.script_tasks.run_script_task:task_reliability_snapshot` |
| script_task | `script_task.task_minidumps_collect` | Minidumps Collect | Safe | False | False | crash | playbooks, reports | `core.script_tasks.run_script_task:task_minidumps_collect` |
| script_task | `script_task.task_arp_a` | ARP Table | Safe | False | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_arp_a` |
| script_task | `script_task.task_netstat` | Netstat | Safe | False | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_netstat` |
| script_task | `script_task.task_tasklist` | Tasklist | Safe | False | False | performance | playbooks, reports | `core.script_tasks.run_script_task:task_tasklist` |
| script_task | `script_task.task_whoami` | WhoAmI | Safe | False | False | system | playbooks, reports | `core.script_tasks.run_script_task:task_whoami` |
| script_task | `script_task.task_systeminfo` | System Info | Safe | False | False | system | playbooks, reports | `core.script_tasks.run_script_task:task_systeminfo` |
| script_task | `script_task.task_driverquery` | Driver Query | Safe | False | False | system | playbooks, reports | `core.script_tasks.run_script_task:task_driverquery` |
| script_task | `script_task.task_sc_query` | Service Query | Safe | False | False | system | playbooks, reports | `core.script_tasks.run_script_task:task_sc_query` |
| script_task | `script_task.task_powercfg_a` | Power States | Safe | False | False | system | playbooks, reports | `core.script_tasks.run_script_task:task_powercfg_a` |
| script_task | `script_task.task_powercfg_battery` | Battery Report | Safe | False | False | system | playbooks, reports | `core.script_tasks.run_script_task:task_powercfg_battery` |
| script_task | `script_task.task_wevtutil_system` | System Events | Safe | False | False | eventlogs | playbooks, reports | `core.script_tasks.run_script_task:task_wevtutil_system` |
| script_task | `script_task.task_wevtutil_application` | App Events | Safe | False | False | eventlogs | playbooks, reports | `core.script_tasks.run_script_task:task_wevtutil_application` |
| script_task | `script_task.task_nslookup_microsoft` | NSLookup | Safe | False | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_nslookup_microsoft` |
| script_task | `script_task.task_print_queue` | Print Queue | Safe | False | False | printer | playbooks, reports | `core.script_tasks.run_script_task:task_print_queue` |
| script_task | `script_task.task_proxy_settings` | Proxy Settings | Safe | False | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_proxy_settings` |
| script_task | `script_task.task_hosts_preview` | Hosts Preview | Safe | False | False | network | playbooks, reports | `core.script_tasks.run_script_task:task_hosts_preview` |
| script_task | `script_task.task_large_file_radar` | Large File Radar Preview | Safe | False | False | evidence | playbooks, reports | `core.script_tasks.run_script_task:task_large_file_radar` |
| script_task | `script_task.task_storage_radar` | Storage Radar (WizTree-lite) | Safe | False | False | storage | playbooks, reports | `core.script_tasks.run_script_task:task_storage_radar` |
| script_task | `script_task.task_downloads_cleanup_buckets` | Downloads Cleanup Buckets | Safe | False | False | evidence | playbooks, reports | `core.script_tasks.run_script_task:task_downloads_cleanup_buckets` |
| script_task | `script_task.task_fast_file_search` | Fast File Search (Everything-lite) | Safe | False | False | storage | playbooks, reports | `core.script_tasks.run_script_task:task_fast_file_search` |
| script_task | `script_task.task_appdata_bloat_scanner` | AppData Bloat Scanner | Safe | False | False | storage | playbooks, reports | `core.script_tasks.run_script_task:task_appdata_bloat_scanner` |
| script_task | `script_task.task_storage_ranked_view` | Storage Ranked View | Safe | False | False | evidence | playbooks, reports | `core.script_tasks.run_script_task:task_storage_ranked_view` |
| script_task | `script_task.task_duplicate_hash_scan` | Duplicate Hash Scan (Pro) | Advanced | False | False | evidence | playbooks, reports | `core.script_tasks.run_script_task:task_duplicate_hash_scan` |
| script_task | `script_task.task_performance_sample` | Performance Sample Window | Safe | False | False | performance | playbooks, reports | `core.script_tasks.run_script_task:task_performance_sample` |
| fix_action | `fix_action.open_storage` | Open Storage Settings | Safe | False | False | fixes | fixes, home, playbooks | `core.fixes.run_fix:open_storage` |
| fix_action | `fix_action.flush_dns` | Flush DNS Cache | Safe | False | False | fixes | fixes, home, playbooks | `core.fixes.run_fix:flush_dns` |
| fix_action | `fix_action.open_network` | Open Network Status | Safe | False | False | fixes | fixes, home, playbooks | `core.fixes.run_fix:open_network` |
| fix_action | `fix_action.open_gethelp` | Open Get Help | Safe | False | False | fixes | fixes, home, playbooks | `core.fixes.run_fix:open_gethelp` |
| fix_action | `fix_action.restart_spooler` | Restart Print Spooler | Admin | True | False | fixes | fixes, home, playbooks | `core.fixes.run_fix:restart_spooler` |
| fix_action | `fix_action.startup_enable` | Enable Startup Launch | Safe | False | False | fixes | fixes, home, playbooks | `core.fixes.run_fix:startup_enable` |
| fix_action | `fix_action.startup_disable` | Disable Startup Launch | Safe | False | False | fixes | fixes, home, playbooks | `core.fixes.run_fix:startup_disable` |
| runbook | `runbook.home_fix_wifi_safe` | Home: Fix Wi-Fi (Safe) | Safe | False | False | home | playbooks, home | `core.runbooks.execute_runbook:home_fix_wifi_safe` |
| runbook | `runbook.home_free_up_space_safe` | Home: Free Up Space (Safe) | Safe | False | False | home | playbooks, home | `core.runbooks.execute_runbook:home_free_up_space_safe` |
| runbook | `runbook.home_speed_up_pc_safe` | Home: Speed Up PC (Safe) | Safe | False | False | home | playbooks, home | `core.runbooks.execute_runbook:home_speed_up_pc_safe` |
| runbook | `runbook.home_printer_rescue` | Home: Printer Rescue (Safe/Admin Optional) | Safe | False | False | home | playbooks, home | `core.runbooks.execute_runbook:home_printer_rescue` |
| runbook | `runbook.home_browser_problems` | Home: Browser Problems | Safe | False | False | home | playbooks, home | `core.runbooks.execute_runbook:home_browser_problems` |
| runbook | `runbook.home_no_audio_mic` | Home: No Audio / Mic | Safe | False | False | home | playbooks, home | `core.runbooks.execute_runbook:home_no_audio_mic` |
| runbook | `runbook.home_onedrive_not_syncing` | Home: OneDrive Not Syncing | Safe | False | False | home | playbooks, home | `core.runbooks.execute_runbook:home_onedrive_not_syncing` |
| runbook | `runbook.home_usb_bt_disconnects` | Home: USB/Bluetooth Disconnects | Safe | False | False | home | playbooks, home | `core.runbooks.execute_runbook:home_usb_bt_disconnects` |
| runbook | `runbook.it_ticket_triage_pack` | IT: Ticket Triage Pack | Admin | True | False | it | playbooks, home | `core.runbooks.execute_runbook:it_ticket_triage_pack` |
| runbook | `runbook.it_app_crash_triage_pack` | IT: App Crash Triage Pack | Admin | True | False | it | playbooks, home | `core.runbooks.execute_runbook:it_app_crash_triage_pack` |
| runbook | `runbook.it_usb_bt_disconnect_triage` | IT: USB/Bluetooth Disconnect Triage | Admin | True | False | it | playbooks, home | `core.runbooks.execute_runbook:it_usb_bt_disconnect_triage` |
| runbook | `runbook.it_office_outlook_triage` | IT: Office/Outlook Triage | Admin | True | False | it | playbooks, home | `core.runbooks.execute_runbook:it_office_outlook_triage` |
| runbook | `runbook.it_windows_update_repair` | IT: Windows Update Repair (Admin) | Admin | True | False | it | playbooks, home | `core.runbooks.execute_runbook:it_windows_update_repair` |
| runbook | `runbook.it_network_stack_repair` | IT: Network Stack Repair (Admin) | Admin | True | False | it | playbooks, home | `core.runbooks.execute_runbook:it_network_stack_repair` |
| runbook | `runbook.it_system_integrity_check` | IT: System Integrity Check (Admin) | Admin | True | False | it | playbooks, home | `core.runbooks.execute_runbook:it_system_integrity_check` |
| tool | `tool.tool_storage` | Storage Settings | Safe | False | False | evidence | playbooks, home | `ui.main_window.launch_tool:tool_storage` |
| tool | `tool.tool_network` | Network Status | Safe | False | False | network | playbooks, home | `ui.main_window.launch_tool:tool_network` |
| tool | `tool.tool_windows_update` | Windows Update | Safe | False | False | updates | playbooks, home | `ui.main_window.launch_tool:tool_windows_update` |
| tool | `tool.tool_apps` | Installed Apps | Safe | False | False | evidence | playbooks, home | `ui.main_window.launch_tool:tool_apps` |
| tool | `tool.tool_device_manager` | Device Manager | Safe | False | False | integrity | playbooks, home | `ui.main_window.launch_tool:tool_device_manager` |
| tool | `tool.tool_event_viewer` | Event Viewer | Safe | False | False | evidence | playbooks, home | `ui.main_window.launch_tool:tool_event_viewer` |
| tool | `tool.tool_services` | Services | Safe | False | False | printer | playbooks, home | `ui.main_window.launch_tool:tool_services` |
| tool | `tool.tool_reliability` | Reliability Monitor | Safe | False | False | integrity | playbooks, home | `ui.main_window.launch_tool:tool_reliability` |
| tool | `tool.tool_get_help` | Get Help | Safe | False | False | network | playbooks, home | `ui.main_window.launch_tool:tool_get_help` |
| tool | `tool.tool_feedback` | Feedback Hub | Safe | False | False | evidence | playbooks, home | `ui.main_window.launch_tool:tool_feedback` |
| tool | `tool.tool_print_mgmt` | Print Management | Safe | False | False | printer | playbooks, home | `ui.main_window.launch_tool:tool_print_mgmt` |
| tool | `tool.tool_cmd_admin` | Terminal (Admin Prompt) | Safe | False | False | integrity | playbooks, home | `ui.main_window.launch_tool:tool_cmd_admin` |
| export_preset | `export_preset.home_share` | Export Preset: home_share | Safe | False | False | exports | reports | `core.exporter.export_session:home_share` |
| export_preset | `export_preset.ticket` | Export Preset: ticket | Safe | False | False | exports | reports | `core.exporter.export_session:ticket` |
| export_preset | `export_preset.full` | Export Preset: full | Safe | False | False | exports | reports | `core.exporter.export_session:full` |
| kb_article | `kb.kb_disk_space` | Low Disk Space | Safe | False | False | knowledge | diagnose, playbooks | `core.kb:kb_disk_space` |
| kb_article | `kb.kb_proxy_hosts` | Proxy or Hosts Overrides | Safe | False | False | knowledge | diagnose, playbooks | `core.kb:kb_proxy_hosts` |
| kb_article | `kb.kb_memory` | Memory Pressure | Safe | False | False | knowledge | diagnose, playbooks | `core.kb:kb_memory` |

## Metadata Fields

- `plain_1liner`: concise plain-English description used in UI surfaces.
- `technical_detail`: deterministic technical context (commands/IDs/timeouts).
- `safety_note`: concise safety/admin guidance.
- `next_steps`: deterministic follow-up actions.
