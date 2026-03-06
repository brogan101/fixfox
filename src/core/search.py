from __future__ import annotations

import threading
import time
from dataclasses import dataclass

from .db import list_recent_runs, list_sessions
from .fixes import FIX_CATALOG
from .kb import KB_CARDS
from .play_registry import list_play_entries
from .registry import CAPABILITIES
from .route_registry import list_routes
from .runbooks import RUNBOOKS
from .support_catalog import list_issues, list_support_playbooks
from .script_tasks import list_script_tasks
from .toolbox import TOOL_DIRECTORY

try:
    from rapidfuzz import fuzz as _fuzz
except Exception:  # pragma: no cover - optional
    _fuzz = None


@dataclass(frozen=True)
class SearchItem:
    kind: str
    key: str
    title: str
    subtitle: str


@dataclass(frozen=True)
class _IndexedRow:
    item: SearchItem
    title_l: str
    subtitle_l: str
    key_l: str
    haystack_l: str


_STATIC_LOCK = threading.Lock()
_STATIC_ROWS: list[_IndexedRow] | None = None
_STATIC_BUILD_COUNT = 0
_STATIC_WARM_INFLIGHT = False

_DYNAMIC_LOCK = threading.Lock()
_DYNAMIC_ROWS: list[_IndexedRow] = []
_DYNAMIC_UPDATED_AT = 0.0
_DYNAMIC_BUILD_COUNT = 0
_DYNAMIC_REFRESH_INFLIGHT = False
_DYNAMIC_TTL_S = 45.0


def _to_indexed(item: SearchItem) -> _IndexedRow:
    title_l = str(item.title or "").lower()
    subtitle_l = str(item.subtitle or "").lower()
    key_l = str(item.key or "").lower()
    return _IndexedRow(
        item=item,
        title_l=title_l,
        subtitle_l=subtitle_l,
        key_l=key_l,
        haystack_l=f"{title_l} {subtitle_l} {key_l}".strip(),
    )


def _build_static_rows() -> list[_IndexedRow]:
    rows: list[_IndexedRow] = []
    for route in list_routes():
        rows.append(_to_indexed(SearchItem("route", route.id, route.title, route.description)))
    for play in list_play_entries():
        subtitle = (
            f"{play.kind} | {play.category} | risk={play.risk_badge} | "
            f"~{play.estimated_minutes}m | {play.automation_level}"
        )
        rows.append(_to_indexed(SearchItem("play", play.id, play.title, subtitle)))
    for cap in CAPABILITIES:
        rows.append(_to_indexed(SearchItem("capability", cap.id, cap.title, cap.desc)))
    for fix in FIX_CATALOG:
        rows.append(_to_indexed(SearchItem("fix", fix.key, fix.title, fix.description)))
    for runbook in RUNBOOKS:
        rows.append(_to_indexed(SearchItem("runbook", runbook.id, runbook.title, runbook.desc)))
    for issue in list_issues():
        alias_text = ", ".join(issue.aliases[:4])
        subtitle = f"{issue.family_label} | {issue.subfamily} | {issue.severity} | {alias_text}"
        rows.append(_to_indexed(SearchItem("issue", issue.id, issue.title, subtitle)))
    for playbook in list_support_playbooks():
        subtitle = f"{playbook.risk} | {playbook.automation} | ~{playbook.minutes}m | {', '.join(playbook.symptoms[:3])}"
        rows.append(_to_indexed(SearchItem("support_playbook", playbook.id, playbook.title, subtitle)))
    for task in list_script_tasks():
        rows.append(_to_indexed(SearchItem("task", task.id, task.title, f"{task.desc} [{task.category}]")))
    for tool in TOOL_DIRECTORY:
        rows.append(_to_indexed(SearchItem("tool", tool.id, tool.title, tool.desc)))
    for card in KB_CARDS:
        rows.append(_to_indexed(SearchItem("kb", card.id, card.title, card.why_it_matters)))
    return rows


def _ensure_static_rows() -> list[_IndexedRow]:
    global _STATIC_ROWS, _STATIC_BUILD_COUNT
    with _STATIC_LOCK:
        if _STATIC_ROWS is None:
            _STATIC_ROWS = _build_static_rows()
            _STATIC_BUILD_COUNT += 1
        return list(_STATIC_ROWS)


