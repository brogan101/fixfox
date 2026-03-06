from __future__ import annotations

from dataclasses import dataclass
from functools import lru_cache
from typing import Any


@dataclass(frozen=True)
class IssueFamily:
    id: str
    code: str
    title: str
    summary: str
    default_subfamily: str


@dataclass(frozen=True)
class EvidencePlan:
    id: str
    title: str
    summary: str
    action_ref: str
    task_ids: tuple[str, ...]


@dataclass(frozen=True)
class DiagnosticCheck:
    id: str
    title: str
    source: str
    good_state: str
    bad_state: str
    result_display: str
    tags: tuple[str, ...]
    refs: tuple[str, ...]


@dataclass(frozen=True)
class SupportFix:
    id: str
    title: str
    risk: str
    automation: str
    summary: str
    permissions: str
    restart: str
    reversible: bool
    rollback: str
    evidence: str
    action_ref: str
    validation: tuple[str, ...]


@dataclass(frozen=True)
class SupportPlaybook:
    id: str
    title: str
    purpose: str
    symptoms: tuple[str, ...]
    prerequisites: tuple[str, ...]
    diagnostics: tuple[str, ...]
    safe_fixes: tuple[str, ...]
    guided_fixes: tuple[str, ...]
    escalation: tuple[str, ...]
    evidence: tuple[str, ...]
    rollback: str
    validation: tuple[str, ...]
    minutes: int
    automation: str
    risk: str
    primary_action_ref: str
    secondary_action_refs: tuple[str, ...]


@dataclass(frozen=True)
class IssueClass:
    id: str
    family_id: str
    family_label: str
    subfamily: str
    title: str
    description: str
    severity: str
    impact: str
    aliases: tuple[str, ...]
    symptom_labels: tuple[str, ...]
    diagnosis_tags: tuple[str, ...]
    fix_tags: tuple[str, ...]
    workflow: str
    permissions: str
    reboot_required: bool
    network_required: bool
    rollback_notes: str
    playbook_ids: tuple[str, ...]
    diagnostic_ids: tuple[str, ...]
    fix_ids: tuple[str, ...]
    evidence_plan_ids: tuple[str, ...]
    validation: tuple[str, ...]
    escalation: tuple[str, ...]


@dataclass(frozen=True)
class SupportCatalogStats:
    issue_count: int
    family_count: int
    playbook_count: int
    diagnostic_count: int
    fix_count: int
    guided_fix_count: int
    escalation_only_count: int


def _uniq(values: list[str] | tuple[str, ...]) -> tuple[str, ...]:
    out: list[str] = []
    seen: set[str] = set()
    for raw in values:
        value = str(raw or "").strip()
        key = value.lower()
        if not value or key in seen:
            continue
        seen.add(key)
        out.append(value)
    return tuple(out)


def _spec(
    issue_id: str,
    subfamily: str,
    title: str,
    *,
    aliases: tuple[str, ...] = (),
    playbooks: tuple[str, ...] = (),
    fixes: tuple[str, ...] = (),
    diagnostics: tuple[str, ...] = (),
    evidence: tuple[str, ...] = (),
    severity: str = "medium",
    workflow: str = "guided",
    permissions: str = "",
    reboot: bool = False,
    network: bool = False,
    impact: str = "",
    escalation: tuple[str, ...] = (),
) -> dict[str, Any]:
    return {
        "id": issue_id,
        "subfamily": subfamily,
        "title": title,
        "aliases": aliases,
        "playbooks": playbooks,
        "fixes": fixes,
        "diagnostics": diagnostics,
        "evidence": evidence,
        "severity": severity,
        "workflow": workflow,
        "permissions": permissions,
        "reboot": reboot,
        "network": network,
        "impact": impact,
        "escalation": escalation,
    }


FAMILIES: tuple[IssueFamily, ...] = (
    IssueFamily("identity", "A", "Sign-in / Identity / Account", "Credential and sign-in failures.", "Credentials"),
    IssueFamily("shell", "B", "Profile / Desktop / Windows Shell", "Profile and shell failures.", "Profile"),
    IssueFamily("network", "C", "Network / Internet Basics", "Adapter, DNS, and connectivity issues.", "Connectivity"),
    IssueFamily("remote", "D", "Wi-Fi / VPN / Remote Access", "Wireless, VPN, and remote session issues.", "Remote Access"),
    IssueFamily("print", "E", "Printing / Scanning / Document Devices", "Printer, queue, and scanner issues.", "Printing"),
    IssueFamily("browser", "F", "Browsers / Web Apps", "Browser and web app issues.", "Browser"),
    IssueFamily("email", "G", "Outlook / Email / Mailbox", "Outlook and mailbox issues.", "Outlook"),
    IssueFamily("collab", "H", "Teams / Zoom / Conferencing / Collaboration", "Meeting and collaboration issues.", "Collaboration"),
    IssueFamily("sync", "I", "Office / OneDrive / SharePoint / Sync", "Office and sync issues.", "Office Sync"),
    IssueFamily("storage", "J", "Files / Storage / Permissions", "Storage and file access issues.", "Storage"),
    IssueFamily("performance", "K", "Performance / Startup / Slow PC", "Performance and startup issues.", "Performance"),
    IssueFamily("apps", "L", "Software Install / Update / App Repair", "App install and repair issues.", "Applications"),
    IssueFamily("security", "M", "Security / Defender / Trust", "Security and trust issues.", "Security"),
    IssueFamily("media", "N", "Audio / Camera / Media Devices", "Audio, mic, and camera issues.", "Media"),
    IssueFamily("display", "O", "Displays / Docks / External Devices", "Display, dock, and device issues.", "Display"),
    IssueFamily("power", "P", "Power / Battery / Laptop Basics", "Power, battery, and sleep issues.", "Power"),
    IssueFamily("windows_update", "Q", "Windows Update / OS Health / Recovery", "Update and OS health issues.", "OS Health"),
    IssueFamily("enterprise", "R", "Domain / Intune / GPO / Certificates", "Managed device and policy issues.", "Enterprise"),
    IssueFamily("recovery", "S", "Backup / Recovery / Migration / Data Safety", "Backup and recovery workflows.", "Recovery"),
    IssueFamily("bundles", "T", "High-value Bundled Support Playbooks", "Quick bundled workflows.", "Bundles"),
)


EVIDENCE_PLANS: tuple[EvidencePlan, ...] = (
    EvidencePlan("system_snapshot", "System Snapshot", "System, driver, service, and startup baseline.", "collector:collect_system_snapshot", ("task_systeminfo_export", "task_hotfixes_export", "task_drivers_export", "task_services_export", "task_scheduled_tasks_export", "task_startup_items_export")),
    EvidencePlan("network_bundle", "Network Bundle", "IP, route, proxy, hosts, Wi-Fi, and DNS evidence.", "collector:collect_network_bundle", ("task_ipconfig_all", "task_route_print", "task_proxy_show", "task_hosts_check", "task_wlan_report", "task_dns_timing")),
    EvidencePlan("update_bundle", "Update Bundle", "Update service, reboot, and servicing evidence.", "collector:collect_update_bundle", ("task_update_services_status", "task_pending_reboot_sources", "task_get_windows_update_log", "task_evtx_windows_update")),
    EvidencePlan("eventlogs_bundle", "Event Logs", "Core EVTX evidence for escalation.", "collector:collect_event_logs", ("task_evtx_application", "task_evtx_system", "task_evtx_setup", "task_evtx_windows_update", "task_evtx_printservice", "task_evtx_devicesetup")),
    EvidencePlan("printer_bundle", "Printer Bundle", "Printer, queue, and PrintService evidence.", "collector:collect_printer_bundle", ("task_printer_status", "task_print_queue", "task_evtx_printservice")),
    EvidencePlan("crash_bundle", "Crash Bundle", "Reliability, minidump, and crash evidence.", "collector:collect_crash_bundle", ("task_reliability_snapshot", "task_minidumps_collect", "task_evtx_application", "task_evtx_system")),
    EvidencePlan("office_bundle", "Office Bundle", "Office, Outlook, and service-health evidence.", "task:task_office_outlook_helper", ("task_office_outlook_helper", "task_service_health_snapshot", "task_wevtutil_application")),
    EvidencePlan("device_bundle", "Device Bundle", "Device inventory, power, and setup evidence.", "task:task_driver_device_inventory_pack", ("task_driver_device_inventory_pack", "task_powercfg_a", "task_wevtutil_system")),
    EvidencePlan("support_bundle", "Support Bundle Export", "Route to Reports for final masking and export.", "route:reports", ()),
    EvidencePlan("backup_snapshot", "Pre-fix Snapshot", "Profile and storage context before risky work.", "task:task_system_profile_belarc_lite", ("task_system_profile_belarc_lite", "task_storage_ranked_view")),
    EvidencePlan("new_pc_validation_bundle", "New PC Validation", "Profile, network, devices, and update posture.", "runbook:it_ticket_triage_pack", ("task_system_profile_belarc_lite", "task_network_evidence_pack", "task_driver_device_inventory_pack", "task_update_repair_evidence_pack")),
)


DIAGNOSTICS: tuple[DiagnosticCheck, ...] = (
    DiagnosticCheck("identity_context", "Identity Context", "Windows sign-in state", "Expected account and provider line up.", "Provider mismatch or stale identity state.", "Card + detail", ("identity",), ("task_whoami", "task_systeminfo_export")),
    DiagnosticCheck("account_lock_status", "Account Lock Signals", "Auth prompt and app sign-in behavior", "No repeated prompt or lock pattern.", "Repeated lockout or auth failures.", "Attention summary", ("identity", "security"), ("task_service_health_snapshot", "task_wevtutil_application")),
    DiagnosticCheck("credential_store_state", "Credential Store State", "Cached credentials and app token posture", "Cached state matches the active account.", "Stale cache or token drift blocks sign-in.", "Risk detail", ("identity", "office", "browser"), ("task_office_outlook_helper",)),
    DiagnosticCheck("profile_health", "Profile Health", "User profile load context", "Normal profile path and load behavior.", "Temp profile or slow profile indicators.", "Finding group", ("shell",), ("task_system_profile_belarc_lite", "task_wevtutil_system")),
    DiagnosticCheck("shell_process_health", "Shell Process Health", "Explorer / Start / taskbar state", "Shell stays stable and responsive.", "Explorer or shell components crash or lag.", "Finding group", ("shell",), ("task_wevtutil_application", "task_service_health_snapshot")),
    DiagnosticCheck("network_baseline", "Network Baseline", "Adapter, route, proxy, and DNS bundle", "Adapter and reachability posture look healthy.", "Adapter, route, proxy, or DNS issues observed.", "Bundle summary", ("network", "remote"), ("task_network_evidence_pack",)),
    DiagnosticCheck("ip_configuration", "IP / Adapter Configuration", "IPConfig and adapter state", "Valid non-APIPA address and adapter state.", "No IP, APIPA, or disabled adapter state.", "Adapter detail", ("network",), ("task_ipconfig_all", "task_ip_release_renew")),
    DiagnosticCheck("dns_resolution", "DNS Resolution", "DNS timing and nslookup", "Normal host resolution timing and responses.", "DNS failure, timeout, or inconsistent resolution.", "Timing table", ("network", "browser", "cloud"), ("task_dns_timing", "task_nslookup_microsoft")),
    DiagnosticCheck("gateway_reachability", "Gateway Reachability", "Route table and gateway clues", "Gateway and routes look healthy.", "Gateway or routing evidence is missing or broken.", "Route card", ("network",), ("task_route_print", "task_ping_1_1_1_1")),
    DiagnosticCheck("wifi_visibility", "Wi-Fi Visibility / Auth", "WLAN report", "Target SSID and auth posture look healthy.", "SSID or auth instability observed.", "Wireless detail", ("remote", "wifi"), ("task_wlan_report", "task_wifi_report_fix_wizard")),
    DiagnosticCheck("vpn_posture", "VPN Posture", "VPN session and internal reachability", "VPN path and internal access are healthy.", "VPN connect/disconnect or internal access issue.", "Guided next step", ("remote", "vpn"), ("task_network_evidence_pack", "task_proxy_show")),
    DiagnosticCheck("remote_access_stack", "Remote Access Stack", "RDP / VDI posture", "Remote session path is reachable.", "RDP / VDI path fails or is unstable.", "Detail card", ("remote", "rdp"), ("task_network_evidence_pack", "task_service_health_snapshot")),
    DiagnosticCheck("printer_queue_status", "Printer / Queue Status", "Printer status and queue snapshot", "Printer online, spooler healthy, queue clear.", "Offline printer, stuck queue, or spooler issue.", "Queue table", ("print",), ("task_printer_status", "task_print_queue")),
    DiagnosticCheck("printer_driver_health", "Printer Driver Health", "Driver and mapping posture", "Expected driver/features are present.", "Driver, mapping, or feature mismatch observed.", "Support detail", ("print",), ("task_driver_device_inventory_pack", "task_evtx_printservice")),
    DiagnosticCheck("browser_state", "Browser State", "Browser rescue summary", "Browser launch and render state look healthy.", "Launch, render, download, or hijack clues observed.", "Issue summary", ("browser",), ("task_browser_rescue", "task_proxy_show")),
    DiagnosticCheck("browser_auth_state", "Browser Auth / SSO", "Pop-up, auth-loop, and trust clues", "SSO and certificate posture look healthy.", "Auth loop, blocked pop-up, or trust issue observed.", "Auth card", ("browser", "identity"), ("task_browser_rescue", "task_hosts_check")),
    DiagnosticCheck("outlook_profile_state", "Outlook Profile State", "Office / Outlook helper", "Profile, mailbox visibility, and prompts look healthy.", "Profile corruption, prompt loop, or mailbox issue.", "Mailbox card", ("email",), ("task_office_outlook_helper",)),
    DiagnosticCheck("mailbox_health", "Mailbox Health", "Mailbox transport/search context", "Transport and search posture look healthy.", "Transport, search, or size issue observed.", "Finding summary", ("email",), ("task_office_outlook_helper", "task_service_health_snapshot")),
    DiagnosticCheck("teams_cache_state", "Teams Cache State", "Teams app state", "Teams cache and sign-in state look healthy.", "Cache corruption or sign-in drift observed.", "Collab detail", ("collab",), ("task_service_health_snapshot", "task_wevtutil_application")),
    DiagnosticCheck("teams_device_state", "Meeting Device Path", "Audio/video device route", "Meeting device path looks healthy.", "Camera, mic, audio route, or lag issue observed.", "Device detail", ("collab", "media"), ("task_audio_mic_helper", "task_camera_privacy_check")),
    DiagnosticCheck("office_activation_state", "Office Activation State", "Office launch and activation context", "Office launch and activation look healthy.", "Activation loop, crash, or runtime drift observed.", "Activation callout", ("sync", "apps"), ("task_office_outlook_helper",)),
    DiagnosticCheck("sync_client_health", "Sync Client Health", "OneDrive / SharePoint sync posture", "Sync status looks healthy.", "Sign-in, sync, cloud-only, or conflict issue observed.", "Sync summary", ("sync",), ("task_onedrive_sync_helper",)),
    DiagnosticCheck("storage_pressure", "Storage Pressure", "Storage radar", "Disk pressure below intervention threshold.", "Low space affects saves, sync, or updates.", "Storage ranking", ("storage", "performance"), ("task_storage_radar", "task_storage_ranked_view")),
    DiagnosticCheck("file_permission_context", "File / Permission Context", "Path and save context", "User can access the target path.", "Permission denied, path-too-long, or access failure.", "Save-path guidance", ("storage",), ("task_systeminfo_export",)),
    DiagnosticCheck("performance_hotspots", "Performance Hotspots", "Performance sample", "CPU, RAM, disk, and thermal pressure look healthy.", "Sustained pressure or heat observed.", "Performance stack", ("performance",), ("task_performance_sample", "task_thermal_hints")),
    DiagnosticCheck("startup_pressure", "Startup / Boot Pressure", "Startup/autostart bundle", "Startup footprint and logon path look healthy.", "Heavy startup load or boot drag observed.", "Startup detail", ("performance",), ("task_startup_autostart_pack", "task_pending_reboot_sources")),
    DiagnosticCheck("app_install_context", "App Install Context", "Installer/runtime context", "Prerequisites and app version state look healthy.", "Installer, runtime, or compatibility issue observed.", "Install guidance", ("apps",), ("task_service_health_snapshot", "task_systeminfo_export")),
    DiagnosticCheck("security_posture", "Security Posture", "Security baseline", "Core security posture looks enabled and current.", "Defender, firewall, extension, or trust issue observed.", "Security summary", ("security",), ("task_firewall_profile_summary", "task_service_health_snapshot")),
    DiagnosticCheck("certificate_trust_state", "Certificate / Trust State", "Certificate and trust posture", "Expected cert and trust posture present.", "Trust, enrollment, or chain issue observed.", "Trust detail", ("security", "enterprise"), ("task_service_health_snapshot",)),
    DiagnosticCheck("media_device_inventory", "Media Device Inventory", "Media helper and device inventory", "Media devices enumerate and route normally.", "Missing, frozen, or misrouted device state.", "Media summary", ("media",), ("task_audio_mic_helper", "task_driver_device_inventory_pack")),
    DiagnosticCheck("display_dock_inventory", "Display / Dock Inventory", "Display and device inventory", "Displays, dock, and peripherals enumerate normally.", "Monitor, dock, USB, or input instability observed.", "Display summary", ("display",), ("task_driver_device_inventory_pack", "task_powercfg_a")),
    DiagnosticCheck("power_battery_state", "Power / Battery State", "PowerCfg and battery posture", "Charging, battery, and sleep state look healthy.", "Charging, battery, or sleep issue observed.", "Power detail", ("power",), ("task_powercfg_a", "task_powercfg_battery")),
    DiagnosticCheck("windows_update_state", "Windows Update State", "Update evidence bundle", "Servicing posture looks healthy.", "Update loop, stuck state, or reboot issue observed.", "Update summary", ("windows_update",), ("task_update_repair_evidence_pack",)),
    DiagnosticCheck("system_integrity_state", "System Integrity State", "Integrity and reliability context", "Integrity posture looks healthy.", "Corruption or integrity failure observed.", "Integrity warning", ("windows_update", "shell", "apps"), ("task_sfc_dism_integrity_tool", "task_reliability_snapshot")),
    DiagnosticCheck("enterprise_posture", "Enterprise Posture", "Join, policy, and compliance state", "Managed posture lines up with expectation.", "Trust, policy, cert, or compliance issue observed.", "Enterprise summary", ("enterprise",), ("task_system_profile_belarc_lite", "task_service_health_snapshot")),
    DiagnosticCheck("backup_readiness", "Backup / Recovery Readiness", "Backup and snapshot readiness", "Backup context is ready for risky work.", "Recovery readiness gap observed.", "Recovery warning", ("recovery",), ("task_system_profile_belarc_lite", "task_storage_ranked_view")),
    DiagnosticCheck("support_bundle_readiness", "Support Bundle Readiness", "Evidence and export posture", "Evidence, masking, and export posture are ready.", "Evidence is incomplete or not reviewed.", "Reports readiness", ("recovery", "bundles"), ("task_eventlog_exporter_pack",)),
    DiagnosticCheck("new_pc_validation", "New PC Validation", "Post-reimage validation", "Core user environment is ready for handoff.", "Post-reimage gap remains.", "Validation checklist", ("bundles", "enterprise"), ("task_system_profile_belarc_lite", "task_network_evidence_pack", "task_driver_device_inventory_pack")),
)


