from __future__ import annotations

from dataclasses import asdict, dataclass
from datetime import datetime
from pathlib import Path
from typing import Any, Callable

from .errors import classify_exit, ensure_next_steps
from .masking import MaskingOptions
from .paths import ensure_dirs
from .script_tasks import run_script_task, script_task_map
from .support_catalog import support_playbook_map


@dataclass(frozen=True)
class PlaybookTaskBinding:
    id: str
    title: str
    task_id: str
    phase: str
    purpose: str
    next_step_hint: str


@dataclass(frozen=True)
class GuidedPlaybookStep:
    id: str
    title: str
    details: str
    validate: str
    escalate_when: str


@dataclass(frozen=True)
class DeepSupportPlaybook:
    playbook_id: str
    family: str
    audience: str
    aliases: tuple[str, ...]
    diagnostics: tuple[PlaybookTaskBinding, ...]
    remediations: tuple[PlaybookTaskBinding, ...]
    validations: tuple[PlaybookTaskBinding, ...]
    guided_steps: tuple[GuidedPlaybookStep, ...]
    evidence_plan_ids: tuple[str, ...]
    escalation_rules: tuple[str, ...]
    success_criteria: tuple[str, ...]
    support_bundle_integrated: bool = True


@dataclass(frozen=True)
class DeepSupportPlaybookStats:
    playbook_count: int
    diagnostic_script_count: int
    remediation_script_count: int
    guided_flow_count: int
    shared_primitive_count: int
    support_bundle_integrated_count: int


def _binding(task_id: str, phase: str, purpose: str, next_step_hint: str) -> PlaybookTaskBinding:
    task = script_task_map()[task_id]
    return PlaybookTaskBinding(
        id=f"{phase}:{task_id}",
        title=task.title,
        task_id=task_id,
        phase=phase,
        purpose=purpose,
        next_step_hint=next_step_hint,
    )


def _guided(step_id: str, title: str, details: str, validate: str, escalate_when: str) -> GuidedPlaybookStep:
    return GuidedPlaybookStep(step_id, title, details, validate, escalate_when)