def _static_warm_worker() -> None:
    global _STATIC_WARM_INFLIGHT
    try:
        _ensure_static_rows()
    finally:
        with _STATIC_LOCK:
            _STATIC_WARM_INFLIGHT = False


def warm_static_index_async() -> bool:
    global _STATIC_WARM_INFLIGHT
    with _STATIC_LOCK:
        if _STATIC_ROWS is not None or _STATIC_WARM_INFLIGHT:
            return False
        _STATIC_WARM_INFLIGHT = True
    thread = threading.Thread(target=_static_warm_worker, daemon=True, name="fixfox-search-static-warm")
    thread.start()
    return True


def _build_dynamic_rows() -> list[_IndexedRow]:
    rows: list[_IndexedRow] = []
    try:
        for session in list_sessions(limit=120):
            rows.append(
                _to_indexed(
                    SearchItem(
                        "session",
                        str(session.get("session_id", "")),
                        str(session.get("goal", "Session")),
                        str(session.get("summary_plain", "")),
                    )
                )
            )
    except Exception:
        pass
    try:
        for run in list_recent_runs(limit=80):
            rows.append(
                _to_indexed(
                    SearchItem(
                        "run",
                        str(run.get("run_id", "")),
                        str(run.get("capability_id", run.get("run_id", "Run"))),
                        f"{run.get('kind', 'run')} | {run.get('status', 'unknown')} | "
                        f"{str(run.get('last_log_line', ''))[:120]}",
                    )
                )
            )
    except Exception:
        pass
    return rows


def refresh_dynamic_index(force: bool = False) -> bool:
    global _DYNAMIC_ROWS, _DYNAMIC_UPDATED_AT, _DYNAMIC_BUILD_COUNT
    now = time.time()
    with _DYNAMIC_LOCK:
        if (not force) and (now - _DYNAMIC_UPDATED_AT) < _DYNAMIC_TTL_S and _DYNAMIC_ROWS:
            return False
    rows = _build_dynamic_rows()
    with _DYNAMIC_LOCK:
        _DYNAMIC_ROWS = rows
        _DYNAMIC_UPDATED_AT = time.time()
        _DYNAMIC_BUILD_COUNT += 1
    return True


def _dynamic_refresh_worker(force: bool) -> None:
    global _DYNAMIC_REFRESH_INFLIGHT
    try:
        refresh_dynamic_index(force=force)
    finally:
        with _DYNAMIC_LOCK:
            _DYNAMIC_REFRESH_INFLIGHT = False


def refresh_dynamic_index_async(force: bool = False) -> bool:
    global _DYNAMIC_REFRESH_INFLIGHT
    with _DYNAMIC_LOCK:
        now = time.time()
        stale = force or (_DYNAMIC_UPDATED_AT <= 0.0) or ((now - _DYNAMIC_UPDATED_AT) > _DYNAMIC_TTL_S)
        if (not stale) or _DYNAMIC_REFRESH_INFLIGHT:
            return False
        _DYNAMIC_REFRESH_INFLIGHT = True
    thread = threading.Thread(target=_dynamic_refresh_worker, args=(force,), daemon=True, name="fixfox-search-refresh")
    thread.start()
    return True


def _current_dynamic_rows() -> list[_IndexedRow]:
    with _DYNAMIC_LOCK:
        rows = list(_DYNAMIC_ROWS)
        updated = _DYNAMIC_UPDATED_AT
    if (not rows) or ((time.time() - updated) > _DYNAMIC_TTL_S):
        refresh_dynamic_index_async(force=False)
    return rows


def _allowed_capability_id(row: SearchItem) -> str:
    kind = str(row.kind or "").lower()
    key = str(row.key or "")
    if kind == "capability":
        return key
    if kind == "fix":
        return f"fix_action.{key}"
    if kind == "runbook":
        return f"runbook.{key}"
    if kind == "task":
        return f"script_task.{key}"
    if kind == "tool":
        return f"tool.{key}"
    if kind == "play":
        return key
    return ""


