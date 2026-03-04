# Next Level Audit (2026-03-04)

## A) Current persistence model (pre-upgrade)

### Session payload storage
- Full sessions are persisted as JSON files under:
  - `%LOCALAPPDATA%\\FixFox\\sessions\\<session_id>.json`
- Code paths:
  - `src/core/paths.py` -> `ensure_dirs()["sessions"]`
  - `src/core/sessions.py` -> `save_session(...)`, `load_session(...)`

### Session index/meta storage
- Lightweight index is a single JSON file:
  - `%LOCALAPPDATA%\\FixFox\\sessions\\index.json`
- Fields currently tracked in `SessionMeta` (`src/core/sessions.py`):
  - `session_id`, `created_utc`, `symptom`, `summary`, `tags`, `pinned`, `last_export_path`

### Settings/favorites storage
- Settings are in:
  - `%LOCALAPPDATA%\\FixFox\\state\\settings.json`
- Favorites are embedded in settings lists:
  - `favorites_fixes`, `favorites_tools`, `favorites_runbooks`
- Code path:
  - `src/core/settings.py`

## B) How History and Search currently work

### History
- Home recent + History page both read from `load_index()` then optionally `load_session(...)` per selection.
- Run Center falls back to scanning active session actions, then iterates `load_index()` and `load_session(...)` for recent actions.
- Main code paths:
  - `src/ui/main_window.py`
    - `_refresh_home_history`
    - `_refresh_history`
    - `_update_history_detail`
    - `_refresh_run_center`

### Search / Command palette
- Search index is rebuilt in-memory each query from mixed sources:
  - capability registry, fixes, runbooks, script tasks, tools, KB
  - sessions via `load_index()`
  - exports by globbing `exports/*.zip`
- Code path:
  - `src/core/search.py` (`build_search_index`, `query_index`)

## C) Performance pain points (pre-upgrade)
- Repeated JSON scanning and deserialization for history/run center refreshes.
- Global search rebuilds a large mixed list every query (no persistent index).
- Export lookup scans export zip directory each search build.
- File index (Everything-lite script task) currently uses JSON blob in `state/file_index.json`:
  - no SQL indices
  - incremental merge is dict-based in Python only
  - no reusable indexed query layer for UI-wide search.

## D) Data duplication already present
- Session data appears in multiple places:
  - full `sessions/<id>.json`
  - summarized `sessions/index.json`
  - derived UI history/run-center rows
- Export metadata exists in:
  - `index.json:last_export_path`
  - export folder manifest/hashes
  - exported `data/session.json`
- Evidence references are embedded in session JSON and copied into export structure, but not centrally queryable.

## E) Proposed architecture and source-of-truth split

### Source of truth
- Keep full session JSON files authoritative for complete payload and compatibility.

### SQLite role
- Add local SQLite Case DB as fast index/query layer (rebuildable cache + operational index):
  - sessions, runs, findings, artifacts, favorites, file index rows.
- DB can be fully rebuilt from sessions + settings without data loss.

### Proposed ownership
- JSON session files: canonical full session content.
- SQLite DB: canonical fast query/index surface for UI history/search/run-center and file search.
- Export folders/manifests: immutable deliverables generated from current session payload.

### Migration/rebuild strategy
- Add schema version table + migration steps.
- On startup and on-demand (Settings Advanced), initialize/migrate DB.
- Provide `rebuild_from_sessions_folder()` to repopulate sessions/findings/artifacts/runs from existing persisted data.

## F) Planned upgrade path (implementation order)
1. Add `src/core/db.py` (sqlite3, migrations, rebuild, query APIs).
2. Wire session save/load/update + run events + exports/evidence updates into DB writes.
3. Switch history/search/run-center to DB-backed reads with filesystem fallback.
4. Move Everything-lite file index to DB tables and expose user-controlled build/search/export flow.
5. Upgrade report HTML to richer offline interactive deliverable.
