from __future__ import annotations

import csv
import os
import time
from datetime import datetime, timezone
from pathlib import Path
from threading import Event
from typing import Any, Callable

from .db import (
    clear_file_index,
    delete_file_index_paths_not_in,
    get_file_index_existing,
    get_file_index_roots,
    search_file_index,
    set_file_index_roots,
    upsert_file_index_rows,
)


def default_roots() -> list[str]:
    return [
        str(Path.home() / "Desktop"),
        str(Path.home() / "Documents"),
        str(Path.home() / "Downloads"),
    ]


def resolve_roots(roots: list[str] | None = None) -> list[str]:
    if roots:
        clean = [str(row).strip() for row in roots if str(row).strip()]
        return sorted({row for row in clean if Path(row).exists()})
    configured = [row for row in get_file_index_roots() if Path(row).exists()]
    if configured:
        return configured
    defaults = [row for row in default_roots() if Path(row).exists()]
    if defaults:
        set_file_index_roots(defaults)
    return defaults


def _mtime_iso(ts: float) -> str:
    return datetime.fromtimestamp(ts, tz=timezone.utc).isoformat()


def index_roots(
    root_paths: list[str],
    *,
    budget_seconds: int = 60,
    cancel_event: Event | None = None,
    progress_cb: Callable[[int, str], None] | None = None,
    log_cb: Callable[[str], None] | None = None,
) -> dict[str, Any]:
    roots = resolve_roots(root_paths)
    set_file_index_roots(roots)
    start = time.monotonic()
    budget = max(5, int(budget_seconds))
    scanned = 0
    changed = 0
    deleted = 0
    upserted = 0
    skipped_unchanged = 0
    processed_roots = 0

    for root in roots:
        if cancel_event is not None and cancel_event.is_set():
            break
        root_path = Path(root)
        if not root_path.exists():
            continue
        processed_roots += 1
        existing = get_file_index_existing(root)
        seen_paths: set[str] = set()
        batch: list[dict[str, Any]] = []
        if log_cb:
            log_cb(f"[file-index] root={root}")
        for dirpath, _dirs, files in os.walk(root):
            if cancel_event is not None and cancel_event.is_set():
                break
            if time.monotonic() - start > budget:
                break
            for name in files:
                if cancel_event is not None and cancel_event.is_set():
                    break
                if time.monotonic() - start > budget:
                    break
                p = Path(dirpath) / name
                try:
                    st = p.stat()
                except OSError:
                    continue
                scanned += 1
                path_text = str(p)
                seen_paths.add(path_text)
                mtime = _mtime_iso(st.st_mtime)
                size = int(st.st_size)
                old = existing.get(path_text)
                if old is not None and old[0] == mtime and int(old[1]) == size:
                    skipped_unchanged += 1
                    continue
                changed += 1
                batch.append(
                    {
                        "path": path_text,
                        "name": p.name,
                        "ext": p.suffix.lower(),
                        "size_bytes": size,
                        "mtime": mtime,
                    }
                )
                if len(batch) >= 400:
                    upserted += upsert_file_index_rows(root, batch)
                    batch = []
            if progress_cb:
                elapsed = max(0.0, time.monotonic() - start)
                pct = min(99, int((elapsed / budget) * 100))
                progress_cb(pct, f"Indexed {scanned} files...")
        if batch:
            upserted += upsert_file_index_rows(root, batch)
        deleted += delete_file_index_paths_not_in(root, seen_paths)

    elapsed_s = round(max(0.0, time.monotonic() - start), 2)
    if progress_cb:
        progress_cb(100, "File index build complete.")
    if log_cb:
        log_cb(
            f"[file-index] done roots={processed_roots} scanned={scanned} changed={changed} "
            f"upserted={upserted} deleted={deleted} unchanged={skipped_unchanged} elapsed={elapsed_s}s"
        )
    return {
        "roots": roots,
        "scanned": scanned,
        "changed": changed,
        "upserted": upserted,
        "deleted": deleted,
        "unchanged": skipped_unchanged,
        "elapsed_s": elapsed_s,
        "cancelled": bool(cancel_event is not None and cancel_event.is_set()),
    }


def search_files(query: str, *, limit: int = 300, root: str = "") -> list[dict[str, Any]]:
    return search_file_index(query, limit=limit, root=root)


def export_results_csv(rows: list[dict[str, Any]], path: Path) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    fields = ["root", "path", "name", "ext", "size_bytes", "mtime"]
    with path.open("w", encoding="utf-8", newline="") as fh:
        writer = csv.DictWriter(fh, fieldnames=fields)
        writer.writeheader()
        for row in rows:
            writer.writerow({field: row.get(field, "") for field in fields})
    return path


def clear_index() -> int:
    return clear_file_index()