SUPPORT_FIXES: tuple[SupportFix, ...] = (
    SupportFix("open_network_status", "Open Network Status", "Safe", "automatic", "Open Windows network status.", "standard user", "none", True, "Close Settings.", "Capture before/after reachability.", "fix_action:open_network", ("Verify adapter state.", "Retest connectivity.")),
    SupportFix("flush_dns", "Flush DNS Cache", "Safe", "automatic", "Run ipconfig /flushdns.", "standard user", "no restart", True, "DNS cache repopulates automatically.", "Attach DNS timing before/after.", "fix_action:flush_dns", ("Retest DNS resolution.",)),
    SupportFix("restart_spooler", "Restart Print Spooler", "Admin", "automatic", "Restart the spooler service.", "local admin", "printer reconnect may be needed", True, "Reboot or remap queue if needed.", "Capture queue and spooler state before/after.", "fix_action:restart_spooler", ("Verify queue clears.", "Print a test page.")),
    SupportFix("storage_settings", "Open Storage Settings", "Safe", "automatic", "Open Windows storage settings.", "standard user", "none", True, "Close Settings.", "Pair with storage radar output.", "fix_action:open_storage", ("Verify free space improves.",)),
    SupportFix("browser_rescue", "Run Browser Rescue", "Safe", "automatic", "Collect browser, proxy, and auth clues.", "standard user", "none", True, "Read-only helper.", "Stores browser rescue evidence.", "task:task_browser_rescue", ("Review rescue summary.", "Retest site/app.")),
    SupportFix("wifi_report_fix", "Run Wi-Fi Report + Fix Wizard", "Safe", "automatic", "Collect Wi-Fi evidence and safe recovery steps.", "standard user", "adapter reconnect possible", True, "Reconnect if prompted.", "Captures WLAN and DNS evidence.", "task:task_wifi_report_fix_wizard", ("Retest SSID and internet access.",)),
    SupportFix("ip_release_renew", "Release / Renew IP Lease", "Safe", "automatic", "Refresh DHCP lease state.", "standard user", "brief interruption", True, "Escalate if lease does not return.", "Capture IP config before/after.", "task:task_ip_release_renew", ("Confirm valid IP address.",)),
    SupportFix("network_stack_repair", "Run Network Stack Repair", "Admin", "automatic", "Run winsock, TCP/IP, and lease reset chain.", "local admin", "restart likely", True, "Restore prior adapter settings if needed.", "Capture network bundle before/after.", "task:task_network_stack_repair_tool", ("Retest DNS, gateway, and app reachability.",)),
    SupportFix("printer_full_reset", "Run Printer Full Reset", "Admin", "automatic", "Run spooler and queue repair chain.", "local admin", "no full reboot expected", True, "Remap queue if needed.", "Capture printer bundle before/after.", "task:task_printer_full_reset_tool", ("Verify queue is clear.",)),
    SupportFix("audio_mic_helper", "Run Audio / Mic Helper", "Safe", "automatic", "Collect audio endpoint and route clues.", "standard user", "device re-enumeration possible", True, "Re-select the prior endpoint if needed.", "Attach audio summary evidence.", "task:task_audio_mic_helper", ("Retest playback and recording.",)),
    SupportFix("camera_privacy_check", "Run Camera / Privacy Check", "Safe", "automatic", "Collect privacy and camera-access posture.", "standard user", "none", True, "Review settings changes manually.", "Attach camera/privacy summary.", "task:task_camera_privacy_check", ("Retest camera feed.",)),
    SupportFix("onedrive_sync_helper", "Run OneDrive Sync Helper", "Safe", "automatic", "Collect OneDrive sync and sign-in clues.", "standard user", "client rescan possible", True, "Pause/resume sync if needed.", "Attach sync summary evidence.", "task:task_onedrive_sync_helper", ("Retest sync state.",)),
    SupportFix("office_outlook_helper", "Run Office / Outlook Helper", "Safe", "automatic", "Collect Office and Outlook posture.", "standard user", "none", True, "Read-only helper.", "Attach Office / Outlook summary.", "task:task_office_outlook_helper", ("Retest Office / Outlook.",)),
    SupportFix("usb_bt_disconnect_helper", "Run USB / Bluetooth Helper", "Safe", "automatic", "Collect disconnect, power, and device clues.", "standard user", "device reconnect possible", True, "Re-pair or reseat if prompted.", "Attach device bundle evidence.", "task:task_usb_bt_disconnect_helper", ("Retest peripheral stability.",)),
    SupportFix("windows_update_reset", "Run Windows Update Reset Tool", "Admin", "automatic", "Run Windows Update reset chain.", "local admin", "restart likely", True, "Restore from snapshot or escalate if servicing worsens.", "Capture update bundle before/after.", "task:task_windows_update_reset_tool", ("Retest the failed update.",)),
    SupportFix("sfc_dism_integrity", "Run SFC / DISM Integrity Tool", "Admin", "automatic", "Run SFC and DISM for corruption repair.", "local admin", "restart likely", True, "Escalate with logs if integrity still fails.", "Attach integrity logs.", "task:task_sfc_dism_integrity_tool", ("Retest the original symptom.",)),
    SupportFix("service_health_snapshot", "Collect Service Health Snapshot", "Safe", "automatic", "Collect service state without changing it.", "standard user", "none", True, "Read-only helper.", "Attach service snapshot.", "task:task_service_health_snapshot", ("Review required services.",)),
    SupportFix("driver_device_inventory", "Collect Driver / Device Inventory", "Safe", "automatic", "Collect device inventory and setup clues.", "standard user", "none", True, "Read-only helper.", "Attach device inventory evidence.", "task:task_driver_device_inventory_pack", ("Review problem devices.",)),
    SupportFix("firewall_profile_summary", "Collect Firewall / Profile Summary", "Safe", "automatic", "Collect firewall profile state.", "standard user", "none", True, "Read-only helper.", "Attach firewall profile evidence.", "task:task_firewall_profile_summary", ("Review active firewall profile.",)),
    SupportFix("eventlog_export_pack", "Collect Event Log Exporter Pack", "Safe", "automatic", "Export core event logs.", "standard user", "none", True, "Read-only helper.", "Stores EVTX artifacts.", "task:task_eventlog_exporter_pack", ("Verify EVTX output exists.",)),
    SupportFix("support_bundle_export", "Open Support Bundle Flow", "Safe", "guided", "Go to Reports for masking review and export.", "standard user", "none", True, "Close Reports if you stop here.", "Encourages evidence review before escalation.", "route:reports", ("Review evidence checklist.", "Create the right bundle type.")),
    SupportFix("guided_password_reset", "Guided Password / Cached Credential Reset", "Guided", "guided", "Guide password reset and stale credential cleanup.", "service desk", "sign-out may be required", True, "Record original account state first.", "Capture prompt timing and affected apps.", "manual:identity_password_reset", ("Verify Windows and app sign-in.",)),
    SupportFix("guided_unlock_account", "Guided Account Unlock / Status Review", "Guided", "guided", "Guide unlock, disabled-state review, and retest order.", "service desk", "none", True, "Document original lock state first.", "Capture lockout evidence before changes.", "manual:identity_unlock_account", ("Verify the account is unlocked and enabled.",)),
    SupportFix("guided_reset_pin_biometrics", "Guided PIN / Biometric Reset", "Guided", "guided", "Guide Windows Hello reset and re-enrollment.", "standard user", "sign-out may be required", True, "Re-enroll after account posture is healthy.", "Capture the original sign-in error.", "uri:ms-settings:signinoptions", ("Verify PIN or biometric sign-in works.",)),
    SupportFix("guided_profile_shell_repair", "Guided Profile / Shell Repair", "Guided", "guided", "Guide Explorer, shell, and profile repair order.", "standard user", "sign-out may be required", True, "Capture shell/profile evidence before deeper work.", "Attach profile and shell evidence.", "manual:profile_shell_repair", ("Verify Start, taskbar, tray, and Explorer.",)),
    SupportFix("guided_network_drive_reconnect", "Guided Network Drive Reconnect", "Guided", "guided", "Guide UNC path validation and drive remap order.", "standard user", "none", True, "Record original drive letters first.", "Capture path and auth evidence.", "manual:network_drive_reconnect", ("Verify the UNC path and mapped drive.",)),
    SupportFix("guided_vpn_reset", "Guided VPN Client Reset", "Guided", "guided", "Guide VPN client/profile reset and retest order.", "standard user or local admin", "reconnect required", True, "Record the original VPN profile first.", "Capture connect/disconnect timing.", "manual:vpn_client_reset", ("Verify VPN and internal access.",)),
    SupportFix("guided_rdp_remediation", "Guided RDP / VDI Remediation", "Guided", "guided", "Guide reachability, saved credential, and remote-session checks.", "standard user", "reconnect required", True, "Record saved settings first.", "Capture reachability and prompt state.", "manual:rdp_remediation", ("Verify the remote session launches.",)),
    SupportFix("guided_default_printer", "Guided Default Printer / Mapping Review", "Guided", "guided", "Guide default-printer and mapping correction.", "standard user", "none", True, "Record the original default printer first.", "Capture printer state and mapping evidence.", "uri:ms-settings:printers", ("Verify the correct default printer.",)),
    SupportFix("guided_browser_profile_cleanup", "Guided Browser Profile / Extension Cleanup", "Guided", "guided", "Guide extension, pop-up, profile, and hijack cleanup.", "standard user", "browser restart required", True, "Record homepage and key extensions first.", "Capture failing URL and prompts.", "manual:browser_profile_cleanup", ("Verify browser and target site behavior.",)),
    SupportFix("guided_outlook_profile_repair", "Guided Outlook Profile Repair", "Guided", "guided", "Guide Outlook profile recreation and OST/cache review.", "standard user", "Outlook restart required", True, "Keep the old profile until the new one is validated.", "Capture mailbox and profile symptoms.", "manual:outlook_profile_repair", ("Verify Outlook open/sign-in/search state.",)),
    SupportFix("guided_teams_cache_reset", "Guided Teams Cache / Device Reset", "Guided", "guided", "Guide Teams cache reset and device-route cleanup.", "standard user", "Teams restart required", True, "Record the current tenant and device path first.", "Capture meeting/device symptoms.", "manual:teams_cache_reset", ("Verify Teams launch/sign-in/devices.",)),
    SupportFix("guided_sync_resync", "Guided OneDrive / SharePoint Resync", "Guided", "guided", "Guide unlink/relink or library resync order.", "standard user", "sync client restart required", True, "Record the current sync root first.", "Capture conflict and cloud-only evidence.", "manual:onedrive_resync", ("Verify sync completes cleanly.",)),
    SupportFix("guided_disk_cleanup", "Guided Disk Cleanup / Large File Review", "Guided", "guided", "Guide safe cleanup sequencing and user approval.", "standard user", "none", True, "Prefer reversible cleanup paths.", "Capture storage ranking and approval.", "uri:ms-settings:storagesense", ("Verify free space and retry the failing workflow.",)),
    SupportFix("guided_permission_review", "Guided Permission / Save Path Review", "Guided", "guided", "Guide path validation and manual permission review.", "standard user or owner", "none", True, "Revert unapproved manual changes.", "Capture path and error details.", "manual:file_permission_review", ("Verify save/open/delete path behavior.",)),
    SupportFix("guided_default_app_reset", "Guided Default App Reset", "Guided", "guided", "Guide default-app association correction.", "standard user", "none", True, "Record the original association first.", "Capture the affected extension or protocol.", "uri:ms-settings:defaultapps", ("Verify the file or link opens in the expected app.",)),
    SupportFix("guided_security_review", "Guided Security / Trust Review", "Guided", "guided", "Guide Defender, SmartScreen, extension, and trust review.", "standard user or security admin", "app restart may be required", True, "Re-enable protections if the app is later judged unsafe.", "Capture the blocking prompt or cert state.", "manual:security_review", ("Verify trusted workload works without broad security regression.",)),
    SupportFix("guided_bluetooth_repair", "Guided Bluetooth / Peripheral Re-pair", "Guided", "guided", "Guide re-pair and power cleanup for Bluetooth devices.", "standard user", "device reconnect required", True, "Record the original pairing state first.", "Capture disconnect timing and device name.", "uri:ms-settings:bluetooth", ("Verify stable Bluetooth reconnect.",)),
    SupportFix("guided_keyboard_layout_reset", "Guided Keyboard / Locale Reset", "Guided", "guided", "Guide keyboard-layout and input-method correction.", "standard user", "none", True, "Record the original input language first.", "Capture the failing characters and layout.", "manual:keyboard_layout_reset", ("Verify typing matches the expected layout.",)),
    SupportFix("guided_power_sleep_reset", "Guided Power / Sleep Reset", "Guided", "guided", "Guide charging, sleep, wake, and power-plan review.", "standard user or local admin", "sleep cycle required", True, "Record original plan settings first.", "Capture powercfg and battery posture.", "manual:power_sleep_reset", ("Verify charging or sleep/wake behavior.",)),
    SupportFix("guided_time_sync", "Guided Date / Time Resync", "Guided", "guided", "Guide date, time, timezone, and sync correction.", "standard user", "none", True, "Record the original timezone first.", "Capture clock drift symptoms.", "uri:ms-settings:dateandtime", ("Verify time sync and auth-related behavior.",)),
    SupportFix("guided_backup_snapshot", "Guided Backup / Snapshot Workflow", "Guided", "guided", "Guide pre-fix backup and support-pack capture.", "standard user", "none", True, "Do not proceed with risky work before the snapshot exists.", "Capture support bundle and storage inventory first.", "manual:backup_snapshot", ("Verify backup or snapshot location is complete.",)),
    SupportFix("guided_file_restore", "Guided File / Version Recovery", "Guided", "guided", "Guide deleted-file, version, and corruption recovery.", "standard user", "none", True, "Keep recovered copies separate until validated.", "Capture file path and recovery source.", "manual:file_restore", ("Verify recovered file integrity.",)),
    SupportFix("guided_data_migration", "Guided Data Migration / Post-Reimage Restore", "Guided", "guided", "Guide migration, restore, and post-reimage order.", "service desk", "sign-in/out may be required", True, "Keep source data until target is validated.", "Capture pre/post migration inventory.", "manual:data_migration", ("Verify target profile data and app posture.",)),
    SupportFix("escalate_directory_services", "Escalate to Identity / Directory Team", "Escalate", "escalation", "Escalate directory, MFA, certificate, or tenant-side issues.", "identity admin", "none", True, "No rollback required.", "Attach identity and support-bundle evidence.", "route:reports", ("Ensure auth timing and prompts are documented.",)),
    SupportFix("escalate_network_team", "Escalate to Network / Remote Access Team", "Escalate", "escalation", "Escalate persistent network, VPN, or Wi-Fi issues.", "network admin", "none", True, "No rollback required.", "Attach network bundle evidence.", "route:reports", ("Ensure DNS, route, and timing evidence are exported.",)),
    SupportFix("escalate_security_team", "Escalate to Security / Compliance Team", "Escalate", "escalation", "Escalate Defender, SmartScreen, BitLocker, cert, or compliance issues.", "security admin", "none", True, "No rollback required.", "Attach trust and policy evidence.", "route:reports", ("Ensure prompts and policy symptoms are captured.",)),
    SupportFix("escalate_hardware_vendor", "Escalate to Hardware / Vendor Support", "Escalate", "escalation", "Escalate thermal, battery, dock, monitor, or device faults.", "hardware or vendor support", "none", True, "No rollback required.", "Attach device, power, and crash evidence.", "route:reports", ("Ensure serial/model and symptom context are captured.",)),
)
PLAYBOOKS: tuple[SupportPlaybook, ...] = (
    SupportPlaybook("meta_unknown_issue", "I Don't Know What's Wrong", "Fast baseline for unclear symptoms.", ("general triage", "unknown issue"), ("Start from a live session.",), ("support_bundle_readiness", "network_baseline", "performance_hotspots"), ("service_health_snapshot",), ("support_bundle_export",), ("Escalate if the issue family still is not clear after baseline review.",), ("system_snapshot", "support_bundle"), "Evidence-first; no direct rollback.", ("Review the top findings and route into the right family.",), 12, "guided", "Safe", "runbook:it_ticket_triage_pack", ("route:diagnose", "route:reports")),
    SupportPlaybook("meta_cant_sign_in", "I Can't Sign In", "Identity workflow for Windows, app, and MFA sign-in issues.", ("can't sign in", "password rejected", "MFA issue"), ("Know the expected account type.",), ("identity_context", "account_lock_status", "credential_store_state"), (), ("guided_password_reset", "guided_unlock_account", "guided_reset_pin_biometrics", "escalate_directory_services"), ("Escalate if tenant, directory, or smart-card action is required.",), ("system_snapshot", "support_bundle"), "Record current identity state before clearing caches.", ("Verify Windows and app sign-in using the expected account.",), 15, "guided", "Guided", "task:task_office_outlook_helper", ("route:fixes", "route:reports")),
    SupportPlaybook("identity_credential_repair", "Sign-in / Credential Repair", "Password, lockout, PIN, biometric, and smart-card workflow.", ("password rejected", "domain login failed", "PIN broken"), ("Know whether the account is local, domain, or Entra.",), ("identity_context", "account_lock_status", "credential_store_state"), (), ("guided_password_reset", "guided_unlock_account", "guided_reset_pin_biometrics", "escalate_directory_services"), ("Escalate for disabled-account, tenant, or smart-card actions.",), ("system_snapshot", "support_bundle"), "Document the original account state first.", ("Verify Windows sign-in and retest dependent apps.",), 18, "guided", "Guided", "task:task_office_outlook_helper", ("route:fixes", "route:reports")),
    SupportPlaybook("identity_token_cache_reset", "Token / Cache Reset", "Browser/app auth-loop and stale-token workflow.", ("auth loop", "password changed but apps still fail"), ("Confirm the current Windows and app account context.",), ("credential_store_state", "browser_auth_state", "identity_context"), (), ("guided_password_reset", "guided_browser_profile_cleanup", "guided_teams_cache_reset", "escalate_directory_services"), ("Escalate for tenant, MFA, or trust-chain issues.",), ("support_bundle", "office_bundle"), "Document the current sign-in context before clearing app/browser state.", ("Verify clean re-auth without prompt loops.",), 14, "guided", "Guided", "task:task_browser_rescue", ("task:task_office_outlook_helper", "route:reports")),
    SupportPlaybook("network_baseline_repair", "Network Baseline and Repair", "Broad network baseline plus safe repair actions.", ("no internet", "connected but no resources", "slow network"), ("Confirm whether the issue affects internet, internal, or both.",), ("network_baseline", "ip_configuration", "dns_resolution", "gateway_reachability"), ("open_network_status", "flush_dns", "ip_release_renew"), ("network_stack_repair", "guided_network_drive_reconnect", "escalate_network_team"), ("Escalate for adapter, routing, or site-side faults after baseline repair.",), ("network_bundle", "support_bundle"), "Capture the pre-repair bundle before admin resets.", ("Verify internet and internal reachability.",), 20, "guided", "Safe -> Admin", "runbook:it_network_stack_repair", ("task:task_network_evidence_pack", "route:reports")),
    SupportPlaybook("dns_ip_adapter_repair", "DNS / IP / Adapter Repair", "APIPA, DNS failure, gateway, and lease workflow.", ("no IP", "DNS failure", "gateway unreachable"), ("Record current adapter and whether the issue is wired or wireless.",), ("ip_configuration", "dns_resolution", "gateway_reachability"), ("flush_dns", "ip_release_renew"), ("network_stack_repair", "escalate_network_team"), ("Escalate for upstream DHCP, DNS, or switch-side faults.",), ("network_bundle", "support_bundle"), "Do not run the reset chain without pre-change network evidence.", ("Verify a valid IP and successful name resolution.",), 16, "guided", "Safe -> Admin", "task:task_network_evidence_pack", ("task:task_ip_release_renew", "route:reports")),
    SupportPlaybook("vpn_remote_access_repair", "VPN / Remote Access Repair", "Wi-Fi auth, VPN, RDP, remote app, and VDI workflow.", ("wifi won't connect", "VPN won't connect", "RDP cannot connect"), ("Confirm whether the issue is Wi-Fi-only, VPN-only, or remote-session-specific.",), ("wifi_visibility", "vpn_posture", "remote_access_stack", "network_baseline"), ("wifi_report_fix", "open_network_status"), ("guided_vpn_reset", "guided_rdp_remediation", "escalate_network_team"), ("Escalate for profile-, gateway-, or remote-service-side faults.",), ("network_bundle", "support_bundle"), "Capture Wi-Fi/VPN posture before clearing or reimporting profiles.", ("Verify Wi-Fi or VPN plus internal access.",), 18, "guided", "Guided", "task:task_wifi_report_fix_wizard", ("route:fixes", "route:reports")),
    SupportPlaybook("printer_spooler_repair", "Printer / Spooler Repair", "Printer offline, queue, spooler, and feature workflow.", ("printer offline", "jobs stuck", "can't add printer"), ("Know the printer name and whether it is direct or shared.",), ("printer_queue_status", "printer_driver_health"), ("restart_spooler",), ("printer_full_reset", "guided_default_printer", "escalate_network_team"), ("Escalate for print-server policy or broken enterprise mappings.",), ("printer_bundle", "support_bundle"), "Record the original default printer and queue mapping.", ("Verify queue, spooler, and test print.",), 17, "guided", "Safe -> Admin", "runbook:home_printer_rescue", ("task:task_printer_status", "route:reports")),
    SupportPlaybook("browser_repair", "Browser Repair", "Browser launch, speed, download, hijack, and rendering workflow.", ("browser won't open", "browser slow", "site broken"), ("Know whether the issue affects one site or all browsing.",), ("browser_state", "browser_auth_state", "dns_resolution"), ("browser_rescue", "open_network_status"), ("guided_browser_profile_cleanup", "guided_security_review", "escalate_security_team"), ("Escalate if policy, cert, or protection keeps reapplying the issue.",), ("network_bundle", "support_bundle"), "Document homepage, extensions, and prompts before cleanup.", ("Verify the browser and target site behavior.",), 16, "guided", "Guided", "runbook:home_browser_problems", ("route:fixes", "route:reports")),
    SupportPlaybook("sso_web_auth_repair", "SSO / Web Auth Repair", "Browser SSO loop, blocked pop-up, and certificate workflow.", ("SSO loop", "auth loop", "popup blocked"), ("Capture the exact site URL and auth behavior.",), ("browser_auth_state", "identity_context", "certificate_trust_state"), (), ("guided_browser_profile_cleanup", "guided_security_review", "escalate_directory_services"), ("Escalate for federation, cert, or tenant-side faults.",), ("support_bundle",), "Document current account context before clearing browser state.", ("Verify clean site sign-in without loops.",), 12, "guided", "Guided", "task:task_browser_rescue", ("route:reports",)),
    SupportPlaybook("outlook_mailbox_repair", "Outlook / Mailbox Repair", "Outlook profile, prompt, mailbox, OST, and search workflow.", ("Outlook won't open", "repeated prompts", "shared mailbox missing"), ("Know whether the failure is client-only or service-wide.",), ("outlook_profile_state", "mailbox_health", "credential_store_state"), ("office_outlook_helper",), ("guided_outlook_profile_repair", "guided_password_reset", "escalate_directory_services"), ("Escalate for mailbox or auth service-side faults.",), ("office_bundle", "support_bundle"), "Keep the old profile until the rebuilt profile is healthy.", ("Verify Outlook open, sign-in, sync, and search state.",), 20, "guided", "Guided", "runbook:it_office_outlook_triage", ("route:fixes", "route:reports")),
    SupportPlaybook("teams_meeting_repair", "Teams / Meeting Repair", "Teams launch, sign-in, cache, device, and meeting-quality workflow.", ("Teams won't open", "camera not working", "audio echo"), ("Confirm whether the issue is app state, device path, or network quality.",), ("teams_cache_state", "teams_device_state", "network_baseline"), ("audio_mic_helper", "camera_privacy_check"), ("guided_teams_cache_reset", "guided_security_review", "escalate_network_team"), ("Escalate if meeting quality remains poor after device posture is healthy.",), ("device_bundle", "support_bundle"), "Document the original device routing before cache cleanup.", ("Verify Teams launch, sign-in, and device route.",), 18, "guided", "Guided", "task:task_audio_mic_helper", ("route:fixes", "route:reports")),
    SupportPlaybook("onedrive_sharepoint_sync_repair", "OneDrive / SharePoint Sync Repair", "OneDrive auth, sync drift, library sync, and conflict workflow.", ("OneDrive not syncing", "SharePoint won't sync", "cloud-only files"), ("Know whether the issue is OneDrive, KFM, or SharePoint sync.",), ("sync_client_health", "storage_pressure", "dns_resolution"), ("onedrive_sync_helper",), ("guided_sync_resync", "guided_disk_cleanup", "escalate_directory_services"), ("Escalate for tenant policy or library ownership issues.",), ("office_bundle", "support_bundle"), "Document the affected library and sync root before relinking.", ("Verify sign-in, sync, and file freshness.",), 18, "guided", "Guided", "runbook:home_onedrive_not_syncing", ("route:fixes", "route:reports")),
    SupportPlaybook("slow_pc_triage", "Slow PC Triage", "CPU, RAM, disk, startup, and freeze workflow.", ("slow PC", "high CPU", "freezes"), ("Capture whether the issue is constant, on login, or app-specific.",), ("performance_hotspots", "startup_pressure", "storage_pressure"), ("storage_settings",), ("guided_disk_cleanup", "escalate_hardware_vendor"), ("Escalate for thermal, disk, or hardware faults after safe cleanup.",), ("system_snapshot", "support_bundle"), "Capture a baseline before cleanup or deeper repair.", ("Verify the machine is more responsive for the original workload.",), 16, "guided", "Safe -> Escalate", "runbook:home_speed_up_pc_safe", ("task:task_performance_sample", "route:reports")),
    SupportPlaybook("disk_cleanup_storage_recovery", "Disk Cleanup / Storage Recovery", "Low space, save failure, path, and storage recovery workflow.", ("disk full", "can't save", "path too long"), ("Confirm whether the issue is local, network, or cloud-backed.",), ("storage_pressure", "file_permission_context", "backup_readiness"), ("storage_settings",), ("guided_disk_cleanup", "guided_permission_review", "guided_backup_snapshot"), ("Escalate if data-risk is higher than cleanup alone can solve.",), ("backup_snapshot", "support_bundle"), "Capture storage and path context before deleting or moving data.", ("Verify free space or path access is restored.",), 18, "guided", "Guided", "runbook:home_free_up_space_safe", ("route:fixes", "route:reports")),
    SupportPlaybook("profile_shell_repair", "Profile / Shell Repair", "Temp profile, Start, tray, and Explorer workflow.", ("temporary profile", "Start menu broken", "Explorer crashing"), ("Know whether the issue is per-user or system-wide.",), ("profile_health", "shell_process_health", "system_integrity_state"), (), ("guided_profile_shell_repair", "sfc_dism_integrity", "escalate_hardware_vendor"), ("Escalate if shell/profile damage survives guided repair.",), ("system_snapshot", "eventlogs_bundle", "support_bundle"), "Capture shell and profile evidence before deeper repair.", ("Verify expected profile and shell behavior.",), 18, "guided", "Guided -> Admin", "manual:profile_shell_repair", ("task:task_system_profile_belarc_lite", "route:reports")),
    SupportPlaybook("audio_video_device_triage", "Audio / Video Device Triage", "Audio, mic, webcam, and media path workflow.", ("no sound", "mic not detected", "camera black"), ("Confirm whether the issue affects one app or all apps.",), ("media_device_inventory", "teams_device_state"), ("audio_mic_helper", "camera_privacy_check"), ("guided_bluetooth_repair", "guided_security_review", "escalate_hardware_vendor"), ("Escalate if the device does not enumerate or hardware posture remains broken.",), ("device_bundle", "support_bundle"), "Capture the current default endpoint and device inventory first.", ("Verify the intended app sees the expected device.",), 16, "guided", "Guided", "runbook:home_no_audio_mic", ("route:fixes", "route:reports")),
    SupportPlaybook("display_dock_triage", "Display / Dock Triage", "Monitor, dock, USB, Bluetooth, and input workflow.", ("monitor not detected", "dock not working", "USB not recognized"), ("Confirm whether the issue appears only when docked or only on one device family.",), ("display_dock_inventory", "power_battery_state"), ("driver_device_inventory", "usb_bt_disconnect_helper"), ("guided_bluetooth_repair", "guided_keyboard_layout_reset", "escalate_hardware_vendor"), ("Escalate for dock firmware, monitor, or USB hardware faults.",), ("device_bundle", "support_bundle"), "Capture dock and display posture before reinstall or firmware escalation.", ("Verify the display or device enumerates and stays stable.",), 18, "guided", "Guided", "runbook:home_usb_bt_disconnects", ("route:fixes", "route:reports")),
    SupportPlaybook("windows_update_repair", "Windows Update Repair", "Stuck update, loop, feature-update, and pending-reboot workflow.", ("Windows Update stuck", "feature update failing", "pending reboot"), ("Capture the exact update code or symptom before reset.",), ("windows_update_state", "system_integrity_state"), (), ("windows_update_reset", "escalate_network_team"), ("Escalate if servicing still fails after reset and evidence capture.",), ("update_bundle", "support_bundle"), "Always capture pre-reset evidence first.", ("Verify update services and retest the failed update.",), 25, "guided", "Admin", "runbook:it_windows_update_repair", ("route:reports",)),
    SupportPlaybook("os_health_integrity_repair", "SFC / DISM / OS Health Repair", "Corruption, integrity, BSOD follow-up, and recovery workflow.", ("system corruption", "SFC errors", "BSOD"), ("Capture crash or update evidence before integrity repair.",), ("system_integrity_state", "windows_update_state", "performance_hotspots"), (), ("sfc_dism_integrity", "escalate_hardware_vendor"), ("Escalate if integrity tools fail or crash evidence points to hardware.",), ("crash_bundle", "update_bundle", "support_bundle"), "Compare results before repeating integrity chains.", ("Verify integrity scans complete and the original symptom improves.",), 35, "guided", "Admin", "runbook:it_system_integrity_check", ("route:reports",)),
    SupportPlaybook("enterprise_posture_check", "Intune / GPO / Certificate Posture Check", "Join, compliance, GPO, cert, and LAPS workflow.", ("domain trust issue", "Intune not checking in", "GPO not applying"), ("Know whether the device is domain joined, Entra joined, Intune managed, or hybrid.",), ("enterprise_posture", "certificate_trust_state", "identity_context"), ("service_health_snapshot", "firewall_profile_summary"), ("guided_security_review", "escalate_directory_services", "escalate_security_team"), ("Escalate when tenant, PKI, or directory-side action is required.",), ("system_snapshot", "support_bundle"), "Capture management posture before trust-sensitive changes.", ("Verify the expected trust and compliance posture.",), 20, "guided", "Guided", "runbook:it_ticket_triage_pack", ("route:reports",)),
    SupportPlaybook("support_bundle_export_playbook", "Support Bundle Export", "Evidence review and export workflow.", ("support bundle", "export diagnostics", "escalate"), ("Have an active or reopened session.",), ("support_bundle_readiness",), ("eventlog_export_pack",), ("support_bundle_export",), ("Escalate if evidence is still incomplete after review.",), ("support_bundle", "eventlogs_bundle"), "No rollback required.", ("Verify masking, evidence checklist, and bundle type before export.",), 10, "guided", "Safe", "route:reports", ("task:task_eventlog_exporter_pack",)),
    SupportPlaybook("new_pc_post_reimage_validation", "New PC / Post-Reimage Validation", "Structured validation after new PC provisioning or reimage.", ("new PC validation", "post reimage"), ("The user should be signed in and core apps should be present.",), ("new_pc_validation", "enterprise_posture", "network_baseline", "display_dock_inventory"), ("driver_device_inventory", "service_health_snapshot"), ("guided_data_migration", "support_bundle_export", "escalate_directory_services"), ("Escalate when policy or provisioning still fails after validation.",), ("new_pc_validation_bundle", "support_bundle"), "Keep the source device/profile until validation is complete.", ("Verify network, apps, devices, updates, and sync for handoff.",), 25, "guided", "Guided", "route:reports", ("runbook:it_ticket_triage_pack",)),
    SupportPlaybook("backup_pre_fix_snapshot_workflow", "Backup / Pre-fix Snapshot Workflow", "Backup and recovery context before risky troubleshooting.", ("backup before troubleshooting", "snapshot before fix"), ("Know what data and apps are in scope.",), ("backup_readiness", "support_bundle_readiness"), ("service_health_snapshot",), ("guided_backup_snapshot", "guided_file_restore", "guided_data_migration"), ("Escalate if recovery tooling is insufficient for safe change.",), ("backup_snapshot", "support_bundle"), "Do not proceed with risky repair until the snapshot path is confirmed.", ("Verify recovery context exists before risky changes.",), 12, "guided", "Guided", "route:reports", ("task:task_system_profile_belarc_lite",)),
    SupportPlaybook("quick_office_outlook_bundle", "Quick Office / Outlook Repair Bundle", "High-value Office and Outlook quick bundle.", ("Outlook password", "Outlook won't open", "Office activation loop"), ("Use when Outlook or Office is the primary symptom.",), ("outlook_profile_state", "mailbox_health", "office_activation_state"), ("office_outlook_helper",), ("guided_outlook_profile_repair", "guided_password_reset"), ("Escalate when tenant-side mailbox state is the real blocker.",), ("office_bundle", "support_bundle"), "Document the original profile before recreation.", ("Verify Outlook or Office sign-in and launch.",), 14, "guided", "Guided", "runbook:it_office_outlook_triage", ("route:fixes",)),
    SupportPlaybook("quick_browser_bundle", "Quick Browser Repair Bundle", "High-value browser quick bundle.", ("browser broken", "web app broken", "certificate warning"), ("Use when browser or web apps are the main symptom.",), ("browser_state", "browser_auth_state", "dns_resolution"), ("browser_rescue",), ("guided_browser_profile_cleanup", "guided_security_review"), ("Escalate when trust or policy re-applies the issue.",), ("network_bundle", "support_bundle"), "Capture the failing site and prompt first.", ("Verify the browser and target site.",), 12, "guided", "Guided", "runbook:home_browser_problems", ("route:fixes",)),
    SupportPlaybook("quick_print_bundle", "Quick Print Repair Bundle", "High-value printer quick bundle.", ("printer offline", "print jobs stuck", "PDF won't print"), ("Use when queue or spooler is the main symptom.",), ("printer_queue_status", "printer_driver_health"), ("restart_spooler",), ("printer_full_reset", "guided_default_printer"), ("Escalate when server-side queue or policy mapping is broken.",), ("printer_bundle", "support_bundle"), "Capture queue state before repair.", ("Verify queue clears and test print succeeds.",), 12, "guided", "Safe -> Admin", "runbook:home_printer_rescue", ("route:fixes",)),
    SupportPlaybook("quick_teams_bundle", "Quick Teams / Meeting Repair Bundle", "High-value Teams and meeting-device quick bundle.", ("Teams mic", "Teams camera", "meeting lag"), ("Use when collaboration tooling is the main symptom.",), ("teams_cache_state", "teams_device_state", "network_baseline"), ("audio_mic_helper", "camera_privacy_check"), ("guided_teams_cache_reset", "guided_security_review"), ("Escalate when meeting quality remains poor after device posture is healthy.",), ("device_bundle", "support_bundle"), "Capture original device route and tenant first.", ("Verify Teams launch, sign-in, and devices.",), 14, "guided", "Guided", "task:task_audio_mic_helper", ("route:fixes",)),
    SupportPlaybook("quick_slow_pc_bundle", "Quick Slow-PC Cleanup Bundle", "High-value slow-PC quick bundle.", ("PC very slow", "high CPU", "100% disk"), ("Use when general performance degradation is the visible issue.",), ("performance_hotspots", "startup_pressure", "storage_pressure"), ("storage_settings",), ("guided_disk_cleanup", "escalate_hardware_vendor"), ("Escalate when thermal or hardware posture remains bad after safe cleanup.",), ("system_snapshot", "support_bundle"), "Capture a baseline before cleanup.", ("Verify improved responsiveness for the original workload.",), 14, "guided", "Safe -> Escalate", "runbook:home_speed_up_pc_safe", ("route:fixes",)),
    SupportPlaybook("quick_profile_shell_bundle", "Quick Profile / Shell Repair Bundle", "High-value profile and shell quick bundle.", ("temporary profile", "Start menu not opening", "taskbar icons missing"), ("Use when shell/profile is the primary issue.",), ("profile_health", "shell_process_health", "system_integrity_state"), (), ("guided_profile_shell_repair", "sfc_dism_integrity"), ("Escalate if shell/profile damage survives guided repair.",), ("system_snapshot", "eventlogs_bundle", "support_bundle"), "Capture profile and shell evidence first.", ("Verify profile and shell behavior.",), 14, "guided", "Guided -> Admin", "manual:profile_shell_repair", ("route:fixes",)),
    SupportPlaybook("quick_support_export_bundle", "Quick Support Export Bundle", "High-value evidence export bundle.", ("support export", "send logs", "collect evidence"), ("Use when the immediate goal is escalation-ready evidence.",), ("support_bundle_readiness",), ("eventlog_export_pack",), ("support_bundle_export",), ("Escalate if evidence still looks incomplete after review.",), ("support_bundle", "eventlogs_bundle"), "No rollback required.", ("Verify Reports shows the expected evidence and masking posture.",), 10, "guided", "Safe", "route:reports", ("task:task_eventlog_exporter_pack",)),
)
_ISSUE_SPECS: dict[str, tuple[dict[str, Any], ...]] = {
    "identity": (
        _spec("identity_windows_password_rejected", "Credentials", "Windows password rejected", aliases=("can't sign in", "password incorrect"), playbooks=("meta_cant_sign_in",), fixes=("guided_password_reset", "guided_unlock_account")),
        _spec("identity_domain_login_fails", "Credentials", "Domain login fails", aliases=("AD login failed",), fixes=("guided_password_reset", "guided_unlock_account", "escalate_directory_services")),
        _spec("identity_entra_signin_loop", "Federation / Cloud Identity", "Azure / Entra sign-in loop", aliases=("entra auth loop",), playbooks=("identity_token_cache_reset",), fixes=("guided_password_reset", "guided_browser_profile_cleanup", "escalate_directory_services"), network=True),
        _spec("identity_mfa_prompt_missing", "MFA", "MFA prompt not appearing", aliases=("no MFA prompt",), playbooks=("identity_token_cache_reset",), fixes=("guided_password_reset", "escalate_directory_services"), network=True),
        _spec("identity_account_locked_repeatedly", "Lockout", "Account locked out repeatedly", aliases=("repeated lockout",), fixes=("guided_unlock_account", "escalate_directory_services")),
        _spec("identity_password_changed_apps_fail", "Cached Credentials", "Password changed but apps still fail", aliases=("changed password still prompts",), playbooks=("identity_token_cache_reset",), fixes=("guided_password_reset", "guided_browser_profile_cleanup")),
        _spec("identity_account_disabled", "Account State", "Account disabled state", aliases=("account disabled",), fixes=("guided_unlock_account", "escalate_directory_services"), severity="high"),
        _spec("identity_pin_signin_broken", "Windows Hello", "PIN sign-in broken", aliases=("pin not working",), fixes=("guided_reset_pin_biometrics",)),
        _spec("identity_biometric_signin_failure", "Windows Hello", "Biometric sign-in failure", aliases=("fingerprint not working", "face sign in failed"), fixes=("guided_reset_pin_biometrics",)),
        _spec("identity_smartcard_certificate_signin_failure", "Certificates / Smart Card", "Smart card / certificate sign-in failure", aliases=("smart card failed",), diagnostics=("certificate_trust_state",), fixes=("escalate_directory_services",), severity="high"),
    ),
    "shell": (
        _spec("shell_temporary_profile_loaded", "Profile", "Temporary profile loaded", aliases=("temp profile",), fixes=("guided_profile_shell_repair", "guided_backup_snapshot"), severity="high"),
        _spec("shell_desktop_icons_missing", "Desktop", "Desktop icons missing", aliases=("desktop empty",), fixes=("guided_profile_shell_repair",)),
        _spec("shell_taskbar_icons_missing", "Taskbar", "Taskbar icons missing or blank", aliases=("taskbar blank",), fixes=("guided_profile_shell_repair",)),
        _spec("shell_start_menu_not_opening", "Start Menu", "Start menu not opening", aliases=("start button not working",), fixes=("guided_profile_shell_repair", "sfc_dism_integrity")),
        _spec("shell_search_not_working", "Search", "Search not working", aliases=("windows search broken",), fixes=("guided_profile_shell_repair", "sfc_dism_integrity")),
        _spec("shell_right_click_broken_slow", "Explorer", "Right-click menu broken or slow", aliases=("context menu slow",), fixes=("guided_profile_shell_repair",)),
        _spec("shell_explorer_crashing", "Explorer", "File Explorer crashing", aliases=("explorer crashes",), fixes=("guided_profile_shell_repair", "sfc_dism_integrity"), severity="high"),
        _spec("shell_system_tray_icons_missing", "Taskbar / Tray", "System tray icons missing", aliases=("tray icons gone",), fixes=("guided_profile_shell_repair",)),
        _spec("shell_wallpaper_theme_not_applying", "Theme", "Wallpaper / theme not applying", aliases=("theme not saving",), fixes=("guided_profile_shell_repair",)),
        _spec("shell_profile_loads_slowly", "Profile", "User profile loads very slowly", aliases=("slow login profile",), diagnostics=("startup_pressure",), fixes=("guided_profile_shell_repair", "guided_backup_snapshot")),
    ),
    "network": (
        _spec("network_no_internet_access", "Connectivity", "No internet access", aliases=("internet not working", "no internet"), playbooks=("meta_unknown_issue",), fixes=("open_network_status", "flush_dns", "network_stack_repair"), severity="high", network=True),
        _spec("network_connected_no_resources", "Connectivity", "Connected but no network resources", aliases=("connected no resources",), fixes=("open_network_status", "guided_network_drive_reconnect", "network_stack_repair"), network=True),
        _spec("network_no_ip_apipa", "DHCP / Adapter", "No IP / APIPA address", aliases=("169.254 address", "apipa"), playbooks=("dns_ip_adapter_repair",), fixes=("ip_release_renew", "network_stack_repair"), severity="high", network=True),
        _spec("network_dns_resolution_failing", "DNS", "DNS resolution failing", aliases=("dns broken",), playbooks=("dns_ip_adapter_repair",), fixes=("flush_dns", "network_stack_repair"), network=True),
        _spec("network_default_gateway_unreachable", "Routing", "Default gateway unreachable", aliases=("can't ping gateway",), playbooks=("dns_ip_adapter_repair",), fixes=("open_network_status", "network_stack_repair"), network=True),
        _spec("network_intermittent_drops", "Stability", "Intermittent network drops", aliases=("network keeps dropping",), fixes=("open_network_status", "network_stack_repair", "escalate_network_team"), network=True),
        _spec("network_slow_speed", "Performance", "Slow network speed", aliases=("internet slow",), fixes=("open_network_status", "escalate_network_team"), network=True),
        _spec("network_internet_works_internal_apps_fail", "Routing / Proxy", "Internet works but internal apps fail", aliases=("internal apps broken only",), fixes=("guided_network_drive_reconnect", "escalate_network_team"), network=True),
        _spec("network_internal_works_internet_fails", "Routing / Proxy", "Internal works but internet fails", aliases=("LAN works no internet",), fixes=("flush_dns", "open_network_status", "network_stack_repair"), network=True),
        _spec("network_mapped_drives_unavailable", "Shares", "Mapped network drives unavailable", aliases=("mapped drives gone",), fixes=("guided_network_drive_reconnect", "guided_password_reset"), network=True),
    ),
    "remote": (
        _spec("remote_wifi_wont_connect", "Wi-Fi", "Wi-Fi won't connect", aliases=("can't join wifi",), fixes=("wifi_report_fix", "guided_vpn_reset"), network=True),
        _spec("remote_wifi_unstable", "Wi-Fi", "Wi-Fi unstable", aliases=("wifi keeps dropping",), fixes=("wifi_report_fix", "escalate_network_team"), network=True),
        _spec("remote_ssid_not_visible", "Wi-Fi", "SSID not visible", aliases=("can't see wifi",), fixes=("wifi_report_fix", "escalate_network_team"), network=True),
        _spec("remote_corporate_wifi_auth_failing", "Wi-Fi Auth", "Corporate Wi-Fi auth failing", aliases=("enterprise wifi auth failed",), fixes=("wifi_report_fix", "escalate_network_team"), severity="high", network=True),
        _spec("remote_vpn_wont_connect", "VPN", "VPN won't connect", aliases=("vpn failed",), fixes=("guided_vpn_reset", "escalate_network_team"), network=True),
        _spec("remote_vpn_connects_no_internal_access", "VPN", "VPN connects but no internal access", aliases=("vpn connected but no shares",), fixes=("guided_vpn_reset", "escalate_network_team"), network=True),
        _spec("remote_vpn_disconnects_repeatedly", "VPN", "VPN disconnects repeatedly", aliases=("vpn keeps dropping",), fixes=("guided_vpn_reset", "escalate_network_team"), network=True),
        _spec("remote_rdp_cannot_connect", "Remote Desktop", "RDP cannot connect", aliases=("rdp failed",), fixes=("guided_rdp_remediation", "escalate_network_team"), network=True),
        _spec("remote_vdi_wont_launch", "VDI / Remote App", "Remote app / VDI won't launch", aliases=("VDI not opening",), fixes=("guided_rdp_remediation", "escalate_network_team"), network=True),
        _spec("remote_remote_desktop_slow", "Remote Desktop", "Remote desktop very slow", aliases=("rdp lag",), fixes=("guided_rdp_remediation", "escalate_network_team"), network=True),
    ),
    "print": (
        _spec("print_printer_offline", "Printer Status", "Printer offline", aliases=("printer unavailable",), fixes=("restart_spooler", "guided_default_printer", "printer_full_reset")),
        _spec("print_cannot_add_printer", "Printer Setup", "Cannot add printer", aliases=("can't install printer",), fixes=("guided_default_printer", "printer_full_reset")),
        _spec("print_jobs_stuck", "Print Queue", "Print jobs stuck", aliases=("queue stuck",), fixes=("restart_spooler", "printer_full_reset")),
        _spec("print_wrong_default_printer", "Printer Setup", "Wrong default printer", aliases=("default printer wrong",), fixes=("guided_default_printer",)),
        _spec("print_gibberish_output", "Output Quality", "Prints gibberish", aliases=("printer prints random characters",), fixes=("printer_full_reset", "guided_default_printer")),
        _spec("print_blank_pages", "Output Quality", "Blank pages", aliases=("printer prints blank",), fixes=("printer_full_reset",)),
        _spec("print_scanner_not_detected", "Scanner", "Scanner not detected", aliases=("scanner missing",), fixes=("driver_device_inventory", "escalate_hardware_vendor")),
        _spec("print_scan_to_email_not_working", "Scanner", "Scan to email not working", aliases=("scan to email failed",), fixes=("escalate_network_team",)),
        _spec("print_pdf_wont_print_correctly", "Document Output", "PDF won't print correctly", aliases=("pdf printing broken",), fixes=("printer_full_reset", "guided_default_printer")),
        _spec("print_mfp_features_missing", "Printer Features", "MFP features missing", aliases=("scan options missing",), fixes=("driver_device_inventory", "guided_default_printer")),
    ),
    "browser": (
        _spec("browser_browser_wont_open", "Launch", "Browser won't open", aliases=("browser won't start",), playbooks=("quick_browser_bundle",), fixes=("browser_rescue", "guided_browser_profile_cleanup")),
        _spec("browser_browser_very_slow", "Performance", "Browser very slow", aliases=("browser laggy",), playbooks=("quick_browser_bundle",), fixes=("browser_rescue", "guided_browser_profile_cleanup")),
        _spec("browser_pages_not_loading_properly", "Rendering", "Pages not loading properly", aliases=("web pages broken",), fixes=("browser_rescue", "flush_dns")),
        _spec("browser_single_website_broken", "Website-Specific", "Single website broken", aliases=("only one site broken",), playbooks=("sso_web_auth_repair",), fixes=("browser_rescue", "guided_browser_profile_cleanup")),
        _spec("browser_browser_crashing", "Stability", "Browser crashing", aliases=("browser crashes",), fixes=("browser_rescue", "guided_browser_profile_cleanup"), severity="high"),
        _spec("browser_downloads_failing", "Downloads", "Downloads failing", aliases=("can't download files",), fixes=("browser_rescue", "guided_browser_profile_cleanup")),
        _spec("browser_required_popups_blocked", "Pop-ups", "Required pop-ups blocked", aliases=("popup blocked",), playbooks=("sso_web_auth_repair",), fixes=("guided_browser_profile_cleanup",)),
        _spec("browser_hijacked_unwanted_pages", "Hijack / Extensions", "Browser hijacked / unwanted pages", aliases=("homepage hijacked",), fixes=("guided_browser_profile_cleanup", "guided_security_review"), severity="high"),
        _spec("browser_sso_auth_loop", "SSO / Auth", "SSO / auth loop in browser", aliases=("web auth loop",), playbooks=("sso_web_auth_repair",), fixes=("guided_browser_profile_cleanup", "escalate_directory_services"), network=True),
        _spec("browser_certificate_warning", "Trust", "Browser certificate warning", aliases=("website certificate warning",), playbooks=("sso_web_auth_repair",), diagnostics=("certificate_trust_state",), fixes=("guided_security_review", "escalate_security_team"), severity="high"),
    ),
    "email": (
        _spec("email_outlook_wont_open", "Launch", "Outlook won't open", aliases=("outlook won't start",), playbooks=("quick_office_outlook_bundle",), fixes=("office_outlook_helper", "guided_outlook_profile_repair")),
        _spec("email_outlook_password_prompts", "Authentication", "Outlook password prompts repeatedly", aliases=("outlook keeps asking for password",), playbooks=("quick_office_outlook_bundle",), fixes=("office_outlook_helper", "guided_password_reset", "guided_outlook_profile_repair")),
        _spec("email_cannot_send_receive", "Transport", "Cannot send or receive mail", aliases=("mail not sending",), fixes=("office_outlook_helper", "guided_outlook_profile_repair")),
        _spec("email_loading_profile", "Profile", "Outlook stuck on Loading Profile", aliases=("loading profile hang",), fixes=("office_outlook_helper", "guided_outlook_profile_repair")),
        _spec("email_search_broken", "Search", "Outlook search broken", aliases=("outlook can't search",), fixes=("office_outlook_helper", "guided_outlook_profile_repair")),
        _spec("email_shared_mailbox_missing", "Mailbox", "Shared mailbox missing", aliases=("shared mailbox gone",), fixes=("office_outlook_helper", "guided_outlook_profile_repair", "escalate_directory_services")),
        _spec("email_calendar_not_updating", "Calendar", "Calendar not updating", aliases=("calendar not syncing",), fixes=("office_outlook_helper", "guided_outlook_profile_repair")),
        _spec("email_attachments_fail_open", "Attachments", "Attachments fail to open", aliases=("can't open attachments",), fixes=("office_outlook_helper", "guided_default_app_reset")),
        _spec("email_mailbox_full_ost_large", "Mailbox Size", "Mailbox full / OST too large", aliases=("OST too large",), diagnostics=("storage_pressure",), fixes=("office_outlook_helper", "guided_outlook_profile_repair", "guided_disk_cleanup")),
        _spec("email_signature_missing_not_syncing", "Profile", "Signature missing / not syncing", aliases=("email signature gone",), fixes=("office_outlook_helper", "guided_outlook_profile_repair")),
    ),
    "collab": (
        _spec("collab_teams_wont_open", "Teams App", "Teams won't open", aliases=("teams not launching",), playbooks=("quick_teams_bundle",), fixes=("guided_teams_cache_reset", "service_health_snapshot")),
        _spec("collab_teams_signin_fails", "Teams App", "Teams sign-in fails", aliases=("can't sign into teams",), playbooks=("quick_teams_bundle",), fixes=("guided_teams_cache_reset", "guided_password_reset", "escalate_directory_services")),
        _spec("collab_teams_camera_not_working", "Meeting Devices", "Teams camera not working", aliases=("teams camera broken",), playbooks=("quick_teams_bundle",), fixes=("camera_privacy_check", "guided_teams_cache_reset")),
        _spec("collab_teams_mic_not_working", "Meeting Devices", "Teams mic not working", aliases=("teams microphone broken",), playbooks=("quick_teams_bundle",), fixes=("audio_mic_helper", "guided_teams_cache_reset")),
        _spec("collab_teams_notifications_missing", "Notifications", "Teams notifications missing", aliases=("teams notifications not showing",), fixes=("guided_teams_cache_reset",)),
        _spec("collab_teams_cache_corruption", "Cache / State", "Teams cache / state corruption", aliases=("teams cache broken",), fixes=("guided_teams_cache_reset",)),
        _spec("collab_meeting_audio_echo", "Meeting Audio", "Meeting audio echo / routing issues", aliases=("meeting echo",), playbooks=("quick_teams_bundle",), fixes=("audio_mic_helper", "guided_teams_cache_reset")),
        _spec("collab_cannot_share_screen", "Meeting Controls", "Cannot share screen", aliases=("screen share broken",), fixes=("guided_teams_cache_reset", "guided_security_review")),
        _spec("collab_video_call_lag", "Meeting Quality", "Video call lag / freezing", aliases=("video call freezing",), fixes=("audio_mic_helper", "escalate_network_team"), network=True),
        _spec("collab_presence_incorrect", "Presence", "Presence / status incorrect", aliases=("teams status wrong",), fixes=("guided_teams_cache_reset", "escalate_directory_services")),
    ),
    "sync": (
        _spec("sync_office_app_wont_open", "Office Launch", "Office app won't open", aliases=("word won't open", "excel won't open"), fixes=("office_outlook_helper", "guided_default_app_reset")),
        _spec("sync_office_activation_loop", "Activation", "Office activation loop", aliases=("office keeps asking to activate",), fixes=("office_outlook_helper", "guided_password_reset", "escalate_directory_services")),
        _spec("sync_office_app_crashes_on_launch", "Office Launch", "Office app crashes on launch", aliases=("word crashes on open",), fixes=("office_outlook_helper", "guided_default_app_reset")),
        _spec("sync_excel_file_opens_blank", "Office Content", "Excel file opens blank", aliases=("excel opens blank workbook",), fixes=("office_outlook_helper", "guided_default_app_reset")),
        _spec("sync_macro_workbook_blocked", "Office Security", "Macro workbook blocked", aliases=("macros blocked",), fixes=("guided_security_review",)),
        _spec("sync_onedrive_signin_failure", "OneDrive", "OneDrive sign-in failure", aliases=("can't sign into onedrive",), fixes=("onedrive_sync_helper", "guided_password_reset", "escalate_directory_services")),
        _spec("sync_onedrive_not_syncing", "OneDrive", "OneDrive not syncing", aliases=("onedrive sync stuck",), fixes=("onedrive_sync_helper", "guided_sync_resync")),
        _spec("sync_sharepoint_library_wont_sync", "SharePoint", "SharePoint library won't sync", aliases=("sharepoint sync failed",), fixes=("onedrive_sync_helper", "guided_sync_resync", "escalate_directory_services")),
        _spec("sync_files_unexpectedly_cloud_only", "Files On-Demand", "Files unexpectedly cloud-only", aliases=("files cloud only",), diagnostics=("storage_pressure",), fixes=("onedrive_sync_helper", "guided_disk_cleanup", "guided_sync_resync")),
        _spec("sync_coauthoring_conflicts", "Collaboration", "Co-authoring conflicts", aliases=("merge conflict in office",), fixes=("onedrive_sync_helper", "guided_sync_resync")),
    ),
    "storage": (
        _spec("storage_disk_almost_full", "Capacity", "Disk almost full", aliases=("disk full", "low disk space"), playbooks=("disk_cleanup_storage_recovery",), fixes=("storage_settings", "guided_disk_cleanup")),
        _spec("storage_cannot_access_folder", "Permissions", "Cannot access folder", aliases=("folder access denied",), fixes=("guided_permission_review",)),
        _spec("storage_path_too_long", "Paths", "Path too long", aliases=("filename too long",), fixes=("guided_permission_review",)),
        _spec("storage_cannot_delete_file", "Files", "Cannot delete file", aliases=("file won't delete",), fixes=("guided_permission_review", "guided_file_restore")),
        _spec("storage_recycle_bin_corrupted", "Files", "Recycle Bin corrupted", aliases=("recycle bin is corrupted",), fixes=("guided_permission_review",)),
        _spec("storage_zip_extract_failure", "Files", "ZIP extract / open failure", aliases=("zip won't open",), fixes=("guided_default_app_reset", "guided_permission_review")),
        _spec("storage_network_share_slow", "Shares", "Network share slow", aliases=("file share slow",), fixes=("guided_network_drive_reconnect", "escalate_network_team"), network=True),
        _spec("storage_offline_files_conflict", "Offline Files", "Offline files conflict", aliases=("offline files out of sync",), fixes=("guided_sync_resync", "guided_file_restore")),
        _spec("storage_lost_file_after_save", "Recovery", "Lost file after save", aliases=("saved file disappeared",), fixes=("guided_file_restore", "guided_backup_snapshot")),
        _spec("storage_permission_denied_saving_document", "Permissions", "Permission denied saving document", aliases=("can't save document",), fixes=("guided_permission_review", "guided_backup_snapshot")),
    ),
    "performance": (
        _spec("performance_pc_very_slow_overall", "General Performance", "PC very slow overall", aliases=("computer very slow",), playbooks=("quick_slow_pc_bundle",), fixes=("storage_settings", "guided_disk_cleanup")),
        _spec("performance_high_cpu_usage", "CPU", "High CPU usage", aliases=("cpu pegged",), playbooks=("quick_slow_pc_bundle",), fixes=("service_health_snapshot", "escalate_hardware_vendor")),
        _spec("performance_high_memory_usage", "Memory", "High memory usage", aliases=("ram maxed",), playbooks=("quick_slow_pc_bundle",), fixes=("service_health_snapshot", "guided_disk_cleanup")),
        _spec("performance_100_disk_usage", "Disk", "100% disk usage", aliases=("disk pegged",), playbooks=("quick_slow_pc_bundle",), fixes=("storage_settings", "guided_disk_cleanup")),
        _spec("performance_apps_launch_slowly", "Application Launch", "Apps launch slowly", aliases=("applications open slow",), fixes=("service_health_snapshot", "guided_disk_cleanup")),
        _spec("performance_long_boot_time", "Boot", "Long boot time", aliases=("boots slowly",), fixes=("guided_disk_cleanup", "escalate_hardware_vendor")),
        _spec("performance_long_login_time", "Logon", "Long login time", aliases=("slow sign in",), fixes=("guided_profile_shell_repair", "guided_disk_cleanup")),
        _spec("performance_random_freezes", "Stability", "Random freezes", aliases=("system hangs",), fixes=("service_health_snapshot", "escalate_hardware_vendor"), severity="high"),
        _spec("performance_mouse_keyboard_lag", "Input Lag", "Mouse / keyboard lag", aliases=("input lag",), fixes=("driver_device_inventory", "escalate_hardware_vendor")),
        _spec("performance_fans_loud_system_hot", "Thermal", "Fans loud / system hot", aliases=("laptop hot",), diagnostics=("performance_hotspots",), fixes=("escalate_hardware_vendor",), severity="high"),
    ),
    "apps": (
        _spec("apps_software_wont_install", "Install", "Software won't install", aliases=("installer failed",), diagnostics=("app_install_context",), fixes=("guided_default_app_reset", "service_health_snapshot")),
        _spec("apps_version_conflict_on_install", "Install", "Version conflict on install", aliases=("conflicting version",), diagnostics=("app_install_context",), fixes=("guided_default_app_reset",)),
        _spec("apps_app_wont_uninstall", "Uninstall", "App won't uninstall", aliases=("can't uninstall app",), diagnostics=("app_install_context",), fixes=("guided_default_app_reset",)),
        _spec("apps_opens_then_closes", "Launch", "App opens then closes", aliases=("app closes immediately",), diagnostics=("app_install_context", "system_integrity_state"), fixes=("service_health_snapshot", "guided_default_app_reset")),
        _spec("apps_missing_dll_runtime", "Runtime", "Missing DLL / runtime", aliases=("missing dll",), diagnostics=("app_install_context",), fixes=("guided_default_app_reset",)),
        _spec("apps_store_app_broken", "Microsoft Store", "Microsoft Store app broken", aliases=("store app broken",), diagnostics=("app_install_context",), fixes=("guided_default_app_reset",)),
        _spec("apps_office_repair_needed", "Office", "Office repair needed", aliases=("repair office",), playbooks=("quick_office_outlook_bundle",), fixes=("office_outlook_helper", "guided_outlook_profile_repair")),
        _spec("apps_app_update_failed", "Update", "App update failed", aliases=("application update failed",), diagnostics=("app_install_context", "windows_update_state"), fixes=("service_health_snapshot",)),
        _spec("apps_wrong_default_app_association", "Default Apps", "Wrong default app association", aliases=("file opens with wrong app",), fixes=("guided_default_app_reset",)),
        _spec("apps_legacy_app_compatibility_issue", "Compatibility", "Legacy app compatibility issue", aliases=("old app won't run",), diagnostics=("app_install_context",), fixes=("guided_default_app_reset", "escalate_hardware_vendor")),
    ),
    "security": (
        _spec("security_defender_disabled", "Defender", "Defender disabled", aliases=("windows defender off",), diagnostics=("security_posture",), fixes=("guided_security_review", "escalate_security_team"), severity="high"),
        _spec("security_definitions_out_of_date", "Defender", "Definitions out of date", aliases=("defender not updated",), diagnostics=("security_posture",), fixes=("guided_security_review", "windows_update_reset")),
        _spec("security_threat_quarantine_event", "Threats", "Threat / quarantine event", aliases=("virus quarantined",), diagnostics=("security_posture",), fixes=("guided_security_review", "escalate_security_team"), severity="high"),
        _spec("security_smartscreen_blocking_trusted_app", "SmartScreen", "SmartScreen blocking trusted app", aliases=("trusted app blocked",), diagnostics=("security_posture",), fixes=("guided_security_review", "escalate_security_team")),
        _spec("security_firewall_blocking_app", "Firewall", "Firewall blocking app", aliases=("app blocked by firewall",), diagnostics=("security_posture",), fixes=("firewall_profile_summary", "guided_security_review")),
        _spec("security_stale_credential_manager_entries", "Credentials", "Stale Credential Manager entries", aliases=("old credentials saved",), diagnostics=("credential_store_state",), fixes=("guided_password_reset",)),
        _spec("security_authenticator_reset_new_phone", "MFA", "Authenticator reset / new phone", aliases=("new phone mfa",), diagnostics=("identity_context",), fixes=("escalate_directory_services",)),
        _spec("security_suspicious_extension_adware", "Browser Security", "Suspicious extension / adware symptoms", aliases=("browser adware",), diagnostics=("security_posture", "browser_state"), fixes=("guided_browser_profile_cleanup", "guided_security_review"), severity="high"),
        _spec("security_bitlocker_recovery_prompt", "BitLocker", "BitLocker recovery prompt", aliases=("bitlocker asking for key",), diagnostics=("enterprise_posture",), fixes=("escalate_security_team",), severity="high"),
        _spec("security_certificate_trust_issue", "Certificates", "Certificate trust issue", aliases=("cert trust broken",), diagnostics=("certificate_trust_state",), fixes=("guided_security_review", "escalate_security_team"), severity="high"),
    ),
    "media": (
        _spec("media_no_sound", "Audio", "No sound", aliases=("no audio",), fixes=("audio_mic_helper",)),
        _spec("media_mic_not_detected", "Microphone", "Mic not detected", aliases=("microphone missing",), fixes=("audio_mic_helper",)),
        _spec("media_webcam_not_detected", "Camera", "Webcam not detected", aliases=("camera missing",), fixes=("camera_privacy_check", "driver_device_inventory")),
        _spec("media_audio_routing_changes_when_docking", "Audio Routing", "Audio routing changes when docking", aliases=("dock changes audio output",), fixes=("audio_mic_helper", "driver_device_inventory")),
        _spec("media_bluetooth_headset_connected_no_audio", "Bluetooth Audio", "Bluetooth headset connected but no audio", aliases=("bluetooth audio missing",), fixes=("guided_bluetooth_repair", "audio_mic_helper")),
        _spec("media_one_app_has_no_sound", "Application Audio", "One app has no sound", aliases=("single app no sound",), fixes=("audio_mic_helper", "guided_default_app_reset")),
        _spec("media_distorted_crackling_audio", "Audio Quality", "Distorted / crackling audio", aliases=("audio crackling",), fixes=("audio_mic_helper", "escalate_hardware_vendor")),
        _spec("media_camera_black_frozen", "Camera", "Camera image black / frozen", aliases=("camera frozen",), fixes=("camera_privacy_check", "driver_device_inventory")),
        _spec("media_playback_recording_issue", "Media", "Media playback / recording issue", aliases=("video won't play",), fixes=("audio_mic_helper", "guided_default_app_reset")),
        _spec("media_media_keys_not_working", "Input / Media", "Media keys not working", aliases=("volume keys not working",), fixes=("driver_device_inventory", "guided_keyboard_layout_reset")),
    ),
    "display": (
        _spec("display_external_monitor_not_detected", "Monitor", "External monitor not detected", aliases=("second monitor missing",), fixes=("driver_device_inventory", "escalate_hardware_vendor")),
        _spec("display_wrong_monitor_order_layout", "Monitor Layout", "Wrong monitor order / layout", aliases=("monitors arranged wrong",), fixes=("guided_default_app_reset",)),
        _spec("display_wrong_blurry_resolution", "Resolution", "Wrong / blurry resolution", aliases=("blurry monitor",), fixes=("driver_device_inventory", "escalate_hardware_vendor")),
        _spec("display_docking_station_not_working", "Dock", "Docking station not working", aliases=("dock failed",), playbooks=("display_dock_triage",), fixes=("usb_bt_disconnect_helper", "escalate_hardware_vendor")),
        _spec("display_laptop_closed_externals_fail", "Dock / Power", "Laptop closed but externals fail", aliases=("clamshell mode broken",), fixes=("guided_power_sleep_reset", "escalate_hardware_vendor")),
        _spec("display_screen_flickering", "Monitor", "Screen flickering", aliases=("display flicker",), fixes=("driver_device_inventory", "escalate_hardware_vendor"), severity="high"),
        _spec("display_usb_device_not_recognized", "USB", "USB device not recognized", aliases=("usb not recognized",), fixes=("usb_bt_disconnect_helper", "driver_device_inventory")),
        _spec("display_bluetooth_mouse_keyboard_unstable", "Bluetooth", "Bluetooth mouse / keyboard unstable", aliases=("bluetooth keyboard disconnects",), fixes=("guided_bluetooth_repair", "usb_bt_disconnect_helper")),
        _spec("display_keyboard_typing_wrong_characters", "Input", "Keyboard typing wrong characters", aliases=("wrong keyboard layout",), fixes=("guided_keyboard_layout_reset",)),
        _spec("display_touchpad_not_working", "Input", "Touchpad not working", aliases=("trackpad not working",), fixes=("driver_device_inventory", "escalate_hardware_vendor")),
    ),
    "power": (
        _spec("power_laptop_not_charging", "Charging", "Laptop not charging", aliases=("plugged in not charging",), fixes=("guided_power_sleep_reset", "escalate_hardware_vendor"), severity="high"),
        _spec("power_battery_drains_fast", "Battery", "Battery drains too fast", aliases=("battery dies quickly",), fixes=("guided_power_sleep_reset", "escalate_hardware_vendor")),
        _spec("power_wont_wake_from_sleep", "Sleep / Wake", "Won't wake from sleep", aliases=("sleep resume failed",), fixes=("guided_power_sleep_reset", "escalate_hardware_vendor"), severity="high"),
        _spec("power_wont_sleep", "Sleep / Wake", "Won't sleep", aliases=("sleep not working",), fixes=("guided_power_sleep_reset",)),
        _spec("power_random_shutdowns", "Stability", "Random shutdowns", aliases=("shuts down randomly",), fixes=("escalate_hardware_vendor",), severity="high"),
        _spec("power_power_button_behavior_wrong", "Power Button", "Power button behavior wrong", aliases=("power button wrong action",), fixes=("guided_power_sleep_reset",)),
        _spec("power_battery_percentage_inaccurate", "Battery", "Battery percentage inaccurate", aliases=("battery percent wrong",), fixes=("guided_power_sleep_reset", "escalate_hardware_vendor")),
        _spec("power_keyboard_backlight_not_working", "Keyboard", "Keyboard backlight not working", aliases=("keyboard light off",), fixes=("driver_device_inventory", "escalate_hardware_vendor")),
        _spec("power_touchscreen_not_working", "Input", "Touchscreen not working", aliases=("touch screen broken",), fixes=("driver_device_inventory", "escalate_hardware_vendor")),
        _spec("power_date_time_keeps_resetting", "Clock", "Date / time keeps resetting", aliases=("clock wrong after reboot",), fixes=("guided_time_sync", "escalate_hardware_vendor")),
    ),
    "windows_update": (
        _spec("windows_update_stuck", "Windows Update", "Windows Update stuck", aliases=("updates stuck",), playbooks=("windows_update_repair",), fixes=("windows_update_reset",), reboot=True, severity="high"),
        _spec("windows_update_error_loop", "Windows Update", "Windows Update error loop", aliases=("updates keep failing",), playbooks=("windows_update_repair",), fixes=("windows_update_reset",), reboot=True, severity="high"),
        _spec("windows_feature_update_failing", "Windows Update", "Feature update failing", aliases=("major update failed",), playbooks=("windows_update_repair",), fixes=("windows_update_reset", "sfc_dism_integrity"), reboot=True, severity="high"),
        _spec("windows_pending_reboot_wont_clear", "Windows Update", "Pending reboot won't clear", aliases=("stuck pending reboot",), playbooks=("windows_update_repair",), fixes=("windows_update_reset",), reboot=True),
        _spec("windows_sfc_finds_errors", "Integrity", "SFC finds errors", aliases=("sfc errors found",), playbooks=("os_health_integrity_repair",), fixes=("sfc_dism_integrity",), reboot=True),
        _spec("windows_dism_fails", "Integrity", "DISM fails", aliases=("dism restorehealth failed",), playbooks=("os_health_integrity_repair",), fixes=("sfc_dism_integrity",), reboot=True, severity="high"),
        _spec("windows_system_corruption_symptoms", "Integrity", "System corruption symptoms", aliases=("os corruption",), playbooks=("os_health_integrity_repair",), fixes=("sfc_dism_integrity",), reboot=True, severity="high"),
        _spec("windows_blue_screen_bsod", "Recovery", "Blue screen / BSOD", aliases=("bsod",), playbooks=("os_health_integrity_repair",), fixes=("escalate_hardware_vendor",), severity="critical"),
        _spec("windows_winre_recovery_needed", "Recovery", "WinRE / recovery needed", aliases=("windows recovery needed",), playbooks=("backup_pre_fix_snapshot_workflow",), fixes=("guided_backup_snapshot", "escalate_hardware_vendor"), severity="critical"),
        _spec("windows_boot_loop_after_update_change", "Recovery", "Boot loop after update / change", aliases=("boot loop",), playbooks=("os_health_integrity_repair", "backup_pre_fix_snapshot_workflow"), fixes=("guided_backup_snapshot", "escalate_hardware_vendor"), severity="critical"),
    ),
    "enterprise": (
        _spec("enterprise_domain_join_trust_issue", "Domain Trust", "Domain join / trust issue", aliases=("trust relationship failed",), fixes=("escalate_directory_services",), severity="high"),
        _spec("enterprise_intune_not_checking_in", "Intune / MDM", "Intune / MDM not checking in", aliases=("intune sync failed",), fixes=("escalate_directory_services", "support_bundle_export"), network=True),
        _spec("enterprise_compliance_policy_failing", "Compliance", "Compliance policy failing", aliases=("device noncompliant",), fixes=("escalate_security_team", "support_bundle_export"), severity="high"),
        _spec("enterprise_gpo_not_applying", "GPO", "GPO not applying", aliases=("policy not applying",), fixes=("escalate_directory_services", "support_bundle_export")),
        _spec("enterprise_printer_drive_mapping_policy_issue", "GPO Mappings", "Printer / drive mapping policy issue", aliases=("mapped drive policy broken",), fixes=("guided_network_drive_reconnect", "escalate_directory_services")),
        _spec("enterprise_certificate_autoenrollment_failure", "Certificates", "Certificate auto-enrollment failure", aliases=("cert auto enroll failed",), diagnostics=("certificate_trust_state",), fixes=("escalate_security_team",), severity="high"),
        _spec("enterprise_missing_root_intermediate_certificate", "Certificates", "Missing root / intermediate certificate", aliases=("missing root cert",), diagnostics=("certificate_trust_state",), fixes=("escalate_security_team",), severity="high"),
        _spec("enterprise_local_admin_laps_workflow_issue", "LAPS / Admin", "Local admin / LAPS workflow issue", aliases=("laps issue",), fixes=("escalate_directory_services",), severity="high"),
        _spec("enterprise_rename_computer_name_issue", "Device Naming", "Rename / computer-name issue", aliases=("computer rename failed",), fixes=("escalate_directory_services",)),
        _spec("enterprise_bitlocker_policy_mismatch", "BitLocker Policy", "BitLocker policy mismatch", aliases=("bitlocker policy issue",), diagnostics=("enterprise_posture",), fixes=("escalate_security_team",), severity="high"),
    ),
    "recovery": (
        _spec("recovery_need_backup_before_troubleshooting", "Backup Readiness", "Need backup before troubleshooting", aliases=("backup first",), playbooks=("backup_pre_fix_snapshot_workflow",), fixes=("guided_backup_snapshot",)),
        _spec("recovery_known_folder_move_confusion", "Profile / Sync", "Known Folder Move confusion / issues", aliases=("KFM issue",), fixes=("guided_sync_resync", "guided_backup_snapshot")),
        _spec("recovery_file_version_recovery_needed", "Version Recovery", "File version recovery needed", aliases=("need previous version",), fixes=("guided_file_restore",)),
        _spec("recovery_accidentally_deleted_file", "File Recovery", "Accidentally deleted file", aliases=("deleted file recovery",), fixes=("guided_file_restore",)),
        _spec("recovery_corrupted_document_recovery", "File Recovery", "Corrupted document recovery", aliases=("document corrupted",), fixes=("guided_file_restore",)),
        _spec("recovery_browser_bookmarks_missing", "Browser Recovery", "Browser bookmarks missing", aliases=("favorites missing",), fixes=("guided_file_restore", "guided_browser_profile_cleanup")),
        _spec("recovery_pst_ost_inventory_issues", "Mail Data", "PST / OST inventory and issues", aliases=("pst problem",), fixes=("office_outlook_helper", "guided_outlook_profile_repair")),
        _spec("recovery_user_data_migration_new_pc", "Migration", "User data migration to new PC", aliases=("move data to new pc",), fixes=("guided_data_migration",)),
        _spec("recovery_restore_after_reimage", "Restore", "Restore after reimage", aliases=("post reimage restore",), fixes=("guided_data_migration", "guided_file_restore")),
        _spec("recovery_prefix_snapshot_support_pack", "Backup Readiness", "Pre-fix snapshot / support pack needed", aliases=("need support pack before fix",), playbooks=("backup_pre_fix_snapshot_workflow", "support_bundle_export_playbook"), fixes=("guided_backup_snapshot", "support_bundle_export")),
    ),
    "bundles": (
        _spec("bundles_pc_health_baseline_check", "Quick Bundles", "PC health baseline check", aliases=("health baseline",), playbooks=("meta_unknown_issue",), fixes=("service_health_snapshot", "support_bundle_export")),
        _spec("bundles_quick_network_repair_bundle", "Quick Bundles", "Quick network repair bundle", aliases=("quick network fix",), playbooks=("network_baseline_repair", "dns_ip_adapter_repair"), fixes=("flush_dns", "network_stack_repair")),
        _spec("bundles_quick_office_outlook_repair_bundle", "Quick Bundles", "Quick Office / Outlook repair bundle", aliases=("quick outlook fix",), playbooks=("quick_office_outlook_bundle",), fixes=("office_outlook_helper", "guided_outlook_profile_repair")),
        _spec("bundles_quick_browser_repair_bundle", "Quick Bundles", "Quick browser repair bundle", aliases=("quick browser fix",), playbooks=("quick_browser_bundle",), fixes=("browser_rescue", "guided_browser_profile_cleanup")),
        _spec("bundles_quick_print_repair_bundle", "Quick Bundles", "Quick print repair bundle", aliases=("quick printer fix",), playbooks=("quick_print_bundle",), fixes=("restart_spooler", "printer_full_reset")),
        _spec("bundles_quick_teams_meeting_repair_bundle", "Quick Bundles", "Quick Teams / meeting repair bundle", aliases=("quick teams fix",), playbooks=("quick_teams_bundle",), fixes=("audio_mic_helper", "guided_teams_cache_reset")),
        _spec("bundles_quick_slow_pc_cleanup_bundle", "Quick Bundles", "Quick slow-PC cleanup bundle", aliases=("slow pc cleanup bundle",), playbooks=("quick_slow_pc_bundle",), fixes=("storage_settings", "guided_disk_cleanup")),
        _spec("bundles_quick_profile_shell_repair_bundle", "Quick Bundles", "Quick profile / shell repair bundle", aliases=("quick profile repair",), playbooks=("quick_profile_shell_bundle",), fixes=("guided_profile_shell_repair", "sfc_dism_integrity")),
        _spec("bundles_quick_support_export_bundle", "Quick Bundles", "Quick support export bundle", aliases=("quick support pack",), playbooks=("quick_support_export_bundle",), fixes=("eventlog_export_pack", "support_bundle_export")),
        _spec("bundles_new_pc_post_reimage_validation_bundle", "Quick Bundles", "New-PC / post-reimage validation bundle", aliases=("new pc bundle",), playbooks=("new_pc_post_reimage_validation",), fixes=("guided_data_migration", "support_bundle_export")),
    ),
}


