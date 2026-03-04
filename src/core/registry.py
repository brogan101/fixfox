from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .brand import APP_DISPLAY_NAME
from .exporter import PRESETS
from .fixes import FIX_CATALOG
from .kb import KB_CARDS
from .runbooks import RUNBOOKS
from .script_tasks import list_script_tasks
from .toolbox import TOOL_DIRECTORY


@dataclass(frozen=True)
class Capability:
    id: str
    title: str
    desc: str
    tags: tuple[str, ...]
    risk: str
    admin: bool
    pro: bool
    default_visibility: str
    contexts: tuple[str, ...]
    entrypoint: str
    kind: str = "capability"
    category: str = ""
    plain_1liner: str = ""
    technical_detail: str = ""
    safety_note: str = ""
    next_steps: tuple[str, ...] = ()
    visibility_basic: bool = True
    visibility_pro: bool = True
    requires_pro: bool = False
    requires_admin: bool = False


_BASE_CAPABILITIES: tuple[Capability, ...] = (
    Capability(
        id="quick_check",
        title="Quick Check",
        desc="Runs a baseline diagnostics scan for performance, storage, and update signals.",
        tags=("diagnostics", "home"),
        risk="Safe",
        admin=False,
        pro=False,
        default_visibility="visible",
        contexts=("home", "diagnose"),
        entrypoint="core.diagnostics.quick_check",
    ),
    Capability(
        id="large_file_radar",
        title="Large File Radar",
        desc="Finds the largest files with filters so storage cleanups start with impact.",
        tags=("storage", "cleanup"),
        risk="Safe",
        admin=False,
        pro=False,
        default_visibility="visible",
        contexts=("diagnose", "toolbox"),
        entrypoint="core.diagnostics.large_file_radar",
    ),
    Capability(
        id="downloads_cleanup_assistant",
        title="Downloads Cleanup Assistant",
        desc="Previews stale files in Downloads before optional cleanup actions.",
        tags=("storage", "cleanup"),
        risk="Safe",
        admin=False,
        pro=False,
        default_visibility="visible",
        contexts=("diagnose", "fixes"),
        entrypoint="core.diagnostics.downloads_cleanup_assistant",
    ),
    Capability(
        id="storage_ranked_view",
        title="Storage Ranked View",
        desc="Shows top folders by size for quick storage triage.",
        tags=("storage", "visualization"),
        risk="Safe",
        admin=False,
        pro=False,
        default_visibility="visible",
        contexts=("diagnose", "reports"),
        entrypoint="core.diagnostics.storage_ranked_view",
    ),
    Capability(
        id="duplicate_finder_hash",
        title="Duplicate Finder (Exact Hash)",
        desc="Finds exact duplicates by SHA-256 and supports preview-first recycle actions.",
        tags=("storage", "pro"),
        risk="Advanced",
        admin=False,
        pro=True,
        default_visibility="hidden",
        contexts=("diagnose", "fixes"),
        entrypoint="core.diagnostics.duplicate_finder_exact_hash",
    ),
    Capability(
        id="uninstall_helper",
        title="Uninstall Helper (Leftover Folders)",
        desc="Surfaces likely leftover folders only; does not edit registry entries.",
        tags=("apps", "cleanup"),
        risk="Safe",
        admin=False,
        pro=False,
        default_visibility="visible",
        contexts=("toolbox", "fixes"),
        entrypoint="core.diagnostics.uninstall_leftover_folders",
    ),
    Capability(
        id="disk_health_snapshot",
        title="Disk Health Snapshot",
        desc="Best-effort physical disk health summary using built-in Windows tooling.",
        tags=("hardware", "disk"),
        risk="Safe",
        admin=False,
        pro=False,
        default_visibility="visible",
        contexts=("diagnose",),
        entrypoint="core.diagnostics.disk_health_snapshot",
    ),
    Capability(
        id="battery_report",
        title="Battery Report",
        desc="Generates a battery report when supported by the device.",
        tags=("power", "pro"),
        risk="Safe",
        admin=False,
        pro=True,
        default_visibility="hidden",
        contexts=("toolbox", "reports"),
        entrypoint="core.diagnostics.battery_report",
    ),
    Capability(
        id="reliability_snapshot",
        title="Reliability Snapshot",
        desc="Summarizes recent reliability events.",
        tags=("stability",),
        risk="Safe",
        admin=False,
        pro=False,
        default_visibility="visible",
        contexts=("diagnose",),
        entrypoint="core.diagnostics.reliability_snapshot",
    ),
    Capability(
        id="problem_devices",
        title="Problem Devices List",
        desc="Lists devices reporting an error state.",
        tags=("hardware", "drivers"),
        risk="Safe",
        admin=False,
        pro=False,
        default_visibility="visible",
        contexts=("diagnose", "toolbox"),
        entrypoint="core.diagnostics.problem_devices",
    ),
    Capability(
        id="proxy_hosts_alert",
        title="Proxy + Hosts Alert",
        desc="Detects proxy settings and hosts file modifications in read-only mode.",
        tags=("network", "security"),
        risk="Safe",
        admin=False,
        pro=False,
        default_visibility="visible",
        contexts=("diagnose", "toolbox"),
        entrypoint="core.diagnostics.proxy_and_hosts_alert",
    ),
    Capability(
        id="weekly_check_reminder",
        title="Weekly Check Reminder",
        desc="Opt-in reminder state for periodic maintenance checks.",
        tags=("reminder",),
        risk="Safe",
        admin=False,
        pro=False,
        default_visibility="visible",
        contexts=("home", "settings"),
        entrypoint="core.diagnostics.weekly_check_status",
    ),
    Capability(
        id="fix_flush_dns",
        title="Flush DNS Cache",
        desc="Runs ipconfig /flushdns.",
        tags=("fix", "network"),
        risk="Safe",
        admin=False,
        pro=False,
        default_visibility="visible",
        contexts=("fixes", "toolbox"),
        entrypoint="core.fixes.run_fix",
    ),
    Capability(
        id="fix_restart_spooler",
        title="Restart Print Spooler",
        desc="Restarts print spooler service with admin confirmation.",
        tags=("fix", "printer"),
        risk="Admin",
        admin=True,
        pro=False,
        default_visibility="hidden",
        contexts=("fixes",),
        entrypoint="core.fixes.run_fix",
    ),
    Capability(
        id="fix_startup_toggle",
        title="Startup Toggle",
        desc=f"Adds or removes startup entry for {APP_DISPLAY_NAME} and supports undo.",
        tags=("fix", "undo"),
        risk="Safe",
        admin=False,
        pro=False,
        default_visibility="visible",
        contexts=("fixes", "settings"),
        entrypoint="core.fixes.run_fix",
    ),
    Capability(
        id="export_home_share_pack",
        title="Home Share Pack Export",
        desc="Exports a redacted support pack tailored for home sharing.",
        tags=("export",),
        risk="Safe",
        admin=False,
        pro=False,
        default_visibility="visible",
        contexts=("reports",),
        entrypoint="core.exporter.export_session",
    ),
    Capability(
        id="export_ticket_pack",
        title="Ticket Pack Export",
        desc="Exports a ticket-focused package with summary copy blocks.",
        tags=("export", "ticket"),
        risk="Safe",
        admin=False,
        pro=False,
        default_visibility="visible",
        contexts=("reports",),
        entrypoint="core.exporter.export_session",
    ),
    Capability(
        id="export_full_pack",
        title="Full Pack Export",
        desc="Exports full diagnostics with technical appendices.",
        tags=("export", "advanced"),
        risk="Safe",
        admin=False,
        pro=False,
        default_visibility="visible",
        contexts=("reports",),
        entrypoint="core.exporter.export_session",
    ),
)


