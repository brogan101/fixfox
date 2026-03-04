from __future__ import annotations

from dataclasses import dataclass

from .fixes import FIX_CATALOG
from .kb import KB_CARDS
from .paths import ensure_dirs
from .registry import CAPABILITIES
from .runbooks import RUNBOOKS
from .script_tasks import list_script_tasks
from .sessions import load_index
from .toolbox import TOOL_DIRECTORY


@dataclass(frozen=True)
class SearchItem:
    kind: str
    key: str
    title: str
    subtitle: str


def build_search_index(allowed_capability_ids: set[str] | None = None) -> list[SearchItem]:
    rows: list[SearchItem] = []
    for cap in CAPABILITIES:
        if allowed_capability_ids is not None and cap.id not in allowed_capability_ids:
            continue
        rows.append(SearchItem("capability", cap.id, cap.title, cap.desc))
    for fix in FIX_CATALOG:
        if allowed_capability_ids is not None and f"fix_action.{fix.key}" not in allowed_capability_ids:
            continue
        rows.append(SearchItem("fix", fix.key, fix.title, fix.description))
    for runbook in RUNBOOKS:
        if allowed_capability_ids is not None and f"runbook.{runbook.id}" not in allowed_capability_ids:
            continue
        rows.append(SearchItem("runbook", runbook.id, runbook.title, runbook.desc))
    for task in list_script_tasks():
        if allowed_capability_ids is not None and f"script_task.{task.id}" not in allowed_capability_ids:
            continue
        rows.append(SearchItem("task", task.id, task.title, f"{task.desc} [{task.category}]"))
    for tool in TOOL_DIRECTORY:
        if allowed_capability_ids is not None and f"tool.{tool.id}" not in allowed_capability_ids:
            continue
        rows.append(SearchItem("tool", tool.id, tool.title, tool.desc))
    for card in KB_CARDS:
        rows.append(SearchItem("kb", card.id, card.title, card.why_it_matters))
    for session in load_index()[:200]:
        rows.append(SearchItem("session", session.session_id, session.symptom, session.summary))
    exports_dir = ensure_dirs()["exports"]
    if exports_dir.exists():
        for archive in sorted(exports_dir.glob("*.zip"), reverse=True)[:200]:
            rows.append(SearchItem("export", archive.stem, archive.name, str(archive)))
    return rows


def query_index(query: str, limit: int = 40, allowed_capability_ids: set[str] | None = None) -> list[SearchItem]:
    q = query.strip().lower()
    rows = build_search_index(allowed_capability_ids=allowed_capability_ids)
    if not q:
        return rows[:limit]
    filtered = [row for row in rows if q in row.title.lower() or q in row.subtitle.lower() or q in row.key.lower()]
    return filtered[:limit]