_FAMILY_LOOKUP = {family.id: family for family in FAMILIES}
_FAMILY_DEFAULTS: dict[str, dict[str, Any]] = {
    "identity": {"diagnostics": ("identity_context", "account_lock_status", "credential_store_state"), "fixes": ("guided_password_reset",), "playbooks": ("identity_credential_repair",), "evidence": ("system_snapshot", "support_bundle"), "permissions": "standard user", "impact": "access_blocked"},
    "shell": {"diagnostics": ("profile_health", "shell_process_health"), "fixes": ("guided_profile_shell_repair",), "playbooks": ("profile_shell_repair",), "evidence": ("system_snapshot", "eventlogs_bundle", "support_bundle"), "permissions": "standard user", "impact": "workflow_blocked"},
    "network": {"diagnostics": ("network_baseline", "ip_configuration", "dns_resolution"), "fixes": ("flush_dns",), "playbooks": ("network_baseline_repair",), "evidence": ("network_bundle", "support_bundle"), "permissions": "standard user", "impact": "connectivity_blocked"},
    "remote": {"diagnostics": ("wifi_visibility", "vpn_posture", "remote_access_stack"), "fixes": ("wifi_report_fix",), "playbooks": ("vpn_remote_access_repair",), "evidence": ("network_bundle", "support_bundle"), "permissions": "standard user", "impact": "remote_work_blocked"},
    "print": {"diagnostics": ("printer_queue_status", "printer_driver_health"), "fixes": ("restart_spooler",), "playbooks": ("printer_spooler_repair",), "evidence": ("printer_bundle", "support_bundle"), "permissions": "standard user", "impact": "document_blocked"},
    "browser": {"diagnostics": ("browser_state", "browser_auth_state"), "fixes": ("browser_rescue",), "playbooks": ("browser_repair",), "evidence": ("network_bundle", "support_bundle"), "permissions": "standard user", "impact": "web_workflow_blocked"},
    "email": {"diagnostics": ("outlook_profile_state", "mailbox_health"), "fixes": ("office_outlook_helper",), "playbooks": ("outlook_mailbox_repair",), "evidence": ("office_bundle", "support_bundle"), "permissions": "standard user", "impact": "communication_blocked"},
    "collab": {"diagnostics": ("teams_cache_state", "teams_device_state"), "fixes": ("audio_mic_helper",), "playbooks": ("teams_meeting_repair",), "evidence": ("device_bundle", "support_bundle"), "permissions": "standard user", "impact": "meeting_blocked"},
    "sync": {"diagnostics": ("sync_client_health", "office_activation_state"), "fixes": ("onedrive_sync_helper",), "playbooks": ("onedrive_sharepoint_sync_repair",), "evidence": ("office_bundle", "support_bundle"), "permissions": "standard user", "impact": "file_workflow_blocked"},
    "storage": {"diagnostics": ("storage_pressure", "file_permission_context"), "fixes": ("storage_settings",), "playbooks": ("disk_cleanup_storage_recovery",), "evidence": ("backup_snapshot", "support_bundle"), "permissions": "standard user", "impact": "data_access_blocked"},
    "performance": {"diagnostics": ("performance_hotspots", "startup_pressure"), "fixes": ("storage_settings",), "playbooks": ("slow_pc_triage",), "evidence": ("system_snapshot", "support_bundle"), "permissions": "standard user", "impact": "productivity_degraded"},
    "apps": {"diagnostics": ("app_install_context", "system_integrity_state"), "fixes": ("service_health_snapshot",), "playbooks": ("quick_office_outlook_bundle",), "evidence": ("system_snapshot", "support_bundle"), "permissions": "standard user", "impact": "application_blocked"},
    "security": {"diagnostics": ("security_posture", "certificate_trust_state"), "fixes": ("firewall_profile_summary",), "playbooks": ("enterprise_posture_check",), "evidence": ("support_bundle",), "permissions": "standard user", "impact": "risk_or_access_blocked"},
    "media": {"diagnostics": ("media_device_inventory",), "fixes": ("audio_mic_helper", "camera_privacy_check"), "playbooks": ("audio_video_device_triage",), "evidence": ("device_bundle", "support_bundle"), "permissions": "standard user", "impact": "communication_blocked"},
    "display": {"diagnostics": ("display_dock_inventory",), "fixes": ("driver_device_inventory", "usb_bt_disconnect_helper"), "playbooks": ("display_dock_triage",), "evidence": ("device_bundle", "support_bundle"), "permissions": "standard user", "impact": "workspace_blocked"},
    "power": {"diagnostics": ("power_battery_state",), "fixes": ("guided_power_sleep_reset",), "playbooks": ("audio_video_device_triage",), "evidence": ("device_bundle", "support_bundle"), "permissions": "standard user", "impact": "device_unstable"},
    "windows_update": {"diagnostics": ("windows_update_state", "system_integrity_state"), "fixes": ("windows_update_reset",), "playbooks": ("windows_update_repair",), "evidence": ("update_bundle", "support_bundle"), "permissions": "local admin", "impact": "servicing_blocked"},
    "enterprise": {"diagnostics": ("enterprise_posture", "certificate_trust_state"), "fixes": ("service_health_snapshot",), "playbooks": ("enterprise_posture_check",), "evidence": ("system_snapshot", "support_bundle"), "permissions": "standard user", "impact": "managed_workflow_blocked"},
    "recovery": {"diagnostics": ("backup_readiness", "support_bundle_readiness"), "fixes": ("guided_backup_snapshot",), "playbooks": ("backup_pre_fix_snapshot_workflow",), "evidence": ("backup_snapshot", "support_bundle"), "permissions": "standard user", "impact": "data_risk"},
    "bundles": {"diagnostics": ("support_bundle_readiness",), "fixes": ("support_bundle_export",), "playbooks": ("support_bundle_export_playbook",), "evidence": ("support_bundle",), "permissions": "standard user", "impact": "support_workflow"},
}


