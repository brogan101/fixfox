from __future__ import annotations

import os
from dataclasses import dataclass, asdict
from threading import Event
from typing import Any, Callable

from .brand import APP_DISPLAY_NAME
from .command_runner import run_command
from .safety import SafetyPolicy, can_execute_fix
from .utils import is_admin, open_uri


@dataclass
class FixAction:
    key: str
    title: str
    risk: str
    description: str
    plain: str
    rollback: str
    commands: list[str]
    admin_required: bool
    reversible: bool
    runner: Callable[..., tuple[int, str]]

    def as_dict(self) -> dict[str, Any]:
        return asdict(self)


def _stderr_cb(log_cb: Callable[[str], None] | None) -> Callable[[str], None] | None:
    if log_cb is None:
        return None
    return lambda line: log_cb(f"[stderr] {line}")


def _open_storage_settings(**_kwargs: Any) -> tuple[int, str]:
    return open_uri("ms-settings:storagesense")


def _open_network_status(**_kwargs: Any) -> tuple[int, str]:
    return open_uri("ms-settings:network-status")


def _open_get_help(**_kwargs: Any) -> tuple[int, str]:
    return open_uri("ms-contact-support:")


def _flush_dns(log_cb: Callable[[str], None] | None = None, cancel_event: Event | None = None, **_kwargs: Any) -> tuple[int, str]:
    result = run_command(
        ["ipconfig", "/flushdns"],
        timeout_s=20,
        cancel_event=cancel_event,
        on_stdout_line=log_cb,
        on_stderr_line=_stderr_cb(log_cb),
    )
    return result.code, result.combined


def _restart_spooler(log_cb: Callable[[str], None] | None = None, cancel_event: Event | None = None, **_kwargs: Any) -> tuple[int, str]:
    if not is_admin():
        return 1, "Administrator privileges are required."
    result = run_command(
        ["powershell", "-NoProfile", "-Command", "Restart-Service Spooler -Force"],
        timeout_s=30,
        cancel_event=cancel_event,
        on_stdout_line=log_cb,
        on_stderr_line=_stderr_cb(log_cb),
    )
    return result.code, result.combined


def _startup_key_name() -> str:
    return "FixFox"


def _startup_enable(log_cb: Callable[[str], None] | None = None, cancel_event: Event | None = None, **_kwargs: Any) -> tuple[int, str]:
    python = os.environ.get("PYTHON_EXE") or "python"
    cmd = (
        "New-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run' "
        f"-Name '{_startup_key_name()}' -Value '{python} -m src.app' -PropertyType String -Force"
    )
    result = run_command(
        ["powershell", "-NoProfile", "-Command", cmd],
        timeout_s=10,
        cancel_event=cancel_event,
        on_stdout_line=log_cb,
        on_stderr_line=_stderr_cb(log_cb),
    )
    return result.code, result.combined or "Startup entry enabled."


def _startup_disable(log_cb: Callable[[str], None] | None = None, cancel_event: Event | None = None, **_kwargs: Any) -> tuple[int, str]:
    cmd = (
        "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run' "
        f"-Name '{_startup_key_name()}' -ErrorAction SilentlyContinue"
    )
    result = run_command(
        ["powershell", "-NoProfile", "-Command", cmd],
        timeout_s=10,
        cancel_event=cancel_event,
        on_stdout_line=log_cb,
        on_stderr_line=_stderr_cb(log_cb),
    )
    return result.code, result.combined or "Startup entry removed."


