from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from threading import Event
from typing import Callable

from .masking import MaskingOptions
from .paths import ensure_dirs
from .script_tasks import run_script_task, script_task_map


@dataclass(frozen=True)
class EvidenceFile:
    task_id: str
    category: str
    path: str


@dataclass(frozen=True)
class EvidenceResult:
    root_dir: str
    files: list[EvidenceFile]
    summary_text: str
    cancelled: bool
    warnings: list[str]

    @property
    def files_created(self) -> list[str]:
        return [row.path for row in self.files]


def _session_evidence_root(session_id: str) -> Path:
    root = ensure_dirs()["sessions"] / str(session_id) / "evidence"
    root.mkdir(parents=True, exist_ok=True)
    return root


def _run_task_batch(
    *,
    session_id: str,
    task_ids: list[str],
    mask_options: MaskingOptions | None,
    output_root: str | Path | None,
    progress_cb: Callable[[int, str], None] | None,
    cancel_event: Event | None,
    timeout_s: int,
    log_cb: Callable[[str], None] | None,
    batch_title: str,
) -> EvidenceResult:
    mapping = script_task_map()
    root = Path(output_root) if output_root is not None else _session_evidence_root(session_id)
    root.mkdir(parents=True, exist_ok=True)

    files: list[EvidenceFile] = []
    lines: list[str] = []
    warnings: list[str] = []
    if not task_ids:
        return EvidenceResult(str(root), [], "No evidence tasks selected.", False, [])

    budget_each = max(10, int(timeout_s / max(len(task_ids), 1)))
    for index, task_id in enumerate(task_ids, start=1):
        if cancel_event is not None and cancel_event.is_set():
            lines.append(f"{batch_title}: cancelled.")
            return EvidenceResult(str(root), files, "\n".join(lines), True, warnings)
        task = mapping.get(task_id)
        if task is None:
            warnings.append(f"Unknown evidence task: {task_id}")
            lines.append(f"[1] {task_id} (unknown)")
            continue
        pct = int((index - 1) * 100 / max(len(task_ids), 1))
        if progress_cb is not None:
            progress_cb(pct, f"{batch_title} {index}/{len(task_ids)}: {task.title}")
        if log_cb is not None:
            log_cb(f"[evidence] {task.id}: {task.title}")
        result = run_script_task(
            task_id,
            dry_run=False,
            output_dir=root / task.category,
            mask_options=mask_options,
            cancel_event=cancel_event,
            timeout_override_s=budget_each,
            log_cb=log_cb,
        )
        code = int(result.get("code", 1))
        lines.append(f"[{code}] {task_id}")
        if code != 0:
            stderr = str(result.get("stderr", "")).strip()
            if stderr:
                warnings.append(f"{task_id}: {stderr[:260]}")
        for file_path in result.get("output_files", []):
            p = str(file_path)
            if p:
                files.append(EvidenceFile(task_id=task_id, category=task.category, path=p))

    if progress_cb is not None:
        progress_cb(100, f"{batch_title} complete")
    summary_text = "\n".join(lines) if lines else f"{batch_title}: no output."
    return EvidenceResult(str(root), files, summary_text, False, warnings)


def _write_bundle_summary(root_dir: str, rel_path: str, result: EvidenceResult, title: str) -> None:
    out = Path(root_dir) / rel_path
    out.parent.mkdir(parents=True, exist_ok=True)
    lines = [title, "", result.summary_text]
    if result.warnings:
        lines.extend(["", "Warnings:"])
        lines.extend([f"- {row}" for row in result.warnings])
    out.write_text("\n".join(lines).strip() + "\n", encoding="utf-8")


def collect_system_snapshot(
    session_id: str,
    *,
    mask_options: MaskingOptions | None = None,
    output_root: str | Path | None = None,
    progress_cb: Callable[[int, str], None] | None = None,
    cancel_event: Event | None = None,
    timeout_s: int = 240,
    log_cb: Callable[[str], None] | None = None,
) -> EvidenceResult:
    result = _run_task_batch(
        session_id=session_id,
        task_ids=[
            "task_systeminfo_export",
            "task_hotfixes_export",
            "task_drivers_export",
            "task_services_export",
            "task_scheduled_tasks_export",
            "task_startup_items_export",
        ],
        mask_options=mask_options,
        output_root=output_root,
        progress_cb=progress_cb,
        cancel_event=cancel_event,
        timeout_s=timeout_s,
        log_cb=log_cb,
        batch_title="System bundle",
    )
    _write_bundle_summary(result.root_dir, "system/summary.txt", result, "System Bundle")
    return result


def collect_event_logs(
    session_id: str,
    *,
    mask_options: MaskingOptions | None = None,
    output_root: str | Path | None = None,
    progress_cb: Callable[[int, str], None] | None = None,
    cancel_event: Event | None = None,
    timeout_s: int = 300,
    log_cb: Callable[[str], None] | None = None,
) -> EvidenceResult:
    result = _run_task_batch(
        session_id=session_id,
        task_ids=[
            "task_evtx_application",
            "task_evtx_system",
            "task_evtx_setup",
            "task_evtx_windows_update",
            "task_evtx_printservice",
            "task_evtx_devicesetup",
        ],
        mask_options=mask_options,
        output_root=output_root,
        progress_cb=progress_cb,
        cancel_event=cancel_event,
        timeout_s=timeout_s,
        log_cb=log_cb,
        batch_title="Event logs bundle",
    )
    _write_bundle_summary(result.root_dir, "eventlogs/summary.txt", result, "Event Logs Bundle")
    return result


