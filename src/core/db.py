from __future__ import annotations

import hashlib
import json
import sqlite3
import threading
from contextlib import contextmanager
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable

from .paths import ensure_dirs, local_app_dir

DB_FILENAME = "fixfox.db"
DB_SCHEMA_VERSION = 2

_WRITE_LOCK = threading.RLock()
_LOG_LOCK = threading.RLock()
_LAST_LOG_TS: dict[str, float] = {}


@dataclass(frozen=True)
class DbStats:
    path: str
    size_bytes: int
    sessions: int
    runs: int
    findings: int
    artifacts: int
    favorites: int
    file_index_rows: int


def db_path() -> Path:
    root = local_app_dir() / "db"
    root.mkdir(parents=True, exist_ok=True)
    return root / DB_FILENAME


@contextmanager
def _conn() -> Iterable[sqlite3.Connection]:
    conn = sqlite3.connect(str(db_path()), timeout=8.0)
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA journal_mode=WAL;")
    conn.execute("PRAGMA synchronous=NORMAL;")
    conn.execute("PRAGMA busy_timeout=8000;")
    try:
        yield conn
        conn.commit()
    finally:
        conn.close()


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def _iso(ts: float | int | None = None) -> str:
    if ts is None:
        return _now_iso()
    try:
        return datetime.fromtimestamp(float(ts), tz=timezone.utc).isoformat()
    except Exception:
        return _now_iso()


def _safe_int(v: Any, default: int = 0) -> int:
    try:
        return int(v)
    except Exception:
        return default


def _sha1(text: str) -> str:
    return hashlib.sha1(text.encode("utf-8", errors="ignore")).hexdigest()


def _sha256_file(path: Path, max_bytes: int = 20 * 1024 * 1024) -> str:
    try:
        if not path.exists() or not path.is_file() or path.stat().st_size > max_bytes:
            return ""
        digest = hashlib.sha256()
        with path.open("rb") as fh:
            while True:
                chunk = fh.read(65536)
                if not chunk:
                    break
                digest.update(chunk)
        return digest.hexdigest()
    except Exception:
        return ""


def initialize_db() -> Path:
    path = db_path()
    with _WRITE_LOCK:
        migrate()
    return path


def migrate() -> None:
    with _conn() as conn:
        conn.execute("CREATE TABLE IF NOT EXISTS schema_version(version INTEGER NOT NULL, updated_at TEXT NOT NULL)")
        row = conn.execute("SELECT version FROM schema_version ORDER BY version DESC LIMIT 1").fetchone()
        version = int(row[0]) if row else 0
        if version < 1:
            conn.executescript(
                """
                CREATE TABLE IF NOT EXISTS sessions(
                    session_id TEXT PRIMARY KEY,
                    created_at TEXT,
                    updated_at TEXT,
                    goal TEXT,
                    status TEXT,
                    ui_mode TEXT,
                    summary_plain TEXT,
                    last_export_preset TEXT,
                    last_export_path TEXT
                );
                CREATE TABLE IF NOT EXISTS runs(
                    run_id TEXT PRIMARY KEY,
                    session_id TEXT,
                    capability_id TEXT,
                    kind TEXT,
                    started_at TEXT,
                    ended_at TEXT,
                    status TEXT,
                    exit_code INTEGER,
                    duration_ms INTEGER,
                    last_log_line TEXT
                );
                CREATE TABLE IF NOT EXISTS findings(
                    finding_id TEXT PRIMARY KEY,
                    session_id TEXT,
                    signature TEXT,
                    severity TEXT,
                    category TEXT,
                    title TEXT,
                    one_liner TEXT,
                    recommended INTEGER
                );
                CREATE TABLE IF NOT EXISTS artifacts(
                    artifact_id TEXT PRIMARY KEY,
                    session_id TEXT,
                    run_id TEXT,
                    kind TEXT,
                    path TEXT,
                    sha256 TEXT,
                    size_bytes INTEGER,
                    created_at TEXT
                );
                CREATE TABLE IF NOT EXISTS favorites(
                    favorite_id TEXT PRIMARY KEY,
                    kind TEXT,
                    capability_id TEXT,
                    pinned INTEGER,
                    created_at TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_sessions_updated ON sessions(updated_at DESC);
                CREATE INDEX IF NOT EXISTS idx_runs_recent ON runs(session_id, started_at DESC);
                CREATE INDEX IF NOT EXISTS idx_runs_cap ON runs(capability_id);
                CREATE INDEX IF NOT EXISTS idx_findings_session ON findings(session_id);
                CREATE INDEX IF NOT EXISTS idx_artifacts_session ON artifacts(session_id);
                CREATE INDEX IF NOT EXISTS idx_artifacts_kind ON artifacts(kind);
                """
            )
            version = 1
        if version < 2:
            conn.executescript(
                """
                CREATE TABLE IF NOT EXISTS file_index(
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    root TEXT,
                    path TEXT,
                    name TEXT,
                    ext TEXT,
                    size_bytes INTEGER,
                    mtime TEXT
                );
                CREATE TABLE IF NOT EXISTS file_index_roots(
                    root TEXT PRIMARY KEY,
                    enabled INTEGER NOT NULL DEFAULT 1,
                    updated_at TEXT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ux_file_index_root_path ON file_index(root, path);
                CREATE INDEX IF NOT EXISTS idx_file_index_name ON file_index(name);
                CREATE INDEX IF NOT EXISTS idx_file_index_ext ON file_index(ext);
                CREATE INDEX IF NOT EXISTS idx_file_index_size ON file_index(size_bytes DESC);
                """
            )
            version = 2
        conn.execute("DELETE FROM schema_version")
        conn.execute("INSERT INTO schema_version(version, updated_at) VALUES(?, ?)", (version, _now_iso()))


