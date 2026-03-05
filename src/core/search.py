from __future__ import annotations

from dataclasses import dataclass

from .db import list_recent_runs, list_sessions, search_index as search_db_index
from .fixes import FIX_CATALOG
from .kb import KB_CARDS
from .play_registry import list_play_entries
from .registry import CAPABILITIES
from .route_registry import list_routes
from .runbooks import RUNBOOKS
from .script_tasks import list_script_tasks
from .toolbox import TOOL_DIRECTORY


@dataclass(frozen=True)
class SearchItem:
    kind: str
    key: str
    title: str
    subtitle: str


def build_search_index(allowed_capability_ids: set[str] | None = None) -> list[SearchItem]:
    rows: list[SearchItem] = []
    for route in list_routes():
        rows.append(SearchItem("route", route.id, route.title, route.description))
    for play in list_play_entries():
        subtitle = (
            f"{play.kind} | {play.category} | risk={play.risk_badge} | "
            f"~{play.estimated_minutes}m | {play.automation_level}"
        )
        rows.append(SearchItem("play", play.id, play.title, subtitle))
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
    for session in list_sessions(limit=120):
        rows.append(
            SearchItem(
                "session",
                str(session.get("session_id", "")),
                str(session.get("goal", "Session")),
                str(session.get("summary_plain", "")),
            )
        )
    for run in list_recent_runs(limit=80):
        rows.append(
            SearchItem(
                "run",
                str(run.get("run_id", "")),
                str(run.get("capability_id", run.get("run_id", "Run"))),
                f"{run.get('kind', 'run')} | {run.get('status', 'unknown')} | {str(run.get('last_log_line', ''))[:120]}",
            )
        )
    return rows


def query_index(query: str, limit: int = 40, allowed_capability_ids: set[str] | None = None) -> list[SearchItem]:
    q = query.strip().lower()
    static_rows = build_search_index(allowed_capability_ids=allowed_capability_ids)
    if not q:
        return static_rows[:limit]

    scored: list[tuple[int, SearchItem]] = []
    for row in static_rows:
        key = row.key.lower()
        title = row.title.lower()
        subtitle = row.subtitle.lower()
        score = 0
        if key == q or title == q:
            score = 300
        elif key.startswith(q) or title.startswith(q):
            score = 220
        elif q in key or q in title:
            score = 160
        elif q in subtitle:
            score = 120
        if score:
            scored.append((score, row))

    for row in search_db_index(q, limit=max(limit * 2, 60)):
        scored.append(
            (
                int(row.get("score", 100)),
                SearchItem(
                    str(row.get("kind", "")),
                    str(row.get("key", "")),
                    str(row.get("title", "")),
                    str(row.get("subtitle", "")),
                ),
            )
        )

    seen: set[str] = set()
    out: list[SearchItem] = []
    for _score, row in sorted(scored, key=lambda item: item[0], reverse=True):
        dedupe = f"{row.kind}:{row.key}".lower()
        if not row.key or dedupe in seen:
            continue
        seen.add(dedupe)
        out.append(row)
        if len(out) >= limit:
            break
    return out
