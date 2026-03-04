from __future__ import annotations

import hashlib
import os
import platform
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import psutil

from .command_runner import run_command
from .settings import AppSettings
from .utils import is_windows


@dataclass
class Finding:
    title: str
    status: str
    detail: str
    confidence: int = 80
    category: str = "General"
    plain: str = ""
    technical: str = ""


def _pct(value: float) -> str:
    return f"{value:.0f}%"


def _hostname() -> str:
    return os.environ.get("COMPUTERNAME", platform.node())


def _username() -> str:
    return os.environ.get("USERNAME") or os.environ.get("USER") or ""


def _safe_path(path: str) -> str:
    return str(Path(path))


def _scan_file_sizes(root: Path, limit: int = 40, min_mb: int = 128) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    min_bytes = min_mb * 1024 * 1024
    seen = 0
    for path in root.rglob("*"):
        if seen > 5000:
            break
        seen += 1
        if not path.is_file():
            continue
        try:
            size = path.stat().st_size
        except OSError:
            continue
        if size < min_bytes:
            continue
        rows.append({"path": str(path), "size_bytes": size})
    rows.sort(key=lambda x: x["size_bytes"], reverse=True)
    return rows[:limit]


def _folder_size(path: Path, file_limit: int = 2500) -> int:
    total = 0
    count = 0
    for file in path.rglob("*"):
        if count > file_limit:
            break
        count += 1
        if not file.is_file():
            continue
        try:
            total += file.stat().st_size
        except OSError:
            continue
    return total


def quick_check(include_capabilities: bool = True) -> dict[str, Any]:
    now_utc = datetime.now(timezone.utc).isoformat()
    sysinfo = {
        "timestamp_utc": now_utc,
        "platform": platform.platform(),
        "machine": platform.machine(),
        "processor": platform.processor(),
        "python": platform.python_version(),
        "hostname": _hostname(),
        "user": _username(),
    }

    cpu = psutil.cpu_percent(interval=0.4)
    mem = psutil.virtual_memory()
    disk = psutil.disk_usage("C:\\" if is_windows() else "/")
    findings: list[Finding] = []

    findings.append(
        Finding(
            title="CPU usage",
            status="WARN" if cpu >= 90 else "OK",
            detail=f"CPU usage is {_pct(cpu)}.",
            confidence=90,
            category="Performance",
            plain=f"CPU is currently at {_pct(cpu)}.",
            technical=f"cpu_percent={cpu}",
        )
    )
    findings.append(
        Finding(
            title="Memory usage",
            status="WARN" if mem.percent >= 85 else "OK",
            detail=f"Memory usage is {_pct(mem.percent)}.",
            confidence=90,
            category="Performance",
            plain=f"Memory pressure is {_pct(mem.percent)}.",
            technical=f"mem_percent={mem.percent}",
        )
    )

    free_gb = disk.free / (1024**3)
    total_gb = disk.total / (1024**3)
    disk_status = "CRIT" if free_gb < 10 else ("WARN" if free_gb < 25 else "OK")
    findings.append(
        Finding(
            title="Disk space",
            status=disk_status,
            detail=f"{free_gb:.1f} GB free of {total_gb:.1f} GB.",
            confidence=90,
            category="Storage",
            plain=f"Disk has {free_gb:.1f} GB free.",
            technical=f"disk_free_gb={free_gb:.2f} disk_total_gb={total_gb:.2f}",
        )
    )

    if is_windows():
        reboot = run_command(
            [
                "powershell",
                "-NoProfile",
                "-Command",
                r"Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending'",
            ],
            timeout_s=8,
        )
        pending = reboot.code == 0 and reboot.stdout.strip().lower() == "true"
        findings.append(
            Finding(
                title="Reboot status",
                status="INFO" if pending else "OK",
                detail="Windows indicates pending reboot." if pending else "No reboot pending flag detected.",
                confidence=75,
                category="Updates",
                plain="A reboot may be pending after updates." if pending else "No reboot pending signal detected.",
                technical=reboot.combined,
            )
        )

    network = proxy_and_hosts_alert()
    if network.get("proxy_enabled"):
        findings.append(
            Finding(
                title="Proxy enabled",
                status="WARN",
                detail="System proxy is enabled.",
                confidence=80,
                category="Network",
                plain="Proxy is enabled and may affect connectivity.",
                technical=json_dump(network),
            )
        )

    top_processes = []
    for proc in psutil.process_iter(attrs=["name", "cpu_percent", "memory_percent"]):
        try:
            top_processes.append(proc.info)
        except (psutil.NoSuchProcess, psutil.AccessDenied):
            continue
    top_processes = sorted(top_processes, key=lambda x: (x.get("cpu_percent", 0), x.get("memory_percent", 0)), reverse=True)[:8]

    payload = {
        "sysinfo": sysinfo,
        "metrics": {
            "cpu_percent": cpu,
            "mem_percent": mem.percent,
            "disk_free_gb": free_gb,
            "disk_total_gb": total_gb,
        },
        "network": network,
        "findings": [asdict(f) for f in findings],
        "top_processes": top_processes,
    }
    if include_capabilities:
        payload["capability_results"] = {
            "storage_ranked_view": storage_ranked_view(),
            "large_file_radar": large_file_radar(),
            "downloads_cleanup_assistant": downloads_cleanup_assistant(),
            "disk_health_snapshot": disk_health_snapshot(),
            "reliability_snapshot": reliability_snapshot(),
            "problem_devices": problem_devices(),
        }
    else:
        payload["capability_results"] = {}
    return payload