DEEP_SUPPORT_PLAYBOOKS: tuple[DeepSupportPlaybook, ...] = (
    DeepSupportPlaybook(
        playbook_id="identity_credential_repair",
        family="identity",
        audience="service desk",
        aliases=("stale credentials", "password changed but apps still fail", "windows password rejected"),
        diagnostics=(
            _binding("task_identity_signin_helper", "diagnostic", "Collect device registration, time sync, cached credential, and Windows Hello state.", "Review stale credentials and device join posture."),
            _binding("task_office_outlook_helper", "diagnostic", "Collect Office/Outlook auth posture for app-side credential drift.", "Confirm whether prompts are Windows-wide or app-specific."),
        ),
        remediations=(),
        validations=(
            _binding("task_identity_signin_helper", "validation", "Re-check credential, time, and device registration posture after guided cleanup.", "Retest Windows sign-in and one affected app."),
        ),
        guided_steps=(
            _guided("identity_clear_stale_targets", "Remove only stale app credentials", "Use Credential Manager or cmdkey to remove entries for the affected app/service, not every stored credential.", "Retest one affected app and confirm the prompt uses the expected account.", "Multiple apps still loop or the device registration state is unhealthy."),
            _guided("identity_reset_app_session", "Reset app sign-in state", "Sign out of the affected Office/browser/Teams app, close it fully, then sign in again with the expected identity.", "The prompt appears once and the app opens without looping.", "The tenant, MFA, or smart-card flow still fails after a clean sign-in."),
            _guided("identity_pin_reset", "Reset PIN / Windows Hello only if needed", "If PIN or biometric failures are isolated to Windows Hello, follow the Hello reset path after documenting the current account state.", "PIN creation succeeds and Windows sign-in works again.", "Windows Hello provisioning fails or the account cannot enroll."),
        ),
        evidence_plan_ids=("system_snapshot", "office_bundle", "support_bundle"),
        escalation_rules=("Escalate to directory/identity support if dsreg/device auth is unhealthy, MFA never prompts, or smart-card/cert action is required.",),
        success_criteria=("Windows sign-in succeeds with the expected account.", "Affected apps stop prompting repeatedly and complete sign-in cleanly."),
    ),
    DeepSupportPlaybook(
        playbook_id="identity_token_cache_reset",
        family="identity",
        audience="service desk",
        aliases=("auth loop", "entra sign in loop", "mfa prompt not appearing"),
        diagnostics=(
            _binding("task_identity_signin_helper", "diagnostic", "Collect identity cache and device registration clues.", "Confirm whether this is a device, time, or cached-token issue."),
            _binding("task_browser_rescue", "diagnostic", "Collect browser auth-loop, proxy, and extension context.", "Determine whether the failure is browser-scoped."),
            _binding("task_teams_support_helper", "diagnostic", "Collect Teams sign-in and cache posture.", "Determine whether cached collaboration state is part of the auth loop."),
        ),
        remediations=(),
        validations=(
            _binding("task_browser_rescue", "validation", "Re-check browser state after guided token reset.", "Confirm the target site signs in cleanly."),
        ),
        guided_steps=(
            _guided("token_browser_profile", "Reset only the affected browser profile state", "Disable suspect extensions, clear the affected site's cookies/session state, and retry using a clean profile or private window.", "The auth flow completes without looping.", "The same loop occurs in multiple browsers or a native Office app."),
            _guided("token_teams_cache", "Reset Teams cache if Teams is part of the sign-in loop", "Close Teams fully, clear the documented cache folders, then relaunch and sign in again.", "Teams launches and completes sign-in once.", "The tenant/account still cannot sign in after cache reset."),
        ),
        evidence_plan_ids=("office_bundle", "support_bundle"),
        escalation_rules=("Escalate if multiple apps still loop after guided cleanup or if MFA/tenant-side conditions remain the blocker.",),
        success_criteria=("One clean interactive prompt followed by successful sign-in.", "No repeated auth loop across browser and app surfaces."),
    ),
    DeepSupportPlaybook(
        playbook_id="network_baseline_repair",
        family="network",
        audience="service desk",
        aliases=("no internet", "dns failure", "internal apps fail", "apipa"),
        diagnostics=(
            _binding("task_network_evidence_pack", "diagnostic", "Collect IP, route, proxy, hosts, WLAN, and DNS evidence.", "Identify whether the failure is adapter, DNS, route, or proxy related."),
            _binding("task_vpn_remote_access_helper", "diagnostic", "Collect VPN client, tunnel, adapter, and reachability posture.", "Decide whether the issue is general network or remote-access specific."),
        ),
        remediations=(
            _binding("task_dns_flush", "remediation", "Run the lowest-risk DNS reset first.", "Retest DNS and the target app."),
            _binding("task_ip_release_renew", "remediation", "Refresh DHCP lease state when the adapter is up but addressing is bad.", "Confirm a valid IPv4 address returns."),
            _binding("task_network_stack_repair_tool", "remediation", "Run the full admin repair chain only when adapter/proxy/DNS evidence still points to local stack corruption.", "Reboot if requested, then re-run diagnostics."),
        ),
        validations=(
            _binding("task_network_evidence_pack", "validation", "Re-run the full network bundle after repair.", "Compare pre/post DNS, proxy, route, and adapter state."),
        ),
        guided_steps=(
            _guided("network_proxy_review", "Review proxy and captive portal state", "If DNS and adapter state look normal but browsing is broken, compare WinHTTP/browser proxy state and complete any captive-portal sign-in.", "The device can reach both public sites and the original target.", "Proxy or site policy keeps reapplying the broken state."),
            _guided("network_drive_reconnect", "Reconnect mapped drives only after core reachability is healthy", "Validate the UNC path directly before remapping the drive letter.", "The share opens by UNC and then remapped drive access succeeds.", "UNC access itself fails or permissions are wrong."),
        ),
        evidence_plan_ids=("network_bundle", "support_bundle"),
        escalation_rules=("Escalate to network support if local stack repair and post-checks still show upstream DNS, routing, DHCP, or site/VPN faults.",),
        success_criteria=("Public and internal name resolution work.", "The affected app/share becomes reachable after repair or guided correction."),
    ),
    DeepSupportPlaybook(
        playbook_id="vpn_remote_access_repair",
        family="remote",
        audience="service desk",
        aliases=("vpn not working", "vpn connected but no resources", "rdp cannot connect"),
        diagnostics=(
            _binding("task_vpn_remote_access_helper", "diagnostic", "Collect VPN profile, adapter, process, and route posture.", "Identify whether the failure is tunnel setup, internal reachability, or saved-state drift."),
            _binding("task_network_evidence_pack", "diagnostic", "Collect supporting baseline network evidence.", "Verify the underlying network path before blaming VPN."),
        ),
        remediations=(),
        validations=(
            _binding("task_vpn_remote_access_helper", "validation", "Re-check VPN/tunnel posture after guided reset.", "Confirm connect state and internal reachability."),
        ),
        guided_steps=(
            _guided("vpn_client_restart", "Restart the VPN client and reconnect", "Close the VPN client fully, relaunch it, reconnect, and then retest an internal DNS name or mapped share.", "The tunnel shows connected and internal resources open.", "The tunnel fails to establish or immediately disconnects."),
            _guided("rdp_saved_state", "Review saved RDP/VDI state", "Clear stale saved credentials for the remote endpoint and retry using the expected gateway/profile.", "The remote session launches once credentials are refreshed.", "The endpoint is unreachable or broker-side launch still fails."),
        ),
        evidence_plan_ids=("network_bundle", "support_bundle"),
        escalation_rules=("Escalate if tunnel/broker-side failures persist after reconnect and the baseline network path is healthy.",),
        success_criteria=("VPN connects cleanly or the remote session launches.", "Internal reachability returns for the target workflow."),
    ),
    DeepSupportPlaybook(
        playbook_id="printer_spooler_repair",
        family="print",
        audience="service desk",
        aliases=("printer offline", "jobs stuck", "cannot add printer", "wrong default printer"),
        diagnostics=(
            _binding("task_printer_status", "diagnostic", "Collect spooler, queue, and PrintService posture.", "Determine whether the issue is queue, spooler, or mapping related."),
            _binding("task_service_health_snapshot", "diagnostic", "Confirm spooler and related services are healthy.", "Check whether spooler stability is the blocker."),
        ),
        remediations=(
            _binding("task_restart_spooler", "remediation", "Restart the spooler when jobs are stuck or the queue is stale.", "Retest queue state after the restart."),
            _binding("task_printer_full_reset_tool", "remediation", "Run the full admin spooler and queue reset when restart alone does not recover printing.", "Retest queue and printer enumeration after reset."),
        ),
        validations=(
            _binding("task_printer_status", "validation", "Re-check queue and spooler posture after repair.", "Verify the queue is clear and the printer is online."),
        ),
        guided_steps=(
            _guided("printer_default_check", "Set the correct default printer only after queue health is restored", "If the wrong printer keeps taking jobs, set the intended queue as default and retest from the affected app.", "A test page reaches the expected device.", "Policy or print-server mapping keeps reverting the default queue."),
        ),
        evidence_plan_ids=("printer_bundle", "eventlogs_bundle", "support_bundle"),
        escalation_rules=("Escalate if the queue keeps corrupting, the print server mapping is broken, or the device stays unreachable after local spooler repair.",),
        success_criteria=("Queue clears and new jobs enter the right queue.", "A test page or PDF print succeeds."),
    ),
    DeepSupportPlaybook(
        playbook_id="outlook_mailbox_repair",
        family="email",
        audience="service desk",
        aliases=("outlook password", "loading profile", "outlook search broken", "shared mailbox missing"),
        diagnostics=(
            _binding("task_outlook_profile_deep_helper", "diagnostic", "Collect Outlook profile, search, OST/PST path, add-in, and auth posture.", "Determine whether the issue is profile corruption, auth, search, or mailbox state."),
            _binding("task_office_outlook_helper", "diagnostic", "Collect Office install and profile path posture.", "Verify Office pathing and baseline app posture."),
        ),
        remediations=(),
        validations=(
            _binding("task_outlook_profile_deep_helper", "validation", "Re-check Outlook posture after guided repair.", "Confirm profile load, search, and prompt state."),
        ),
        guided_steps=(
            _guided("outlook_safe_mode", "Launch Outlook in safe mode to isolate add-ins", "Start Outlook with safe mode, compare startup behavior, and disable suspect add-ins only if the safe-mode result improves.", "Outlook opens without hanging and the trigger add-in is identified.", "Safe mode still hangs or the mailbox itself is unavailable."),
            _guided("outlook_profile_rebuild", "Recreate the Outlook profile only after evidence is captured", "Keep the original profile intact, create a new profile, test send/receive and search, then retire the old profile only after validation.", "Outlook opens, authenticates once, and search/mailbox state returns.", "Mailbox service, autodiscover, or tenant-side issues remain."),
        ),
        evidence_plan_ids=("office_bundle", "support_bundle"),
        escalation_rules=("Escalate if Outlook profile recreation does not restore launch/auth/search or if mailbox-side state is clearly the blocker.",),
        success_criteria=("Outlook opens without looping or hanging.", "Search, mailbox visibility, and attachments behave normally."),
    ),
    DeepSupportPlaybook(
        playbook_id="teams_meeting_repair",
        family="collab",
        audience="service desk",
        aliases=("teams camera", "teams mic", "teams sign in", "teams notifications"),
        diagnostics=(
            _binding("task_teams_support_helper", "diagnostic", "Collect Teams process, cache, update, and log posture.", "Determine whether the issue is app state, sign-in, or device route related."),
            _binding("task_audio_mic_helper", "diagnostic", "Collect endpoint inventory and device-change evidence.", "Check whether the problem is at the Windows device layer."),
            _binding("task_camera_privacy_check", "diagnostic", "Collect privacy posture for camera/mic access.", "Confirm whether Windows privacy is blocking Teams."),
        ),
        remediations=(),
        validations=(
            _binding("task_teams_support_helper", "validation", "Re-check Teams posture after guided cache/device cleanup.", "Confirm launch, sign-in, and meeting devices."),
        ),
        guided_steps=(
            _guided("teams_cache_reset", "Reset Teams cache and relaunch", "Close Teams fully, clear the documented cache folders, relaunch, and sign in again.", "Teams opens cleanly and meeting UI/device selection returns.", "The app still fails before sign-in or cache re-corrupts immediately."),
            _guided("teams_device_route", "Re-select the intended meeting devices", "Confirm the Windows default device and the Teams in-app device selection match the intended camera, mic, and speaker.", "A test call uses the expected devices without echo or blank video.", "The device does not enumerate or stays blocked by policy/privacy."),
        ),
        evidence_plan_ids=("device_bundle", "support_bundle"),
        escalation_rules=("Escalate if Teams still fails after cache reset and Windows device/privacy posture is healthy.",),
        success_criteria=("Teams launches and signs in once.", "Camera, mic, notifications, and screen share work for the affected user flow."),
    ),
    DeepSupportPlaybook(
        playbook_id="browser_repair",
        family="browser",
        audience="service desk",
        aliases=("browser won't open", "sso loop", "one site broken", "certificate warning"),
        diagnostics=(
            _binding("task_browser_rescue", "diagnostic", "Collect browser association, versions, profiles, proxy, extension, and DNS context.", "Decide whether the failure is browser-wide, profile-specific, or site/auth specific."),
            _binding("task_identity_signin_helper", "diagnostic", "Collect time and identity posture when the failure looks trust/auth related.", "Verify whether clock skew or device registration is contributing."),
        ),
        remediations=(),
        validations=(
            _binding("task_browser_rescue", "validation", "Re-check browser/profile/proxy posture after guided cleanup.", "Retest the failing site or download."),
        ),
        guided_steps=(
            _guided("browser_clean_profile", "Use a clean profile or private window to isolate extensions and profile corruption", "Retest the target workload in a clean profile/private window before resetting the primary profile.", "The site works in a clean profile and the failure scope is isolated.", "The same failure occurs in a clean profile or across browsers."),
            _guided("browser_reset", "Reset only the affected browser profile/settings", "If the issue is isolated to one browser, reset homepage/search/startup state and remove suspect extensions.", "The browser opens normally and the affected site/download works.", "The issue is cross-browser, certificate-related, or policy re-applies the bad state."),
        ),
        evidence_plan_ids=("network_bundle", "support_bundle"),
        escalation_rules=("Escalate if trust/policy or cross-browser auth failure persists after profile isolation and time/device checks.",),
        success_criteria=("The browser opens and the target site/download works.", "SSO/auth completes without looping or trust prompts."),
    ),
    DeepSupportPlaybook(
        playbook_id="slow_pc_triage",
        family="performance",
        audience="service desk",
        aliases=("slow pc", "high cpu", "high memory", "fan loud", "freezing"),
        diagnostics=(
            _binding("task_performance_sample", "diagnostic", "Collect CPU, RAM, disk, and top-process pressure.", "Identify the hottest process or resource bottleneck."),
            _binding("task_startup_autostart_pack", "diagnostic", "Collect startup pressure and logon load.", "Decide whether startup bloat is the main driver."),
            _binding("task_thermal_hints", "diagnostic", "Collect thermal/throttle hints.", "Check whether heat or sustained load is part of the symptom."),
            _binding("task_smart_snapshot", "diagnostic", "Collect disk health hints.", "Look for hardware risk before cleanup-only advice."),
        ),
        remediations=(
            _binding("task_storage_radar", "remediation", "Run cleanup preview and storage ranking to reduce obvious space pressure safely.", "Review the top space consumers before deleting anything."),
        ),
        validations=(
            _binding("task_performance_sample", "validation", "Re-sample process pressure after safe cleanup or reboot.", "Compare whether the hottest offenders changed."),
        ),
        guided_steps=(
            _guided("slowpc_reboot", "Use a clean reboot when uptime/startup pressure is high", "If uptime is excessive or multiple pending updates exist, perform a clean reboot before deeper app cleanup.", "The system becomes responsive for the original workload.", "Performance remains poor immediately after reboot."),
            _guided("slowpc_startup_trim", "Disable only clearly unnecessary startup items", "Use the collected startup pack to trim non-essential user-space items first.", "Login and app launch time improve without functional regression.", "A core business app is the offender or startup pressure is not the main cause."),
        ),
        evidence_plan_ids=("system_snapshot", "crash_bundle", "support_bundle"),
        escalation_rules=("Escalate if hardware health, thermal state, or disk health looks bad, or if performance remains poor after safe cleanup and reboot validation.",),
        success_criteria=("The original workload becomes measurably more responsive.", "No obvious thermal/disk health blockers remain unresolved."),
    ),
    DeepSupportPlaybook(
        playbook_id="windows_update_repair",
        family="windows_update",
        audience="it",
        aliases=("windows update stuck", "feature update failing", "pending reboot"),
        diagnostics=(
            _binding("task_update_repair_evidence_pack", "diagnostic", "Collect service, pending reboot, Windows Update log, and EVTX evidence.", "Capture servicing posture before any reset."),
        ),
        remediations=(
            _binding("task_windows_update_reset_tool", "remediation", "Run the full Windows Update reset chain when servicing state is locally broken.", "Reboot if requested, then re-run update evidence."),
        ),
        validations=(
            _binding("task_update_repair_evidence_pack", "validation", "Re-check service and log posture after reset.", "Retest the failed update with the new evidence pack."),
        ),
        guided_steps=(
            _guided("wu_capture_error", "Capture the exact update code before retrying", "Document the failing KB/feature update and exact error from Settings or event logs before starting the reset chain.", "The post-reset retest is tied to the same failing update code.", "Servicing still fails with the same code after reset."),
        ),
        evidence_plan_ids=("update_bundle", "eventlogs_bundle", "support_bundle"),
        escalation_rules=("Escalate if the reset chain and post-check still leave servicing broken, especially when CBS/DISM or feature-update readiness faults remain.",),
        success_criteria=("Update services look healthy post-reset.", "The original update progresses further or succeeds."),
    ),
    DeepSupportPlaybook(
        playbook_id="os_health_integrity_repair",
        family="windows_update",
        audience="it",
        aliases=("sfc errors", "dism fails", "system corruption", "bsod follow up"),
        diagnostics=(
            _binding("task_update_repair_evidence_pack", "diagnostic", "Collect update/log posture before integrity repair.", "Capture servicing context before SFC/DISM."),
            _binding("task_smart_snapshot", "diagnostic", "Collect disk health hints before assuming software-only corruption.", "Rule out obvious storage-health risk."),
        ),
        remediations=(
            _binding("task_sfc_dism_integrity_tool", "remediation", "Run SFC and DISM for local integrity repair.", "Review logs and reboot if requested."),
        ),
        validations=(
            _binding("task_sfc_dism_integrity_tool", "validation", "Review integrity chain output after execution.", "Compare whether corruption persists."),
        ),
        guided_steps=(
            _guided("integrity_log_review", "Review CBS/DISM evidence before repeating repair loops", "Use the captured logs to avoid running the same integrity chain blindly.", "The next repair or escalation is based on actual log evidence.", "Integrity tools still fail or crash evidence points to hardware."),
        ),
        evidence_plan_ids=("update_bundle", "crash_bundle", "support_bundle"),
        escalation_rules=("Escalate if integrity repair still fails, corruption recurs immediately, or storage/crash evidence points to hardware faults.",),
        success_criteria=("SFC/DISM completes or shows improved integrity state.", "The original symptom is reduced or clearly escalated with evidence."),
    ),
    DeepSupportPlaybook(
        playbook_id="profile_shell_repair",
        family="shell",
        audience="service desk",
        aliases=("start menu broken", "search broken", "temporary profile", "explorer crashing"),
        diagnostics=(
            _binding("task_shell_start_search_helper", "diagnostic", "Collect Explorer, Search, profile, and Start/Search process posture.", "Determine whether the problem is shell process, search service, or profile damage."),
            _binding("task_system_profile_belarc_lite", "diagnostic", "Collect broader system profile context before deeper shell repair.", "Capture machine posture for escalation."),
        ),
        remediations=(
            _binding("task_restart_explorer_safe", "remediation", "Restart Explorer safely for stuck shell/taskbar/tray state.", "Retest Start, taskbar, and tray immediately after restart."),
        ),
        validations=(
            _binding("task_shell_start_search_helper", "validation", "Re-check shell/search posture after Explorer restart or guided cleanup.", "Confirm Start/Search/Explorer stability."),
        ),
        guided_steps=(
            _guided("shell_icon_cache", "Rebuild icon or search index only after evidence is captured", "If Explorer restart helps only briefly, proceed to icon-cache or search-index rebuild using the documented manual flow.", "Icons/Search return and remain stable after the rebuild.", "A temp profile or corruption symptom remains after shell cleanup."),
            _guided("shell_temp_profile", "Treat temporary-profile symptoms as profile-risk events", "Do not discard the original profile path; confirm profile state and data protection before deeper repair or reprofile.", "The expected profile loads and user data is intact.", "Profile load remains broken or data risk is present."),
        ),
        evidence_plan_ids=("system_snapshot", "eventlogs_bundle", "support_bundle"),
        escalation_rules=("Escalate if temp-profile or shell corruption symptoms remain after Explorer restart and guided cleanup, or if integrity repair becomes necessary.",),
        success_criteria=("Start, Search, taskbar, tray, and Explorer behave normally.", "The expected user profile loads instead of a temporary profile."),
    ),
    DeepSupportPlaybook(
        playbook_id="support_bundle_export_playbook",
        family="recovery",
        audience="service desk",
        aliases=("support bundle", "collect evidence", "export logs"),
        diagnostics=(
            _binding("task_eventlog_exporter_pack", "diagnostic", "Collect the core event-log evidence set.", "Verify the exportable evidence set is populated."),
            _binding("task_system_profile_belarc_lite", "diagnostic", "Collect a support-friendly system profile snapshot.", "Ensure baseline posture is included in the bundle."),
        ),
        remediations=(),
        validations=(
            _binding("task_eventlog_exporter_pack", "validation", "Confirm the event-log export exists and is attachable.", "Open Reports and verify the evidence checklist."),
        ),
        guided_steps=(
            _guided("bundle_review", "Review masking and bundle type in Reports", "Use Reports to validate masking, evidence coverage, and bundle type before sharing externally.", "Reports shows the expected files and masking posture.", "Evidence is still incomplete after collection."),
        ),
        evidence_plan_ids=("system_snapshot", "eventlogs_bundle", "support_bundle"),
        escalation_rules=("Escalate only after Reports confirms the bundle contains the required evidence and masking posture.",),
        success_criteria=("Reports shows a populated evidence checklist.", "The bundle can be generated and handed off safely."),
    ),
    DeepSupportPlaybook(
        playbook_id="backup_pre_fix_snapshot_workflow",
        family="recovery",
        audience="service desk",
        aliases=("backup before troubleshooting", "pre fix snapshot", "snapshot before repair"),
        diagnostics=(
            _binding("task_system_profile_belarc_lite", "diagnostic", "Collect pre-change system/profile posture.", "Record the baseline before risky change."),
            _binding("task_storage_ranked_view", "diagnostic", "Collect storage and path context for backup/snapshot planning.", "Confirm where user data and heavy storage live."),
        ),
        remediations=(),
        validations=(
            _binding("task_system_profile_belarc_lite", "validation", "Reconfirm the baseline record exists in evidence.", "Verify the pre-fix record is attached to the case."),
        ),
        guided_steps=(
            _guided("backup_confirm", "Create or confirm the backup location before risky work", "Do not proceed with profile rebuild, update reset, or cleanup that risks user data until the backup path is validated.", "A technician can point to the backup/snapshot location and evidence files.", "Backup tooling is missing or the data set is too risky for local-only handling."),
        ),
        evidence_plan_ids=("backup_snapshot", "support_bundle"),
        escalation_rules=("Escalate if backup tooling or recovery posture is insufficient for the planned risky change.",),
        success_criteria=("A pre-fix baseline and snapshot path exist before risky remediation starts.",),
    ),
    DeepSupportPlaybook(
        playbook_id="new_pc_post_reimage_validation",
        family="recovery",
        audience="service desk",
        aliases=("new pc validation", "post reimage checklist", "handoff validation"),
        diagnostics=(
            _binding("task_system_profile_belarc_lite", "diagnostic", "Collect machine/profile baseline after provisioning.", "Verify the device was actually provisioned as expected."),
            _binding("task_network_evidence_pack", "diagnostic", "Confirm core connectivity and internal reachability.", "Validate internet and enterprise reachability."),
            _binding("task_driver_device_inventory_pack", "diagnostic", "Collect device and driver posture.", "Check for missing/problem devices before handoff."),
            _binding("task_update_repair_evidence_pack", "diagnostic", "Confirm update and reboot posture.", "Ensure the device is not being handed off mid-servicing."),
        ),
        remediations=(),
        validations=(
            _binding("task_driver_device_inventory_pack", "validation", "Re-check device posture after any guided correction.", "Confirm no obvious device blockers remain."),
        ),
        guided_steps=(
            _guided("newpc_user_flow", "Validate the user’s actual day-one workflow", "Confirm sign-in, sync, VPN, Teams/Outlook, printing, and dock/device posture that the user needs on day one.", "The user can perform the target workflow without new blockers.", "Provisioning/policy state is still incomplete."),
        ),
        evidence_plan_ids=("new_pc_validation_bundle", "support_bundle"),
        escalation_rules=("Escalate if provisioning, policy, join, or device state is still incomplete after validation.",),
        success_criteria=("Core apps, network, devices, and updates are ready for handoff.",),
    ),
)