def _session_summary(session: dict[str, Any]) -> str:
    findings = session.get("findings", []) if isinstance(session.get("findings", []), list) else []
    top = str((findings[0] or {}).get("title", "")).strip() if findings else ""
    out = f"{session.get('symptom', 'Quick Check')} | {len(findings)} finding(s)"
    if top:
        out += f" | {top}"
    return out


def upsert_session_row(
    session_id: str,
    *,
    created_at: str = "",
    updated_at: str = "",
    goal: str = "",
    status: str = "",
    ui_mode: str = "",
    summary_plain: str = "",
    last_export_preset: str = "",
    last_export_path: str = "",
) -> None:
    sid = str(session_id or "").strip()
    if not sid:
        return
    with _WRITE_LOCK, _conn() as conn:
        existing = conn.execute("SELECT created_at, last_export_preset, last_export_path FROM sessions WHERE session_id=?", (sid,)).fetchone()
        created = str(created_at or "").strip() or (str(existing[0]) if existing and existing[0] else _now_iso())
        preset = str(last_export_preset or "").strip() or (str(existing[1]) if existing and existing[1] else "")
        export_path = str(last_export_path or "").strip() or (str(existing[2]) if existing and existing[2] else "")
        conn.execute(
            """
            INSERT INTO sessions(session_id, created_at, updated_at, goal, status, ui_mode, summary_plain, last_export_preset, last_export_path)
            VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(session_id) DO UPDATE SET
                created_at=excluded.created_at,
                updated_at=excluded.updated_at,
                goal=excluded.goal,
                status=excluded.status,
                ui_mode=excluded.ui_mode,
                summary_plain=excluded.summary_plain,
                last_export_preset=excluded.last_export_preset,
                last_export_path=excluded.last_export_path
            """,
            (sid, created, str(updated_at or _now_iso()), goal, status, ui_mode, summary_plain, preset, export_path),
        )


def upsert_session_from_payload(session: dict[str, Any], *, status: str = "open") -> None:
    sid = str(session.get("session_id", "")).strip()
    if not sid:
        return
    created = str(session.get("sysinfo", {}).get("timestamp_utc", "")).strip() or str(session.get("created_local", "")).strip()
    upsert_session_row(
        sid,
        created_at=created,
        updated_at=_now_iso(),
        goal=str(session.get("symptom", "Quick Check")),
        status=status,
        ui_mode=str(session.get("ui_mode", "")),
        summary_plain=_session_summary(session),
        last_export_preset=str(session.get("last_export_preset", "")),
        last_export_path=str(session.get("last_export_path", "")),
    )


