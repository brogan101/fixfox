from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any, Callable

from .masking import MaskingOptions
from .paths import ensure_dirs
from .run_events import RunEventBus, RunEventType
from .script_tasks import create_restore_point, run_script_task, script_task_map


@dataclass(frozen=True)
class RunbookStep:
    id: str
    title: str
    task_id: str
    checkpoint: bool = False


@dataclass(frozen=True)
class Runbook:
    id: str
    title: str
    audience: str
    desc: str
    steps: tuple[RunbookStep, ...]


RUNBOOKS: tuple[Runbook, ...] = (
    Runbook(
        "home_fix_wifi_safe",
        "Home: Fix Wi-Fi (Safe)",
        "home",
        "Collect network evidence, apply safe reset, and prepare export-ready context.",
        (
            RunbookStep("wifi_wizard", "Run Wi-Fi Report + Fix Wizard", "task_wifi_report_fix_wizard", True),
            RunbookStep("network_pack", "Collect network evidence pack", "task_network_evidence_pack", True),
            RunbookStep("flush", "Flush DNS cache", "task_dns_flush", True),
            RunbookStep("ping1", "Ping Cloudflare", "task_ping_1_1_1_1"),
            RunbookStep("ping2", "Ping Google DNS", "task_ping_8_8_8_8"),
        ),
    ),
    Runbook(
        "home_free_up_space_safe",
        "Home: Free Up Space (Safe)",
        "home",
        "Preview high-impact storage opportunities with no destructive actions.",
        (
            RunbookStep("storage_radar", "Run Storage Radar", "task_storage_radar", True),
            RunbookStep("radar", "Run Large File Radar", "task_large_file_radar", True),
            RunbookStep("downloads", "Preview Downloads cleanup buckets", "task_downloads_cleanup_buckets"),
            RunbookStep("appdata", "Run AppData Bloat Scanner", "task_appdata_bloat_scanner"),
            RunbookStep("ranked", "Generate ranked storage view", "task_storage_ranked_view"),
            RunbookStep("systeminfo", "Collect system snapshot", "task_systeminfo_export"),
        ),
    ),
    Runbook(
        "home_speed_up_pc_safe",
        "Home: Speed Up PC (Safe)",
        "home",
        "Capture sustained process pressure and suggest reversible startup adjustments.",
        (
            RunbookStep("sample", "Collect process sampling window", "task_performance_sample", True),
            RunbookStep("startup_pack", "Collect startup/autostart pack", "task_startup_autostart_pack", True),
            RunbookStep("reboot", "Collect pending reboot signals", "task_pending_reboot_sources"),
            RunbookStep("system_profile", "Collect Belarc-lite profile", "task_system_profile_belarc_lite"),
        ),
    ),
    Runbook(
        "home_printer_rescue",
        "Home: Printer Rescue (Safe/Admin Optional)",
        "home",
        "Collect print evidence and apply spooler fixes if elevated.",
        (
            RunbookStep("printer_status", "Collect printer status", "task_printer_status", True),
            RunbookStep("services", "Collect services snapshot", "task_services_export", True),
            RunbookStep("queue", "Collect print queue", "task_print_queue"),
            RunbookStep("evtx", "Export PrintService log", "task_evtx_printservice"),
            RunbookStep("restart", "Restart spooler (admin if available)", "task_restart_spooler", True),
        ),
    ),
    Runbook(
        "home_browser_problems",
        "Home: Browser Problems",
        "home",
        "Runs browser rescue checks, network baselines, and prepares export-ready evidence.",
        (
            RunbookStep("browser", "Run Browser Rescue", "task_browser_rescue", True),
            RunbookStep("dns", "Measure DNS timing", "task_dns_timing", True),
            RunbookStep("proxy", "Collect proxy status", "task_proxy_show", True),
            RunbookStep("hosts", "Check hosts file", "task_hosts_check", True),
            RunbookStep("wlan", "Collect WLAN report", "task_wlan_report", True),
        ),
    ),
    Runbook(
        "home_no_audio_mic",
        "Home: No Audio / Mic",
        "home",
        "Runs audio helper checks with device inventory hints and export-ready output.",
        (
            RunbookStep("audio", "Run Audio & Mic Helper", "task_audio_mic_helper", True),
            RunbookStep("device_inventory", "Collect device inventory", "task_driver_device_inventory_pack", True),
            RunbookStep("system", "Collect system snapshot", "task_systeminfo_export", True),
        ),
    ),
    Runbook(
        "home_onedrive_not_syncing",
        "Home: OneDrive Not Syncing",
        "home",
        "Runs OneDrive helper with storage and network checks before export.",
        (
            RunbookStep("onedrive", "Run OneDrive Sync Helper", "task_onedrive_sync_helper", True),
            RunbookStep("storage", "Run storage ranked view", "task_storage_ranked_view", True),
            RunbookStep("dns", "Run DNS timing test", "task_dns_timing", True),
            RunbookStep("proxy", "Collect proxy status", "task_proxy_show", True),
        ),
    ),
    Runbook(
        "home_usb_bt_disconnects",
        "Home: USB/Bluetooth Disconnects",
        "home",
        "Runs USB/Bluetooth helper with inventory and power-state checks.",
        (
            RunbookStep("usb_bt", "Run USB/Bluetooth Disconnect Helper", "task_usb_bt_disconnect_helper", True),
            RunbookStep("inventory", "Collect Driver/Device Inventory", "task_driver_device_inventory_pack", True),
            RunbookStep("power", "Collect power state hints", "task_powercfg_a", True),
            RunbookStep("events", "Collect recent system events", "task_wevtutil_system", True),
        ),
    ),
    Runbook(
        "it_ticket_triage_pack",
        "IT: Ticket Triage Pack",
        "it",
        "Collect support evidence and prepare ticket-grade diagnostics.",
        (
            RunbookStep("system_profile", "Collect Belarc-lite profile", "task_system_profile_belarc_lite", True),
            RunbookStep("startup", "Collect startup/autostart pack", "task_startup_autostart_pack", True),
            RunbookStep("eventlogs_pack", "Collect event logs pack", "task_eventlog_exporter_pack", True),
            RunbookStep("network_pack", "Collect network evidence pack", "task_network_evidence_pack", True),
            RunbookStep("updates_pack", "Collect update repair evidence pack", "task_update_repair_evidence_pack", True),
            RunbookStep("crash_pack", "Collect crash evidence pack", "task_crash_evidence_pack", True),
            RunbookStep("printer", "Collect printer status snapshot", "task_printer_status"),
        ),
    ),
    Runbook(
        "it_app_crash_triage_pack",
        "IT: App Crash Triage Pack",
        "it",
        "Collects crash-focused evidence and ready-to-export ticket artifacts.",
        (
            RunbookStep("crash_helper", "Run App Crash Helper", "task_app_crash_helper", True),
            RunbookStep("app_log", "Export Application EVTX", "task_evtx_application", True),
            RunbookStep("sys_log", "Export System EVTX", "task_evtx_system", True),
            RunbookStep("reliability", "Collect reliability snapshot", "task_reliability_snapshot", True),
            RunbookStep("dumps", "Collect minidumps", "task_minidumps_collect", True),
            RunbookStep("services", "Collect service health snapshot", "task_service_health_snapshot", True),
        ),
    ),
    Runbook(
        "it_usb_bt_disconnect_triage",
        "IT: USB/Bluetooth Disconnect Triage",
        "it",
        "Collects disconnect hints, device inventory, and event evidence for escalation.",
        (
            RunbookStep("usb_bt", "Run USB/Bluetooth Disconnect Helper", "task_usb_bt_disconnect_helper", True),
            RunbookStep("inventory", "Run Driver/Device Inventory Pack", "task_driver_device_inventory_pack", True),
            RunbookStep("system_events", "Collect recent system events", "task_wevtutil_system", True),
            RunbookStep("power_states", "Collect power states", "task_powercfg_a", True),
        ),
    ),
    Runbook(
        "it_office_outlook_triage",
        "IT: Office/Outlook Triage",
        "it",
        "Collects Office/Outlook metadata plus system baselines for ticket handoff.",
        (
            RunbookStep("office", "Run Office/Outlook Helper", "task_office_outlook_helper", True),
            RunbookStep("system", "Collect system snapshot", "task_systeminfo_export", True),
            RunbookStep("services", "Collect service health snapshot", "task_service_health_snapshot", True),
            RunbookStep("app_events", "Collect recent app events", "task_wevtutil_application", True),
        ),
    ),
    Runbook(
        "it_windows_update_repair",
        "IT: Update Repair (Admin)",
        "it",
        "Collect update evidence, repair update components, and rerun checks.",
        (
            RunbookStep("evidence_pre", "Collect update evidence pack", "task_update_repair_evidence_pack", True),
            RunbookStep("repair", "Run Windows Update reset chain", "task_windows_update_reset_tool", True),
            RunbookStep("evidence_post", "Collect post-repair update evidence", "task_update_repair_evidence_pack", True),
        ),
    ),
    Runbook(
        "it_network_stack_repair",
        "IT: Network Stack Repair (Admin)",
        "it",
        "Capture network evidence, run stack resets, then rerun checks.",
        (
            RunbookStep("evidence_pre", "Collect network evidence pack", "task_network_evidence_pack", True),
            RunbookStep("repair", "Run network stack repair chain", "task_network_stack_repair_tool", True),
            RunbookStep("evidence_post", "Collect post-repair network evidence", "task_network_evidence_pack", True),
        ),
    ),
    Runbook(
        "it_system_integrity_check",
        "IT: Integrity Check (Admin)",
        "it",
        "Run integrity scans and collect post-scan evidence.",
        (
            RunbookStep("integrity_chain", "Run SFC/DISM integrity chain", "task_sfc_dism_integrity_tool", True),
            RunbookStep("system", "Collect system profile", "task_system_profile_belarc_lite"),
            RunbookStep("pending_reboot", "Collect pending reboot sources", "task_pending_reboot_sources"),
            RunbookStep("events", "Collect recent system events", "task_wevtutil_system"),
        ),
    ),
)


