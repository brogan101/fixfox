from __future__ import annotations

import json
from dataclasses import dataclass, asdict, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from .paths import ensure_dirs


@dataclass
class SessionMeta:
    session_id: str
    created_utc: str
    symptom: str
    summary: str
    tags: list[str] = field(default_factory=list)
    pinned: bool = False
    last_export_path: str = ""


def new_session_id() -> str:
    return "S" + datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")


def _index_path() -> Path:
    return ensure_dirs()["sessions"] / "index.json"


def _session_file(session_id: str) -> Path:
    return ensure_dirs()["sessions"] / f"{session_id}.json"


def _coerce_meta(raw: dict[str, Any]) -> SessionMeta:
    defaults = asdict(
        SessionMeta(
            session_id="",
            created_utc="",
            symptom="Quick Check",
            summary="",
            tags=[],
        )
    )
    defaults.update({k: raw[k] for k in defaults.keys() if k in raw})
    return SessionMeta(**defaults)


def load_index() -> list[SessionMeta]:
    p = _index_path()
    if not p.exists():
        return []
    try:
        payload = json.loads(p.read_text(encoding="utf-8"))
    except Exception:
        return []
    if not isinstance(payload, list):
        return []
    items: list[SessionMeta] = []
    for row in payload:
        if isinstance(row, dict):
            items.append(_coerce_meta(row))
    return items


def save_index(items: list[SessionMeta]) -> Path:
    p = _index_path()
    p.write_text(json.dumps([asdict(i) for i in items], indent=2), encoding="utf-8")
    return p


def save_session(session: dict[str, Any]) -> Path:
    sid = str(session.get("session_id", "")).strip()
    if not sid:
        raise ValueError("session_id is required")
    path = _session_file(sid)
    path.write_text(json.dumps(session, indent=2), encoding="utf-8")
    return path


def load_session(session_id: str) -> dict[str, Any]:
    path = _session_file(session_id)
    if not path.exists():
        raise FileNotFoundError(path)
    return json.loads(path.read_text(encoding="utf-8"))


def add_or_update_meta(meta: SessionMeta) -> None:
    items = load_index()
    existing = {item.session_id: item for item in items}
    existing[meta.session_id] = meta
    merged = sorted(
        existing.values(),
        key=lambda x: x.created_utc,
        reverse=True,
    )
    save_index(merged)


def persist_new_session(session: dict[str, Any], summary: str = "") -> SessionMeta:
    sid = str(session.get("session_id", ""))
    created = str(session.get("sysinfo", {}).get("timestamp_utc", ""))
    symptom = str(session.get("symptom", "Quick Check"))
    findings = session.get("findings", [])
    if not summary:
        summary = f"{len(findings)} findings"
    save_session(session)
    meta = SessionMeta(
        session_id=sid,
        created_utc=created,
        symptom=symptom,
        summary=summary,
        tags=[],
    )
    add_or_update_meta(meta)
    return meta


def update_meta_export_path(session_id: str, export_path: str) -> None:
    items = load_index()
    for item in items:
        if item.session_id == session_id:
            item.last_export_path = export_path
            break
    save_index(items)