def _build_issue(family_id: str, spec: dict[str, Any]) -> IssueClass:
    family = _FAMILY_LOOKUP[family_id]
    defaults = _FAMILY_DEFAULTS[family_id]
    title = str(spec["title"])
    aliases = tuple(spec.get("aliases", ()))
    symptom_labels = _uniq([title, *aliases])
    return IssueClass(
        id=str(spec["id"]),
        family_id=family_id,
        family_label=family.title,
        subfamily=str(spec.get("subfamily") or family.default_subfamily),
        title=title,
        description=f"{title} is covered by shared FixFox playbooks, diagnostics, fixes, and escalation paths.",
        severity=str(spec.get("severity", "medium")),
        impact=str(spec.get("impact") or defaults["impact"]),
        aliases=_uniq([title.lower(), *aliases]),
        symptom_labels=symptom_labels,
        diagnosis_tags=_uniq([family_id]),
        fix_tags=_uniq([family_id]),
        workflow=str(spec.get("workflow", "guided")),
        permissions=str(spec.get("permissions") or defaults["permissions"]),
        reboot_required=bool(spec.get("reboot", False)),
        network_required=bool(spec.get("network", False)),
        rollback_notes="Capture evidence before risky actions and follow the mapped rollback guidance.",
        playbook_ids=_uniq([*defaults["playbooks"], *tuple(spec.get("playbooks", ()))]),
        diagnostic_ids=_uniq([*defaults["diagnostics"], *tuple(spec.get("diagnostics", ()))]),
        fix_ids=_uniq([*defaults["fixes"], *tuple(spec.get("fixes", ()))]),
        evidence_plan_ids=_uniq([*defaults["evidence"], *tuple(spec.get("evidence", ()))]),
        validation=("Re-run the mapped diagnostic and confirm the original symptom is gone.",),
        escalation=_uniq([*tuple(spec.get("escalation", ())), "Escalate if the symptom remains after the mapped workflow."]),
    )