def list_deep_support_playbooks() -> tuple[DeepSupportPlaybook, ...]:
    return DEEP_SUPPORT_PLAYBOOKS


def deep_support_playbook_map() -> dict[str, DeepSupportPlaybook]:
    return {row.playbook_id: row for row in DEEP_SUPPORT_PLAYBOOKS}


def deep_support_playbook_stats() -> DeepSupportPlaybookStats:
    diagnostics = {row.task_id for plan in DEEP_SUPPORT_PLAYBOOKS for row in plan.diagnostics}
    remediations = {row.task_id for plan in DEEP_SUPPORT_PLAYBOOKS for row in plan.remediations}
    primitives = {row.task_id for plan in DEEP_SUPPORT_PLAYBOOKS for bucket in (plan.diagnostics, plan.remediations, plan.validations) for row in bucket}
    return DeepSupportPlaybookStats(
        playbook_count=len(DEEP_SUPPORT_PLAYBOOKS),
        diagnostic_script_count=len(diagnostics),
        remediation_script_count=len(remediations),
        guided_flow_count=sum(1 for plan in DEEP_SUPPORT_PLAYBOOKS if plan.guided_steps),
        shared_primitive_count=len(primitives),
        support_bundle_integrated_count=sum(1 for plan in DEEP_SUPPORT_PLAYBOOKS if plan.support_bundle_integrated),
    )