def upsert_findings(session_id: str, findings: list[dict[str, Any]]) -> int:
    sid = str(session_id or "").strip()
    if not sid:
        return 0
    count = 0
    with _WRITE_LOCK, _conn() as conn:
        conn.execute("DELETE FROM findings WHERE session_id=?", (sid,))
        for row in findings or []:
            if not isinstance(row, dict):
                continue
            signature = str(row.get("signature", "")).strip() or "|".join([str(row.get("category", "General")), str(row.get("title", "")), str(row.get("detail", ""))])
            severity = str(row.get("status", row.get("severity", "INFO"))).upper()
            one_liner = str(row.get("plain", row.get("detail", "")))
            fid = _sha1(f"{sid}|{signature}")
            conn.execute(
                """
                INSERT INTO findings(finding_id, session_id, signature, severity, category, title, one_liner, recommended)
                VALUES(?, ?, ?, ?, ?, ?, ?, ?)
                ON CONFLICT(finding_id) DO UPDATE SET
                    session_id=excluded.session_id,
                    signature=excluded.signature,
                    severity=excluded.severity,
                    category=excluded.category,
                    title=excluded.title,
                    one_liner=excluded.one_liner,
                    recommended=excluded.recommended
                """,
                (fid, sid, signature, severity, str(row.get("category", "General")), str(row.get("title", "")), one_liner, 1 if severity in {"CRIT", "WARN"} else 0),
            )
            count += 1
    return count


def upsert_artifacts(session_id: str, artifacts: list[dict[str, Any] | str], *, run_id: str = "") -> int:
    sid = str(session_id or "").strip()
    rid = str(run_id or "").strip()
    if not sid:
        return 0
    count = 0
    with _WRITE_LOCK, _conn() as conn:
        for raw in artifacts or []:
            if isinstance(raw, dict):
                path = str(raw.get("path", "")).strip()
                kind = str(raw.get("kind", raw.get("category", "evidence"))).strip() or "evidence"
            else:
                path = str(raw or "").strip()
                kind = "evidence"
            if not path:
                continue
            p = Path(path)
            size = 0
            created = ""
            sha = ""
            if p.exists() and p.is_file():
                try:
                    size = int(p.stat().st_size)
                    created = datetime.fromtimestamp(p.stat().st_mtime, tz=timezone.utc).isoformat()
                except OSError:
                    ...
                sha = _sha256_file(p)
            aid = _sha1(f"{sid}|{rid}|{path}")
            conn.execute(
                """
                INSERT INTO artifacts(artifact_id, session_id, run_id, kind, path, sha256, size_bytes, created_at)
                VALUES(?, ?, ?, ?, ?, ?, ?, ?)
                ON CONFLICT(artifact_id) DO UPDATE SET
                    session_id=excluded.session_id,
                    run_id=excluded.run_id,
                    kind=excluded.kind,
                    path=excluded.path,
                    sha256=excluded.sha256,
                    size_bytes=excluded.size_bytes,
                    created_at=excluded.created_at
                """,
                (aid, sid, rid, kind, path, sha, size, created or _now_iso()),
            )
            count += 1
    return count


def replace_all_favorites(*, fixes: list[str], tools: list[str], runbooks: list[str]) -> None:
    initialize_db()
    now = _now_iso()
    with _WRITE_LOCK, _conn() as conn:
        conn.execute("DELETE FROM favorites")
        for kind, rows in (("fix", fixes), ("tool", tools), ("runbook", runbooks)):
            for cap in rows or []:
                cid = str(cap or "").strip()
                if not cid:
                    continue
                fid = _sha1(f"{kind}|{cid}")
                conn.execute("INSERT INTO favorites(favorite_id, kind, capability_id, pinned, created_at) VALUES(?, ?, ?, 1, ?)", (fid, kind, cid, now))


def _derive_identity(data: dict[str, Any]) -> tuple[str, str]:
    if data.get("tool_id"):
        return str(data.get("tool_id")), "tool"
    if data.get("task_id"):
        return str(data.get("task_id")), "script"
    if data.get("runbook_id"):
        return str(data.get("runbook_id")), "runbook"
    if data.get("fix_id"):
        return str(data.get("fix_id")), "fix"
    if data.get("export_preset"):
        return str(data.get("export_preset")), "export"
    if data.get("capability_id"):
        return str(data.get("capability_id")), str(data.get("kind", "task") or "task")
    return str(data.get("name", "")), str(data.get("kind", "task") or "task")