def json_dump(value: Any) -> str:
    import json

    return json.dumps(value, indent=2, ensure_ascii=False)


def storage_ranked_view() -> dict[str, Any]:
    home = Path.home()
    targets = [home / "Downloads", home / "Desktop", home / "Documents", home / "Pictures", home / "Videos"]
    rows = []
    for folder in targets:
        if not folder.exists():
            continue
        size = _folder_size(folder)
        rows.append({"folder": str(folder), "size_gb": round(size / (1024**3), 3)})
    rows.sort(key=lambda x: x["size_gb"], reverse=True)
    return {"mode": "ranked", "rows": rows}


def large_file_radar(min_size_mb: int = 256, limit: int = 40, root: str | None = None) -> dict[str, Any]:
    target = Path(root) if root else Path.home()
    files = _scan_file_sizes(target, limit=limit, min_mb=min_size_mb)
    return {"root": str(target), "min_size_mb": min_size_mb, "files": files}


def downloads_cleanup_assistant(days_old: int = 30) -> dict[str, Any]:
    downloads = Path.home() / "Downloads"
    now = datetime.now()
    previews = []
    if downloads.exists():
        for file in downloads.iterdir():
            if not file.is_file():
                continue
            try:
                age_days = (now - datetime.fromtimestamp(file.stat().st_mtime)).days
                if age_days >= days_old:
                    previews.append(
                        {
                            "path": str(file),
                            "size_mb": round(file.stat().st_size / (1024**2), 2),
                            "age_days": age_days,
                        }
                    )
            except OSError:
                continue
    previews.sort(key=lambda x: x["size_mb"], reverse=True)
    return {
        "folder": str(downloads),
        "days_old": days_old,
        "preview_only": True,
        "candidates": previews[:150],
    }


def duplicate_finder_exact_hash(root: str | None = None, min_size_mb: int = 20, max_files: int = 300) -> dict[str, Any]:
    target = Path(root) if root else (Path.home() / "Downloads")
    candidates = []
    min_bytes = min_size_mb * 1024 * 1024
    for path in target.rglob("*"):
        if len(candidates) >= max_files:
            break
        if not path.is_file():
            continue
        try:
            if path.stat().st_size < min_bytes:
                continue
            candidates.append(path)
        except OSError:
            continue

    hash_map: dict[str, list[str]] = {}
    for file in candidates:
        h = hashlib.sha256()
        try:
            with file.open("rb") as f:
                while chunk := f.read(65536):
                    h.update(chunk)
        except OSError:
            continue
        digest = h.hexdigest()
        hash_map.setdefault(digest, []).append(str(file))

    dupes = [{"sha256": k, "files": v, "count": len(v)} for k, v in hash_map.items() if len(v) > 1]
    dupes.sort(key=lambda x: x["count"], reverse=True)
    return {"root": str(target), "preview_only": True, "delete_mode": "recycle", "groups": dupes}