FIX_CATALOG: list[FixAction] = [
    FixAction(
        key="open_storage",
        title="Open Storage Settings",
        risk="Safe",
        description="Open Windows storage settings to review large categories.",
        plain="This opens storage settings so you can clean up safely.",
        rollback="No system changes are made by this app action.",
        commands=["start ms-settings:storagesense"],
        admin_required=False,
        reversible=True,
        runner=_open_storage_settings,
    ),
    FixAction(
        key="flush_dns",
        title="Flush DNS Cache",
        risk="Safe",
        description="Clear cached DNS entries for network troubleshooting.",
        plain="This refreshes DNS so stale address lookups are removed.",
        rollback="Automatic repopulation occurs as you browse.",
        commands=["ipconfig /flushdns"],
        admin_required=False,
        reversible=True,
        runner=_flush_dns,
    ),
    FixAction(
        key="open_network",
        title="Open Network Status",
        risk="Safe",
        description="Open Windows network status and troubleshooting entry points.",
        plain="This opens built-in network diagnostics.",
        rollback="No system changes are made by this app action.",
        commands=["start ms-settings:network-status"],
        admin_required=False,
        reversible=True,
        runner=_open_network_status,
    ),
    FixAction(
        key="open_gethelp",
        title="Open Get Help",
        risk="Safe",
        description="Launch Microsoft Get Help troubleshooters.",
        plain="This opens Microsoft's guided troubleshooting app.",
        rollback="No system changes are made by this app action.",
        commands=["start ms-contact-support:"],
        admin_required=False,
        reversible=True,
        runner=_open_get_help,
    ),
    FixAction(
        key="restart_spooler",
        title="Restart Print Spooler",
        risk="Admin",
        description="Restart print spooler service (admin required).",
        plain="This restarts printer queue services for stuck jobs.",
        rollback="Service will auto-restart; if issues persist reboot.",
        commands=["Restart-Service Spooler -Force"],
        admin_required=True,
        reversible=True,
        runner=_restart_spooler,
    ),
    FixAction(
        key="startup_enable",
        title="Enable Startup Launch",
        risk="Safe",
        description=f"Add {APP_DISPLAY_NAME} startup entry for current user.",
        plain=f"This lets {APP_DISPLAY_NAME} launch when you sign in.",
        rollback="Use 'Disable Startup Launch' in Undo Center.",
        commands=[rf"HKCU\Software\Microsoft\Windows\CurrentVersion\Run -> {_startup_key_name()}"],
        admin_required=False,
        reversible=True,
        runner=_startup_enable,
    ),
    FixAction(
        key="startup_disable",
        title="Disable Startup Launch",
        risk="Safe",
        description=f"Remove {APP_DISPLAY_NAME} startup entry for current user.",
        plain=f"This stops {APP_DISPLAY_NAME} from launching on sign-in.",
        rollback="Use 'Enable Startup Launch' to restore.",
        commands=[rf"Remove HKCU\Software\Microsoft\Windows\CurrentVersion\Run\{_startup_key_name()}"],
        admin_required=False,
        reversible=True,
        runner=_startup_disable,
    ),
]


FIXES = FIX_CATALOG


def get_fix(key: str) -> FixAction:
    for fix in FIX_CATALOG:
        if fix.key == key:
            return fix
    raise KeyError(key)


def list_fixes(policy: SafetyPolicy) -> list[FixAction]:
    rows: list[FixAction] = []
    for fix in FIX_CATALOG:
        if fix.risk == "Admin" and not policy.show_admin:
            continue
        if fix.risk == "Advanced" and not policy.show_advanced:
            continue
        rows.append(fix)
    return rows


def run_fix(
    key: str,
    policy: SafetyPolicy,
    require_admin_confirmation: bool = False,
    reboot_warning_ack: bool = False,
    *,
    log_cb: Callable[[str], None] | None = None,
    cancel_event: Event | None = None,
) -> tuple[int, str]:
    fix = get_fix(key)
    if not can_execute_fix(fix.risk, policy):
        return 1, "Blocked by safety policy or diagnostic mode."
    if fix.admin_required and not is_admin():
        return 1, "Administrator privileges are required."
    if fix.admin_required and require_admin_confirmation and not reboot_warning_ack:
        return 1, "Admin run requires reboot warning acknowledgement."
    return fix.runner(log_cb=log_cb, cancel_event=cancel_event)