def _script_task_capabilities() -> tuple[Capability, ...]:
    rows: list[Capability] = []
    for task in list_script_tasks():
        risk = str(task.risk or "Safe")
        visibility = "hidden" if risk in {"Admin", "Advanced"} else "recommended"
        admin_required = bool(task.admin_required)
        rows.append(
            Capability(
                id=f"script_task.{task.id}",
                title=task.title,
                desc=task.desc,
                tags=("script", task.category.lower()),
                risk=risk,
                admin=admin_required,
                pro=False,
                default_visibility=visibility,
                contexts=("playbooks", "reports"),
                entrypoint=f"core.script_tasks.run_script_task:{task.id}",
                kind="script_task",
                category=task.category.lower(),
                plain_1liner=task.desc,
                technical_detail=f"task_id={task.id} category={task.category} timeout={task.timeout_s}s",
                safety_note="Requires administrator approval." if admin_required else "Safe-by-default read-only or guided action.",
                next_steps=(
                    "Run in dry-run mode first.",
                    "Review artifacts and logs in ToolRunner.",
                    "Export a support pack if escalation is needed.",
                ),
                visibility_basic=False,
                visibility_pro=True,
                requires_pro=True,
                requires_admin=admin_required,
            )
        )
    return tuple(rows)


def _fix_capabilities() -> tuple[Capability, ...]:
    rows: list[Capability] = []
    for fix in FIX_CATALOG:
        rows.append(
            Capability(
                id=f"fix_action.{fix.key}",
                title=fix.title,
                desc=fix.description,
                tags=("fix", fix.risk.lower()),
                risk=fix.risk,
                admin=bool(fix.admin_required),
                pro=False,
                default_visibility="hidden" if fix.risk in {"Admin", "Advanced"} else "recommended",
                contexts=("fixes", "home", "playbooks"),
                entrypoint=f"core.fixes.run_fix:{fix.key}",
                kind="fix_action",
                category="fixes",
                plain_1liner=fix.plain,
                technical_detail=" | ".join(fix.commands),
                safety_note=fix.rollback,
                next_steps=(
                    "Review rollback notes before running.",
                    "Run the action via ToolRunner and check status.",
                    "Re-run diagnostics to verify impact.",
                ),
                visibility_basic=(fix.risk == "Safe"),
                visibility_pro=True,
                requires_pro=(fix.risk != "Safe"),
                requires_admin=bool(fix.admin_required),
            )
        )
    return tuple(rows)