def is_deep_support_playbook(playbook_id: str) -> bool:
    return str(playbook_id or "").strip() in deep_support_playbook_map()


def _result_status(binding: PlaybookTaskBinding, payload: dict[str, Any], *, skipped: bool = False) -> str:
    if skipped:
        return "manual"
    code = int(payload.get("code", 0 if payload.get("dry_run") else 1))
    if code != 0:
        return "fail"
    if binding.phase == "remediation":
        return "changed"
    return "pass"


def _normalize_step_result(binding: PlaybookTaskBinding, payload: dict[str, Any], *, skipped: bool = False) -> dict[str, Any]:
    if skipped:
        return {
            "task_id": binding.task_id,
            "title": binding.title,
            "phase": binding.phase,
            "status": "manual",
            "summary": f"{binding.title} was skipped because admin approval or elevation was not available.",
            "next_best_action": binding.next_step_hint,
        }
    findings = payload.get("normalized_findings")
    if isinstance(findings, list) and findings:
        return {
            "task_id": binding.task_id,
            "title": binding.title,
            "phase": binding.phase,
            "status": _result_status(binding, payload),
            "summary": str(findings[0].get("summary", payload.get("summary_text", ""))).strip(),
            "next_best_action": str(findings[0].get("next_best_action", binding.next_step_hint)).strip() or binding.next_step_hint,
            "findings": findings,
        }
    summary = str(payload.get("summary_text", "") or payload.get("stdout", "") or payload.get("stderr", "")).strip()
    return {
        "task_id": binding.task_id,
        "title": binding.title,
        "phase": binding.phase,
        "status": _result_status(binding, payload),
        "summary": summary[:1400],
        "next_best_action": binding.next_step_hint,
    }