def runbook_map() -> dict[str, Runbook]:
    return {runbook.id: runbook for runbook in RUNBOOKS}


def _runbook_output_root(runbook_id: str) -> Path:
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    root = ensure_dirs()["state"] / "runbooks" / f"{runbook_id}_{stamp}"
    root.mkdir(parents=True, exist_ok=True)
    return root


def _flatten_files(rows: list[dict[str, Any]]) -> list[str]:
    files: list[str] = []
    for row in rows:
        result = row.get("result", {})
        for path in result.get("output_files", []):
            files.append(str(path))
    dedup: list[str] = []
    seen: set[str] = set()
    for path in files:
        if path in seen:
            continue
        seen.add(path)
        dedup.append(path)
    return dedup


def execute_runbook(
    runbook_id: str,
    *,
    dry_run: bool = False,
    progress_cb: Callable[[int, str], None] | None = None,
    partial_cb: Callable[[dict[str, Any]], None] | None = None,
    log_cb: Callable[[str], None] | None = None,
    cancel_event: Any = None,
    timeout_s: int = 120,
    mask_options: MaskingOptions | None = None,
    output_root: str | Path | None = None,
    create_restore_point_before_admin: bool = False,
    run_event_bus: RunEventBus | None = None,
    run_id: str = "",
) -> dict[str, Any]:
    mapping = runbook_map()
    if runbook_id not in mapping:
        raise KeyError(runbook_id)
    runbook = mapping[runbook_id]
    tasks = script_task_map()
    requires_admin = any(tasks.get(step.task_id) and tasks[step.task_id].admin_required for step in runbook.steps)
    reboot_likely = any(tasks.get(step.task_id) and tasks[step.task_id].reboot_likely for step in runbook.steps)

    work_root = Path(output_root) if output_root is not None else _runbook_output_root(runbook.id)
    work_root.mkdir(parents=True, exist_ok=True)
    rows: list[dict[str, Any]] = []
    total = len(runbook.steps)
    restore_point_result: dict[str, Any] | None = None
    bus = run_event_bus
    rid = str(run_id or "").strip()

    if bus is not None and rid:
        bus.publish(
            rid,
            RunEventType.START,
            message=f"Runbook started: {runbook.title}",
            data={"runbook_id": runbook.id, "audience": runbook.audience, "dry_run": dry_run},
        )

    if (not dry_run) and requires_admin and create_restore_point_before_admin:
        if progress_cb:
            progress_cb(1, "Creating restore point")
        if log_cb:
            log_cb("[runbook] Creating restore point (best effort).")
        if bus is not None and rid:
            bus.publish(rid, RunEventType.PROGRESS, message="Creating restore point.", progress=1)
        restore_point_result = create_restore_point(
            cancel_event=cancel_event,
            timeout_s=min(max(timeout_s // 2, 30), 180),
            log_cb=log_cb,
        )

    budget_each = max(15, int(timeout_s / max(total + 1, 1)))
    for index, step in enumerate(runbook.steps, start=1):
        if cancel_event is not None and getattr(cancel_event, "is_set", lambda: False)():
            if bus is not None and rid:
                bus.publish(rid, RunEventType.WARNING, message="Runbook cancelled before completion.")
                bus.publish(rid, RunEventType.END, message="Runbook cancelled.", data={"code": 130})
            return {
                "runbook_id": runbook.id,
                "title": runbook.title,
                "cancelled": True,
                "steps": rows,
                "audience": runbook.audience,
                "dry_run": dry_run,
                "requires_admin": requires_admin,
                "reboot_likely": reboot_likely,
                "evidence_root": str(work_root),
                "evidence_files": _flatten_files(rows),
                "restore_point": restore_point_result,
                "user_message": "Runbook execution was cancelled.",
                "technical_message": "Cancellation requested before completion.",
                "next_steps_list": [
                    "Re-run the runbook when ready.",
                    "Export partial evidence from Reports if needed.",
                    "Review completed checkpoints before retrying.",
                ],
            }
        pct = int((index - 1) * 100 / max(total, 1))
        if progress_cb:
            progress_cb(pct, f"Step {index}/{total}: {step.title}")
        if bus is not None and rid:
            bus.publish(rid, RunEventType.PROGRESS, message=f"Step {index}/{total}: {step.title}", progress=pct)
        if log_cb:
            log_cb(f"[runbook] Step {index}/{total}: {step.title} ({step.task_id})")
        task = tasks.get(step.task_id)
        category = task.category if task is not None else "misc"
        result = run_script_task(
            step.task_id,
            dry_run=dry_run,
            output_dir=work_root / category,
            mask_options=mask_options,
            cancel_event=cancel_event,
            timeout_override_s=budget_each,
            log_cb=log_cb,
            run_event_bus=bus,
            run_id=rid,
        )
        row = {
            "id": step.id,
            "title": step.title,
            "task_id": step.task_id,
            "checkpoint": step.checkpoint,
            "result": result,
        }
        rows.append(row)
        if log_cb and isinstance(result, dict):
            stdout = str(result.get("stdout", "")).strip()
            stderr = str(result.get("stderr", "")).strip()
            if stdout:
                log_cb(f"[stdout] {stdout[:2000]}")
            if stderr:
                log_cb(f"[stderr] {stderr[:2000]}")
        if partial_cb:
            partial_cb(row)
    if progress_cb:
        progress_cb(100, "Runbook complete")
    if bus is not None and rid:
        bus.publish(rid, RunEventType.PROGRESS, message="Runbook complete.", progress=100)
    codes = [int((row.get("result", {}) or {}).get("code", 0 if dry_run else 1)) for row in rows]
    failures = [code for code in codes if code not in (0,)]
    checkpoints = [row for row in rows if row.get("checkpoint")]
    checked = [f"{step.get('title', '')} ({step.get('task_id', '')})" for step in rows]
    found = [f"Steps executed: {len(rows)}", f"Failures: {len(failures)}", f"Checkpoints: {len(checkpoints)}"]
    changed = ["No steps changed system state in dry-run mode."] if dry_run else ["See per-step outputs for any system changes."]
    technical = [f"runbook_id={runbook.id}", f"audience={runbook.audience}", f"requires_admin={requires_admin}"]
    summary = "\n".join(
        [
            f"{runbook.title} Summary",
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
        ]
    )
    next_steps = (
        "Review output for failing steps, then export a Ticket Pack."
        if failures
        else "Review evidence files and export a support pack."
    )
    summary = summary + f"\n- {next_steps}\n\nTechnical appendix:\n" + "\n".join([f"- {row}" for row in technical]) + "\n"
    payload = {
        "runbook_id": runbook.id,
        "title": runbook.title,
        "audience": runbook.audience,
        "dry_run": dry_run,
        "cancelled": False,
        "requires_admin": requires_admin,
        "reboot_likely": reboot_likely,
        "steps": rows,
        "evidence_root": str(work_root),
        "evidence_files": _flatten_files(rows),
        "restore_point": restore_point_result,
        "summary_text": summary,
        "next_steps": next_steps,
        "next_steps_list": [
            next_steps,
            "Open Reports and export the recommended preset.",
            "Copy ticket summary from ToolRunner.",
            "Review failing steps in ToolRunner details." if failures else "Share the summary with support if needed.",
        ],
        "user_message": "One or more runbook steps failed." if failures else "",
        "technical_message": f"failures={len(failures)} checkpoints={len(checkpoints)}",
        "recommended_export_preset": "home_share" if runbook.audience == "home" else "ticket",
    }
    if bus is not None and rid:
        for path in payload.get("evidence_files", []):
            if path:
                bus.publish(rid, RunEventType.ARTIFACT, message=f"Artifact: {path}", data={"path": str(path)})
        code = 1 if failures else 0
        if failures:
            bus.publish(rid, RunEventType.ERROR, message="One or more runbook steps failed.", data={"code": code})
        bus.publish(rid, RunEventType.END, message=f"Runbook finished with code {code}.", data={"code": code})
    return payload


def generate_runbook_catalog(path: str) -> str:
    lines = [
        "# Runbook Catalog",
        "",
        "| ID | Title | Audience | Steps |",
        "|---|---|---|---|",
    ]
    for runbook in RUNBOOKS:
        lines.append(f"| `{runbook.id}` | {runbook.title} | {runbook.audience} | {len(runbook.steps)} |")
    p = Path(path)
    p.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return str(p)