ISSUES: tuple[IssueClass, ...] = tuple(
    _build_issue(family_id, spec)
    for family_id, specs in _ISSUE_SPECS.items()
    for spec in specs
)


def list_families() -> tuple[IssueFamily, ...]:
    return FAMILIES


def list_evidence_plans() -> tuple[EvidencePlan, ...]:
    return EVIDENCE_PLANS


def list_diagnostics() -> tuple[DiagnosticCheck, ...]:
    return DIAGNOSTICS


def list_support_fixes() -> tuple[SupportFix, ...]:
    return SUPPORT_FIXES


def list_support_playbooks() -> tuple[SupportPlaybook, ...]:
    return PLAYBOOKS


def list_issues() -> tuple[IssueClass, ...]:
    return ISSUES


@lru_cache(maxsize=1)
def family_map() -> dict[str, IssueFamily]:
    return {family.id: family for family in FAMILIES}


@lru_cache(maxsize=1)
def evidence_plan_map() -> dict[str, EvidencePlan]:
    return {plan.id: plan for plan in EVIDENCE_PLANS}


@lru_cache(maxsize=1)
def diagnostic_map() -> dict[str, DiagnosticCheck]:
    return {row.id: row for row in DIAGNOSTICS}


@lru_cache(maxsize=1)
def support_fix_map() -> dict[str, SupportFix]:
    return {row.id: row for row in SUPPORT_FIXES}