def record_run_event(run_id: str, event_type: str, ts_utc: float, *, message: str = "", data: dict[str, Any] | None = None) -> None:
    rid = str(run_id or "").strip()
    if not rid:
        return
    initialize_db()
    event = str(event_type or "").strip().upper()
    payload = data if isinstance(data, dict) else {}
    if event == "START":
        sid = str(payload.get("session_id", "")).strip()
        cap, kind = _derive_identity(payload)
        with _WRITE_LOCK, _conn() as conn:
            conn.execute(
                """
                INSERT INTO runs(run_id, session_id, capability_id, kind, started_at, ended_at, status, exit_code, duration_ms, last_log_line)
                VALUES(?, ?, ?, ?, ?, '', 'running', NULL, NULL, ?)
                ON CONFLICT(run_id) DO UPDATE SET
                    session_id=excluded.session_id,
                    capability_id=excluded.capability_id,
                    kind=excluded.kind,
                    started_at=excluded.started_at,
                    status='running',
                    last_log_line=excluded.last_log_line
                """,
                (rid, sid, cap, kind, _iso(ts_utc), str(message or "")[:400]),
            )
        return
    if event in {"STDOUT", "STDERR", "STATUS", "WARNING", "ERROR", "PROGRESS"}:
        if not message:
            return
        if event not in {"STATUS", "WARNING", "ERROR"}:
            with _LOG_LOCK:
                last = _LAST_LOG_TS.get(rid, 0.0)
                now = float(ts_utc or 0.0)
                if now - last < 0.35:
                    return
                _LAST_LOG_TS[rid] = now
        with _WRITE_LOCK, _conn() as conn:
            conn.execute("UPDATE runs SET last_log_line=? WHERE run_id=?", (str(message).strip()[:400], rid))
            if event == "ERROR":
                conn.execute("UPDATE runs SET status='fail' WHERE run_id=?", (rid,))
        return
    if event == "ARTIFACT":
        artifact_path = ""
        if isinstance(payload, dict):
            artifact_path = str(payload.get("path", "")).strip()
        if not artifact_path and message:
            text = str(message).strip()
            if ":" in text:
                artifact_path = text.split(":", 1)[1].strip()
        if artifact_path:
            with _WRITE_LOCK, _conn() as conn:
                row = conn.execute("SELECT session_id FROM runs WHERE run_id=?", (rid,)).fetchone()
            sid = str(row[0]).strip() if row and row[0] else ""
            if sid:
                upsert_artifacts(sid, [{"path": artifact_path, "kind": "artifact"}], run_id=rid)
        return
    if event == "END":
        code = _safe_int(payload.get("code", 0), 0)
        end_iso = _iso(ts_utc)
        with _WRITE_LOCK, _conn() as conn:
            row = conn.execute("SELECT started_at FROM runs WHERE run_id=?", (rid,)).fetchone()
            duration_ms = 0
            if row and row[0]:
                try:
                    start_dt = datetime.fromisoformat(str(row[0]).replace("Z", "+00:00"))
                    end_dt = datetime.fromisoformat(end_iso.replace("Z", "+00:00"))
                    duration_ms = max(0, int((end_dt - start_dt).total_seconds() * 1000))
                except Exception:
                    duration_ms = 0
            status = "success" if code == 0 else ("canceled" if code == 130 else "fail")
            conn.execute("UPDATE runs SET ended_at=?, status=?, exit_code=?, duration_ms=?, last_log_line=COALESCE(NULLIF(last_log_line,''), ?) WHERE run_id=?", (end_iso, status, code, duration_ms, str(message or "")[:400], rid))