def _runbook_capabilities() -> tuple[Capability, ...]:
    rows: list[Capability] = []
    for runbook in RUNBOOKS:
        rows.append(
            Capability(
                id=f"runbook.{runbook.id}",
                title=runbook.title,
                desc=runbook.desc,
                tags=("runbook", runbook.audience),
                risk="Admin" if runbook.audience == "it" else "Safe",
                admin=(runbook.audience == "it"),
                pro=False,
                default_visibility="recommended",
                contexts=("playbooks", "home"),
                entrypoint=f"core.runbooks.execute_runbook:{runbook.id}",
                kind="runbook",
                category=runbook.audience,
                plain_1liner=runbook.desc,
                technical_detail=f"steps={len(runbook.steps)}",
                safety_note="Runs step-by-step with checkpoints in ToolRunner.",
                next_steps=(
                    "Run dry-run first.",
                    "Review checkpoints after each step.",
                    "Export the recommended pack at completion.",
                ),
                visibility_basic=(runbook.audience == "home"),
                visibility_pro=True,
                requires_pro=(runbook.audience != "home"),
                requires_admin=(runbook.audience == "it"),
            )
        )
    return tuple(rows)


def _tool_capabilities() -> tuple[Capability, ...]:
    rows: list[Capability] = []
    for idx, tool in enumerate(TOOL_DIRECTORY):
        basic_visible = idx < 8
        rows.append(
            Capability(
                id=f"tool.{tool.id}",
                title=tool.title,
                desc=tool.desc,
                tags=("tool", tool.category.lower()),
                risk="Safe",
                admin=False,
                pro=False,
                default_visibility="recommended",
                contexts=("playbooks", "home"),
                entrypoint=f"core.toolbox.launch_tool:{tool.id}",
                kind="tool",
                category=tool.category.lower(),
                plain_1liner=tool.plain,
                technical_detail=tool.command,
                safety_note="Opens built-in Windows tool or settings page.",
                next_steps=(
                    "Capture evidence with a related script task.",
                    "Run a runbook if you need guided troubleshooting.",
                    "Export a support pack for escalation.",
                ),
                visibility_basic=basic_visible,
                visibility_pro=True,
                requires_pro=(not basic_visible),
                requires_admin=False,
            )
        )
    return tuple(rows)


def _export_capabilities() -> tuple[Capability, ...]:
    rows: list[Capability] = []
    for preset in PRESETS:
        rows.append(
            Capability(
                id=f"export_preset.{preset}",
                title=f"Export Preset: {preset}",
                desc=f"Generate {preset} export package.",
                tags=("export", preset),
                risk="Safe",
                admin=False,
                pro=False,
                default_visibility="recommended",
                contexts=("reports",),
                entrypoint=f"core.exporter.export_session:{preset}",
                kind="export_preset",
                category="exports",
                plain_1liner="Build validated export bundle with manifest and hashes.",
                technical_detail="Includes report, session data, logs, evidence, and manifest.",
                safety_note="Share-safe masking is enabled by default in UI presets.",
                next_steps=(
                    "Validate generated pack.",
                    "Open report.html for quick review.",
                    "Share the zip with support if needed.",
                ),
                visibility_basic=(preset == "home_share"),
                visibility_pro=True,
                requires_pro=(preset != "home_share"),
                requires_admin=False,
            )
        )
    return tuple(rows)


