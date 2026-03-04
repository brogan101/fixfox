from __future__ import annotations

from dataclasses import dataclass
import os
import subprocess

from .utils import open_uri


@dataclass(frozen=True)
class ToolItem:
    id: str
    title: str
    desc: str
    category: str
    command: str
    plain: str


TOOL_DIRECTORY: tuple[ToolItem, ...] = (
    ToolItem("tool_storage", "Storage Settings", "Open storage settings.", "evidence", "ms-settings:storagesense", "Open built-in storage cleanup settings."),
    ToolItem("tool_network", "Network Status", "Open network status page.", "network", "ms-settings:network-status", "Open network diagnostics entry points."),
    ToolItem("tool_windows_update", "Windows Update", "Open update settings.", "updates", "ms-settings:windowsupdate", "Open update settings and status."),
    ToolItem("tool_apps", "Installed Apps", "Open apps list.", "evidence", "ms-settings:appsfeatures", "Review installed applications."),
    ToolItem("tool_device_manager", "Device Manager", "Open Device Manager.", "integrity", "devmgmt.msc", "Inspect driver and hardware errors."),
    ToolItem("tool_event_viewer", "Event Viewer", "Open Event Viewer.", "evidence", "eventvwr.msc", "Inspect system and application logs."),
    ToolItem("tool_services", "Services", "Open Services MMC.", "printer", "services.msc", "Inspect and restart services."),
    ToolItem("tool_reliability", "Reliability Monitor", "Open reliability monitor.", "integrity", "perfmon /rel", "Review stability timeline."),
    ToolItem("tool_get_help", "Get Help", "Open Microsoft Get Help.", "network", "ms-contact-support:", "Launch guided troubleshooting."),
    ToolItem("tool_feedback", "Feedback Hub", "Open Feedback Hub.", "evidence", "feedback-hub:", "Open Windows feedback tool."),
    ToolItem("tool_print_mgmt", "Print Management", "Open print management.", "printer", "printmanagement.msc", "Inspect printers and queues."),
    ToolItem("tool_cmd_admin", "Terminal (Admin Prompt)", "Open elevated terminal prompt manually.", "integrity", "powershell", "Run advanced commands with admin consent."),
)


TOP_TOOLS: tuple[ToolItem, ...] = TOOL_DIRECTORY[:10]


def search_tools(query: str) -> list[ToolItem]:
    q = query.strip().lower()
    if not q:
        return list(TOOL_DIRECTORY)
    return [tool for tool in TOOL_DIRECTORY if q in tool.title.lower() or q in tool.desc.lower() or q in tool.category.lower()]


def tool_map() -> dict[str, ToolItem]:
    return {row.id: row for row in TOOL_DIRECTORY}


def launch_tool(tool_id: str, *, dry_run: bool = False) -> tuple[int, str]:
    tool = tool_map().get(str(tool_id or "").strip())
    if tool is None:
        return 2, f"Unknown tool id: {tool_id}"
    if dry_run:
        return 0, f"[dry-run] Would launch {tool.id}: {tool.command}"
    command = str(tool.command or "").strip()
    if not command:
        return 1, f"Tool {tool.id} has no command configured."
    if command.startswith("ms-"):
        return open_uri(command)
    if os.name == "nt":
        try:
            subprocess.Popen(command, shell=True)
            return 0, f"Launched: {command}"
        except Exception as exc:
            return 1, str(exc)
    try:
        proc = subprocess.run(command.split(), capture_output=True, text=True, timeout=30)
    except Exception as exc:
        return 1, str(exc)
    output = (proc.stdout or proc.stderr or "").strip()
    return int(proc.returncode), output