def collect_network_bundle(
    session_id: str,
    *,
    mask_options: MaskingOptions | None = None,
    output_root: str | Path | None = None,
    progress_cb: Callable[[int, str], None] | None = None,
    cancel_event: Event | None = None,
    timeout_s: int = 240,
    log_cb: Callable[[str], None] | None = None,
) -> EvidenceResult:
    result = _run_task_batch(
        session_id=session_id,
        task_ids=[
            "task_ipconfig_all",
            "task_route_print",
            "task_proxy_show",
            "task_hosts_check",
            "task_wlan_report",
            "task_dns_timing",
        ],
        mask_options=mask_options,
        output_root=output_root,
        progress_cb=progress_cb,
        cancel_event=cancel_event,
        timeout_s=timeout_s,
        log_cb=log_cb,
        batch_title="Network bundle",
    )
    _write_bundle_summary(result.root_dir, "network/summary.txt", result, "Network Bundle")
    return result


def collect_update_bundle(
    session_id: str,
    *,
    mask_options: MaskingOptions | None = None,
    output_root: str | Path | None = None,
    progress_cb: Callable[[int, str], None] | None = None,
    cancel_event: Event | None = None,
    timeout_s: int = 260,
    log_cb: Callable[[str], None] | None = None,
) -> EvidenceResult:
    result = _run_task_batch(
        session_id=session_id,
        task_ids=[
            "task_update_services_status",
            "task_pending_reboot_sources",
            "task_get_windows_update_log",
            "task_evtx_windows_update",
        ],
        mask_options=mask_options,
        output_root=output_root,
        progress_cb=progress_cb,
        cancel_event=cancel_event,
        timeout_s=timeout_s,
        log_cb=log_cb,
        batch_title="Updates bundle",
    )
    _write_bundle_summary(result.root_dir, "updates/summary.txt", result, "Updates Bundle")
    return result


def collect_printer_bundle(
    session_id: str,
    *,
    mask_options: MaskingOptions | None = None,
    output_root: str | Path | None = None,
    progress_cb: Callable[[int, str], None] | None = None,
    cancel_event: Event | None = None,
    timeout_s: int = 220,
    log_cb: Callable[[str], None] | None = None,
) -> EvidenceResult:
    result = _run_task_batch(
        session_id=session_id,
        task_ids=[
            "task_printer_status",
            "task_print_queue",
            "task_evtx_printservice",
        ],
        mask_options=mask_options,
        output_root=output_root,
        progress_cb=progress_cb,
        cancel_event=cancel_event,
        timeout_s=timeout_s,
        log_cb=log_cb,
        batch_title="Printer bundle",
    )
    _write_bundle_summary(result.root_dir, "printer/summary.txt", result, "Printer Bundle")
    return result


def collect_crash_bundle(
    session_id: str,
    *,
    mask_options: MaskingOptions | None = None,
    output_root: str | Path | None = None,
    progress_cb: Callable[[int, str], None] | None = None,
    cancel_event: Event | None = None,
    timeout_s: int = 220,
    log_cb: Callable[[str], None] | None = None,
) -> EvidenceResult:
    result = _run_task_batch(
        session_id=session_id,
        task_ids=[
            "task_reliability_snapshot",
            "task_minidumps_collect",
            "task_evtx_application",
            "task_evtx_system",
        ],
        mask_options=mask_options,
        output_root=output_root,
        progress_cb=progress_cb,
        cancel_event=cancel_event,
        timeout_s=timeout_s,
        log_cb=log_cb,
        batch_title="Crash bundle",
    )
    _write_bundle_summary(result.root_dir, "crash/summary.txt", result, "Crash Bundle")
    return result


def collect_evidence(
    session_id: str,
    task_ids: list[str],
    *,
    mask_options: MaskingOptions | None = None,
    output_root: str | Path | None = None,
    progress_cb: Callable[[int, str], None] | None = None,
    cancel_event: Event | None = None,
    timeout_s: int = 900,
    log_cb: Callable[[str], None] | None = None,
) -> EvidenceResult:
    result = _run_task_batch(
        session_id=session_id,
        task_ids=task_ids,
        mask_options=mask_options,
        output_root=output_root,
        progress_cb=progress_cb,
        cancel_event=cancel_event,
        timeout_s=timeout_s,
        log_cb=log_cb,
        batch_title="Evidence collection",
    )
    summary_path = Path(result.root_dir) / "evidence" / "summary.txt"
    summary_path.parent.mkdir(parents=True, exist_ok=True)
    summary_path.write_text(result.summary_text + "\n", encoding="utf-8")
    return result