def list_sessions(limit: int = 50, query: str = "") -> list[dict[str, Any]]:
    initialize_db()
    q = str(query or "").strip().lower()
    params: list[Any] = []
    sql = "SELECT session_id, created_at, updated_at, goal, status, ui_mode, summary_plain, last_export_preset, last_export_path FROM sessions"
    if q:
        like = f"%{q}%"
        sql += " WHERE lower(session_id) LIKE ? OR lower(goal) LIKE ? OR lower(summary_plain) LIKE ?"
        params.extend([like, like, like])
    sql += " ORDER BY COALESCE(updated_at, created_at) DESC LIMIT ?"
    params.append(max(1, int(limit)))
    with _conn() as conn:
        rows = conn.execute(sql, tuple(params)).fetchall()
    return [{k: row[k] for k in row.keys()} for row in rows]


def get_session(session_id: str) -> dict[str, Any] | None:
    initialize_db()
    sid = str(session_id or "").strip()
    if not sid:
        return None
    with _conn() as conn:
        row = conn.execute(
            "SELECT session_id, created_at, updated_at, goal, status, ui_mode, summary_plain, last_export_preset, last_export_path FROM sessions WHERE session_id=?",
            (sid,),
        ).fetchone()
    return {k: row[k] for k in row.keys()} if row else None


def list_recent_runs(limit: int = 20, session_id: str = "") -> list[dict[str, Any]]:
    initialize_db()
    sid = str(session_id or "").strip()
    sql = "SELECT run_id, session_id, capability_id, kind, started_at, ended_at, status, exit_code, duration_ms, last_log_line FROM runs"
    params: list[Any] = []
    if sid:
        sql += " WHERE session_id=?"
        params.append(sid)
    sql += " ORDER BY COALESCE(ended_at, started_at) DESC LIMIT ?"
    params.append(max(1, int(limit)))
    with _conn() as conn:
        rows = conn.execute(sql, tuple(params)).fetchall()
    return [{k: row[k] for k in row.keys()} for row in rows]

def search_index(query: str, limit: int = 60) -> list[dict[str, Any]]:
    initialize_db()
    q = str(query or "").strip().lower()
    if not q:
        return []
    like = f"%{q}%"
    out: list[dict[str, Any]] = []
    with _conn() as conn:
        sessions = conn.execute(
            "SELECT session_id, goal, summary_plain, created_at, updated_at FROM sessions WHERE lower(session_id) LIKE ? OR lower(goal) LIKE ? OR lower(summary_plain) LIKE ? LIMIT 120",
            (like, like, like),
        ).fetchall()
        for row in sessions:
            out.append(
                {
                    "kind": "session",
                    "key": str(row["session_id"]),
                    "title": str(row["goal"] or row["session_id"]),
                    "subtitle": str(row["summary_plain"] or ""),
                    "score": 300 if str(row["session_id"]).lower() == q else (220 if str(row["session_id"]).lower().startswith(q) else 140),
                }
            )

        runs = conn.execute(
            "SELECT run_id, capability_id, kind, status, last_log_line, ended_at, started_at FROM runs WHERE lower(run_id) LIKE ? OR lower(capability_id) LIKE ? OR lower(last_log_line) LIKE ? LIMIT 180",
            (like, like, like),
        ).fetchall()
        for row in runs:
            cap = str(row["capability_id"] or row["run_id"])
            base = 260 if cap.lower() == q else (200 if cap.lower().startswith(q) else 120)
            out.append(
                {
                    "kind": "run",
                    "key": str(row["run_id"]),
                    "title": cap,
                    "subtitle": f"{row['kind'] or 'run'} | {row['status'] or 'unknown'} | {str(row['last_log_line'] or '')[:120]}",
                    "score": base,
                }
            )

        findings = conn.execute(
            "SELECT finding_id, session_id, severity, category, title, one_liner FROM findings WHERE lower(title) LIKE ? OR lower(one_liner) LIKE ? OR lower(category) LIKE ? LIMIT 240",
            (like, like, like),
        ).fetchall()
        for row in findings:
            out.append(
                {
                    "kind": "finding",
                    "key": str(row["title"] or row["finding_id"]),
                    "title": str(row["title"]),
                    "subtitle": f"{row['severity'] or 'INFO'} | {row['category'] or 'General'} | session={row['session_id']}",
                    "score": 180,
                }
            )

        artifacts = conn.execute(
            "SELECT artifact_id, session_id, kind, path FROM artifacts WHERE lower(path) LIKE ? OR lower(kind) LIKE ? LIMIT 240",
            (like, like),
        ).fetchall()
        for row in artifacts:
            path = str(row["path"] or "")
            out.append(
                {
                    "kind": "artifact",
                    "key": path,
                    "title": Path(path).name if path else str(row["artifact_id"]),
                    "subtitle": f"{row['kind'] or 'artifact'} | session={row['session_id']} | {path}",
                    "score": 150,
                }
            )
        files = conn.execute(
            "SELECT root, path, name, ext, size_bytes FROM file_index WHERE lower(path) LIKE ? OR lower(name) LIKE ? LIMIT 180",
            (like, like),
        ).fetchall()
        for row in files:
            path = str(row["path"] or "")
            size_mb = float(row["size_bytes"] or 0) / (1024 * 1024)
            out.append(
                {
                    "kind": "file",
                    "key": path,
                    "title": str(row["name"] or Path(path).name),
                    "subtitle": f"{row['ext'] or ''} | {size_mb:.2f} MB | {path}",
                    "score": 145,
                }
            )
    out.sort(key=lambda item: (int(item.get("score", 0)), item.get("title", "")), reverse=True)
    return out[: max(10, int(limit))]