def _kb_capabilities() -> tuple[Capability, ...]:
    rows: list[Capability] = []
    for card in KB_CARDS:
        rows.append(
            Capability(
                id=f"kb.{card.id}",
                title=card.title,
                desc=card.why_it_matters,
                tags=("kb",),
                risk="Safe",
                admin=False,
                pro=False,
                default_visibility="recommended",
                contexts=("diagnose", "playbooks"),
                entrypoint=f"core.kb:{card.id}",
                kind="kb_article",
                category="knowledge",
                plain_1liner=card.why_it_matters,
                technical_detail=f"next_steps={card.next_steps} | escalate={card.when_to_escalate}",
                safety_note="Read-only guidance content.",
                next_steps=(
                    "Apply the suggested safe checks.",
                    "Use related tool/runbook from Playbooks.",
                    "Export support pack for deeper escalation.",
                ),
            )
        )
    return tuple(rows)


def _dedupe_capabilities(rows: tuple[Capability, ...]) -> tuple[Capability, ...]:
    seen: set[str] = set()
    out: list[Capability] = []
    for row in rows:
        if row.id in seen:
            continue
        seen.add(row.id)
        out.append(row)
    return tuple(out)


def _normalize_mode_visibility(rows: tuple[Capability, ...]) -> tuple[Capability, ...]:
    normalized: list[Capability] = []
    for row in rows:
        requires_pro = bool(row.requires_pro or row.pro)
        visibility_basic = bool(row.visibility_basic and not requires_pro)
        visibility_pro = bool(row.visibility_pro)
        normalized.append(
            Capability(
                **{
                    **row.__dict__,
                    "visibility_basic": visibility_basic,
                    "visibility_pro": visibility_pro,
                    "requires_pro": requires_pro,
                    "requires_admin": bool(row.requires_admin or row.admin),
                }
            )
        )
    return tuple(normalized)


CAPABILITIES: tuple[Capability, ...] = _dedupe_capabilities(
    _normalize_mode_visibility(
        _BASE_CAPABILITIES
        + _script_task_capabilities()
        + _fix_capabilities()
        + _runbook_capabilities()
        + _tool_capabilities()
        + _export_capabilities()
        + _kb_capabilities()
    )
)


def get_visible_capabilities(
    ui_mode: str,
    safety_policy: Any,
    admin_enabled: bool,
) -> tuple[Capability, ...]:
    mode = "pro" if str(ui_mode).strip().lower() == "pro" else "basic"
    safe_only = bool(getattr(safety_policy, "safe_only", False))
    show_admin = bool(getattr(safety_policy, "show_admin", False) or admin_enabled)
    show_advanced = bool(getattr(safety_policy, "show_advanced", False) or mode == "pro")
    out: list[Capability] = []
    for cap in CAPABILITIES:
        if mode == "basic":
            if not cap.visibility_basic or cap.requires_pro:
                continue
        else:
            if not cap.visibility_pro:
                continue
        risk = str(cap.risk or "Safe")
        if risk == "Admin" and mode == "basic" and not show_admin:
            continue
        if risk == "Advanced" and mode == "basic" and (safe_only or not show_advanced):
            continue
        out.append(cap)
    return tuple(out)


def generate_capability_catalog(path: Path) -> Path:
    lines = [
        "# Capability Catalog",
        "",
        "| Kind | ID | Title | Risk | Admin | Pro | Category | Contexts | Entry Point |",
        "|---|---|---|---|---|---|---|---|---|",
    ]
    for item in CAPABILITIES:
        lines.append(
            f"| {item.kind} | `{item.id}` | {item.title} | {item.risk} | {item.admin} | "
            f"{item.pro} | {item.category or '-'} | {', '.join(item.contexts)} | `{item.entrypoint}` |"
        )
    lines.append("")
    lines.extend(
        [
            "## Metadata Fields",
            "",
            "- `plain_1liner`: concise plain-English description used in UI surfaces.",
            "- `technical_detail`: deterministic technical context (commands/IDs/timeouts).",
            "- `safety_note`: concise safety/admin guidance.",
            "- `next_steps`: deterministic follow-up actions.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")
    return path