@lru_cache(maxsize=1)
def support_playbook_map() -> dict[str, SupportPlaybook]:
    return {row.id: row for row in PLAYBOOKS}


@lru_cache(maxsize=1)
def issue_map() -> dict[str, IssueClass]:
    return {row.id: row for row in ISSUES}


def issues_for_family(family_id: str) -> tuple[IssueClass, ...]:
    target = str(family_id or "").strip().lower()
    return tuple(row for row in ISSUES if row.family_id == target)


def diagnostics_for_issue(issue_id: str) -> tuple[DiagnosticCheck, ...]:
    issue = issue_map().get(str(issue_id or "").strip())
    mapping = diagnostic_map()
    return tuple(mapping[row] for row in issue.diagnostic_ids if issue is not None and row in mapping)


def fixes_for_issue(issue_id: str) -> tuple[SupportFix, ...]:
    issue = issue_map().get(str(issue_id or "").strip())
    mapping = support_fix_map()
    return tuple(mapping[row] for row in issue.fix_ids if issue is not None and row in mapping)


def playbooks_for_issue(issue_id: str) -> tuple[SupportPlaybook, ...]:
    issue = issue_map().get(str(issue_id or "").strip())
    mapping = support_playbook_map()
    return tuple(mapping[row] for row in issue.playbook_ids if issue is not None and row in mapping)