def _summary_lines(plan: DeepSupportPlaybook, rows: list[dict[str, Any]], mode: str) -> str:
    checked = [f"{row['phase']}: {row['title']}" for row in rows]
    found = [f"{row['title']} -> {row['status']}" for row in rows]
    changed = [f"{row['title']}: remediation executed" for row in rows if row["phase"] == "remediation" and row["status"] == "changed"]
    if not changed:
        changed = ["No direct system changes were made." if mode != "full" else "No automated remediation completed in this run."]
    next_steps = [row.get("next_best_action", "") for row in rows if row.get("next_best_action")]
    next_steps.extend(plan.escalation_rules[:1])
    lines = [
        f"{support_playbook_map()[plan.playbook_id].title} Summary",
        "",
        "What we checked:",
        *[f"- {row}" for row in checked],
        "",
        "What we found:",
        *[f"- {row}" for row in found],
        "",
        "What we changed:",
        *[f"- {row}" for row in changed],
        "",
        "Next steps:",
        *[f"- {row}" for row in next_steps[:8]],
        "",
        "Success looks like:",
        *[f"- {row}" for row in plan.success_criteria],
        "",
        "Escalate when:",
        *[f"- {row}" for row in plan.escalation_rules],
    ]
    return "\n".join(lines).strip() + "\n"


