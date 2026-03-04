# Next Level DB (SQLite Case Index)

## Purpose
Fix Fox now uses a local SQLite case database as a fast query/index layer while keeping session JSON files as the canonical full payload.

- Full session payload source of truth: `%LOCALAPPDATA%\\FixFox\\sessions\\*.json`
- Query/index layer: `%LOCALAPPDATA%\\FixFox\\db\\fixfox.db`

## Module
- `src/core/db.py`

## Schema
Versioned with `schema_version` table and `migrate()`.

### Core tables
- `sessions(session_id, created_at, updated_at, goal, status, ui_mode, summary_plain, last_export_preset, last_export_path)`
- `runs(run_id, session_id, capability_id, kind, started_at, ended_at, status, exit_code, duration_ms, last_log_line)`
- `findings(finding_id, session_id, signature, severity, category, title, one_liner, recommended)`
- `artifacts(artifact_id, session_id, run_id, kind, path, sha256, size_bytes, created_at)`
- `favorites(favorite_id, kind, capability_id, pinned, created_at)`

### File index tables
- `file_index(id, root, path, name, ext, size_bytes, mtime)`
- `file_index_roots(root, enabled, updated_at)`

## Migration and startup
- DB is initialized at app start in `src/app.py` via `initialize_db()`.
- Migrations are idempotent and auto-applied.

## Rebuild strategy
- `rebuild_from_sessions_folder()` repopulates sessions/runs/findings/artifacts from persisted session JSON + `sessions/index.json`.
- Rebuild is exposed in UI:
  - Settings -> Advanced -> `Rebuild Database Index`

## Write integration

### Sessions + findings + artifacts
- `src/core/sessions.py` now updates DB on:
  - save/load session
  - persist new session
  - metadata update
- Session findings and evidence files are indexed into `findings` and `artifacts`.

### Runs and live log line indexing
- `src/core/run_events.py` now mirrors published events to DB through `record_run_event(...)`.
- START/END create and finalize run rows.
- STDOUT/STDERR/STATUS/WARNING/ERROR update `last_log_line` (throttled for high-volume streams).

### Exports
- `src/core/exporter.py` now indexes generated report/data/log/evidence files and final zip path via `record_export_artifacts(...)`.

### Favorites
- `src/core/settings.py` syncs favorites lists to DB on save.

## Query integration
- History and Home Recent sessions are DB-backed first (`list_sessions(...)`) with JSON fallback.
- Run Center is DB-backed first (`list_recent_runs(...)`) with action-list fallback.
- Command palette/global search includes DB-backed session/run/finding/artifact results.

## Maintenance controls (Settings -> Advanced)
- Rebuild Database Index
- Vacuum Database
- Clear File Index
- Live DB path/size/counts display

## Troubleshooting
1. If history/search appears stale:
- Run `Rebuild Database Index` from Settings -> Advanced.

2. If DB file grows:
- Run `Vacuum Database`.

3. If fast file search results are outdated:
- Rebuild index from Playbooks -> Fast File Search.
- Or use `Clear File Index` in Settings -> Advanced and rebuild.

4. Safety/compatibility notes:
- DB is an index layer; full session JSON remains canonical.
- If DB is deleted, data can be rebuilt from sessions.
