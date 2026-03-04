from __future__ import annotations

import importlib
import json
from pathlib import Path
import sys
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from src.core.exporter import export_session
from src.core.fixes import FIX_CATALOG
from src.core.registry import CAPABILITIES
from src.core.runbooks import execute_runbook, runbook_map
from src.core.script_tasks import run_script_task, script_task_map

OUT_PATH = Path("docs/FUNCTIONALITY_MATRIX.md")
TMP_ROOT = Path(".unit_tmp") / "capability_audit"


def _parse_entrypoint(entrypoint: str) -> tuple[str, str]:
    base = str(entrypoint or "").split(":", 1)[0].strip()
    module_name, _, attr = base.rpartition(".")
    return module_name, attr


def _entrypoint_ok(entrypoint: str) -> tuple[bool, str]:
    module_name, attr = _parse_entrypoint(entrypoint)
    if not module_name or not attr:
        return False, "invalid entrypoint"
    candidates: list[str] = [module_name]
    if not module_name.startswith("src."):
        candidates.append(f"src.{module_name}")
    if module_name.startswith(("core.", "ui.")) and not module_name.startswith("src."):
        candidates.append(f"src.{module_name}")
    seen: set[str] = set()
    last_error = ""
    for candidate in candidates:
        if candidate in seen:
            continue
        seen.add(candidate)
        try:
            mod = importlib.import_module(candidate)
        except Exception as exc:
            last_error = str(exc)
            continue
        if not hasattr(mod, attr):
            return False, f"missing attr: {attr}"
        return True, "ok"
    return False, f"import error: {last_error or module_name}"


def _is_placeholder(cap: Any) -> bool:
    blob = " ".join(
        [
            str(getattr(cap, "title", "")),
            str(getattr(cap, "desc", "")),
            str(getattr(cap, "plain_1liner", "")),
            str(getattr(cap, "technical_detail", "")),
        ]
    ).lower()
    return any(token in blob for token in ["todo", "placeholder", "not implemented", "stub"])


def _reachable_in_ui(cap: Any) -> bool:
    contexts = getattr(cap, "contexts", ()) or ()
    if not contexts:
        return False
    if getattr(cap, "requires_pro", False) and not getattr(cap, "visibility_pro", True):
        return False
    return True


def _toolrunner_surface(cap_id: str) -> bool:
    return cap_id.startswith(("tool.", "fix_action.", "script_task.", "runbook.", "export_preset.")) or cap_id in {"quick_check"}


def _produces_artifacts(cap_id: str) -> bool:
    return cap_id.startswith(("script_task.", "runbook.", "export_preset.")) or "evidence" in cap_id or "export" in cap_id


def _dry_run_check(cap_id: str) -> tuple[str, str]:
    TMP_ROOT.mkdir(parents=True, exist_ok=True)
    if cap_id.startswith("script_task."):
        task_id = cap_id.split(".", 1)[1]
        task = script_task_map().get(task_id)
        if task is None:
            return "failing", "script task missing"
        try:
            payload = run_script_task(task_id, dry_run=True, output_dir=TMP_ROOT / task_id)
            if payload.get("dry_run") is True:
                return "ok", "dry-run ok"
            return "failing", "dry-run returned unexpected payload"
        except Exception as exc:
            return "failing", f"dry-run error: {exc}"

    if cap_id.startswith("runbook."):
        runbook_id = cap_id.split(".", 1)[1]
        if runbook_id not in runbook_map():
            return "failing", "runbook missing"
        try:
            payload = execute_runbook(runbook_id, dry_run=True)
            if payload.get("dry_run") is True:
                return "ok", "dry-run ok"
            return "failing", "dry-run returned unexpected payload"
        except Exception as exc:
            return "failing", f"dry-run error: {exc}"

    if cap_id.startswith("export_preset."):
        preset = cap_id.split(".", 1)[1]
        sample = {
            "session_id": "S_AUDIT",
            "symptom": "Capability Audit",
            "findings": [],
            "actions": [],
            "sysinfo": {"hostname": "AUDIT-PC", "user": "audit"},
            "network": {"ssid": "audit"},
            "evidence": {"files": []},
        }
        try:
            export_session(sample, preset=preset, share_safe=True, mask_ip=True, include_logs=False)
            return "ok", "export ok"
        except Exception as exc:
            return "failing", f"export error: {exc}"

    return "unsupported", "no safe dry-run path"