def execute_support_playbook(
    playbook_id: str,
    *,
    mode: str = "diagnose",
    issue_id: str = "",
    session_id: str = "",
    mask_options: MaskingOptions | None = None,
    output_root: str | Path | None = None,
    cancel_event: Any = None,
    progress_cb: Callable[[int, str], None] | None = None,
    partial_cb: Callable[[dict[str, Any]], None] | None = None,
    log_cb: Callable[[str], None] | None = None,
    timeout_s: int = 300,
    allow_admin_actions: bool = False,
    run_event_bus: Any = None,
    run_id: str = "",
) -> dict[str, Any]:
    mapping = deep_support_playbook_map()
    if playbook_id not in mapping:
        raise KeyError(playbook_id)
    plan = mapping[playbook_id]
    playbook = support_playbook_map()[playbook_id]
    mode_key = str(mode or "diagnose").strip().lower()
    include_remediation = mode_key == "full"
    selected: list[PlaybookTaskBinding] = list(plan.diagnostics)
    if include_remediation:
        selected.extend(plan.remediations)
    selected.extend(plan.validations)
    work_root = Path(output_root) if output_root is not None else ensure_dirs()["state"] / "support_playbooks" / f"{playbook_id}_{datetime.now().strftime('%Y%m%d_%H%M%S')}"
    work_root.mkdir(parents=True, exist_ok=True)
    rows: list[dict[str, Any]] = []
    findings: list[dict[str, Any]] = []
    evidence_files: list[str] = []
    total = max(1, len(selected))
    task_meta = script_task_map()
    for index, binding in enumerate(selected, start=1):
        if cancel_event is not None and getattr(cancel_event, "is_set", lambda: False)():
            normalized_rows = [row.get("normalized", row) if isinstance(row, dict) else {} for row in rows]
            return {
                "support_playbook_id": playbook_id,
                "title": playbook.title,
                "issue_id": issue_id,
                "session_id": session_id,
                "mode": mode_key,
                "cancelled": True,
                "code": 130,
                "steps": rows,
                "findings": findings,
                "summary_text": _summary_lines(plan, normalized_rows, mode_key),
                "evidence_root": str(work_root),
                "evidence_files": evidence_files,
                "guided_steps": [asdict(row) for row in plan.guided_steps],
                "escalation_rules": list(plan.escalation_rules),
                "success_criteria": list(plan.success_criteria),
                "support_bundle_integrated": plan.support_bundle_integrated,
            }
        pct = int((index - 1) * 100 / total)
        if progress_cb is not None:
            progress_cb(pct, f"{binding.phase.title()} {index}/{total}: {binding.title}")
        if log_cb is not None:
            log_cb(f"[support-playbook] {playbook_id} :: {binding.phase} :: {binding.task_id}")
        meta = task_meta[binding.task_id]
        skipped = bool(meta.admin_required and include_remediation and (not allow_admin_actions))
        if skipped:
            result = {"code": 1, "stderr": "Administrator approval required for this remediation step.", "output_files": [], "summary_text": ""}
        else:
            result = run_script_task(
                binding.task_id,
                dry_run=False,
                output_dir=work_root / binding.phase,
                mask_options=mask_options,
                cancel_event=cancel_event,
                timeout_override_s=max(20, int(timeout_s / total)),
                log_cb=log_cb,
                run_event_bus=run_event_bus,
                run_id=run_id,
            )
        normalized = _normalize_step_result(binding, result, skipped=skipped)
        row = {
            "id": binding.id,
            "phase": binding.phase,
            "title": binding.title,
            "task_id": binding.task_id,
            "purpose": binding.purpose,
            "skipped": skipped,
            "result": result,
            "normalized": normalized,
        }
        rows.append(row)
        findings.append(normalized)
        for path in result.get("output_files", []):
            p = str(path or "").strip()
            if p and p not in evidence_files:
                evidence_files.append(p)
        if partial_cb is not None:
            partial_cb(row)
    if progress_cb is not None:
        progress_cb(100, "Support playbook complete")
    failure_count = sum(1 for row in findings if row.get("status") == "fail")
    summary_text = _summary_lines(plan, findings, mode_key)
    payload = {
        "support_playbook_id": playbook_id,
        "title": playbook.title,
        "issue_id": issue_id,
        "session_id": session_id,
        "mode": mode_key,
        "cancelled": False,
        "code": 1 if failure_count else 0,
        "steps": rows,
        "findings": findings,
        "summary_text": summary_text,
        "user_message": "One or more script-backed support checks failed." if failure_count else "",
        "technical_message": f"failures={failure_count} mode={mode_key}",
        "next_steps_list": ensure_next_steps([*plan.success_criteria[:2], *plan.escalation_rules[:1]]),
        "evidence_root": str(work_root),
        "evidence_files": evidence_files,
        "guided_steps": [asdict(row) for row in plan.guided_steps],
        "escalation_rules": list(plan.escalation_rules),
        "success_criteria": list(plan.success_criteria),
        "evidence_plan_ids": list(plan.evidence_plan_ids),
        "support_bundle_integrated": plan.support_bundle_integrated,
        "requires_admin": any(script_task_map()[row.task_id].admin_required for row in plan.remediations),
        "reboot_possible": any(script_task_map()[row.task_id].reboot_likely for row in plan.remediations),
    }
    err = classify_exit(int(payload["code"]), payload["user_message"] or payload["technical_message"])
    if err is not None and payload["code"] != 0:
        payload.update(err.as_payload())
    return payload