def record_export_artifacts(session_id: str, folder: str | Path, zip_path: str | Path, preset: str) -> None:
    sid = str(session_id or "").strip()
    if not sid:
        return
    folder_path = Path(str(folder))
    zip_file = Path(str(zip_path))
    rows: list[dict[str, Any]] = []
    if folder_path.exists():
        for file in folder_path.rglob("*"):
            if not file.is_file():
                continue
            rel = str(file.relative_to(folder_path)).replace("\\", "/")
            kind = rel.split("/", 1)[0] if "/" in rel else "export"
            rows.append({"path": str(file), "kind": kind})
    if zip_file.exists() and zip_file.is_file():
        rows.append({"path": str(zip_file), "kind": "zip"})
    if rows:
        upsert_artifacts(sid, rows)
    upsert_session_row(
        sid,
        updated_at=_now_iso(),
        status="exported",
        last_export_preset=str(preset or ""),
        last_export_path=str(zip_file),
    )


def _legacy_kind(key: str) -> str:
    token = str(key or "").strip().lower()
    if token.startswith("task_"):
        return "script"
    if token.startswith("home_") or token.startswith("it_"):
        return "runbook"
    if token.startswith("tool_"):
        return "tool"
    if token.startswith("export"):
        return "export"
    if token.startswith("fix") or token.endswith("_fix"):
        return "fix"
    return "run"