def evidence_for_issue(issue_id: str) -> tuple[EvidencePlan, ...]:
    issue = issue_map().get(str(issue_id or "").strip())
    mapping = evidence_plan_map()
    return tuple(mapping[row] for row in issue.evidence_plan_ids if issue is not None and row in mapping)


def issue_search_blob(issue: IssueClass) -> str:
    return " ".join(
        [
            issue.family_label,
            issue.subfamily,
            issue.title,
            issue.description,
            *issue.aliases,
            *issue.symptom_labels,
            *issue.diagnosis_tags,
            *issue.fix_tags,
        ]
    ).lower()


def query_issues(query: str, *, family_id: str = "", limit: int = 40) -> tuple[IssueClass, ...]:
    q = str(query or "").strip().lower()
    family_filter = str(family_id or "").strip().lower()
    rows: list[tuple[int, IssueClass]] = []
    for issue in ISSUES:
        if family_filter and issue.family_id != family_filter:
            continue
        if not q:
            rows.append((0, issue))
            continue
        blob = issue_search_blob(issue)
        if q not in blob:
            continue
        score = blob.count(q)
        if issue.title.lower().startswith(q):
            score += 20
        if any(alias.lower().startswith(q) for alias in issue.aliases):
            score += 12
        rows.append((score, issue))
    rows.sort(key=lambda item: (item[0], item[1].family_id, item[1].title.lower()), reverse=True)
    return tuple(issue for _score, issue in rows[: max(1, int(limit))])