def uninstall_leftover_folders() -> dict[str, Any]:
    roots = [
        Path.home() / "AppData" / "Local",
        Path.home() / "AppData" / "Roaming",
        Path("C:/Program Files"),
        Path("C:/Program Files (x86)"),
    ]
    candidates = []
    for root in roots:
        if not root.exists():
            continue
        for child in root.iterdir():
            if not child.is_dir():
                continue
            name = child.name.lower()
            if any(token in name for token in ("old", "backup", "leftover", "unused")):
                candidates.append({"path": str(child), "reason": "name heuristic"})
    return {"preview_only": True, "registry_edit": False, "candidates": candidates[:120]}


def disk_health_snapshot() -> dict[str, Any]:
    if not is_windows():
        return {"supported": False, "detail": "Windows-only"}
    cmd = run_command(
        ["powershell", "-NoProfile", "-Command", "Get-PhysicalDisk | Select-Object FriendlyName,HealthStatus,OperationalStatus | ConvertTo-Json -Depth 3"],
        timeout_s=12,
    )
    return {
        "supported": cmd.code == 0,
        "code": cmd.code,
        "output": cmd.combined,
    }


def battery_report() -> dict[str, Any]:
    if not is_windows():
        return {"supported": False, "detail": "Windows-only"}
    out = Path.home() / "battery_report_fixfox.html"
    cmd = run_command(
        [
            "powercfg",
            "/batteryreport",
            "/output",
            str(out),
        ],
        timeout_s=20,
    )
    return {"code": cmd.code, "path": str(out), "output": cmd.combined, "pro": True}


def reliability_snapshot() -> dict[str, Any]:
    if not is_windows():
        return {"supported": False, "detail": "Windows-only"}
    cmd = run_command(
        [
            "powershell",
            "-NoProfile",
            "-Command",
            "Get-WinEvent -LogName Application -MaxEvents 30 | Select-Object TimeCreated,Id,LevelDisplayName,ProviderName,Message | ConvertTo-Json -Depth 4",
        ],
        timeout_s=16,
    )
    return {"code": cmd.code, "output": cmd.combined}


def problem_devices() -> dict[str, Any]:
    if not is_windows():
        return {"supported": False, "detail": "Windows-only"}
    cmd = run_command(
        [
            "powershell",
            "-NoProfile",
            "-Command",
            "Get-PnpDevice -Status Error | Select-Object Class,FriendlyName,InstanceId,Status | ConvertTo-Json -Depth 3",
        ],
        timeout_s=16,
    )
    return {"code": cmd.code, "output": cmd.combined}


def proxy_and_hosts_alert() -> dict[str, Any]:
    hosts = Path(r"C:\Windows\System32\drivers\etc\hosts")
    hosts_modified = False
    hosts_lines = 0
    if hosts.exists():
        try:
            lines = hosts.read_text(encoding="utf-8", errors="ignore").splitlines()
            content = [line for line in lines if line.strip() and not line.strip().startswith("#")]
            hosts_lines = len(content)
            hosts_modified = hosts_lines > 0
        except OSError:
            ...

    proxy_enabled = False
    proxy_server = ""
    if is_windows():
        cmd = run_command(
            [
                "powershell",
                "-NoProfile",
                "-Command",
                "Get-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings' | Select-Object ProxyEnable,ProxyServer | ConvertTo-Json",
            ],
            timeout_s=8,
        )
        if cmd.code == 0:
            payload = cmd.combined.lower()
            proxy_enabled = '"proxyenable":  1' in payload or '"proxyenable":1' in payload
            proxy_server = cmd.combined

    return {
        "proxy_enabled": proxy_enabled,
        "proxy_details": proxy_server,
        "hosts_modified": hosts_modified,
        "hosts_entries": hosts_lines,
    }