def rebuild_from_sessions_folder() -> dict[str, int]:
    initialize_db()
    sessions_dir = ensure_dirs()["sessions"]
    session_files = sorted([p for p in sessions_dir.glob("*.json") if p.name.lower() != "index.json"])
    sessions_count = 0
    findings_count = 0
    artifacts_count = 0
    runs_count = 0

    with _WRITE_LOCK, _conn() as conn:
        conn.execute("DELETE FROM sessions")
        conn.execute("DELETE FROM findings")
        conn.execute("DELETE FROM artifacts")
        conn.execute("DELETE FROM runs")

    for file in session_files:
        try:
            payload = json.loads(file.read_text(encoding="utf-8"))
        except Exception:
            continue
        if not isinstance(payload, dict):
            continue
        sid = str(payload.get("session_id", "")).strip()
        if not sid:
            continue
        upsert_session_from_payload(payload)
        sessions_count += 1
        findings_count += upsert_findings(sid, payload.get("findings", []) if isinstance(payload.get("findings", []), list) else [])
        evidence_rows = payload.get("evidence", {}).get("files", []) if isinstance(payload.get("evidence", {}), dict) else []
        if isinstance(evidence_rows, list):
            artifacts_count += upsert_artifacts(sid, evidence_rows)
        actions = payload.get("actions", [])
        if isinstance(actions, list):
            created = str(payload.get("sysinfo", {}).get("timestamp_utc", "")).strip() or str(payload.get("created_local", "")).strip() or _now_iso()
            with _WRITE_LOCK, _conn() as conn:
                for idx, action in enumerate(actions):
                    if not isinstance(action, dict):
                        continue
                    key = str(action.get("key", "")).strip()
                    code = _safe_int(action.get("code", 0), 0)
                    run_id = f"legacy_{sid}_{idx:04d}_{_sha1(key)[:8]}"
                    status = "success" if code == 0 else ("canceled" if code == 130 else "fail")
                    last_line = str(action.get("result", "")).strip().splitlines()[0][:400] if str(action.get("result", "")).strip() else ""
                    conn.execute(
                        """
                        INSERT INTO runs(run_id, session_id, capability_id, kind, started_at, ended_at, status, exit_code, duration_ms, last_log_line)
                        VALUES(?, ?, ?, ?, ?, ?, ?, ?, 0, ?)
                        ON CONFLICT(run_id) DO UPDATE SET
                            session_id=excluded.session_id,
                            capability_id=excluded.capability_id,
                            kind=excluded.kind,
                            started_at=excluded.started_at,
                            ended_at=excluded.ended_at,
                            status=excluded.status,
                            exit_code=excluded.exit_code,
                            last_log_line=excluded.last_log_line
                        """,
                        (run_id, sid, key, _legacy_kind(key), created, created, status, code, last_line),
                    )
                    runs_count += 1

    index_json = sessions_dir / "index.json"
    if index_json.exists():
        try:
            rows = json.loads(index_json.read_text(encoding="utf-8"))
        except Exception:
            rows = []
        if isinstance(rows, list):
            for row in rows:
                if not isinstance(row, dict):
                    continue
                sid = str(row.get("session_id", "")).strip()
                if not sid:
                    continue
                upsert_session_row(
                    sid,
                    created_at=str(row.get("created_utc", "")),
                    updated_at=_now_iso(),
                    goal=str(row.get("symptom", "Quick Check")),
                    status="open",
                    summary_plain=str(row.get("summary", "")),
                    last_export_path=str(row.get("last_export_path", "")),
                )

    return {"sessions": sessions_count, "findings": findings_count, "artifacts": artifacts_count, "runs": runs_count}


def set_file_index_roots(roots: list[str]) -> None:
    initialize_db()
    clean = sorted({str(r).strip() for r in roots if str(r).strip()})
    with _WRITE_LOCK, _conn() as conn:
        conn.execute("DELETE FROM file_index_roots")
        for root in clean:
            conn.execute("INSERT INTO file_index_roots(root, enabled, updated_at) VALUES(?, 1, ?)", (root, _now_iso()))


def get_file_index_roots() -> list[str]:
    initialize_db()
    with _conn() as conn:
        rows = conn.execute("SELECT root FROM file_index_roots WHERE enabled=1 ORDER BY root").fetchall()
    return [str(row[0]) for row in rows]


def get_file_index_existing(root: str) -> dict[str, tuple[str, int]]:
    initialize_db()
    key = str(root or "").strip()
    if not key:
        return {}
    with _conn() as conn:
        rows = conn.execute("SELECT path, mtime, size_bytes FROM file_index WHERE root=?", (key,)).fetchall()
    return {str(row["path"]): (str(row["mtime"] or ""), _safe_int(row["size_bytes"], 0)) for row in rows}


def upsert_file_index_rows(root: str, rows: list[dict[str, Any]]) -> int:
    initialize_db()
    key = str(root or "").strip()
    if not key:
        return 0
    count = 0
    with _WRITE_LOCK, _conn() as conn:
        for row in rows:
            path = str(row.get("path", "")).strip()
            if not path:
                continue
            conn.execute(
                """
                INSERT INTO file_index(root, path, name, ext, size_bytes, mtime)
                VALUES(?, ?, ?, ?, ?, ?)
                ON CONFLICT(root, path) DO UPDATE SET
                    name=excluded.name,
                    ext=excluded.ext,
                    size_bytes=excluded.size_bytes,
                    mtime=excluded.mtime
                """,
                (key, path, str(row.get("name", "")), str(row.get("ext", "")), _safe_int(row.get("size_bytes", 0), 0), str(row.get("mtime", ""))),
            )
            count += 1
    return count


