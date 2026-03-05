from __future__ import annotations

import csv
import json
import os
import shutil
import time
from dataclasses import dataclass, replace
from pathlib import Path
from threading import Event
from typing import Any, Callable

from .command_runner import run_command
from .brand import APP_DISPLAY_NAME
from .diagnostics import downloads_cleanup_assistant, large_file_radar
from .errors import classify_exit, ensure_next_steps
from .masking import MaskingOptions, mask_text
from .paths import ensure_dirs
from .run_events import RunEventBus, RunEventType
from .utils import is_admin


TaskRunner = Callable[["ScriptTask", "ScriptTaskContext", bool], dict[str, Any]]


@dataclass(frozen=True)
class ScriptTask:
    id: str
    title: str
    desc: str
    command: tuple[str, ...] | None
    timeout_s: int
    risk: str
    category: str
    admin_required: bool = False
    reboot_likely: bool = False
    output_name: str = ""
    output_ext: str = "txt"
    runner: TaskRunner | None = None


@dataclass(frozen=True)
class ScriptTaskContext:
    output_dir: Path | None = None
    mask_options: MaskingOptions | None = None
    cancel_event: Event | None = None
    log_cb: Callable[[str], None] | None = None
    run_event_bus: RunEventBus | None = None
    run_id: str = ""


def _sanitize(name: str) -> str:
    out = "".join(ch if ch.isalnum() or ch in {"-", "_", "."} else "_" for ch in name.strip().lower())
    return out[:96] if out else "task_output"


def _effective_mask(text: str, mask_options: MaskingOptions | None) -> str:
    if mask_options is None:
        return text
    return mask_text(text, mask_options)


def _publish_event(
    context: ScriptTaskContext,
    event_type: str,
    message: str,
    *,
    data: dict[str, Any] | None = None,
) -> None:
    if context.run_event_bus is None or not context.run_id:
        return
    context.run_event_bus.publish(context.run_id, event_type, message=message, data=data)


def _log(context: ScriptTaskContext, line: str, event_type: str = RunEventType.STDOUT) -> None:
    if not line:
        return
    if context.log_cb is not None:
        try:
            context.log_cb(line)
        except Exception:
            ...
    _publish_event(context, event_type, line)


def _stdout_cb(context: ScriptTaskContext) -> Callable[[str], None]:
    return lambda line: _log(context, line, RunEventType.STDOUT)


def _stderr_cb(context: ScriptTaskContext) -> Callable[[str], None]:
    return lambda line: _log(context, f"[stderr] {line}", RunEventType.STDERR)


def _ensure_output_dir(output_dir: Path | None) -> Path:
    if output_dir is not None:
        output_dir.mkdir(parents=True, exist_ok=True)
        return output_dir
    path = ensure_dirs()["state"] / "script_task_output"
    path.mkdir(parents=True, exist_ok=True)
    return path


def _build_output_path(task: ScriptTask, output_dir: Path) -> Path:
    stem = task.output_name.strip() or _sanitize(task.id)
    ext = task.output_ext.strip().lstrip(".") or "txt"
    return output_dir / f"{stem}.{ext}"


def _write_csv(path: Path, rows: list[dict[str, Any]], fields: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fields)
        writer.writeheader()
        for row in rows:
            writer.writerow({field: row.get(field, "") for field in fields})


def _sectioned_summary(
    *,
    title: str,
    checked: list[str],
    found: list[str],
    changed: list[str],
    next_steps: list[str],
    technical_lines: list[str],
) -> str:
    lines = [
        f"{title}",
        "",
        "What we checked:",
    ]
    lines.extend([f"- {row}" for row in checked] or ["- No checks recorded."])
    lines.append("")
    lines.append("What we found:")
    lines.extend([f"- {row}" for row in found] or ["- No significant findings."])
    lines.append("")
    lines.append("What we changed:")
    lines.extend([f"- {row}" for row in changed] or ["- No system changes were made."])
    lines.append("")
    lines.append("Next steps:")
    lines.extend([f"- {row}" for row in next_steps] or ["- Export a support pack if you need to escalate."])
    lines.append("")
    lines.append("Technical appendix:")
    lines.extend([f"- {row}" for row in technical_lines] or ["- No technical appendix details."])
    return "\n".join(lines).strip() + "\n"


def _write_summary_file(path: Path, text: str, mask_options: MaskingOptions | None) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(_effective_mask(text, mask_options), encoding="utf-8")
    return path


def _default_next_steps_for_category(category: str) -> list[str]:
    key = (category or "").strip().lower()
    if key in {"network", "browser", "cloud"}:
        return [
            "Retry after reconnecting to your network.",
            "Run the matching network runbook in dry-run mode.",
            "Export a support pack for escalation if instability continues.",
        ]
    if key in {"audio", "privacy", "devices", "printer"}:
        return [
            "Use the suggested Windows settings page links.",
            "Re-test the device after applying one safe step.",
            "Export a support pack if symptoms persist.",
        ]
    if key in {"services", "updates", "integrity", "wmi", "security"}:
        return [
            "Review the service or health snapshot output.",
            "Run the relevant IT runbook with admin approval if needed.",
            "Export a Ticket Pack for escalation.",
        ]
    return [
        "Review this summary and the captured artifacts.",
        "Try the related safe tool or runbook.",
        "Export a support pack if you need help from support.",
    ]


def _finalize_task_result(task: ScriptTask, result: dict[str, Any], context: ScriptTaskContext) -> dict[str, Any]:
    payload = dict(result or {})
    code = int(payload.get("code", 0 if payload.get("dry_run") else 1))
    stdout = str(payload.get("stdout", "")).strip()
    stderr = str(payload.get("stderr", "")).strip()
    summary = str(payload.get("summary_text", "")).strip()
    if not summary:
        found = []
        if code == 0:
            found.append("The command completed without an error code.")
        else:
            found.append(f"The command returned code {code}.")
        if stderr:
            found.append(stderr[:260])
        summary = _sectioned_summary(
            title=f"{task.title} Summary",
            checked=[task.desc],
            found=found,
            changed=["No direct system change detected." if not task.admin_required else "May include administrative changes."],
            next_steps=_default_next_steps_for_category(task.category),
            technical_lines=[f"task_id={task.id}", f"category={task.category}", f"risk={task.risk}"],
        )
    payload["summary_text"] = _effective_mask(summary, context.mask_options)
    if "next_steps_list" not in payload:
        payload["next_steps_list"] = _default_next_steps_for_category(task.category)
    if "suggested_next_steps" not in payload:
        payload["suggested_next_steps"] = list(payload.get("next_steps_list", []))
    if "technical_message" not in payload and stderr:
        payload["technical_message"] = _effective_mask(stderr[:1200], context.mask_options)
    if code != 0:
        mapped = classify_exit(code, stderr or stdout)
        if mapped is not None:
            payload.setdefault("user_message", mapped.user_message)
            payload.setdefault("technical_message", mapped.technical_message)
            payload["suggested_next_steps"] = ensure_next_steps(
                payload.get("suggested_next_steps") or mapped.suggested_next_steps
            )
            payload["next_steps_list"] = list(payload["suggested_next_steps"])
    if "user_message" not in payload and code != 0:
        payload["user_message"] = "The task did not complete successfully."
    if "output_files" not in payload:
        payload["output_files"] = []
    if "outcome" not in payload:
        if code == 0 and bool(payload.get("dry_run")):
            payload["outcome"] = "no_issue_found"
        elif code == 0:
            payload["outcome"] = "fixed"
        elif "administrator privileges are required" in str(payload.get("stderr", "")).strip().lower():
            payload["outcome"] = "needs_user_action"
        else:
            payload["outcome"] = "cannot_fix"
    payload.setdefault("before_snapshot", "not_applicable")
    payload.setdefault("after_snapshot", "not_applicable")
    payload.setdefault("rollback_notice", "not reversible")
    payload.setdefault("evidence_items", [])
    return payload


def _resolve_command(task: ScriptTask, output_path: Path | None) -> list[str]:
    if task.command is None:
        return []
    out = str(output_path) if output_path is not None else ""
    return [part.replace("{out}", out) for part in task.command]


def _write_text_capture(
    task: ScriptTask,
    output_dir: Path,
    command: list[str],
    code: int,
    stdout: str,
    stderr: str,
    mask_options: MaskingOptions | None,
) -> Path:
    stem = task.output_name.strip() or _sanitize(task.id)
    out_file = output_dir / f"{stem}_capture.json"
    payload = {
        "task_id": task.id,
        "title": task.title,
        "command": [_effective_mask(part, mask_options) for part in command],
        "code": code,
        "stdout": _effective_mask(stdout, mask_options),
        "stderr": _effective_mask(stderr, mask_options),
    }
    out_file.write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")
    return out_file


def _run_command_task(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    output_dir = _ensure_output_dir(context.output_dir)
    output_path = _build_output_path(task, output_dir)
    command_uses_out = "{out}" in " ".join(task.command or ())
    command = _resolve_command(task, output_path if command_uses_out else None)
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": command,
            "timeout_s": task.timeout_s,
            "category": task.category,
            "admin_required": task.admin_required,
            "reboot_likely": task.reboot_likely,
        }
    if task.admin_required and not is_admin():
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": False,
            "code": 1,
            "stdout": "",
            "stderr": "Administrator privileges are required.",
            "timeout_s": task.timeout_s,
            "category": task.category,
            "admin_required": task.admin_required,
            "reboot_likely": task.reboot_likely,
            "output_files": [],
        }
    _log(context, f"[task] Running command: {' '.join(command)}")
    result = run_command(
        command,
        timeout_s=task.timeout_s,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    files: list[str] = []
    if command_uses_out and output_path.exists():
        if output_path.suffix.lower() in {".txt", ".json", ".md", ".log", ".html", ".csv"}:
            try:
                text = output_path.read_text(encoding="utf-8", errors="ignore")
                output_path.write_text(_effective_mask(text, context.mask_options), encoding="utf-8")
            except OSError:
                ...
        files.append(str(output_path))
    out_file = _write_text_capture(
        task,
        output_dir,
        command,
        result.code,
        result.stdout,
        result.stderr,
        context.mask_options,
    )
    files.append(str(out_file))
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": result.code,
        "stdout": _effective_mask(result.stdout, context.mask_options),
        "stderr": _effective_mask(result.stderr, context.mask_options),
        "duration_s": result.duration_s,
        "timed_out": result.timed_out,
        "category": task.category,
        "admin_required": task.admin_required,
        "reboot_likely": task.reboot_likely,
        "output_files": files,
    }