def integrity_pack(run_dism: bool = False) -> dict[str, Any]:
    if not is_windows():
        return {"supported": False, "detail": "Windows-only"}
    sfc = run_command(["sfc", "/verifyonly"], timeout_s=120)
    payload: dict[str, Any] = {
        "supported": True,
        "sfc_code": sfc.code,
        "sfc_output": sfc.combined,
        "dism_ran": False,
        "dism_code": 0,
        "dism_output": "",
    }
    if run_dism:
        dism = run_command(["dism", "/online", "/cleanup-image", "/scanhealth"], timeout_s=240)
        payload["dism_ran"] = True
        payload["dism_code"] = dism.code
        payload["dism_output"] = dism.combined
    return payload


def updates_health_pack() -> dict[str, Any]:
    services = run_command(
        [
            "powershell",
            "-NoProfile",
            "-Command",
            "Get-Service -Name wuauserv,bits,cryptsvc,msiserver | Select-Object Name,Status,StartType | ConvertTo-Json -Depth 3",
        ],
        timeout_s=20,
    )
    pending = False
    if is_windows():
        pending_cmd = run_command(
            [
                "powershell",
                "-NoProfile",
                "-Command",
                r"Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending'",
            ],
            timeout_s=8,
        )
        pending = pending_cmd.code == 0 and pending_cmd.stdout.strip().lower() == "true"
    return {
        "services_code": services.code,
        "services_output": services.combined,
        "pending_reboot": pending,
    }


def printer_pack() -> dict[str, Any]:
    if not is_windows():
        return {"supported": False, "detail": "Windows-only"}
    status = run_command(["powershell", "-NoProfile", "-Command", "Get-Service Spooler | Select-Object Name,Status,StartType | ConvertTo-Json"], timeout_s=12)
    events = run_command(
        [
            "powershell",
            "-NoProfile",
            "-Command",
            "Get-WinEvent -LogName 'Microsoft-Windows-PrintService/Operational' -MaxEvents 20 | Select-Object TimeCreated,Id,LevelDisplayName,Message | ConvertTo-Json -Depth 4",
        ],
        timeout_s=20,
    )
    return {"status_code": status.code, "status_output": status.combined, "events_code": events.code, "events_output": events.combined}


def network_pack() -> dict[str, Any]:
    dns = run_command(["nslookup", "microsoft.com"], timeout_s=10)
    ping = run_command(["ping", "1.1.1.1", "-n", "2"], timeout_s=10)
    proxy = run_command(["netsh", "winhttp", "show", "proxy"], timeout_s=10)
    return {
        "dns_code": dns.code,
        "dns_output": dns.combined,
        "ping_code": ping.code,
        "ping_output": ping.combined,
        "proxy_code": proxy.code,
        "proxy_output": proxy.combined,
        "proxy_hosts_alert": proxy_and_hosts_alert(),
    }


def performance_pack(sample_window_s: int = 12) -> dict[str, Any]:
    if not is_windows():
        return {"supported": True, "window_s": sample_window_s, "samples": []}
    rounds = max(3, min(8, sample_window_s // 2))
    rows: list[dict[str, Any]] = []
    for tick in range(rounds):
        procs = []
        for proc in psutil.process_iter(attrs=["name", "cpu_percent", "memory_percent"]):
            try:
                info = proc.info
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                continue
            procs.append(info)
        top = sorted(procs, key=lambda x: (x.get("cpu_percent", 0), x.get("memory_percent", 0)), reverse=True)[:6]
        rows.append({"tick": tick + 1, "top": top})
    return {"supported": True, "window_s": sample_window_s, "samples": rows}


def weekly_check_status(settings: AppSettings) -> dict[str, Any]:
    return {
        "enabled": settings.weekly_reminder_enabled,
        "day": settings.weekly_reminder_day,
        "last_weekly_check_utc": settings.last_weekly_check_utc,
    }