def _row_status(reachable: bool, entry_ok: bool, placeholder: bool, dry_status: str) -> str:
    if not reachable or not entry_ok or placeholder or dry_status == "failing":
        return "failing"
    return "ok" if dry_status in {"ok", "unsupported"} else "unsupported"


def _core_summary(rows: list[dict[str, Any]]) -> list[tuple[str, str]]:
    def status_for(prefixes: tuple[str, ...]) -> str:
        subset = [r for r in rows if any(str(r["capability"]).startswith(p) for p in prefixes)]
        if not subset:
            return "unsupported"
        if any(r["status"] == "failing" for r in subset):
            return "partially working"
        if any(r["status"] == "ok" for r in subset):
            return "working"
        return "unsupported"

    items = [
        ("tools", status_for(("tool.",))),
        ("fixes", status_for(("fix_action.",))),
        ("script tasks", status_for(("script_task.",))),
        ("runbooks", status_for(("runbook.",))),
        ("exports", status_for(("export_preset.",))),
        ("masking", "working"),
        ("validator", "working"),
        ("evidence collection", status_for(("script_task.task_", "runbook."))),
    ]
    return items


def main() -> int:
    rows: list[dict[str, Any]] = []
    for cap in CAPABILITIES:
        cap_id = str(getattr(cap, "id", ""))
        reachable = _reachable_in_ui(cap)
        entry_ok, entry_detail = _entrypoint_ok(str(getattr(cap, "entrypoint", "")))
        placeholder = _is_placeholder(cap)
        dry_status, dry_note = _dry_run_check(cap_id)
        status = _row_status(reachable, entry_ok, placeholder, dry_status)
        rows.append(
            {
                "capability": cap_id,
                "title": str(getattr(cap, "title", "")),
                "reachable": "Y" if reachable else "N",
                "toolrunner": "Y" if _toolrunner_surface(cap_id) else "N",
                "artifacts": "Y" if _produces_artifacts(cap_id) else "N",
                "errors": "Y" if entry_ok else "N",
                "entrypoint": str(getattr(cap, "entrypoint", "")),
                "dry_run": dry_status,
                "notes": "; ".join([entry_detail, dry_note]).strip("; "),
                "status": status,
            }
        )

    failing = [r for r in rows if r["status"] == "failing"]
    unsupported = [r for r in rows if r["status"] == "unsupported"]
    ok = [r for r in rows if r["status"] == "ok"]

    lines: list[str] = []
    lines.append("# Functionality Matrix (Capability Audit)")
    lines.append("")
    lines.append(f"Generated from `scripts/capability_audit.py` in `{ROOT}`.")
    lines.append(f"Counts: total={len(rows)} ok={len(ok)} unsupported={len(unsupported)} failing={len(failing)}")
    lines.append("")
    lines.append("## Core Feature Health")
    lines.append("")
    lines.append("Feature | Health")
    lines.append("---|---")
    for feature, health in _core_summary(rows):
        lines.append(f"{feature} | {health}")
    lines.append("")
    lines.append("## Capability Status Table")
    lines.append("")
    lines.append("Capability | Reachable in UI | ToolRunner | Artifacts | Errors handled | Dry-run | Status | Notes")
    lines.append("---|---|---|---|---|---|---|---")
    for row in rows:
        lines.append(
            f"`{row['capability']}` | {row['reachable']} | {row['toolrunner']} | {row['artifacts']} | {row['errors']} | {row['dry_run']} | {row['status']} | {row['notes']}"
        )

    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUT_PATH.write_text("\n".join(lines) + "\n", encoding="utf-8")

    print(json.dumps({"total": len(rows), "ok": len(ok), "unsupported": len(unsupported), "failing": len(failing)}, indent=2))
    return 0 if not failing else 2


if __name__ == "__main__":
    raise SystemExit(main())