def family_issue_counts() -> dict[str, int]:
    counts = {family.id: 0 for family in FAMILIES}
    for issue in ISSUES:
        counts[issue.family_id] = counts.get(issue.family_id, 0) + 1
    return counts


def playbook_issue_counts() -> dict[str, int]:
    counts = {playbook.id: 0 for playbook in PLAYBOOKS}
    for issue in ISSUES:
        for playbook_id in issue.playbook_ids:
            counts[playbook_id] = counts.get(playbook_id, 0) + 1
    return counts


def catalog_stats() -> SupportCatalogStats:
    guided_fix_count = len([row for row in SUPPORT_FIXES if row.automation in {"guided", "escalation"}])
    escalation_only_count = len([row for row in SUPPORT_FIXES if row.automation == "escalation"])
    return SupportCatalogStats(len(ISSUES), len(FAMILIES), len(PLAYBOOKS), len(DIAGNOSTICS), len(SUPPORT_FIXES), guided_fix_count, escalation_only_count)


def search_alias_examples() -> dict[str, tuple[str, ...]]:
    return {
        "internet not working": ("network_no_internet_access", "bundles_quick_network_repair_bundle"),
        "wifi": ("remote_wifi_wont_connect", "remote_wifi_unstable"),
        "vpn": ("remote_vpn_wont_connect", "remote_vpn_disconnects_repeatedly"),
        "outlook password": ("email_outlook_password_prompts", "identity_password_changed_apps_fail"),
        "teams camera": ("collab_teams_camera_not_working", "media_webcam_not_detected"),
        "printer offline": ("print_printer_offline",),
        "slow pc": ("performance_pc_very_slow_overall", "bundles_quick_slow_pc_cleanup_bundle"),
        "full disk": ("storage_disk_almost_full",),
        "bitlocker": ("security_bitlocker_recovery_prompt", "enterprise_bitlocker_policy_mismatch"),
        "windows update": ("windows_update_stuck", "windows_update_error_loop"),
    }


def validate_support_catalog() -> list[str]:
    errors: list[str] = []
    if len(ISSUES) != 200:
        errors.append(f"expected 200 issues, found {len(ISSUES)}")
    playbooks = support_playbook_map()
    diagnostics = diagnostic_map()
    fixes = support_fix_map()
    evidence = evidence_plan_map()
    for issue in ISSUES:
        if issue.family_id not in family_map():
            errors.append(f"{issue.id}: unknown family")
        if not issue.playbook_ids:
            errors.append(f"{issue.id}: missing playbooks")
        if not issue.diagnostic_ids:
            errors.append(f"{issue.id}: missing diagnostics")
        if not issue.fix_ids:
            errors.append(f"{issue.id}: missing fixes")
        if not issue.evidence_plan_ids:
            errors.append(f"{issue.id}: missing evidence")
        for item in issue.playbook_ids:
            if item not in playbooks:
                errors.append(f"{issue.id}: bad playbook {item}")
        for item in issue.diagnostic_ids:
            if item not in diagnostics:
                errors.append(f"{issue.id}: bad diagnostic {item}")
        for item in issue.fix_ids:
            if item not in fixes:
                errors.append(f"{issue.id}: bad fix {item}")
        for item in issue.evidence_plan_ids:
            if item not in evidence:
                errors.append(f"{issue.id}: bad evidence {item}")
    return errors


def export_catalog_summary() -> dict[str, Any]:
    stats = catalog_stats()
    family_counts = family_issue_counts()
    playbook_counts = playbook_issue_counts()
    return {
        "stats": {
            "issue_count": stats.issue_count,
            "family_count": stats.family_count,
            "playbook_count": stats.playbook_count,
            "diagnostic_count": stats.diagnostic_count,
            "fix_count": stats.fix_count,
            "guided_fix_count": stats.guided_fix_count,
            "escalation_only_count": stats.escalation_only_count,
        },
        "families": [
            {"id": family.id, "code": family.code, "title": family.title, "issue_count": family_counts.get(family.id, 0)}
            for family in FAMILIES
        ],
        "playbooks": [
            {"id": playbook.id, "title": playbook.title, "issue_count": playbook_counts.get(playbook.id, 0), "action": playbook.primary_action_ref}
            for playbook in PLAYBOOKS
        ],
        "search_alias_examples": {query: list(issue_ids) for query, issue_ids in search_alias_examples().items()},
        "validation_errors": validate_support_catalog(),
    }