def _is_allowed(item: SearchItem, allowed_capability_ids: set[str] | None) -> bool:
    if allowed_capability_ids is None:
        return True
    capability_id = _allowed_capability_id(item)
    if not capability_id:
        return True
    if capability_id.startswith(("runbook.", "task.", "tool.")):
        if capability_id.startswith("runbook."):
            capability_id = f"runbook.{capability_id.split('.', 1)[1]}"
        elif capability_id.startswith("task."):
            capability_id = f"script_task.{capability_id.split('.', 1)[1]}"
        elif capability_id.startswith("tool."):
            capability_id = f"tool.{capability_id.split('.', 1)[1]}"
    return capability_id in allowed_capability_ids


def _score_row(q: str, row: _IndexedRow) -> float:
    if not q:
        return 0.0
    if _fuzz is not None:
        score = max(
            float(_fuzz.WRatio(q, row.title_l)),
            float(_fuzz.partial_ratio(q, row.haystack_l)),
        )
    else:
        score = 100.0 if q in row.haystack_l else 0.0
    if row.title_l.startswith(q):
        score += 24.0
    if row.key_l.startswith(q):
        score += 14.0
    if q == row.title_l or q == row.key_l:
        score += 30.0
    return score


def build_search_index(allowed_capability_ids: set[str] | None = None) -> list[SearchItem]:
    rows = _ensure_static_rows() + _current_dynamic_rows()
    out: list[SearchItem] = []
    for row in rows:
        if _is_allowed(row.item, allowed_capability_ids):
            out.append(row.item)
    return out


def query_index(query: str, limit: int = 40, allowed_capability_ids: set[str] | None = None) -> list[SearchItem]:
    q = query.strip().lower()
    rows = _ensure_static_rows() + _current_dynamic_rows()
    if not q:
        out: list[SearchItem] = []
        for row in rows:
            if _is_allowed(row.item, allowed_capability_ids):
                out.append(row.item)
                if len(out) >= limit:
                    break
        return out

    ranked: list[tuple[float, SearchItem]] = []
    for row in rows:
        if not _is_allowed(row.item, allowed_capability_ids):
            continue
        score = _score_row(q, row)
        if score > 0:
            ranked.append((score, row.item))
    ranked.sort(key=lambda item: item[0], reverse=True)

    seen: set[str] = set()
    out: list[SearchItem] = []
    for _score, item in ranked:
        dedupe = f"{item.kind}:{item.key}".lower()
        if not item.key or dedupe in seen:
            continue
        seen.add(dedupe)
        out.append(item)
        if len(out) >= limit:
            break
    return out


def get_search_cache_stats() -> dict[str, float]:
    with _STATIC_LOCK:
        static_rows = len(_STATIC_ROWS or [])
        static_builds = _STATIC_BUILD_COUNT
    with _DYNAMIC_LOCK:
        dynamic_rows = len(_DYNAMIC_ROWS)
        dynamic_builds = _DYNAMIC_BUILD_COUNT
        dynamic_age_s = max(0.0, time.time() - _DYNAMIC_UPDATED_AT) if _DYNAMIC_UPDATED_AT > 0 else -1.0
    return {
        "static_rows": float(static_rows),
        "dynamic_rows": float(dynamic_rows),
        "static_builds": float(static_builds),
        "dynamic_builds": float(dynamic_builds),
        "dynamic_age_s": float(dynamic_age_s),
    }


def reset_search_cache_for_tests() -> None:
    global _STATIC_ROWS, _STATIC_BUILD_COUNT, _STATIC_WARM_INFLIGHT, _DYNAMIC_ROWS, _DYNAMIC_UPDATED_AT, _DYNAMIC_BUILD_COUNT, _DYNAMIC_REFRESH_INFLIGHT
    with _STATIC_LOCK:
        _STATIC_ROWS = None
        _STATIC_BUILD_COUNT = 0
        _STATIC_WARM_INFLIGHT = False
    with _DYNAMIC_LOCK:
        _DYNAMIC_ROWS = []
        _DYNAMIC_UPDATED_AT = 0.0
        _DYNAMIC_BUILD_COUNT = 0
        _DYNAMIC_REFRESH_INFLIGHT = False