def delete_file_index_paths_not_in(root: str, keep_paths: set[str]) -> int:
    initialize_db()
    key = str(root or "").strip()
    if not key:
        return 0
    with _WRITE_LOCK, _conn() as conn:
        if not keep_paths:
            cur = conn.execute("DELETE FROM file_index WHERE root=?", (key,))
            return int(cur.rowcount or 0)
        placeholders = ",".join(["?"] * len(keep_paths))
        params: list[Any] = [key]
        params.extend(sorted(keep_paths))
        cur = conn.execute(f"DELETE FROM file_index WHERE root=? AND path NOT IN ({placeholders})", tuple(params))
        return int(cur.rowcount or 0)


def search_file_index(query: str, *, limit: int = 500, root: str = "") -> list[dict[str, Any]]:
    initialize_db()
    q = str(query or "").strip().lower()
    sql = "SELECT root, path, name, ext, size_bytes, mtime FROM file_index"
    where: list[str] = []
    params: list[Any] = []
    if root:
        where.append("root=?")
        params.append(str(root))
    if q:
        like = f"%{q}%"
        where.append("(lower(path) LIKE ? OR lower(name) LIKE ? OR lower(ext) LIKE ?)")
        params.extend([like, like, like])
    if where:
        sql += " WHERE " + " AND ".join(where)
    sql += " ORDER BY size_bytes DESC, mtime DESC LIMIT ?"
    params.append(max(1, int(limit)))
    with _conn() as conn:
        rows = conn.execute(sql, tuple(params)).fetchall()
    return [{k: row[k] for k in row.keys()} for row in rows]


def get_run(run_id: str) -> dict[str, Any] | None:
    initialize_db()
    rid = str(run_id or "").strip()
    if not rid:
        return None
    with _conn() as conn:
        row = conn.execute(
            "SELECT run_id, session_id, capability_id, kind, started_at, ended_at, status, exit_code, duration_ms, last_log_line FROM runs WHERE run_id=?",
            (rid,),
        ).fetchone()
    return {k: row[k] for k in row.keys()} if row else None


def count_findings_for_session(session_id: str) -> int:
    initialize_db()
    sid = str(session_id or "").strip()
    if not sid:
        return 0
    with _conn() as conn:
        row = conn.execute("SELECT COUNT(*) FROM findings WHERE session_id=?", (sid,)).fetchone()
    return _safe_int(row[0] if row else 0, 0)


def count_artifacts_for_session(session_id: str) -> int:
    initialize_db()
    sid = str(session_id or "").strip()
    if not sid:
        return 0
    with _conn() as conn:
        row = conn.execute("SELECT COUNT(*) FROM artifacts WHERE session_id=?", (sid,)).fetchone()
    return _safe_int(row[0] if row else 0, 0)


def clear_file_index() -> int:
    initialize_db()
    with _WRITE_LOCK, _conn() as conn:
        before = _safe_int(conn.execute("SELECT COUNT(*) FROM file_index").fetchone()[0], 0)
        conn.execute("DELETE FROM file_index")
    return before


def vacuum_database() -> None:
    initialize_db()
    with _WRITE_LOCK, _conn() as conn:
        conn.execute("VACUUM")


def db_stats() -> DbStats:
    initialize_db()
    path = db_path()
    with _conn() as conn:
        sessions = _safe_int(conn.execute("SELECT COUNT(*) FROM sessions").fetchone()[0], 0)
        runs = _safe_int(conn.execute("SELECT COUNT(*) FROM runs").fetchone()[0], 0)
        findings = _safe_int(conn.execute("SELECT COUNT(*) FROM findings").fetchone()[0], 0)
        artifacts = _safe_int(conn.execute("SELECT COUNT(*) FROM artifacts").fetchone()[0], 0)
        favorites = _safe_int(conn.execute("SELECT COUNT(*) FROM favorites").fetchone()[0], 0)
        file_rows = _safe_int(conn.execute("SELECT COUNT(*) FROM file_index").fetchone()[0], 0)
    return DbStats(
        path=str(path),
        size_bytes=path.stat().st_size if path.exists() else 0,
        sessions=sessions,
        runs=runs,
        findings=findings,
        artifacts=artifacts,
        favorites=favorites,
        file_index_rows=file_rows,
    )