def _run_startup_items(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["powershell", "-NoProfile", "-Command", "Get startup entries from registry and startup folders"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    out_file = _build_output_path(task, output_dir)
    ps = (
        "$items = @(); "
        "$keys = @('HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run','HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run'); "
        "foreach ($k in $keys) { if (Test-Path $k) { "
        "$obj = Get-ItemProperty -Path $k; "
        "$obj.PSObject.Properties | Where-Object { $_.Name -notmatch '^PS' } | ForEach-Object { "
        "$items += [PSCustomObject]@{Source=$k;Name=$_.Name;Value=[string]$_.Value} } } }; "
        "$folders = @([Environment]::GetFolderPath('Startup'), [Environment]::GetFolderPath('CommonStartup')); "
        "foreach ($f in $folders) { if (Test-Path $f) { Get-ChildItem -Path $f -Force | ForEach-Object { "
        "$items += [PSCustomObject]@{Source=$f;Name=$_.Name;Value=[string]$_.FullName} } } }; "
        "$items | ConvertTo-Json -Depth 4"
    )
    result = run_command(
        ["powershell", "-NoProfile", "-Command", ps],
        timeout_s=task.timeout_s,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    masked = _effective_mask(result.combined, context.mask_options)
    out_file.write_text(masked, encoding="utf-8", errors="ignore")
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": result.code,
        "stdout": masked,
        "stderr": "",
        "duration_s": result.duration_s,
        "timed_out": result.timed_out,
        "category": task.category,
        "output_files": [str(out_file)],
    }


def _run_wlan_report(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["netsh", "wlan", "show", "wlanreport"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    result = run_command(
        ["netsh", "wlan", "show", "wlanreport"],
        timeout_s=task.timeout_s,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    report_src = Path(r"C:\ProgramData\Microsoft\Windows\WlanReport\wlan-report-latest.html")
    files: list[str] = []
    if report_src.exists():
        target = output_dir / "wlan-report-latest.html"
        try:
            shutil.copy2(report_src, target)
            files.append(str(target))
        except OSError:
            ...
    sidecar = _write_text_capture(task, output_dir, ["netsh", "wlan", "show", "wlanreport"], result.code, result.stdout, result.stderr, context.mask_options)
    files.append(str(sidecar))
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": result.code,
        "stdout": _effective_mask(result.stdout, context.mask_options),
        "stderr": _effective_mask(result.stderr, context.mask_options),
        "duration_s": result.duration_s,
        "timed_out": result.timed_out,
        "category": task.category,
        "output_files": files,
    }


def _run_minidump_copy(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["copy newest minidumps to evidence folder"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    dump_dir = Path(r"C:\Windows\Minidump")
    copied: list[str] = []
    lines: list[str] = []
    if dump_dir.exists():
        files = sorted((f for f in dump_dir.glob("*.dmp") if f.is_file()), key=lambda p: p.stat().st_mtime, reverse=True)[:8]
        for file in files:
            target = output_dir / file.name
            try:
                shutil.copy2(file, target)
                copied.append(str(target))
                lines.append(f"copied: {file}")
            except OSError as exc:
                lines.append(f"copy failed: {file} ({exc})")
    else:
        lines.append("Minidump folder not found.")
    summary = output_dir / "minidumps_summary.txt"
    summary.write_text(_effective_mask("\n".join(lines), context.mask_options), encoding="utf-8")
    copied.append(str(summary))
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": 0,
        "stdout": _effective_mask("\n".join(lines), context.mask_options),
        "stderr": "",
        "duration_s": 0.0,
        "timed_out": False,
        "category": task.category,
        "output_files": copied,
    }


def _run_reset_update_components(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["powershell", "-NoProfile", "-Command", "Reset Windows Update service components"],
            "timeout_s": task.timeout_s,
            "category": task.category,
            "admin_required": True,
            "reboot_likely": True,
        }
    if not is_admin():
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": False,
            "code": 1,
            "stdout": "",
            "stderr": "Administrator privileges are required.",
            "duration_s": 0.0,
            "timed_out": False,
            "category": task.category,
            "admin_required": True,
            "reboot_likely": True,
            "output_files": [],
        }
    output_dir = _ensure_output_dir(context.output_dir)
    ps = (
        "$ts=Get-Date -Format yyyyMMdd_HHmmss; "
        "$services=@('wuauserv','bits','cryptsvc'); "
        "foreach($s in $services){Stop-Service -Name $s -Force -ErrorAction SilentlyContinue}; "
        "$sd='C:\\Windows\\SoftwareDistribution'; if(Test-Path $sd){Rename-Item $sd ($sd + '.bak_' + $ts) -ErrorAction SilentlyContinue}; "
        "$cr='C:\\Windows\\System32\\catroot2'; if(Test-Path $cr){Rename-Item $cr ($cr + '.bak_' + $ts) -ErrorAction SilentlyContinue}; "
        "foreach($s in $services){Start-Service -Name $s -ErrorAction SilentlyContinue}; "
        "Get-Service -Name wuauserv,bits,cryptsvc | Select-Object Name,Status,StartType | ConvertTo-Json -Depth 3"
    )
    result = run_command(
        ["powershell", "-NoProfile", "-Command", ps],
        timeout_s=task.timeout_s,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    out_file = _write_text_capture(task, output_dir, ["powershell", "-NoProfile", "-Command", "<update reset>"], result.code, result.stdout, result.stderr, context.mask_options)
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": result.code,
        "stdout": _effective_mask(result.stdout, context.mask_options),
        "stderr": _effective_mask(result.stderr, context.mask_options),
        "duration_s": result.duration_s,
        "timed_out": result.timed_out,
        "category": task.category,
        "admin_required": True,
        "reboot_likely": True,
        "output_files": [str(out_file)],
    }


def _run_clear_spool_folder(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["powershell", "-NoProfile", "-Command", "Move spool files to Recycle Bin (preview-only in dry-run)."],
            "timeout_s": task.timeout_s,
            "category": task.category,
            "admin_required": True,
            "reboot_likely": False,
        }
    if not is_admin():
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": False,
            "code": 1,
            "stdout": "",
            "stderr": "Administrator privileges are required.",
            "duration_s": 0.0,
            "timed_out": False,
            "category": task.category,
            "admin_required": True,
            "reboot_likely": False,
            "output_files": [],
        }
    output_dir = _ensure_output_dir(context.output_dir)
    ps = (
        "Add-Type -AssemblyName Microsoft.VisualBasic; "
        "$root='C:\\Windows\\System32\\spool\\PRINTERS'; "
        "$rows=@(); "
        "if(Test-Path $root){ "
        "Get-ChildItem -Path $root -File -ErrorAction SilentlyContinue | ForEach-Object { "
        "try { "
        "[Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile($_.FullName,[Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs,[Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin); "
        "$rows += [PSCustomObject]@{file=$_.FullName; recycled=$true} "
        "} catch { "
        "$rows += [PSCustomObject]@{file=$_.FullName; recycled=$false; error=$_.Exception.Message} "
        "} "
        "} "
        "} else { "
        "$rows += [PSCustomObject]@{note='Spool folder not found'} "
        "}; "
        "$rows | ConvertTo-Json -Depth 4"
    )
    result = run_command(
        ["powershell", "-NoProfile", "-Command", ps],
        timeout_s=task.timeout_s,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    out_file = _write_text_capture(task, output_dir, ["powershell", "-NoProfile", "-Command", "<spool recycle>"], result.code, result.stdout, result.stderr, context.mask_options)
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": result.code,
        "stdout": _effective_mask(result.stdout, context.mask_options),
        "stderr": _effective_mask(result.stderr, context.mask_options),
        "duration_s": result.duration_s,
        "timed_out": result.timed_out,
        "category": task.category,
        "admin_required": True,
        "reboot_likely": False,
        "output_files": [str(out_file)],
    }


def _run_ip_release_renew(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["ipconfig", "/release", "&&", "ipconfig", "/renew"],
            "timeout_s": task.timeout_s,
            "category": task.category,
            "reboot_likely": task.reboot_likely,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    first = run_command(
        ["ipconfig", "/release"],
        timeout_s=max(10, task.timeout_s // 2),
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    second = run_command(
        ["ipconfig", "/renew"],
        timeout_s=max(10, task.timeout_s // 2),
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    combined_stdout = f"[release]\n{first.stdout}\n\n[renew]\n{second.stdout}"
    combined_stderr = f"[release]\n{first.stderr}\n\n[renew]\n{second.stderr}"
    code = 0 if first.code == 0 and second.code == 0 else max(first.code, second.code)
    out_file = _write_text_capture(task, output_dir, ["ipconfig /release", "ipconfig /renew"], code, combined_stdout, combined_stderr, context.mask_options)
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": code,
        "stdout": _effective_mask(combined_stdout, context.mask_options),
        "stderr": _effective_mask(combined_stderr, context.mask_options),
        "duration_s": first.duration_s + second.duration_s,
        "timed_out": first.timed_out or second.timed_out,
        "category": task.category,
        "output_files": [str(out_file)],
    }


def _run_large_file_radar(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["diagnostics.large_file_radar(min_size_mb=256, limit=80)"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    if context.cancel_event is not None and context.cancel_event.is_set():
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": False,
            "code": 130,
            "stdout": "",
            "stderr": "Cancelled.",
            "duration_s": 0.0,
            "timed_out": False,
            "category": task.category,
            "output_files": [],
        }
    payload = large_file_radar(min_size_mb=256, limit=80)
    rows = payload.get("rows", []) if isinstance(payload, dict) else []
    csv_rows: list[dict[str, Any]] = []
    for row in rows:
        if not isinstance(row, dict):
            continue
        csv_rows.append(
            {
                "path": str(row.get("path", "")),
                "size_mb": row.get("size_mb", 0),
                "ext": str(row.get("ext", "")),
                "modified": str(row.get("modified", "")),
            }
        )
    csv_path = output_dir / "large_files.csv"
    _write_csv(csv_path, csv_rows, ["path", "size_mb", "ext", "modified"])
    summary = _sectioned_summary(
        title="Large File Radar Summary",
        checked=["Scanned for large files with bounded limits."],
        found=[f"candidate_rows={len(csv_rows)}", f"root={payload.get('root', '')}"],
        changed=["No files were changed or deleted."],
        next_steps=[
            "Review top candidates and confirm relevance.",
            "Use preview-first cleanup tools before deleting anything.",
            "Export a support pack if you need help validating cleanup choices.",
        ],
        technical_lines=[f"csv={csv_path}", f"limit={payload.get('limit', 0)}", f"min_size_mb={payload.get('min_size_mb', 0)}"],
    )
    summary_file = _build_output_path(task, output_dir)
    _write_summary_file(summary_file, summary, context.mask_options)
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": 0,
        "stdout": _effective_mask(summary[:2400], context.mask_options),
        "stderr": "",
        "duration_s": 0.0,
        "timed_out": False,
        "category": task.category,
        "output_files": [str(summary_file), str(csv_path)],
        "summary_text": summary,
    }


def _run_downloads_cleanup_buckets(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["diagnostics.downloads_cleanup_assistant(days_old=30)"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    if context.cancel_event is not None and context.cancel_event.is_set():
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": False,
            "code": 130,
            "stdout": "",
            "stderr": "Cancelled.",
            "duration_s": 0.0,
            "timed_out": False,
            "category": task.category,
            "output_files": [],
        }
    payload = downloads_cleanup_assistant(days_old=30)
    candidates = payload.get("candidates", []) if isinstance(payload, dict) else []
    csv_rows: list[dict[str, Any]] = []
    for row in candidates:
        if not isinstance(row, dict):
            continue
        csv_rows.append(
            {
                "path": str(row.get("path", "")),
                "size_mb": row.get("size_mb", 0),
                "age_days": row.get("age_days", 0),
            }
        )
    plan_csv = output_dir / "downloads_plan.csv"
    _write_csv(plan_csv, csv_rows, ["path", "size_mb", "age_days"])
    summary = _sectioned_summary(
        title="Downloads Cleanup Buckets Summary",
        checked=["Scanned Downloads for old files and grouped cleanup candidates."],
        found=[f"candidates={len(csv_rows)}", f"days_old_threshold={payload.get('days_old', 30)}"],
        changed=["No files were deleted. Preview-only plan generated."],
        next_steps=[
            "Review the CSV plan and keep anything uncertain.",
            "If deleting, move to Recycle Bin only.",
            "Export a support pack if you need validation from IT.",
        ],
        technical_lines=[f"plan_csv={plan_csv}", f"folder={payload.get('folder', '')}"],
    )
    summary_file = _build_output_path(task, output_dir)
    _write_summary_file(summary_file, summary, context.mask_options)
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": 0,
        "stdout": _effective_mask(summary[:2400], context.mask_options),
        "stderr": "",
        "duration_s": 0.0,
        "timed_out": False,
        "category": task.category,
        "output_files": [str(summary_file), str(plan_csv)],
        "summary_text": summary,
    }


def _run_performance_sample(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["powershell", "-NoProfile", "-Command", "sample process CPU/memory for 15 seconds + collect startup inventory (read-only)"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    sample_window_s = 15
    tick_s = 3
    ticks = max(4, min(7, sample_window_s // tick_s))
    ps = (
        "$samples=@(); "
        f"1..{ticks} | ForEach-Object {{ "
        "$rows=Get-Process | Sort-Object CPU -Descending | Select-Object -First 8 Name,Id,CPU,PM; "
        "$samples += [PSCustomObject]@{tick=$_; top=$rows}; "
        f"Start-Sleep -Seconds {tick_s} }}; "
        "$samples | ConvertTo-Json -Depth 6"
    )
    result = run_command(
        ["powershell", "-NoProfile", "-Command", ps],
        timeout_s=task.timeout_s,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    sample_csv = output_dir / "perf_sample.csv"
    rows: list[dict[str, Any]] = []
    try:
        parsed = json.loads(result.stdout) if result.stdout.strip() else []
    except Exception:
        parsed = []
    if isinstance(parsed, list):
        for tick_row in parsed:
            if not isinstance(tick_row, dict):
                continue
            tick = tick_row.get("tick", "")
            top_rows = tick_row.get("top", [])
            if not isinstance(top_rows, list):
                continue
            for proc in top_rows:
                if not isinstance(proc, dict):
                    continue
                rows.append(
                    {
                        "tick": tick,
                        "name": str(proc.get("Name", "")),
                        "pid": proc.get("Id", ""),
                        "cpu": proc.get("CPU", ""),
                        "memory_mb": round(float(proc.get("PM", 0) or 0) / (1024 * 1024), 2),
                    }
                )
    _write_csv(sample_csv, rows, ["tick", "name", "pid", "cpu", "memory_mb"])
    startup = _run_startup_items(
        ScriptTask("task_startup_items_tmp", "Startup Inventory", "", None, 45, "Safe", "system", output_name="startup_inventory"),
        context,
        False,
    )
    top_names: list[str] = []
    proc_hits: dict[str, int] = {}
    for row in rows:
        name = str(row.get("name", "")).strip()
        if not name:
            continue
        proc_hits[name] = proc_hits.get(name, 0) + 1
    for key, _count in sorted(proc_hits.items(), key=lambda kv: kv[1], reverse=True)[:5]:
        top_names.append(key)
    summary = _sectioned_summary(
        title="Performance Sample Summary",
        checked=[
            "Captured sustained process snapshots over a short window (10-20s target).",
            "Collected startup/autostart inventory in read-only mode.",
        ],
        found=[
            f"samples={len(rows)}",
            f"exit_code={result.code}",
            f"recurring_top_processes={', '.join(top_names) if top_names else 'none'}",
            f"startup_inventory_code={startup.get('code', 1)}",
        ],
        changed=["Read-only performance sampling and startup inventory only."],
        next_steps=[
            "Focus on repeated top processes across ticks.",
            "Review startup inventory for persistent offenders.",
            "Reboot and compare with a second performance sample.",
        ],
        technical_lines=[f"perf_csv={sample_csv}", f"window_s={sample_window_s}", f"tick_s={tick_s}"],
    )
    summary_file = _build_output_path(task, output_dir)
    _write_summary_file(summary_file, summary, context.mask_options)
    raw_capture = _write_text_capture(task, output_dir, ["powershell", "-NoProfile", "-Command", "<perf sample>"], result.code, result.stdout, result.stderr, context.mask_options)
    files = [str(summary_file), str(sample_csv), str(raw_capture)]
    files.extend([str(p) for p in startup.get("output_files", [])])
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": result.code,
        "stdout": _effective_mask(summary[:2400], context.mask_options),
        "stderr": _effective_mask(result.stderr, context.mask_options),
        "duration_s": result.duration_s,
        "timed_out": result.timed_out,
        "category": task.category,
        "output_files": files,
        "summary_text": summary,
    }


def _run_storage_ranked_view(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    from .diagnostics import storage_ranked_view

    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["diagnostics.storage_ranked_view()"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    if context.cancel_event is not None and context.cancel_event.is_set():
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": False,
            "code": 130,
            "stdout": "",
            "stderr": "Cancelled.",
            "duration_s": 0.0,
            "timed_out": False,
            "category": task.category,
            "output_files": [],
        }
    payload = storage_ranked_view()
    json_path = _build_output_path(task, output_dir)
    json_path.write_text(_effective_mask(json.dumps(payload, indent=2, ensure_ascii=False), context.mask_options), encoding="utf-8")
    rows = payload.get("rows", []) if isinstance(payload, dict) else []
    csv_rows: list[dict[str, Any]] = []
    for row in rows:
        if isinstance(row, dict):
            csv_rows.append({"folder": row.get("folder", ""), "size_gb": row.get("size_gb", 0)})
    csv_path = output_dir / "storage_ranked.csv"
    _write_csv(csv_path, csv_rows, ["folder", "size_gb"])
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": 0,
        "stdout": f"Storage ranked view generated with {len(csv_rows)} rows.",
        "stderr": "",
        "duration_s": 0.0,
        "timed_out": False,
        "category": task.category,
        "output_files": [str(json_path), str(csv_path)],
    }


def _run_duplicate_hash_scan(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    from .diagnostics import duplicate_finder_exact_hash

    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["diagnostics.duplicate_finder_exact_hash(min_size_mb=20,max_files=400)"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    if context.cancel_event is not None and context.cancel_event.is_set():
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": False,
            "code": 130,
            "stdout": "",
            "stderr": "Cancelled.",
            "duration_s": 0.0,
            "timed_out": False,
            "category": task.category,
            "output_files": [],
        }
    override_root = os.environ.get("FIXFOX_TEST_DUP_ROOT", "").strip()
    min_size_mb = 0 if override_root else 20
    payload = duplicate_finder_exact_hash(root=override_root or None, min_size_mb=min_size_mb, max_files=400)
    json_path = _build_output_path(task, output_dir)
    json_path.write_text(_effective_mask(json.dumps(payload, indent=2, ensure_ascii=False), context.mask_options), encoding="utf-8")
    rows: list[dict[str, Any]] = []
    for group in payload.get("groups", []):
        if not isinstance(group, dict):
            continue
        sha = str(group.get("sha256", ""))
        for file_path in group.get("files", []):
            rows.append({"sha256": sha, "path": str(file_path), "group_count": int(group.get("count", 0))})
    csv_path = output_dir / "duplicates_report.csv"
    _write_csv(csv_path, rows, ["sha256", "path", "group_count"])
    summary = _sectioned_summary(
        title="Duplicate Finder Summary",
        checked=["Scanned candidate files and grouped exact hash matches."],
        found=[f"duplicate_groups={len(payload.get('groups', []))}", f"rows={len(rows)}", f"root={payload.get('root', '')}"],
        changed=["No files were deleted. Preview-only duplicate report generated."],
        next_steps=[
            "Validate duplicate groups before deleting anything.",
            "If deleting, use Recycle Bin only.",
            "Export the report for peer review in IT workflows.",
        ],
        technical_lines=[f"csv={csv_path}", f"json={json_path}"],
    )
    summary_file = output_dir / "duplicates_summary.txt"
    _write_summary_file(summary_file, summary, context.mask_options)
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": 0,
        "stdout": _effective_mask(summary[:2400], context.mask_options),
        "stderr": "",
        "duration_s": 0.0,
        "timed_out": False,
        "category": task.category,
        "output_files": [str(summary_file), str(json_path), str(csv_path)],
        "summary_text": summary,
    }


def _run_hosts_check(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["read hosts file and report non-comment entries"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    hosts = Path(r"C:\Windows\System32\drivers\etc\hosts")
    lines: list[str] = []
    code = 0
    if hosts.exists():
        try:
            raw = hosts.read_text(encoding="utf-8", errors="ignore").splitlines()
            active = [line for line in raw if line.strip() and not line.strip().startswith("#")]
            lines.append(f"active_entries={len(active)}")
            lines.extend(active[:120])
        except Exception as exc:
            code = 1
            lines.append(f"read_failed={exc}")
    else:
        code = 1
        lines.append("hosts file not found")
    out_file = _build_output_path(task, output_dir)
    out_file.write_text(_effective_mask("\n".join(lines), context.mask_options), encoding="utf-8")
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": code,
        "stdout": _effective_mask("\n".join(lines), context.mask_options),
        "stderr": "",
        "duration_s": 0.0,
        "timed_out": False,
        "category": task.category,
        "output_files": [str(out_file)],
    }


def _run_dns_timing(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["socket.getaddrinfo timing for microsoft.com/openai.com/cloudflare.com"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    domains = ["microsoft.com", "openai.com", "cloudflare.com"]
    rows: list[dict[str, Any]] = []
    for domain in domains:
        if context.cancel_event is not None and context.cancel_event.is_set():
            return {
                "task_id": task.id,
                "title": task.title,
                "dry_run": False,
                "code": 130,
                "stdout": "Cancelled.",
                "stderr": "Cancelled.",
                "duration_s": 0.0,
                "timed_out": False,
                "category": task.category,
                "output_files": [],
            }
        start = time.perf_counter()
        ok = True
        error = ""
        try:
            result = run_command(
                ["nslookup", domain],
                timeout_s=max(4, min(10, task.timeout_s // max(len(domains), 1))),
                cancel_event=context.cancel_event,
                on_stdout_line=_stdout_cb(context),
                on_stderr_line=_stderr_cb(context),
            )
            ok = result.code == 0
            if not ok:
                error = result.stderr or result.stdout
        except Exception as exc:
            ok = False
            error = str(exc)
        elapsed = round((time.perf_counter() - start) * 1000.0, 2)
        rows.append({"domain": domain, "ok": ok, "ms": elapsed, "error": error})
        _log(context, f"[dns] {domain}: {'ok' if ok else 'fail'} {elapsed} ms")
    output_dir = _ensure_output_dir(context.output_dir)
    out_file = _build_output_path(task, output_dir)
    _write_csv(out_file, rows, ["domain", "ok", "ms", "error"])
    json_sidecar = output_dir / "dns_timing.json"
    json_sidecar.write_text(_effective_mask(json.dumps(rows, indent=2, ensure_ascii=False), context.mask_options), encoding="utf-8")
    failures = [row for row in rows if not row["ok"]]
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": 0 if not failures else 1,
        "stdout": f"DNS timing complete. {len(rows) - len(failures)}/{len(rows)} succeeded.",
        "stderr": "" if not failures else "One or more DNS lookups failed.",
        "duration_s": 0.0,
        "timed_out": False,
        "category": task.category,
        "output_files": [str(out_file), str(json_sidecar)],
    }


def _run_pending_reboot_sources(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["check reboot-pending registry sources"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    cmd = (
        "$checks = @(); "
        "$checks += [PSCustomObject]@{source='CBS'; pending=(Test-Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Component Based Servicing\\RebootPending')}; "
        "$checks += [PSCustomObject]@{source='WU_AutoUpdate'; pending=(Test-Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\WindowsUpdate\\Auto Update\\RebootRequired')}; "
        "$checks += [PSCustomObject]@{source='PendingFileRenameOperations'; pending=([bool](Get-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Session Manager' -ErrorAction SilentlyContinue).PendingFileRenameOperations)}; "
        "$checks | ConvertTo-Json -Depth 3"
    )
    result = run_command(
        ["powershell", "-NoProfile", "-Command", cmd],
        timeout_s=task.timeout_s,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    out_file = _build_output_path(task, output_dir)
    out_file.write_text(_effective_mask(result.combined, context.mask_options), encoding="utf-8", errors="ignore")
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": result.code,
        "stdout": _effective_mask(result.stdout, context.mask_options),
        "stderr": _effective_mask(result.stderr, context.mask_options),
        "duration_s": result.duration_s,
        "timed_out": result.timed_out,
        "category": task.category,
        "output_files": [str(out_file)],
    }


def _run_printer_status(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["collect spooler status + queue + PrintService log export (+ optional admin spooler restart)"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    status_cmd = (
        "$svc = Get-Service Spooler -ErrorAction SilentlyContinue | Select-Object Name,Status,StartType; "
        "$printers = Get-Printer -ErrorAction SilentlyContinue | Select-Object Name,DriverName,PortName,PrinterStatus; "
        "[PSCustomObject]@{spooler=$svc; printers=$printers} | ConvertTo-Json -Depth 6"
    )
    status = run_command(
        ["powershell", "-NoProfile", "-Command", status_cmd],
        timeout_s=task.timeout_s,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    queue = run_command(
        [
            "powershell",
            "-NoProfile",
            "-Command",
            "Get-PrintJob -ErrorAction SilentlyContinue | Select-Object PrinterName,ID,DocumentName,JobStatus,SubmittedTime | ConvertTo-Json -Depth 6",
        ],
        timeout_s=max(20, task.timeout_s // 2),
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    evtx_file = output_dir / "printservice_operational.evtx"
    evtx = run_command(
        ["wevtutil", "epl", "Microsoft-Windows-PrintService/Operational", str(evtx_file), "/ow:true"],
        timeout_s=max(25, task.timeout_s // 2),
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    restart_requested = os.environ.get("FIXFOX_PRINTER_OPTIONAL_RESTART", "").strip() == "1"
    restart_code = -1
    restart_note = "Optional admin restart not requested."
    if restart_requested:
        if is_admin():
            restart = run_command(
                ["powershell", "-NoProfile", "-Command", "Restart-Service Spooler -Force"],
                timeout_s=25,
                cancel_event=context.cancel_event,
                on_stdout_line=_stdout_cb(context),
                on_stderr_line=_stderr_cb(context),
            )
            restart_code = restart.code
            restart_note = "Optional admin spooler restart executed." if restart.code == 0 else "Optional admin spooler restart failed."
        else:
            restart_code = 1
            restart_note = "Optional admin spooler restart requested but admin privileges were unavailable."

    status_file = output_dir / "printer_status.json"
    queue_file = output_dir / "printer_queue.json"
    status_file.write_text(_effective_mask(status.stdout or status.combined, context.mask_options), encoding="utf-8", errors="ignore")
    queue_file.write_text(_effective_mask(queue.stdout or queue.combined, context.mask_options), encoding="utf-8", errors="ignore")
    summary = _sectioned_summary(
        title="Printer Rescue Summary",
        checked=[
            "Collected spooler service state and printer inventory.",
            "Collected print queue snapshot (best effort).",
            "Attempted PrintService operational event log export (.evtx).",
        ],
        found=[
            f"spooler_status_code={status.code}",
            f"queue_code={queue.code}",
            f"printservice_evtx_code={evtx.code}",
            restart_note,
        ],
        changed=[
            "No printer settings were changed by default.",
            "Optional admin spooler restart can be enabled when needed.",
        ],
        next_steps=[
            "If jobs are stuck, run Restart Print Spooler (admin).",
            "If issue persists, run Home: Printer Rescue runbook and export Ticket Pack.",
            "Share printer summary plus EVTX evidence with IT.",
        ],
        technical_lines=[
            f"status_json={status_file}",
            f"queue_json={queue_file}",
            f"evtx_path={evtx_file if evtx_file.exists() else 'not generated'}",
            f"restart_code={restart_code}",
        ],
    )
    summary_file = _build_output_path(task, output_dir)
    _write_summary_file(summary_file, summary, context.mask_options)
    files = [str(summary_file), str(status_file), str(queue_file)]
    if evtx_file.exists():
        files.append(str(evtx_file))
    code = 0 if status.code == 0 else status.code
    if restart_requested and restart_code not in (-1, 0):
        code = 1
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": code,
        "stdout": _effective_mask(summary[:3000], context.mask_options),
        "stderr": _effective_mask((status.stderr or queue.stderr or evtx.stderr)[:1200], context.mask_options),
        "duration_s": status.duration_s + queue.duration_s + evtx.duration_s,
        "timed_out": status.timed_out or queue.timed_out or evtx.timed_out,
        "category": task.category,
        "output_files": files,
        "summary_text": summary,
    }


def _run_browser_rescue(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["detect default browser, profile paths, proxy, dns and generate guided summary"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    paths_file = output_dir / "browser_profile_paths.txt"
    context_file = output_dir / "browser_context.json"
    summary_file = _build_output_path(task, output_dir)

    browser_cmd = (
        "$u='HKCU:\\Software\\Microsoft\\Windows\\Shell\\Associations\\UrlAssociations\\https\\UserChoice'; "
        "$prog=(Get-ItemProperty -Path $u -ErrorAction SilentlyContinue).ProgId; "
        "$prog"
    )
    browser_result = run_command(
        ["powershell", "-NoProfile", "-Command", browser_cmd],
        timeout_s=18,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    default_browser = (browser_result.stdout or "").strip() or "Unknown"
    version_cmd = (
        "$rows=@(); "
        "$pairs=@("
        "@{name='Chrome'; path='C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe'},"
        "@{name='Edge'; path='C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe'},"
        "@{name='Firefox'; path='C:\\Program Files\\Mozilla Firefox\\firefox.exe'}"
        "); "
        "foreach($p in $pairs){ "
        "if(Test-Path $p.path){ "
        "$v=(Get-Item $p.path).VersionInfo.ProductVersion; "
        "$rows += [PSCustomObject]@{name=$p.name; version=$v; path=$p.path} "
        "} "
        "}; "
        "$rows | ConvertTo-Json -Depth 4"
    )
    versions = run_command(
        ["powershell", "-NoProfile", "-Command", version_cmd],
        timeout_s=20,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    version_lines: list[str] = []
    try:
        parsed_versions = json.loads(versions.stdout) if versions.stdout.strip() else []
    except Exception:
        parsed_versions = []
    if isinstance(parsed_versions, dict):
        parsed_versions = [parsed_versions]
    if isinstance(parsed_versions, list):
        for row in parsed_versions:
            if not isinstance(row, dict):
                continue
            version_lines.append(f"{row.get('name', 'Browser')}: {row.get('version', 'unknown')}")
    if not version_lines:
        version_lines = ["Version info unavailable."]
    profile_paths = [
        str(Path.home() / "AppData" / "Local" / "Google" / "Chrome" / "User Data"),
        str(Path.home() / "AppData" / "Local" / "Microsoft" / "Edge" / "User Data"),
        str(Path.home() / "AppData" / "Roaming" / "Mozilla" / "Firefox" / "Profiles"),
    ]
    existing_profiles = [path for path in profile_paths if Path(path).exists()]
    paths_file.write_text(_effective_mask("\n".join(existing_profiles), context.mask_options), encoding="utf-8")

    proxy_result = run_command(
        ["netsh", "winhttp", "show", "proxy"],
        timeout_s=15,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    dns_result = run_command(
        ["ipconfig", "/all"],
        timeout_s=22,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    dns_lines = [line.strip() for line in dns_result.stdout.splitlines() if "DNS Servers" in line or line.strip().startswith(":")]
    extension_rows: list[str] = []
    ext_dirs = [
        Path.home() / "AppData" / "Local" / "Google" / "Chrome" / "User Data" / "Default" / "Extensions",
        Path.home() / "AppData" / "Local" / "Microsoft" / "Edge" / "User Data" / "Default" / "Extensions",
    ]
    for ext in ext_dirs:
        if ext.exists():
            count = len([x for x in ext.iterdir() if x.is_dir()])
            extension_rows.append(f"{ext}: {count} extension folders (best effort)")
    context_payload = {
        "default_browser": default_browser,
        "versions": version_lines,
        "profiles_found": existing_profiles,
        "proxy_code": proxy_result.code,
        "dns_code": dns_result.code,
        "dns_lines": dns_lines[:20],
    }
    context_file.write_text(_effective_mask(json.dumps(context_payload, indent=2), context.mask_options), encoding="utf-8")

    summary = _sectioned_summary(
        title="Browser Rescue Summary",
        checked=[
            "Detected default browser association.",
            "Collected browser profile paths only (no content).",
            "Collected WinHTTP proxy output and DNS adapter lines.",
            "Collected browser version information (best effort).",
            "Enumerated extension folder counts (best effort).",
        ],
        found=[
            f"Default browser ProgId: {default_browser}",
            f"Profile paths found: {len(existing_profiles)}",
            f"Proxy summary: {(proxy_result.stdout or proxy_result.stderr).strip()[:180]}",
            f"DNS lines captured: {len(dns_lines)}",
            *version_lines[:3],
            *(extension_rows[:3] or ["Extension export not available."]),
        ],
        changed=[
            "No browser settings were changed automatically.",
            "Use guided links to clear cache, disable extensions, or reset settings manually.",
        ],
        next_steps=[
            "Chrome settings: chrome://settings/reset",
            "Edge settings: edge://settings/reset",
            "Open Network Status if pages still fail after browser checks.",
        ],
        technical_lines=[
            f"default_browser={default_browser}",
            f"profile_paths_file={paths_file}",
            f"context_json={context_file}",
            f"proxy_code={proxy_result.code}",
            f"dns_code={dns_result.code}",
            f"versions_code={versions.code}",
        ],
    )
    _write_summary_file(summary_file, summary, context.mask_options)
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": 0 if browser_result.code == 0 else browser_result.code,
        "stdout": _effective_mask(summary[:3000], context.mask_options),
        "stderr": "",
        "duration_s": browser_result.duration_s + proxy_result.duration_s + dns_result.duration_s + versions.duration_s,
        "timed_out": browser_result.timed_out or proxy_result.timed_out or dns_result.timed_out or versions.timed_out,
        "category": task.category,
        "output_files": [str(summary_file), str(paths_file), str(context_file)],
        "summary_text": summary,
    }


def _run_app_crash_helper(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["collect app crash/hang events and reliability snapshot"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    summary_file = _build_output_path(task, output_dir)
    events_cmd = (
        "Get-WinEvent -FilterHashtable @{LogName='Application'; Id=1000,1001,1002; StartTime=(Get-Date).AddDays(-7)} "
        "-ErrorAction SilentlyContinue | Select-Object -First 40 TimeCreated,Id,ProviderName,LevelDisplayName,Message | ConvertTo-Json -Depth 4"
    )
    events = run_command(
        ["powershell", "-NoProfile", "-Command", events_cmd],
        timeout_s=45,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    reliability = run_command(
        [
            "powershell",
            "-NoProfile",
            "-Command",
            "Get-WinEvent -LogName Application -MaxEvents 60 | Select-Object TimeCreated,Id,ProviderName,Message | ConvertTo-Json -Depth 4",
        ],
        timeout_s=45,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    found = [
        f"Crash/hang event command code: {events.code}",
        f"Reliability snapshot command code: {reliability.code}",
    ]
    summary = _sectioned_summary(
        title="App Crash Helper Summary",
        checked=[
            "Collected recent crash/hang event entries (ID 1000/1001/1002).",
            "Collected reliability snapshot data.",
        ],
        found=found,
        changed=["No system changes were made."],
        next_steps=[
            "Update the affected app to the latest version.",
            "Update graphics/chipset drivers if crashes continue.",
            "If crashes persist, reinstall the app and export a Ticket Pack.",
        ],
        technical_lines=[
            f"events_code={events.code}",
            f"reliability_code={reliability.code}",
        ],
    )
    details_file = output_dir / "crash_events.json"
    details_file.write_text(_effective_mask(events.stdout or "[]", context.mask_options), encoding="utf-8")
    rel_file = output_dir / "reliability_snapshot.json"
    rel_file.write_text(_effective_mask(reliability.stdout or "[]", context.mask_options), encoding="utf-8")
    _write_summary_file(summary_file, summary, context.mask_options)
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": 0 if events.code == 0 else events.code,
        "stdout": _effective_mask(summary[:3000], context.mask_options),
        "stderr": _effective_mask(events.stderr[:1000], context.mask_options),
        "duration_s": events.duration_s + reliability.duration_s,
        "timed_out": events.timed_out or reliability.timed_out,
        "category": task.category,
        "output_files": [str(summary_file), str(details_file), str(rel_file)],
        "summary_text": summary,
    }


def _run_audio_mic_helper(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["collect audio device snapshot and provide guided sound setting links"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    summary_file = _build_output_path(task, output_dir)
    device_cmd = (
        "Get-PnpDevice -Class AudioEndpoint -ErrorAction SilentlyContinue | "
        "Select-Object FriendlyName,Status,Class | ConvertTo-Json -Depth 4"
    )
    devices = run_command(
        ["powershell", "-NoProfile", "-Command", device_cmd],
        timeout_s=30,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    recent_cmd = (
        "Get-WinEvent -FilterHashtable @{LogName='System'; ProviderName='Microsoft-Windows-Kernel-PnP'; StartTime=(Get-Date).AddDays(-3)} "
        "-ErrorAction SilentlyContinue | Select-Object -First 20 TimeCreated,Id,LevelDisplayName,Message | ConvertTo-Json -Depth 4"
    )
    recent = run_command(
        ["powershell", "-NoProfile", "-Command", recent_cmd],
        timeout_s=35,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    summary = _sectioned_summary(
        title="Audio and Mic Fix Helper Summary",
        checked=[
            "Collected AudioEndpoint device status.",
            "Collected recent Kernel-PnP device changes.",
        ],
        found=[
            f"Audio endpoint query code: {devices.code}",
            f"Recent device change query code: {recent.code}",
        ],
        changed=["No audio settings were changed automatically."],
        next_steps=[
            "Open Sound Settings: ms-settings:sound",
            "Open microphone privacy settings: ms-settings:privacy-microphone",
            "Run Windows audio troubleshooter if device remains unavailable.",
        ],
        technical_lines=[f"devices_code={devices.code}", f"recent_code={recent.code}"],
    )
    devices_file = output_dir / "audio_devices.json"
    devices_file.write_text(_effective_mask(devices.stdout or "[]", context.mask_options), encoding="utf-8")
    changes_file = output_dir / "audio_recent_device_changes.json"
    changes_file.write_text(_effective_mask(recent.stdout or "[]", context.mask_options), encoding="utf-8")
    _write_summary_file(summary_file, summary, context.mask_options)
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": 0 if devices.code == 0 else devices.code,
        "stdout": _effective_mask(summary[:2800], context.mask_options),
        "stderr": _effective_mask(devices.stderr[:1000], context.mask_options),
        "duration_s": devices.duration_s + recent.duration_s,
        "timed_out": devices.timed_out or recent.timed_out,
        "category": task.category,
        "output_files": [str(summary_file), str(devices_file), str(changes_file)],
        "summary_text": summary,
    }


def _run_camera_privacy_check(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["collect camera/mic privacy consent summary and links"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    summary_file = _build_output_path(task, output_dir)
    privacy_cmd = (
        "$cam='HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam'; "
        "$mic='HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone'; "
        "[PSCustomObject]@{camera=(Get-ItemProperty -Path $cam -ErrorAction SilentlyContinue); microphone=(Get-ItemProperty -Path $mic -ErrorAction SilentlyContinue)} | ConvertTo-Json -Depth 5"
    )
    privacy = run_command(
        ["powershell", "-NoProfile", "-Command", privacy_cmd],
        timeout_s=25,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    details_file = output_dir / "privacy_camera_mic.json"
    details_file.write_text(_effective_mask(privacy.stdout or "{}", context.mask_options), encoding="utf-8")
    summary = _sectioned_summary(
        title="Camera Privacy Quick Check Summary",
        checked=[
            "Read camera consent-store summary (best effort).",
            "Read microphone consent-store summary (best effort).",
        ],
        found=[f"Privacy query code: {privacy.code}"],
        changed=["No privacy setting was changed automatically."],
        next_steps=[
            "Open camera privacy settings: ms-settings:privacy-webcam",
            "Open microphone privacy settings: ms-settings:privacy-microphone",
            "Confirm app-level permissions for the affected app.",
        ],
        technical_lines=[f"privacy_code={privacy.code}"],
    )
    _write_summary_file(summary_file, summary, context.mask_options)
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": privacy.code,
        "stdout": _effective_mask(summary[:2800], context.mask_options),
        "stderr": _effective_mask(privacy.stderr[:1000], context.mask_options),
        "duration_s": privacy.duration_s,
        "timed_out": privacy.timed_out,
        "category": task.category,
        "output_files": [str(summary_file), str(details_file)],
        "summary_text": summary,
    }


def _run_onedrive_sync_helper(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["collect OneDrive process/log hints, disk free and network status"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    summary_file = _build_output_path(task, output_dir)
    proc = run_command(
        ["powershell", "-NoProfile", "-Command", "Get-Process OneDrive -ErrorAction SilentlyContinue | Select-Object Name,Id,StartTime | ConvertTo-Json -Depth 4"],
        timeout_s=20,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    disk = run_command(
        ["powershell", "-NoProfile", "-Command", "Get-PSDrive -Name C | Select-Object Name,Free,Used | ConvertTo-Json -Depth 3"],
        timeout_s=15,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    net = run_command(
        ["netsh", "winhttp", "show", "proxy"],
        timeout_s=15,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    log_dir = Path.home() / "AppData" / "Local" / "Microsoft" / "OneDrive" / "logs"
    log_hint = f"OneDrive logs path exists: {log_dir.exists()} ({log_dir})"
    details_file = output_dir / "onedrive_details.txt"
    details_file.write_text(
        _effective_mask(
            f"process_code={proc.code}\n{proc.stdout}\n\nproxy_code={net.code}\n{net.stdout}\n\n{log_hint}\n",
            context.mask_options,
        ),
        encoding="utf-8",
    )
    summary = _sectioned_summary(
        title="OneDrive Sync Helper Summary",
        checked=[
            "Checked OneDrive process state.",
            "Checked disk free summary.",
            "Checked proxy/network baseline.",
        ],
        found=[
            f"OneDrive process query code: {proc.code}",
            f"Disk query code: {disk.code}",
            f"Network proxy query code: {net.code}",
            log_hint,
        ],
        changed=["No sync state was changed automatically."],
        next_steps=[
            "Open OneDrive settings from taskbar icon (or run OneDrive.exe).",
            "Try pause and resume sync.",
            "If issues continue, export a support pack including logs.",
        ],
        technical_lines=[f"proc_code={proc.code}", f"disk_code={disk.code}", f"net_code={net.code}"],
    )
    _write_summary_file(summary_file, summary, context.mask_options)
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": 0 if proc.code == 0 else proc.code,
        "stdout": _effective_mask(summary[:2800], context.mask_options),
        "stderr": _effective_mask(proc.stderr[:1000], context.mask_options),
        "duration_s": proc.duration_s + disk.duration_s + net.duration_s,
        "timed_out": proc.timed_out or disk.timed_out or net.timed_out,
        "category": task.category,
        "output_files": [str(summary_file), str(details_file)],
        "summary_text": summary,
    }


def _run_usb_bt_disconnect_helper(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["collect USB/Bluetooth event and power management hints"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    summary_file = _build_output_path(task, output_dir)
    power = run_command(
        ["powercfg", "/devicequery", "wake_armed"],
        timeout_s=20,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    events = run_command(
        [
            "powershell",
            "-NoProfile",
            "-Command",
            "Get-WinEvent -FilterHashtable @{LogName='System'; StartTime=(Get-Date).AddDays(-3)} -ErrorAction SilentlyContinue | "
            "Where-Object {$_.ProviderName -match 'Kernel-PnP|BTHUSB|USB'} | Select-Object -First 40 TimeCreated,Id,ProviderName,Message | ConvertTo-Json -Depth 4",
        ],
        timeout_s=45,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    event_file = output_dir / "usb_bt_events.json"
    event_file.write_text(_effective_mask(events.stdout or "[]", context.mask_options), encoding="utf-8")
    summary = _sectioned_summary(
        title="USB/Bluetooth Disconnect Helper Summary",
        checked=[
            "Collected wake-armed power management list.",
            "Collected recent USB/Bluetooth-related system events.",
        ],
        found=[f"Power query code: {power.code}", f"USB/Bluetooth events query code: {events.code}"],
        changed=["No device power settings were changed automatically."],
        next_steps=[
            "Open Device Manager: devmgmt.msc",
            "Disable selective suspend for troubleshooting.",
            "Export a support pack if disconnects continue.",
        ],
        technical_lines=[f"power_code={power.code}", f"events_code={events.code}"],
    )
    _write_summary_file(summary_file, summary, context.mask_options)
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": 0 if events.code == 0 else events.code,
        "stdout": _effective_mask(summary[:3000], context.mask_options),
        "stderr": _effective_mask(events.stderr[:1000], context.mask_options),
        "duration_s": power.duration_s + events.duration_s,
        "timed_out": power.timed_out or events.timed_out,
        "category": task.category,
        "output_files": [str(summary_file), str(event_file)],
        "summary_text": summary,
    }


def _run_service_health_snapshot(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["collect key service states and startup types"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    out_file = _build_output_path(task, output_dir)
    cmd = (
        "Get-Service -Name wuauserv,bits,spooler,winmgmt,eventlog -ErrorAction SilentlyContinue | "
        "Select-Object Name,Status,StartType | Format-Table -AutoSize"
    )
    result = run_command(
        ["powershell", "-NoProfile", "-Command", cmd],
        timeout_s=task.timeout_s,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    summary = _sectioned_summary(
        title="Service Health Snapshot",
        checked=["Queried wuauserv, bits, spooler, winmgmt, eventlog."],
        found=[f"Query result code: {result.code}"],
        changed=["No services were changed."],
        next_steps=_default_next_steps_for_category(task.category),
        technical_lines=[line for line in (result.stdout or "").splitlines()[:12]],
    )
    _write_summary_file(out_file, summary, context.mask_options)
    raw_file = output_dir / "services_health_raw.txt"
    raw_file.write_text(_effective_mask(result.combined, context.mask_options), encoding="utf-8")
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": result.code,
        "stdout": _effective_mask(summary[:2800], context.mask_options),
        "stderr": _effective_mask(result.stderr[:1000], context.mask_options),
        "duration_s": result.duration_s,
        "timed_out": result.timed_out,
        "category": task.category,
        "output_files": [str(out_file), str(raw_file)],
        "summary_text": summary,
    }


def _run_driver_device_inventory(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["collect pnputil drivers + problem devices + setup logs excerpt"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    pnputil = run_command(
        ["pnputil", "/enum-drivers"],
        timeout_s=90,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    problem = run_command(
        ["powershell", "-NoProfile", "-Command", "Get-PnpDevice -Status Error -ErrorAction SilentlyContinue | Select-Object Class,FriendlyName,Status,InstanceId | ConvertTo-Json -Depth 4"],
        timeout_s=45,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    setup = run_command(
        ["wevtutil", "qe", "Microsoft-Windows-DeviceSetupManager/Admin", "/c:60", "/f:text"],
        timeout_s=40,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    drivers_file = output_dir / "drivers.txt"
    devices_file = _build_output_path(task, output_dir)
    setup_file = output_dir / "devicesetup_logs.txt"
    drivers_file.write_text(_effective_mask(pnputil.combined, context.mask_options), encoding="utf-8")
    devices_file.write_text(_effective_mask(problem.combined, context.mask_options), encoding="utf-8")
    setup_file.write_text(_effective_mask(setup.combined, context.mask_options), encoding="utf-8")
    summary = _sectioned_summary(
        title="Driver and Device Inventory Pack",
        checked=[
            "Enumerated driver packages with pnputil.",
            "Collected problem devices (status error).",
            "Collected DeviceSetupManager log excerpt.",
        ],
        found=[f"pnputil={pnputil.code}", f"problem_devices={problem.code}", f"setup_log={setup.code}"],
        changed=["No driver settings were changed."],
        next_steps=["Review problem devices first, then update/reinstall affected drivers.", "Export a Ticket Pack for escalation."],
        technical_lines=[f"drivers_file={drivers_file}", f"devices_file={devices_file}", f"setup_file={setup_file}"],
    )
    summary_file = output_dir / "devices_inventory_summary.txt"
    _write_summary_file(summary_file, summary, context.mask_options)
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": 0 if pnputil.code == 0 else pnputil.code,
        "stdout": _effective_mask(summary[:3000], context.mask_options),
        "stderr": _effective_mask(problem.stderr[:1000], context.mask_options),
        "duration_s": pnputil.duration_s + problem.duration_s + setup.duration_s,
        "timed_out": pnputil.timed_out or problem.timed_out or setup.timed_out,
        "category": task.category,
        "output_files": [str(summary_file), str(drivers_file), str(devices_file), str(setup_file)],
        "summary_text": summary,
    }


def _run_firewall_profile_summary(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["collect active firewall profile state (read-only)"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    out_file = _build_output_path(task, output_dir)
    cmd = "Get-NetFirewallProfile -ErrorAction SilentlyContinue | Select-Object Name,Enabled,DefaultInboundAction,DefaultOutboundAction | Format-Table -AutoSize"
    result = run_command(
        ["powershell", "-NoProfile", "-Command", cmd],
        timeout_s=30,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    summary = _sectioned_summary(
        title="Firewall Profile Summary",
        checked=["Collected NetFirewallProfile details in read-only mode."],
        found=[f"Query code: {result.code}"],
        changed=["No firewall settings were modified."],
        next_steps=[
            "Review profile enabled states before making policy changes.",
            "Use approved policy tooling for any firewall changes.",
        ],
        technical_lines=[line for line in result.stdout.splitlines()[:14]],
    )
    _write_summary_file(out_file, summary, context.mask_options)
    raw_file = output_dir / "firewall_summary_raw.txt"
    raw_file.write_text(_effective_mask(result.combined, context.mask_options), encoding="utf-8")
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": result.code,
        "stdout": _effective_mask(summary[:2800], context.mask_options),
        "stderr": _effective_mask(result.stderr[:1000], context.mask_options),
        "duration_s": result.duration_s,
        "timed_out": result.timed_out,
        "category": task.category,
        "output_files": [str(out_file), str(raw_file)],
        "summary_text": summary,
    }


def _run_wmi_repair_helper(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["check WMI query health and provide guided repair steps"],
            "timeout_s": task.timeout_s,
            "category": task.category,
            "admin_required": task.admin_required,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    out_file = _build_output_path(task, output_dir)
    cmd = "Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue | Select-Object CSName,Version,BuildNumber | ConvertTo-Json -Depth 3"
    result = run_command(
        ["powershell", "-NoProfile", "-Command", cmd],
        timeout_s=30,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    verify = run_command(
        ["winmgmt", "/verifyrepository"],
        timeout_s=25,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    summary = _sectioned_summary(
        title="WMI Repair Helper Summary",
        checked=[
            "Ran a baseline CIM query.",
            "Verified WMI repository status.",
        ],
        found=[f"CIM query code: {result.code}", f"Repository verify code: {verify.code}"],
        changed=["No destructive WMI repair commands were executed automatically."],
        next_steps=[
            "If repository is inconsistent, run approved WMI repair steps with admin review.",
            "Create restore point before any repair commands.",
            "Export Ticket Pack before and after repair attempts.",
        ],
        technical_lines=[f"cim_output={result.stdout[:120]}", f"verify_output={verify.stdout[:120]}"],
    )
    _write_summary_file(out_file, summary, context.mask_options)
    raw_file = output_dir / "wmi_health_raw.txt"
    raw_file.write_text(_effective_mask(f"{result.combined}\n\n{verify.combined}", context.mask_options), encoding="utf-8")
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": 0 if result.code == 0 else result.code,
        "stdout": _effective_mask(summary[:3000], context.mask_options),
        "stderr": _effective_mask((result.stderr or verify.stderr)[:1000], context.mask_options),
        "duration_s": result.duration_s + verify.duration_s,
        "timed_out": result.timed_out or verify.timed_out,
        "category": task.category,
        "admin_required": task.admin_required,
        "output_files": [str(out_file), str(raw_file)],
        "summary_text": summary,
    }


def _run_office_outlook_helper(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["collect Office/Outlook paths and version hints (no content access)"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    summary_file = _build_output_path(task, output_dir)
    office_cmd = (
        "$paths=@('HKLM:\\SOFTWARE\\Microsoft\\Office\\ClickToRun\\Configuration','HKLM:\\SOFTWARE\\Microsoft\\Office\\16.0\\Common\\InstallRoot'); "
        "$rows=@(); foreach($p in $paths){if(Test-Path $p){$rows += Get-ItemProperty -Path $p -ErrorAction SilentlyContinue}}; "
        "$rows | ConvertTo-Json -Depth 5"
    )
    office = run_command(
        ["powershell", "-NoProfile", "-Command", office_cmd],
        timeout_s=30,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    outlook_profile_path = Path.home() / "AppData" / "Roaming" / "Microsoft" / "Outlook"
    pst_candidates = list((Path.home() / "Documents").glob("Outlook Files/*.pst"))
    details_file = output_dir / "office_outlook_paths.txt"
    details_lines = [f"Outlook profile path exists: {outlook_profile_path.exists()} ({outlook_profile_path})"]
    details_lines.extend([f"PST: {row}" for row in pst_candidates[:40]])
    details_file.write_text(_effective_mask("\n".join(details_lines), context.mask_options), encoding="utf-8")
    office_file = output_dir / "office_version.json"
    office_file.write_text(_effective_mask(office.stdout or "[]", context.mask_options), encoding="utf-8")
    summary = _sectioned_summary(
        title="Office/Outlook Helper Summary",
        checked=[
            "Collected Office install/version registry hints.",
            "Collected Outlook profile and PST path locations (paths only).",
        ],
        found=[
            f"Office query code: {office.code}",
            f"Outlook profile path exists: {outlook_profile_path.exists()}",
            f"PST files found (path-only): {len(pst_candidates)}",
        ],
        changed=["No Outlook profile or Office settings were changed."],
        next_steps=[
            "Open Apps and Features to run Office repair options.",
            "Recreate Outlook profile only after backup confirmation.",
            "Export Ticket Pack before escalation.",
        ],
        technical_lines=[f"office_code={office.code}", f"pst_count={len(pst_candidates)}"],
    )
    _write_summary_file(summary_file, summary, context.mask_options)
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": office.code,
        "stdout": _effective_mask(summary[:3000], context.mask_options),
        "stderr": _effective_mask(office.stderr[:1000], context.mask_options),
        "duration_s": office.duration_s,
        "timed_out": office.timed_out,
        "category": task.category,
        "output_files": [str(summary_file), str(details_file), str(office_file)],
        "summary_text": summary,
    }


def _run_wifi_report_fix_wizard(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": [
                "collect wifi interfaces + wlan report + dns/proxy/hosts checks",
                "collect network evidence bundle",
                "run safe flush dns action",
                "prepare network settings links",
            ],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    iface = run_command(
        ["netsh", "wlan", "show", "interfaces"],
        timeout_s=25,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    proxy = run_command(
        ["netsh", "winhttp", "show", "proxy"],
        timeout_s=15,
        cancel_event=context.cancel_event,
        on_stdout_line=_stdout_cb(context),
        on_stderr_line=_stderr_cb(context),
    )
    hosts = _run_hosts_check(ScriptTask("task_hosts_check_tmp", "Hosts", "", None, 20, "Safe", "network", output_name="hosts_check"), context, False)
    dns = _run_dns_timing(
        ScriptTask("task_dns_timing_tmp", "DNS", "", None, 25, "Safe", "network", output_name="dns_timing", output_ext="csv"),
        context,
        False,
    )
    wlan = _run_wlan_report(ScriptTask("task_wlan_report_tmp", "WLAN Report", "", None, 45, "Safe", "network", output_name="wlan_report"), context, False)
    flush = run_script_task(
        "task_dns_flush",
        dry_run=False,
        output_dir=output_dir,
        mask_options=context.mask_options,
        cancel_event=context.cancel_event,
        timeout_override_s=25,
        log_cb=context.log_cb,
        run_event_bus=context.run_event_bus,
        run_id=context.run_id,
    )
    bundle = run_script_task(
        "task_network_evidence_pack",
        dry_run=False,
        output_dir=output_dir,
        mask_options=context.mask_options,
        cancel_event=context.cancel_event,
        timeout_override_s=max(60, task.timeout_s),
        log_cb=context.log_cb,
        run_event_bus=context.run_event_bus,
        run_id=context.run_id,
    )
    links_file = output_dir / "wifi_settings_links.txt"
    links_file.write_text(
        _effective_mask(
            "\n".join(
                [
                    "ms-settings:network-status",
                    "ms-settings:network-wifi",
                    "ms-settings:network-proxy",
                ]
            ),
            context.mask_options,
        ),
        encoding="utf-8",
    )
    summary_file = _build_output_path(task, output_dir)
    summary = _sectioned_summary(
        title="Wi-Fi Report + Fix Wizard Summary",
        checked=[
            "Collected WLAN interface status.",
            "Collected WLAN report artifact.",
            "Collected proxy and hosts checks.",
            "Ran DNS timing checks.",
            "Collected full network evidence bundle.",
            "Ran safe DNS flush action.",
        ],
        found=[
            f"wlan_interface_code={iface.code}",
            f"wlan_report_code={wlan.get('code', 1)}",
            f"proxy_code={proxy.code}",
            f"dns_timing_code={dns.get('code', 1)}",
            f"network_bundle_code={bundle.get('code', 1)}",
            f"dns_flush_code={flush.get('code', 1)}",
        ],
        changed=[
            "DNS cache was flushed as a safe remediation.",
            "No adapter reset or destructive network changes were run.",
        ],
        next_steps=[
            "Open Network Status: ms-settings:network-status",
            "Open Wi-Fi settings: ms-settings:network-wifi",
            "Export Home Share Pack or Ticket Pack if issue persists.",
        ],
        technical_lines=[
            "Guided workflow with safe actions only.",
            f"settings_links_file={links_file}",
            f"bundle_artifacts={len(bundle.get('output_files', []))}",
        ],
    )
    _write_summary_file(summary_file, summary, context.mask_options)
    iface_file = output_dir / "wifi_interfaces.txt"
    iface_file.write_text(_effective_mask(iface.combined, context.mask_options), encoding="utf-8")
    files = [str(summary_file), str(iface_file), str(links_file)]
    files.extend([str(p) for p in dns.get("output_files", [])])
    files.extend([str(p) for p in hosts.get("output_files", [])])
    files.extend([str(p) for p in wlan.get("output_files", []) if p])
    files.extend([str(p) for p in flush.get("output_files", []) if p])
    files.extend([str(p) for p in bundle.get("output_files", []) if p])
    dedup_files: list[str] = []
    seen_files: set[str] = set()
    for path in files:
        if path in seen_files:
            continue
        seen_files.add(path)
        dedup_files.append(path)
    code = 0 if bundle.get("code", 1) == 0 else int(bundle.get("code", 1))
    if code == 0 and int(iface.code) != 0:
        code = int(iface.code)
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": code,
        "stdout": _effective_mask(summary[:3000], context.mask_options),
        "stderr": _effective_mask((iface.stderr or proxy.stderr)[:1000], context.mask_options),
        "duration_s": iface.duration_s + proxy.duration_s + float(wlan.get("duration_s", 0.0)),
        "timed_out": iface.timed_out or proxy.timed_out or bool(wlan.get("timed_out")),
        "category": task.category,
        "output_files": dedup_files,
        "summary_text": summary,
    }


def _run_storage_radar(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {
            "task_id": task.id,
            "title": task.title,
            "dry_run": True,
            "command": ["collect top folders with bounded budget + filter out hidden/system noise + generate ranked bars + downloads plan"],
            "timeout_s": task.timeout_s,
            "category": task.category,
        }
    output_dir = _ensure_output_dir(context.output_dir)
    roots = [
        Path.home() / "Desktop",
        Path.home() / "Documents",
        Path.home() / "Downloads",
        Path.home() / "Pictures",
        Path.home() / "Videos",
        Path.home() / "AppData" / "Local",
    ]
    budget_files = 5000
    file_seen = 0
    rows: list[dict[str, Any]] = []
    for root in roots:
        if not root.exists():
            continue
        total = 0
        root_files = 0
        for dirpath, _dirs, files in os.walk(root):
            if context.cancel_event is not None and context.cancel_event.is_set():
                break
            path_lower = dirpath.lower()
            if "\\temp" in path_lower or "\\cache" in path_lower:
                continue
            if "\\$recycle.bin" in path_lower:
                continue
            for name in files:
                if file_seen >= budget_files:
                    break
                file_seen += 1
                root_files += 1
                p = Path(dirpath) / name
                try:
                    total += p.stat().st_size
                except OSError:
                    continue
            if file_seen >= budget_files:
                break
        rows.append(
            {
                "folder": str(root),
                "size_gb": round(total / (1024**3), 3),
                "files_scanned": root_files,
            }
        )
        if file_seen >= budget_files:
            break
    rows.sort(key=lambda item: float(item.get("size_gb", 0)), reverse=True)
    csv_file = output_dir / "storage_radar.csv"
    csv_rows: list[dict[str, Any]] = []
    for row in rows:
        if isinstance(row, dict):
            csv_rows.append(
                {
                    "folder": row.get("folder", ""),
                    "size_gb": row.get("size_gb", 0),
                    "files_scanned": row.get("files_scanned", 0),
                }
            )
    _write_csv(csv_file, csv_rows, ["folder", "size_gb", "files_scanned"])
    bars_file = output_dir / "storage_radar_bars.txt"
    max_size = max([float(r.get("size_gb", 0) or 0) for r in csv_rows], default=1.0)
    bar_lines: list[str] = []
    for row in csv_rows[:12]:
        size = float(row.get("size_gb", 0) or 0)
        bar = "#" * max(1, int((size / max_size) * 40)) if max_size > 0 else "#"
        bar_lines.append(f"{row.get('folder', '')}\n  {bar} {size:.2f} GB ({row.get('files_scanned', 0)} files)")
    bars_file.write_text(_effective_mask("\n".join(bar_lines), context.mask_options), encoding="utf-8")
    downloads = downloads_cleanup_assistant(days_old=30)
    download_rows = downloads.get("candidates", []) if isinstance(downloads, dict) else []
    downloads_plan = output_dir / "downloads_plan.csv"
    normalized_rows: list[dict[str, Any]] = []
    for row in download_rows[:300]:
        if not isinstance(row, dict):
            continue
        normalized_rows.append(
            {
                "path": str(row.get("path", "")),
                "size_mb": row.get("size_mb", 0),
                "age_days": row.get("age_days", 0),
            }
        )
    _write_csv(downloads_plan, normalized_rows, ["path", "size_mb", "age_days"])
    summary = _sectioned_summary(
        title="Storage Radar Summary",
        checked=[
            "Scanned top home folders using bounded file budget.",
            "Applied safe filters to reduce temp/cache noise.",
            "Generated ranked bar visualization output.",
            "Generated Downloads cleanup preview plan (no deletions).",
        ],
        found=[
            f"rows_captured={len(csv_rows)}",
            f"files_scanned_budget={file_seen}/{budget_files}",
            f"downloads_candidates={len(normalized_rows)}",
        ],
        changed=["No files were deleted; preview-only."],
        next_steps=[
            "Review top folders first.",
            "Review downloads_plan.csv before deleting anything.",
            "Export results if you need support help.",
        ],
        technical_lines=[f"csv={csv_file}", f"bars={bars_file}", f"downloads_plan={downloads_plan}"],
    )
    summary_file = _build_output_path(task, output_dir)
    _write_summary_file(summary_file, summary, context.mask_options)
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": 0,
        "stdout": _effective_mask(summary[:3000], context.mask_options),
        "stderr": "",
        "duration_s": 0.0,
        "timed_out": False,
        "category": task.category,
        "output_files": [str(summary_file), str(csv_file), str(bars_file), str(downloads_plan)],
        "summary_text": summary,
    }


def _run_startup_autostart_pack(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {"task_id": task.id, "title": task.title, "dry_run": True, "command": ["collect startup folders, Run keys, task triggers, optional autorunsc"], "timeout_s": task.timeout_s, "category": task.category}
    output_dir = _ensure_output_dir(context.output_dir)
    startup = _run_startup_items(ScriptTask("task_startup_tmp", "Startup", "", None, 40, "Safe", "system"), context, False)
    tasks = run_command(["schtasks", "/query", "/fo", "CSV", "/v"], timeout_s=90, cancel_event=context.cancel_event, on_stdout_line=_stdout_cb(context), on_stderr_line=_stderr_cb(context))
    inventory_csv = output_dir / "startup_inventory.csv"
    inventory_csv.write_text(_effective_mask(tasks.stdout, context.mask_options), encoding="utf-8", errors="ignore")
    autorunsc_paths = [
        Path.cwd() / "tools" / "autorunsc.exe",
        Path.cwd() / "assets" / "tools" / "autorunsc.exe",
    ]
    autorunsc_file = output_dir / "autorunsc.csv"
    autorunsc_note = "autorunsc.exe not found."
    for candidate in autorunsc_paths:
        if candidate.exists():
            res = run_command([str(candidate), "-accepteula", "-a", "*", "-c"], timeout_s=120, cancel_event=context.cancel_event, on_stdout_line=_stdout_cb(context), on_stderr_line=_stderr_cb(context))
            autorunsc_file.write_text(_effective_mask(res.stdout, context.mask_options), encoding="utf-8", errors="ignore")
            autorunsc_note = f"autorunsc executed from {candidate}"
            break
    summary = _sectioned_summary(
        title="Startup/Autostart Pack Summary",
        checked=["Collected startup items (folders + Run keys).", "Collected scheduled task inventory.", "Attempted optional autorunsc collection."],
        found=[f"startup_items_files={len(startup.get('output_files', []))}", f"scheduled_tasks_code={tasks.code}", autorunsc_note],
        changed=["No startup entry was modified."],
        next_steps=["Disable only clearly unnecessary startup items.", "Reboot and compare performance sample results.", "Export Ticket Pack for support review."],
        technical_lines=[f"inventory_csv={inventory_csv}", f"autorunsc_file={autorunsc_file if autorunsc_file.exists() else 'n/a'}"],
    )
    summary_file = _build_output_path(task, output_dir)
    _write_summary_file(summary_file, summary, context.mask_options)
    files = [str(summary_file), str(inventory_csv), *[str(p) for p in startup.get("output_files", [])]]
    if autorunsc_file.exists():
        files.append(str(autorunsc_file))
    return {"task_id": task.id, "title": task.title, "dry_run": False, "code": 0 if tasks.code == 0 else tasks.code, "stdout": _effective_mask(summary[:3000], context.mask_options), "stderr": _effective_mask(tasks.stderr[:1000], context.mask_options), "duration_s": tasks.duration_s, "timed_out": tasks.timed_out, "category": task.category, "output_files": files, "summary_text": summary}


def _run_system_profile(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    from .diagnostics import quick_check

    if dry_run:
        return {"task_id": task.id, "title": task.title, "dry_run": True, "command": ["collect local system profile json + summary"], "timeout_s": task.timeout_s, "category": task.category}
    output_dir = _ensure_output_dir(context.output_dir)
    data = quick_check(include_capabilities=False)
    profile_json = output_dir / "system_profile.json"
    profile_json.write_text(_effective_mask(json.dumps(data, indent=2), context.mask_options), encoding="utf-8")
    summary = _sectioned_summary(
        title="Belarc-lite System Profile Summary",
        checked=["Collected core sysinfo, metrics, and baseline findings."],
        found=[f"hostname={data.get('sysinfo', {}).get('hostname', '')}", f"findings={len(data.get('findings', []))}"],
        changed=["Read-only data collection only."],
        next_steps=["Attach profile json to support ticket.", "Collect full Ticket Pack if escalation is needed."],
        technical_lines=[f"profile_json={profile_json}"],
    )
    summary_file = _build_output_path(task, output_dir)
    _write_summary_file(summary_file, summary, context.mask_options)
    return {"task_id": task.id, "title": task.title, "dry_run": False, "code": 0, "stdout": _effective_mask(summary[:3000], context.mask_options), "stderr": "", "duration_s": 0.0, "timed_out": False, "category": task.category, "output_files": [str(summary_file), str(profile_json)], "summary_text": summary}


def _run_evidence_pack(task: ScriptTask, context: ScriptTaskContext, dry_run: bool, task_ids: list[str], label: str) -> dict[str, Any]:
    if dry_run:
        return {"task_id": task.id, "title": task.title, "dry_run": True, "command": [f"collect {label} evidence pack"], "timeout_s": task.timeout_s, "category": task.category}
    output_dir = _ensure_output_dir(context.output_dir)
    files: list[str] = []
    failures = 0
    for tid in task_ids:
        res = run_script_task(
            tid,
            dry_run=False,
            output_dir=output_dir,
            mask_options=context.mask_options,
            cancel_event=context.cancel_event,
            timeout_override_s=max(15, task.timeout_s // max(len(task_ids), 1)),
            log_cb=context.log_cb,
            run_event_bus=context.run_event_bus,
            run_id=context.run_id,
        )
        if int(res.get("code", 1)) != 0:
            failures += 1
        files.extend([str(p) for p in res.get("output_files", [])])
    summary = _sectioned_summary(
        title=f"{label} Evidence Pack Summary",
        checked=[f"Ran {len(task_ids)} evidence tasks."],
        found=[f"Failures: {failures}", f"Artifacts: {len(files)}"],
        changed=["No remediation changes were performed by this pack."],
        next_steps=["Review captured files.", "Export Ticket Pack for escalation."],
        technical_lines=[f"task_ids={','.join(task_ids)}"],
    )
    summary_file = _build_output_path(task, output_dir)
    _write_summary_file(summary_file, summary, context.mask_options)
    files.append(str(summary_file))
    return {"task_id": task.id, "title": task.title, "dry_run": False, "code": 0 if failures == 0 else 1, "stdout": _effective_mask(summary[:2500], context.mask_options), "stderr": "" if failures == 0 else f"{failures} task(s) failed.", "duration_s": 0.0, "timed_out": False, "category": task.category, "output_files": files, "summary_text": summary}


def _run_fast_file_search(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {"task_id": task.id, "title": task.title, "dry_run": True, "command": ["build/update file index and export sample search results"], "timeout_s": task.timeout_s, "category": task.category}
    output_dir = _ensure_output_dir(context.output_dir)
    from .file_index import export_results_csv, index_roots, resolve_roots, search_files
    from .settings import load_settings

    settings = load_settings()
    configured_roots = settings.file_index_roots or []
    roots = resolve_roots(configured_roots)
    start = time.monotonic()
    budget_s = min(max(task.timeout_s, 15), 120)
    payload = index_roots(
        roots,
        budget_seconds=budget_s,
        cancel_event=context.cancel_event,
        log_cb=context.log_cb,
    )
    query = "log"
    matches = search_files(query, limit=250)
    results_csv = output_dir / "search_results.csv"
    export_results_csv(matches, results_csv)
    index_meta = output_dir / "file_index_meta.json"
    index_meta.write_text(_effective_mask(json.dumps(payload, indent=2), context.mask_options), encoding="utf-8")
    summary = _sectioned_summary(
        title="Fast File Search (Everything-lite) Summary",
        checked=["Indexed selected roots with a bounded time budget.", "Used SQLite-backed incremental index for fast subsequent queries."],
        found=[f"files_scanned={payload.get('scanned', 0)}", f"index_rows_changed={payload.get('changed', 0)}", f"sample_query={query} matches={len(matches)}"],
        changed=["No files were modified; index metadata only."],
        next_steps=["Run the tool again to refresh index incrementally.", "Use exported results csv to shortlist large/problem files."],
        technical_lines=[f"roots={';'.join([str(r) for r in roots])}", f"budget_s={budget_s}", f"index_meta={index_meta}"],
    )
    summary_file = _build_output_path(task, output_dir)
    _write_summary_file(summary_file, summary, context.mask_options)
    return {
        "task_id": task.id,
        "title": task.title,
        "dry_run": False,
        "code": 130 if payload.get("cancelled") else 0,
        "stdout": _effective_mask(summary[:3000], context.mask_options),
        "stderr": "" if not payload.get("cancelled") else "Cancelled by user.",
        "duration_s": round(time.monotonic() - start, 2),
        "timed_out": False,
        "category": task.category,
        "output_files": [str(summary_file), str(index_meta), str(results_csv)],
        "summary_text": summary,
    }


def _run_appdata_bloat_scanner(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {"task_id": task.id, "title": task.title, "dry_run": True, "command": ["scan appdata folder sizes with budget"], "timeout_s": task.timeout_s, "category": task.category}
    output_dir = _ensure_output_dir(context.output_dir)
    app_roots = [Path.home() / "AppData" / "Local", Path.home() / "AppData" / "Roaming"]
    rows: list[dict[str, Any]] = []
    limit = 4000
    seen = 0
    for root in app_roots:
        if not root.exists():
            continue
        for child in root.iterdir():
            if not child.is_dir():
                continue
            total = 0
            for file in child.rglob("*"):
                if seen >= limit:
                    break
                seen += 1
                if not file.is_file():
                    continue
                try:
                    total += file.stat().st_size
                except OSError:
                    continue
            rows.append({"folder": str(child), "size_mb": round(total / (1024 * 1024), 2), "root": str(root)})
    rows.sort(key=lambda x: float(x.get("size_mb", 0)), reverse=True)
    csv_file = output_dir / "appdata_bloat.csv"
    _write_csv(csv_file, rows[:300], ["folder", "size_mb", "root"])
    summary = _sectioned_summary(
        title="AppData Bloat Scanner Summary",
        checked=["Scanned top AppData folders with bounded file budget."],
        found=[f"folders_ranked={len(rows)}", f"files_seen={seen}"],
        changed=["No files were deleted."],
        next_steps=["Review top folders before any manual cleanup.", "Use preview-first cleanup tools for safer removal."],
        technical_lines=[f"csv={csv_file}"],
    )
    summary_file = _build_output_path(task, output_dir)
    _write_summary_file(summary_file, summary, context.mask_options)
    return {"task_id": task.id, "title": task.title, "dry_run": False, "code": 0, "stdout": _effective_mask(summary[:2500], context.mask_options), "stderr": "", "duration_s": 0.0, "timed_out": False, "category": task.category, "output_files": [str(summary_file), str(csv_file)], "summary_text": summary}


def _run_admin_repair_chain(task: ScriptTask, context: ScriptTaskContext, dry_run: bool, ids: list[str], log_name: str, label: str) -> dict[str, Any]:
    if dry_run:
        return {"task_id": task.id, "title": task.title, "dry_run": True, "command": [f"run admin chain: {' -> '.join(ids)}"], "timeout_s": task.timeout_s, "category": task.category, "admin_required": True, "reboot_likely": True}
    output_dir = _ensure_output_dir(context.output_dir)
    lines: list[str] = []
    files: list[str] = []
    failures = 0
    for tid in ids:
        res = run_script_task(
            tid,
            dry_run=False,
            output_dir=output_dir,
            mask_options=context.mask_options,
            cancel_event=context.cancel_event,
            timeout_override_s=max(20, task.timeout_s // max(len(ids), 1)),
            log_cb=context.log_cb,
            run_event_bus=context.run_event_bus,
            run_id=context.run_id,
        )
        code = int(res.get("code", 1))
        if code != 0:
            failures += 1
        lines.append(f"[{code}] {tid}")
        files.extend([str(p) for p in res.get("output_files", [])])
    repair_log = output_dir / log_name
    repair_log.write_text(_effective_mask("\n".join(lines), context.mask_options), encoding="utf-8")
    summary = _sectioned_summary(
        title=f"{label} Summary",
        checked=[f"Executed {len(ids)} steps in chain."],
        found=[f"failures={failures}"],
        changed=["This chain may change system network/update/printer/integrity state."],
        next_steps=["Reboot if requested by reset commands.", "Re-run a diagnostic check to verify results.", "Export Ticket Pack with repair logs."],
        technical_lines=[f"steps={','.join(ids)}", f"log={repair_log}"],
    )
    summary_file = _build_output_path(task, output_dir)
    _write_summary_file(summary_file, summary, context.mask_options)
    files.extend([str(repair_log), str(summary_file)])
    return {"task_id": task.id, "title": task.title, "dry_run": False, "code": 0 if failures == 0 else 1, "stdout": _effective_mask(summary[:2600], context.mask_options), "stderr": "" if failures == 0 else f"{failures} step(s) failed", "duration_s": 0.0, "timed_out": False, "category": task.category, "admin_required": True, "reboot_likely": True, "output_files": files, "summary_text": summary}


def _run_smart_snapshot(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {"task_id": task.id, "title": task.title, "dry_run": True, "command": ["collect SMART/physical disk health hints (best effort)"], "timeout_s": task.timeout_s, "category": task.category}
    output_dir = _ensure_output_dir(context.output_dir)
    physical = run_command(["powershell", "-NoProfile", "-Command", "Get-PhysicalDisk -ErrorAction SilentlyContinue | Select-Object FriendlyName,HealthStatus,OperationalStatus | Format-Table -AutoSize"], timeout_s=25, cancel_event=context.cancel_event, on_stdout_line=_stdout_cb(context), on_stderr_line=_stderr_cb(context))
    wmic = run_command(["wmic", "diskdrive", "get", "status,model"], timeout_s=20, cancel_event=context.cancel_event, on_stdout_line=_stdout_cb(context), on_stderr_line=_stderr_cb(context))
    summary = _sectioned_summary(
        title="SMART Snapshot Summary",
        checked=["Queried physical disk health (PowerShell).", "Queried diskdrive status (wmic) best effort."],
        found=[f"physicaldisk_code={physical.code}", f"wmic_code={wmic.code}"],
        changed=["Read-only hardware health collection only."],
        next_steps=["If health is degraded, back up immediately.", "Run vendor disk diagnostics for confirmation.", "Export report for support review."],
        technical_lines=[f"physical_output_len={len(physical.stdout)}", f"wmic_output_len={len(wmic.stdout)}"],
    )
    out_file = _build_output_path(task, output_dir)
    _write_summary_file(out_file, summary, context.mask_options)
    raw_file = output_dir / "smart_raw.txt"
    raw_file.write_text(_effective_mask(f"{physical.combined}\n\n{wmic.combined}", context.mask_options), encoding="utf-8")
    return {"task_id": task.id, "title": task.title, "dry_run": False, "code": 0 if physical.code == 0 else physical.code, "stdout": _effective_mask(summary[:2500], context.mask_options), "stderr": _effective_mask((physical.stderr or wmic.stderr)[:1000], context.mask_options), "duration_s": physical.duration_s + wmic.duration_s, "timed_out": physical.timed_out or wmic.timed_out, "category": task.category, "output_files": [str(out_file), str(raw_file)], "summary_text": summary}


def _run_thermal_hints(task: ScriptTask, context: ScriptTaskContext, dry_run: bool) -> dict[str, Any]:
    if dry_run:
        return {"task_id": task.id, "title": task.title, "dry_run": True, "command": ["collect thermal/throttle hints best effort"], "timeout_s": task.timeout_s, "category": task.category}
    output_dir = _ensure_output_dir(context.output_dir)
    cim = run_command(["powershell", "-NoProfile", "-Command", "Get-CimInstance -Namespace root/wmi -ClassName MSAcpi_ThermalZoneTemperature -ErrorAction SilentlyContinue | Select-Object InstanceName,CurrentTemperature | ConvertTo-Json -Depth 4"], timeout_s=25, cancel_event=context.cancel_event, on_stdout_line=_stdout_cb(context), on_stderr_line=_stderr_cb(context))
    cpu = run_command(["powershell", "-NoProfile", "-Command", "Get-Counter '\\Processor(_Total)\\% Processor Time' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty CounterSamples | Select-Object CookedValue | ConvertTo-Json"], timeout_s=20, cancel_event=context.cancel_event, on_stdout_line=_stdout_cb(context), on_stderr_line=_stderr_cb(context))
    unsupported = (not cim.stdout.strip()) and (not cpu.stdout.strip())
    summary = _sectioned_summary(
        title="Thermal/Throttle Hints Summary",
        checked=["Queried thermal zone sensors (best effort).", "Collected CPU utilization hint sample."],
        found=[f"thermal_query_code={cim.code}", f"cpu_counter_code={cpu.code}", "Thermal sensors not supported on this device." if unsupported else "Thermal hint data was collected."],
        changed=["No system settings were changed."],
        next_steps=["Ensure vents/fans are clear and airflow is adequate.", "Check power mode and background load.", "Use OEM tools for deeper thermal diagnostics."],
        technical_lines=[f"thermal_output_len={len(cim.stdout)}", f"cpu_output_len={len(cpu.stdout)}"],
    )
    out_file = _build_output_path(task, output_dir)
    _write_summary_file(out_file, summary, context.mask_options)
    raw_file = output_dir / "thermal_raw.txt"
    raw_file.write_text(_effective_mask(f"{cim.combined}\n\n{cpu.combined}", context.mask_options), encoding="utf-8")
    return {"task_id": task.id, "title": task.title, "dry_run": False, "code": 0, "stdout": _effective_mask(summary[:2600], context.mask_options), "stderr": _effective_mask((cim.stderr or cpu.stderr)[:1000], context.mask_options), "duration_s": cim.duration_s + cpu.duration_s, "timed_out": cim.timed_out or cpu.timed_out, "category": task.category, "output_files": [str(out_file), str(raw_file)], "summary_text": summary}


def _run_create_restore_point(
    cancel_event: Event | None = None,
    timeout_s: int = 70,
    log_cb: Callable[[str], None] | None = None,
) -> dict[str, Any]:
    if not is_admin():
        return {"code": 1, "stdout": "", "stderr": "Administrator privileges are required for restore point.", "duration_s": 0.0, "timed_out": False}
    cmd = (
        f"Checkpoint-Computer -Description '{APP_DISPLAY_NAME} Pre-Runbook' -RestorePointType MODIFY_SETTINGS "
        "-ErrorAction Continue; "
        "Get-ComputerRestorePoint | Select-Object -First 5 | ConvertTo-Json -Depth 3"
    )
    result = run_command(
        ["powershell", "-NoProfile", "-Command", cmd],
        timeout_s=timeout_s,
        cancel_event=cancel_event,
        on_stdout_line=log_cb,
        on_stderr_line=(lambda line: log_cb(f"[stderr] {line}")) if log_cb is not None else None,
    )
    return {
        "code": result.code,
        "stdout": result.stdout,
        "stderr": result.stderr,
        "duration_s": result.duration_s,
        "timed_out": result.timed_out,
    }


SCRIPT_TASKS: tuple[ScriptTask, ...] = (
    # Evidence: system
    ScriptTask("task_systeminfo_export", "SystemInfo Export", "Capture systeminfo snapshot.", ("systeminfo",), 60, "Safe", "system", output_name="systeminfo"),
    ScriptTask(
        "task_hotfixes_export",
        "HotFixes Export",
        "Capture installed hotfixes.",
        ("powershell", "-NoProfile", "-Command", "Get-HotFix | Sort-Object InstalledOn -Descending | Format-Table -AutoSize"),
        45,
        "Safe",
        "system",
        output_name="hotfixes",
    ),
    ScriptTask("task_drivers_export", "Drivers Export", "Capture installed driver inventory.", ("pnputil", "/enum-drivers"), 60, "Safe", "system", output_name="drivers"),
    ScriptTask(
        "task_services_export",
        "Services Export",
        "Capture service status snapshot.",
        ("powershell", "-NoProfile", "-Command", "Get-Service | Sort-Object Status,DisplayName | Format-Table -AutoSize"),
        45,
        "Safe",
        "system",
        output_name="services",
    ),
    ScriptTask("task_scheduled_tasks_export", "Scheduled Tasks Export", "Capture scheduled tasks in verbose list format.", ("schtasks", "/query", "/fo", "LIST", "/v"), 90, "Safe", "system", output_name="scheduled_tasks"),
    ScriptTask("task_startup_items_export", "Startup Items Export", "Collect startup entries from registry and startup folders.", None, 40, "Safe", "system", output_name="startup", runner=_run_startup_items),
    # Event logs evidence
    ScriptTask("task_evtx_application", "Export Application Event Log", "Export Application log to EVTX.", ("wevtutil", "epl", "Application", "{out}", "/ow:true"), 60, "Safe", "eventlogs", output_name="application", output_ext="evtx"),
    ScriptTask("task_evtx_system", "Export System Event Log", "Export System log to EVTX.", ("wevtutil", "epl", "System", "{out}", "/ow:true"), 60, "Safe", "eventlogs", output_name="system", output_ext="evtx"),
    ScriptTask("task_evtx_setup", "Export Setup Event Log", "Export Setup log to EVTX.", ("wevtutil", "epl", "Setup", "{out}", "/ow:true"), 60, "Safe", "eventlogs", output_name="setup", output_ext="evtx"),
    ScriptTask(
        "task_evtx_windows_update",
        "Export WindowsUpdateClient Operational",
        "Export Windows Update operational log.",
        ("wevtutil", "epl", "Microsoft-Windows-WindowsUpdateClient/Operational", "{out}", "/ow:true"),
        80,
        "Safe",
        "eventlogs",
        output_name="windows_update_operational",
        output_ext="evtx",
    ),
    ScriptTask(
        "task_evtx_printservice",
        "Export PrintService Operational",
        "Export print service operational log.",
        ("wevtutil", "epl", "Microsoft-Windows-PrintService/Operational", "{out}", "/ow:true"),
        80,
        "Safe",
        "eventlogs",
        output_name="printservice_operational",
        output_ext="evtx",
    ),
    ScriptTask(
        "task_evtx_devicesetup",
        "Export DeviceSetupManager Admin",
        "Export device setup manager admin log (best effort).",
        ("wevtutil", "epl", "Microsoft-Windows-DeviceSetupManager/Admin", "{out}", "/ow:true"),
        80,
        "Safe",
        "eventlogs",
        output_name="devicesetup_admin",
        output_ext="evtx",
    ),
    # Windows update evidence
    ScriptTask(
        "task_update_services_status",
        "Update Services Status",
        "Capture status of update-related services.",
        ("powershell", "-NoProfile", "-Command", "Get-Service -Name wuauserv,bits,cryptsvc,msiserver | Select-Object Name,Status,StartType | Format-Table -AutoSize"),
        25,
        "Safe",
        "updates",
        output_name="services_state",
    ),
    ScriptTask(
        "task_get_windows_update_log",
        "Windows Update Log",
        "Run Get-WindowsUpdateLog (best effort).",
        (
            "powershell",
            "-NoProfile",
            "-Command",
            "$p='{out}'; Get-WindowsUpdateLog -LogPath $p -ErrorAction Continue; if (Test-Path $p) { Get-Content $p -TotalCount 400 }",
        ),
        180,
        "Safe",
        "updates",
        output_name="windows_update_log",
    ),
    ScriptTask(
        "task_pending_reboot_sources",
        "Pending Reboot Sources",
        "Check common reboot-pending registry sources.",
        None,
        30,
        "Safe",
        "updates",
        output_name="pending_reboot",
        runner=_run_pending_reboot_sources,
    ),
    # Network evidence/remediation
    ScriptTask("task_ipconfig_all", "IP Config", "Collect full IP configuration.", ("ipconfig", "/all"), 30, "Safe", "network", output_name="ipconfig"),
    ScriptTask("task_route_print", "Route Print", "Show route table.", ("route", "print"), 25, "Safe", "network", output_name="route"),
    ScriptTask("task_proxy_show", "WinHTTP Proxy", "Show WinHTTP proxy settings.", ("netsh", "winhttp", "show", "proxy"), 20, "Safe", "network", output_name="proxy"),
    ScriptTask("task_dns_timing", "DNS Timing Test", "Measure DNS resolution timing across common domains.", None, 25, "Safe", "network", output_name="dns_timing", output_ext="csv", runner=_run_dns_timing),
    ScriptTask("task_hosts_check", "Hosts File Check", "Read-only check of active hosts file entries.", None, 20, "Safe", "network", output_name="hosts_check", runner=_run_hosts_check),
    ScriptTask("task_wlan_report", "WLAN Report", "Generate Wi-Fi WLAN report.", None, 45, "Safe", "network", output_name="wlan_report", runner=_run_wlan_report),
    ScriptTask("task_wifi_report_fix_wizard", "Wi-Fi Report + Fix Wizard", "Collect Wi-Fi evidence bundle, run safe DNS flush, and provide guided network links.", None, 120, "Safe", "network", output_name="wifi_summary", runner=_run_wifi_report_fix_wizard),
    ScriptTask("task_ping_1_1_1_1", "Ping Cloudflare", "Test external connectivity.", ("ping", "1.1.1.1", "-n", "4"), 18, "Safe", "network", output_name="ping_cloudflare"),
    ScriptTask("task_ping_8_8_8_8", "Ping Google DNS", "Test external connectivity to DNS.", ("ping", "8.8.8.8", "-n", "4"), 18, "Safe", "network", output_name="ping_google_dns"),
    ScriptTask("task_dns_flush", "Flush DNS", "Flush DNS resolver cache.", ("ipconfig", "/flushdns"), 30, "Safe", "network", output_name="flush_dns"),
    ScriptTask("task_ip_release_renew", "IP Release/Renew", "Release and renew IP lease.", None, 120, "Safe", "network", output_name="ip_release_renew", runner=_run_ip_release_renew),
    # Home-first guided helpers
    ScriptTask("task_browser_rescue", "Browser Rescue", "Diagnose browser issues safely with default-browser/version/proxy/DNS context and guided next steps.", None, 90, "Safe", "browser", output_name="browser_rescue_summary", runner=_run_browser_rescue),
    ScriptTask("task_app_crash_helper", "App Crash Helper", "Collect crash/hang evidence and provide deterministic app stability next steps.", None, 90, "Safe", "crash", output_name="crash_summary", runner=_run_app_crash_helper),
    ScriptTask("task_audio_mic_helper", "Audio & Mic Fix Helper", "Collect audio endpoint state and provide guided sound recovery steps.", None, 70, "Safe", "audio", output_name="audio_summary", runner=_run_audio_mic_helper),
    ScriptTask("task_camera_privacy_check", "Camera/Privacy Quick Check", "Review camera/mic privacy status and provide safe settings links.", None, 60, "Safe", "privacy", output_name="privacy_camera_mic_summary", runner=_run_camera_privacy_check),
    ScriptTask("task_onedrive_sync_helper", "OneDrive Sync Helper", "Collect OneDrive/process/network hints and provide guided sync recovery steps.", None, 70, "Safe", "cloud", output_name="onedrive_summary", runner=_run_onedrive_sync_helper),
    ScriptTask("task_usb_bt_disconnect_helper", "USB/Bluetooth Disconnect Helper", "Collect device disconnect hints and power guidance in read-only mode.", None, 80, "Safe", "devices", output_name="usb_bt_summary", runner=_run_usb_bt_disconnect_helper),
    ScriptTask("task_winsock_reset", "Winsock Reset", "Reset winsock catalog.", ("netsh", "winsock", "reset"), 45, "Admin", "network", admin_required=True, reboot_likely=True, output_name="winsock_reset"),
    ScriptTask("task_tcpip_reset", "TCP/IP Reset", "Reset TCP/IP stack.", ("netsh", "int", "ip", "reset"), 45, "Admin", "network", admin_required=True, reboot_likely=True, output_name="tcpip_reset"),
    # IT/MSP helpers
    ScriptTask("task_service_health_snapshot", "Service Health Snapshot", "Collect key service states and startup types for triage.", None, 45, "Safe", "services", output_name="services_health", runner=_run_service_health_snapshot),
    ScriptTask("task_driver_device_inventory_pack", "Driver / Device Inventory Pack", "Collect driver inventory, problem devices, and setup log excerpts.", None, 120, "Safe", "devices", output_name="devices_inventory", runner=_run_driver_device_inventory),
    ScriptTask("task_firewall_profile_summary", "Firewall/Profile Summary", "Read-only firewall profile state summary.", None, 45, "Safe", "security", output_name="firewall_summary", runner=_run_firewall_profile_summary),
    ScriptTask("task_wmi_repair_helper", "WMI Repair Helper", "Collect WMI health and provide cautious guided repair steps.", None, 70, "Admin", "wmi", admin_required=False, reboot_likely=False, output_name="wmi_health", runner=_run_wmi_repair_helper),
    ScriptTask("task_office_outlook_helper", "Office/Outlook Helper", "Collect Office/Outlook path and version basics (paths only).", None, 70, "Safe", "office", output_name="office_outlook_summary", runner=_run_office_outlook_helper),
    ScriptTask("task_eventlog_exporter_pack", "Event Log Exporter Pack", "Export core event logs bundle on demand.", None, 140, "Safe", "eventlogs", output_name="eventlogs_export_summary", runner=lambda t, c, d: _run_evidence_pack(t, c, d, ["task_evtx_application", "task_evtx_system", "task_evtx_setup", "task_evtx_windows_update", "task_evtx_printservice", "task_evtx_devicesetup"], "Event Log")),
    ScriptTask("task_system_profile_belarc_lite", "Belarc-lite System Profile", "Build local system profile summary (hardware/software/security best effort).", None, 80, "Safe", "system", output_name="system_profile_summary", runner=_run_system_profile),
    ScriptTask("task_startup_autostart_pack", "Startup/Autostart Pack", "Collect startup folders, Run keys, tasks, and optional autorunsc output.", None, 120, "Safe", "system", output_name="startup_pack_summary", runner=_run_startup_autostart_pack),
    ScriptTask("task_update_repair_evidence_pack", "Update Repair Evidence Pack", "Collect update services/reboot/log evidence bundle.", None, 200, "Safe", "updates", output_name="updates_pack_summary", runner=lambda t, c, d: _run_evidence_pack(t, c, d, ["task_update_services_status", "task_pending_reboot_sources", "task_get_windows_update_log", "task_evtx_windows_update"], "Update")),
    ScriptTask("task_network_evidence_pack", "Network Evidence Pack", "Collect network evidence bundle on demand.", None, 180, "Safe", "network", output_name="network_pack_summary", runner=lambda t, c, d: _run_evidence_pack(t, c, d, ["task_ipconfig_all", "task_route_print", "task_proxy_show", "task_hosts_check", "task_dns_timing", "task_wlan_report"], "Network")),
    ScriptTask("task_crash_evidence_pack", "Crash Evidence Pack", "Collect crash/reliability/minidump evidence bundle.", None, 180, "Safe", "crash", output_name="crash_pack_summary", runner=lambda t, c, d: _run_evidence_pack(t, c, d, ["task_reliability_snapshot", "task_minidumps_collect", "task_evtx_application", "task_evtx_system"], "Crash")),
    ScriptTask("task_network_stack_repair_tool", "Network Stack Repair Tool", "Admin repair chain: flush DNS, winsock/tcp reset, release/renew.", None, 420, "Admin", "repair", admin_required=True, reboot_likely=True, output_name="network_repair_summary", runner=lambda t, c, d: _run_admin_repair_chain(t, c, d, ["task_dns_flush", "task_winsock_reset", "task_tcpip_reset", "task_ip_release_renew"], "network_repair_log.txt", "Network Stack Repair")),
    ScriptTask("task_windows_update_reset_tool", "Windows Update Reset Tool", "Admin reset chain for Windows Update components.", None, 420, "Admin", "repair", admin_required=True, reboot_likely=True, output_name="update_reset_summary", runner=lambda t, c, d: _run_admin_repair_chain(t, c, d, ["task_update_services_status", "task_reset_update_components", "task_update_services_status"], "update_reset_log.txt", "Windows Update Reset")),
    ScriptTask("task_sfc_dism_integrity_tool", "SFC/DISM Integrity Tool", "Admin integrity chain using SFC and DISM.", None, 3600, "Admin", "repair", admin_required=True, reboot_likely=True, output_name="integrity_repair_summary", runner=lambda t, c, d: _run_admin_repair_chain(t, c, d, ["task_sfc_scannow", "task_dism_restorehealth"], "integrity_log.txt", "SFC/DISM Integrity")),
    ScriptTask("task_printer_full_reset_tool", "Printer Full Reset Tool", "Admin printer repair chain with spooler restart and optional clear spool folder.", None, 420, "Admin", "repair", admin_required=True, reboot_likely=False, output_name="printer_reset_summary", runner=lambda t, c, d: _run_admin_repair_chain(t, c, d, ["task_restart_spooler", "task_clear_spool_folder", "task_printer_status"], "printer_reset_log.txt", "Printer Full Reset")),
    ScriptTask("task_smart_snapshot", "SMART Snapshot + Warnings", "Collect disk SMART/health hints best effort.", None, 70, "Safe", "hardware", output_name="smart_summary", runner=_run_smart_snapshot),
    ScriptTask("task_thermal_hints", "Thermal/Throttle Hints", "Collect thermal and throttle hints best effort.", None, 60, "Safe", "hardware", output_name="thermal_summary", runner=_run_thermal_hints),
    # Printer remediations/evidence
    ScriptTask("task_printer_status", "Printer Status", "Collect spooler state, queue snapshot, and PrintService event export.", None, 45, "Safe", "printer", output_name="printer_summary", runner=_run_printer_status),
    ScriptTask("task_restart_spooler", "Restart Print Spooler", "Restart print spooler service.", ("powershell", "-NoProfile", "-Command", "Restart-Service Spooler -Force"), 40, "Admin", "printer", admin_required=True, output_name="restart_spooler"),
    ScriptTask(
        "task_clear_spool_folder",
        "Clear Spool Folder",
        "Delete pending spool files (admin, explicit warning required).",
        None,
        60,
        "Admin",
        "printer",
        admin_required=True,
        reboot_likely=False,
        output_name="clear_spool_folder",
        runner=_run_clear_spool_folder,
    ),
    # Integrity and updates remediations
    ScriptTask("task_sfc_scannow", "SFC Scan", "Run system file checker scan.", ("sfc", "/scannow"), 1800, "Admin", "integrity", admin_required=True, output_name="sfc_scannow"),
    ScriptTask("task_dism_restorehealth", "DISM RestoreHealth", "Run DISM restore health scan.", ("dism", "/online", "/cleanup-image", "/restorehealth"), 2400, "Admin", "integrity", admin_required=True, output_name="dism_restorehealth"),
    ScriptTask("task_reset_update_components", "Reset Update Components", "Reset Windows Update components (admin).", None, 300, "Admin", "updates", admin_required=True, reboot_likely=True, output_name="reset_update_components", runner=_run_reset_update_components),
    # Crash evidence
    ScriptTask("task_reliability_snapshot", "Reliability Snapshot", "Collect reliability-focused event excerpt.", ("powershell", "-NoProfile", "-Command", "Get-WinEvent -LogName Application -MaxEvents 80 | Select-Object TimeCreated,Id,LevelDisplayName,ProviderName,Message | ConvertTo-Json -Depth 4"), 50, "Safe", "crash", output_name="reliability_snapshot"),
    ScriptTask("task_minidumps_collect", "Minidumps Collect", "Copy recent minidumps if present.", None, 30, "Safe", "crash", output_name="minidumps", runner=_run_minidump_copy),
    # Backward compatibility from prior runbooks
    ScriptTask("task_arp_a", "ARP Table", "Show ARP cache.", ("arp", "-a"), 20, "Safe", "network", output_name="arp_table"),
    ScriptTask("task_netstat", "Netstat", "Show active connections.", ("netstat", "-ano"), 30, "Safe", "network", output_name="netstat"),
    ScriptTask("task_tasklist", "Tasklist", "List running processes.", ("tasklist",), 30, "Safe", "performance", output_name="tasklist"),
    ScriptTask("task_whoami", "WhoAmI", "Print current user context.", ("whoami",), 10, "Safe", "system", output_name="whoami"),
    ScriptTask("task_systeminfo", "System Info", "Collect systeminfo output.", ("systeminfo",), 60, "Safe", "system", output_name="systeminfo_legacy"),
    ScriptTask("task_driverquery", "Driver Query", "Collect loaded drivers.", ("driverquery",), 60, "Safe", "system", output_name="driverquery"),
    ScriptTask("task_sc_query", "Service Query", "List running services.", ("sc", "query", "type=", "service", "state=", "all"), 45, "Safe", "system", output_name="sc_query"),
    ScriptTask("task_powercfg_a", "Power States", "Show available power sleep states.", ("powercfg", "/a"), 20, "Safe", "system", output_name="power_states"),
    ScriptTask("task_powercfg_battery", "Battery Report", "Generate battery report.", ("powercfg", "/batteryreport"), 30, "Safe", "system", output_name="battery_report"),
    ScriptTask("task_wevtutil_system", "System Events", "Read recent system events.", ("wevtutil", "qe", "System", "/c:30", "/f:text"), 25, "Safe", "eventlogs", output_name="system_events_text"),
    ScriptTask("task_wevtutil_application", "App Events", "Read recent app events.", ("wevtutil", "qe", "Application", "/c:30", "/f:text"), 25, "Safe", "eventlogs", output_name="application_events_text"),
    ScriptTask("task_nslookup_microsoft", "NSLookup", "Resolve microsoft.com.", ("nslookup", "microsoft.com"), 15, "Safe", "network", output_name="nslookup_microsoft"),
    ScriptTask("task_print_queue", "Print Queue", "List print jobs.", ("powershell", "-NoProfile", "-Command", "Get-PrintJob -ErrorAction SilentlyContinue"), 20, "Safe", "printer", output_name="print_queue"),
    ScriptTask("task_proxy_settings", "Proxy Settings", "Read internet proxy settings.", ("powershell", "-NoProfile", "-Command", "Get-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings' | Format-List Proxy*"), 20, "Safe", "network", output_name="proxy_settings"),
    ScriptTask("task_hosts_preview", "Hosts Preview", "Read hosts file entries.", ("powershell", "-NoProfile", "-Command", "Get-Content C:\\Windows\\System32\\drivers\\etc\\hosts"), 20, "Safe", "network", output_name="hosts_preview"),
    # Additional analysis tasks for runbooks
    ScriptTask("task_large_file_radar", "Large File Radar Preview", "Preview largest files (no delete).", None, 120, "Safe", "evidence", output_name="large_files_summary", runner=_run_large_file_radar),
    ScriptTask("task_storage_radar", "Storage Radar (WizTree-lite)", "Rank top folders with bounded scan budget plus downloads cleanup preview plan.", None, 120, "Safe", "storage", output_name="storage_radar_summary", runner=_run_storage_radar),
    ScriptTask("task_downloads_cleanup_buckets", "Downloads Cleanup Buckets", "Bucket old downloads (preview only).", None, 90, "Safe", "evidence", output_name="downloads_summary", runner=_run_downloads_cleanup_buckets),
    ScriptTask("task_fast_file_search", "Fast File Search (Everything-lite)", "Build local index with incremental updates and export quick search results.", None, 120, "Safe", "storage", output_name="fast_file_search_summary", runner=_run_fast_file_search),
    ScriptTask("task_appdata_bloat_scanner", "AppData Bloat Scanner", "Scan AppData top folders by size (preview only).", None, 120, "Safe", "storage", output_name="appdata_summary", runner=_run_appdata_bloat_scanner),
    ScriptTask("task_storage_ranked_view", "Storage Ranked View", "Generate ranked folder usage view (fast, cancellable).", None, 45, "Safe", "evidence", output_name="storage_ranked_view", runner=_run_storage_ranked_view),
    ScriptTask("task_duplicate_hash_scan", "Duplicate Hash Scan (Pro)", "Exact hash duplicate scan (preview only, recycle-bin delete policy).", None, 240, "Advanced", "evidence", output_name="duplicates_summary", runner=_run_duplicate_hash_scan),
    ScriptTask("task_performance_sample", "Performance Sample Window", "Sample top CPU/RAM processes over 10-20s and attach startup inventory (read-only).", None, 90, "Safe", "performance", output_name="perf_summary", runner=_run_performance_sample),
)


def script_task_map() -> dict[str, ScriptTask]:
    return {task.id: task for task in SCRIPT_TASKS}


def list_script_tasks() -> list[ScriptTask]:
    return list(SCRIPT_TASKS)


def create_restore_point(
    cancel_event: Event | None = None,
    timeout_s: int = 70,
    log_cb: Callable[[str], None] | None = None,
) -> dict[str, Any]:
    return _run_create_restore_point(cancel_event=cancel_event, timeout_s=timeout_s, log_cb=log_cb)


def run_script_task(
    task_id: str,
    dry_run: bool = False,
    *,
    output_dir: str | Path | None = None,
    mask_options: MaskingOptions | None = None,
    cancel_event: Event | None = None,
    timeout_override_s: int | None = None,
    log_cb: Callable[[str], None] | None = None,
    run_event_bus: RunEventBus | None = None,
    run_id: str = "",
) -> dict[str, Any]:
    mapping = script_task_map()
    if task_id not in mapping:
        raise KeyError(task_id)
    task = mapping[task_id]
    effective = task if timeout_override_s is None else replace(task, timeout_s=int(max(1, timeout_override_s)))
    preflight = {
        "can_run": not (effective.admin_required and (not dry_run) and (not is_admin())),
        "admin_required": bool(effective.admin_required),
        "reason": "" if (not effective.admin_required or dry_run or is_admin()) else "Administrator privileges are required.",
        "os_supported": True,
    }
    context = ScriptTaskContext(
        output_dir=Path(output_dir) if output_dir is not None else None,
        mask_options=mask_options,
        cancel_event=cancel_event,
        log_cb=log_cb,
        run_event_bus=run_event_bus,
        run_id=str(run_id or "").strip(),
    )
    _publish_event(
        context,
        RunEventType.START,
        f"Script task started: {effective.title}",
        data={"task_id": effective.id, "category": effective.category, "dry_run": dry_run},
    )
    _publish_event(context, RunEventType.STATUS, f"Script task running: {effective.title}")
    if effective.runner is not None:
        result = effective.runner(effective, context, dry_run)
    else:
        result = _run_command_task(effective, context, dry_run)
    final = _finalize_task_result(effective, result, context)
    final.setdefault("preflight", preflight)
    output_files = final.get("output_files", [])
    if isinstance(output_files, list):
        for path in output_files:
            if path:
                _publish_event(context, RunEventType.ARTIFACT, f"Artifact: {path}", data={"path": str(path)})
    code = int(final.get("code", 0 if final.get("dry_run") else 1))
    if code != 0:
        _publish_event(context, RunEventType.ERROR, str(final.get("user_message", "Script task failed.")))
        _publish_event(context, RunEventType.STATUS, f"Script task failed with code {code}.")
    else:
        _publish_event(context, RunEventType.STATUS, "Script task completed successfully.")
    _publish_event(context, RunEventType.END, f"Script task finished with code {code}.", data={"code": code})
    return final
