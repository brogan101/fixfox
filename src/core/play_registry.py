from __future__ import annotations

from dataclasses import dataclass
from typing import Any

from .runbooks import RUNBOOKS, runbook_map
from .script_tasks import list_script_tasks, script_task_map


@dataclass(frozen=True)
class PlayEntry:
    id: str
    title: str
    category: str
    kind: str
    risk_badge: str
    estimated_minutes: int
    admin_required: bool
    automation_level: str
    entrypoint: str


CATEGORY_ICON_MAP: dict[str, str] = {
    "home": "home",
    "it": "gear",
    "network": "diagnose",
    "storage": "playbooks",
    "browser": "playbooks",
    "crash": "history",
    "audio": "playbooks",
    "privacy": "privacy",
    "cloud": "playbooks",
    "devices": "diagnose",
    "services": "settings",
    "security": "shield",
    "wmi": "settings",
    "office": "playbooks",
    "eventlogs": "history",
    "updates": "reports",
    "repair": "wrench",
    "integrity": "shield",
    "hardware": "diagnose",
    "performance": "quick_check",
    "evidence": "details",
    "system": "settings",
    "printer": "wrench",
}


def _automation_level_for_task(category: str) -> str:
    c = str(category or "").strip().lower()
    if c in {"evidence", "eventlogs", "crash"}:
        return "evidence-only"
    if c in {"network", "storage", "performance", "printer", "repair", "updates"}:
        return "guided"
    return "auto"


def _estimated_minutes_from_timeout(timeout_s: int) -> int:
    if timeout_s <= 0:
        return 1
    return max(1, int(round(float(timeout_s) / 60.0)))


def list_play_entries() -> tuple[PlayEntry, ...]:
    rows: list[PlayEntry] = []
    tasks = script_task_map()
    for task in list_script_tasks():
        rows.append(
            PlayEntry(
                id=f"task.{task.id}",
                title=task.title,
                category=str(task.category or "misc").strip().lower(),
                kind="task",
                risk_badge=str(task.risk or "Safe"),
                estimated_minutes=_estimated_minutes_from_timeout(int(task.timeout_s)),
                admin_required=bool(task.admin_required),
                automation_level=_automation_level_for_task(task.category),
                entrypoint=f"run_script_task:{task.id}",
            )
        )

    for runbook in RUNBOOKS:
        step_tasks = [tasks.get(step.task_id) for step in runbook.steps]
        total_timeout = sum(int(t.timeout_s) for t in step_tasks if t is not None)
        admin_required = any(bool(t.admin_required) for t in step_tasks if t is not None)
        rows.append(
            PlayEntry(
                id=f"runbook.{runbook.id}",
                title=runbook.title,
                category=str(runbook.audience or "home").strip().lower(),
                kind="runbook",
                risk_badge="Admin" if admin_required else "Safe",
                estimated_minutes=max(2, _estimated_minutes_from_timeout(total_timeout)),
                admin_required=admin_required,
                automation_level="guided",
                entrypoint=f"execute_runbook:{runbook.id}",
            )
        )

    # Stable ordering and dedupe by ID.
    deduped: dict[str, PlayEntry] = {}
    for row in sorted(rows, key=lambda item: (item.kind, item.category, item.title.lower(), item.id)):
        deduped[row.id] = row
    return tuple(deduped.values())


def play_map() -> dict[str, PlayEntry]:
    return {row.id: row for row in list_play_entries()}


def run_play(play_id: str, *, dry_run: bool = False, **kwargs: Any) -> dict[str, Any]:
    key = str(play_id or "").strip()
    if not key:
        raise KeyError("play_id")
    if key.startswith("runbook."):
        rid = key.split(".", 1)[1]
        if rid not in runbook_map():
            raise KeyError(key)
        from .runbooks import execute_runbook

        return execute_runbook(rid, dry_run=dry_run, **kwargs)
    if key.startswith("task."):
        tid = key.split(".", 1)[1]
        if tid not in script_task_map():
            raise KeyError(key)
        from .script_tasks import run_script_task

        return run_script_task(tid, dry_run=dry_run, **kwargs)
    raise KeyError(key)

