from __future__ import annotations

import json
from dataclasses import dataclass, asdict, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from .db import upsert_artifacts, upsert_findings, upsert_session_from_payload, upsert_session_row
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
    try:
        upsert_session_from_payload(session)
        findings = session.get("findings", [])
        if isinstance(findings, list):
            upsert_findings(sid, findings)
        evidence = session.get("evidence", {})
        files = evidence.get("files", []) if isinstance(evidence, dict) else []
        if isinstance(files, list):
            upsert_artifacts(sid, files)
    except Exception:
        pass
    return path


def load_session(session_id: str) -> dict[str, Any]:
    path = _session_file(session_id)
    if not path.exists():
        raise FileNotFoundError(path)
    payload = json.loads(path.read_text(encoding="utf-8"))
    try:
        if isinstance(payload, dict):
            upsert_session_from_payload(payload)
    except Exception:
        pass
    return payload


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
    try:
        upsert_session_row(
            meta.session_id,
            created_at=meta.created_utc,
            updated_at=meta.created_utc,
            goal=meta.symptom,
            status="open",
            summary_plain=meta.summary,
            last_export_path=meta.last_export_path,
        )
    except Exception:
        pass


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
    try:
        upsert_session_row(
            sid,
            created_at=created,
            updated_at=created,
            goal=symptom,
            status="open",
            summary_plain=summary,
        )
    except Exception:
        pass
    return meta


def update_meta_export_path(session_id: str, export_path: str) -> None:
    items = load_index()
    for item in items:
        if item.session_id == session_id:
            item.last_export_path = export_path
            break
    save_index(items)
    try:
        upsert_session_row(
            session_id,
            updated_at=datetime.now(timezone.utc).isoformat(),
            status="exported",
            last_export_path=export_path,
        )
    except Exception:
        pass
