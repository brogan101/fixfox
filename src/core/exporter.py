from __future__ import annotations

import csv
import hashlib
import json
import re
import shutil
import zipfile
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from .brand import APP_TAGLINE, EXPORT_PREFIX, ICON_PNG, REPORT_TITLE
from .db import record_export_artifacts
from .masking import MaskingOptions, mask_text
from .paths import ensure_dirs
from .report import render_html
from .utils import resource_path


PRESETS = ("home_share", "ticket", "full")

TEXT_EXPORT_EXTS = {".txt", ".md", ".json", ".html", ".csv", ".log"}
SHARE_SAFE_SCAN_EXTS = {".txt", ".md", ".json", ".html", ".csv", ".log"}

USER_PATH_RE = re.compile(r"(?i)C:\\Users\\(?!<user>)[^\\\s]+")
HOST_RE = re.compile(r"\b(?:DESKTOP|LAPTOP)-[A-Z0-9]{4,}\b", re.IGNORECASE)
SSID_LINE_RE = re.compile(r"(?im)^\s*SSID(?:\s+\d+)?\s*:\s*(.+)$")


@dataclass
class ExportResult:
    zip_path: Path
    folder_path: Path
    manifest_path: Path
    hashes_path: Path
    validation_passed: bool
    validation_warnings: list[str]
    ticket_summary_short: str
    ticket_summary_detailed: str


def _safe_session_payload(session: dict[str, Any], options: MaskingOptions) -> dict[str, Any]:
    payload = json.loads(json.dumps(session))

    def walk(value: Any) -> Any:
        if isinstance(value, dict):
            return {k: walk(v) for k, v in value.items()}
        if isinstance(value, list):
            return [walk(v) for v in value]
        if isinstance(value, str):
            return mask_text(value, options)
        return value

    return walk(payload)


def _summary_markdown(session: dict[str, Any]) -> str:
    finding_count = len(session.get("findings", []))
    symptom = session.get("symptom", "Quick Check")
    sid = session.get("session_id", "unknown")
    lines = [
        f"# {REPORT_TITLE}",
        "",
        APP_TAGLINE,
        "",
        f"Plain English: This session analyzed '{symptom}' and found {finding_count} notable signals.",
        "",
        "## Next Steps",
    ]
    findings = session.get("findings", [])
    if findings:
        top = findings[0]
        lines.append(f"- Start with: {top.get('title', 'Top finding')}. {top.get('detail', '')}")
    else:
        lines.append("- No critical findings. Keep weekly checks enabled.")
    lines.extend(
        [
            "",
            "## Technical Appendix",
            f"- Session ID: `{sid}`",
            f"- Generated UTC: `{datetime.now(timezone.utc).isoformat()}`",
            "",
            "### Findings",
        ]
    )
    for f in findings:
        lines.append(
            f"- [{f.get('status', 'INFO')}] {f.get('title', '')}: {f.get('detail', '')} "
            f"(category={f.get('category', 'General')}, confidence={f.get('confidence', 0)}%)"
        )
    return "\n".join(lines)


def _ticket_summaries(session: dict[str, Any]) -> tuple[str, str]:
    sid = session.get("session_id", "unknown")
    symptom = session.get("symptom", "Quick Check")
    findings = session.get("findings", [])
    short = f"Session {sid}: {symptom}. Findings: {len(findings)}."
    details = [short]
    for row in findings[:6]:
        details.append(f"- {row.get('status', 'INFO')}: {row.get('title', '')} - {row.get('detail', '')}")
    if session.get("actions"):
        details.append("Actions:")
        for action in session.get("actions", []):
            details.append(
                f"- {action.get('title', '')}: code={action.get('code', '?')} result={action.get('result', '')}"
            )
    return short, "\n".join(details)


def _write_findings_csv(path: Path, findings: list[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fields = ["status", "category", "title", "plain", "detail", "confidence"]
    with path.open("w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fields)
        writer.writeheader()
        for row in findings:
            writer.writerow({field: row.get(field, "") for field in fields})


def _write_actions_log(path: Path, actions: list[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    lines: list[str] = []
    for action in actions:
        lines.append(
            f"{action.get('title', '')}\tcode={action.get('code', '?')}\trisk={action.get('risk', '')}\t{action.get('result', '')}"
        )
    if not lines:
        lines.append("No actions recorded.")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def _write_diagnostics_log(path: Path, session: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = [
        f"Session: {session.get('session_id', '')}",
        f"Symptom: {session.get('symptom', '')}",
        f"Findings: {len(session.get('findings', []))}",
        "",
        "Findings:",
    ]
    for finding in session.get("findings", []):
        lines.append(
            f"[{finding.get('status', 'INFO')}] {finding.get('title', '')}: {finding.get('detail', '')}"
        )
    if len(lines) == 5:
        lines.append("No findings.")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def _copy_brand_icon(report_dir: Path) -> str:
    src = Path(resource_path(ICON_PNG))
    dst_dir = report_dir / "brand"
    dst_dir.mkdir(parents=True, exist_ok=True)
    dst = dst_dir / "fixfox_mark.png"
    if src.exists():
        shutil.copy2(src, dst)
    return "brand/fixfox_mark.png"


def _as_evidence_rows(session: dict[str, Any]) -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    evidence = session.get("evidence", {})
    if isinstance(evidence, dict):
        files = evidence.get("files", [])
        for row in files:
            if isinstance(row, dict):
                path = str(row.get("path", ""))
                if path:
                    rows.append(
                        {
                            "path": path,
                            "category": str(row.get("category", "evidence")),
                            "task_id": str(row.get("task_id", "")),
                        }
                    )
            elif isinstance(row, str):
                rows.append({"path": row, "category": "evidence", "task_id": ""})
    return rows


def _allowed_categories(preset: str, include_logs: bool) -> set[str]:
    if preset == "full":
        return {"system", "network", "eventlogs", "updates", "crash", "printer", "evidence", "integrity", "performance"}
    if preset == "ticket":
        return {"system", "network", "eventlogs", "updates", "crash", "printer", "evidence"}
    allowed = {"system", "network", "evidence"}
    if include_logs:
        allowed.update({"eventlogs", "updates"})
    return allowed


def _target_subdir(category: str) -> str:
    key = (category or "").strip().lower()
    if key in {"system", "network", "eventlogs", "updates", "crash", "printer"}:
        return key
    if key in {"integrity", "performance"}:
        return "system"
    return "evidence"


def _copy_evidence(
    session: dict[str, Any],
    evidence_root: Path,
    mask_options: MaskingOptions,
    *,
    preset: str,
    include_logs: bool,
) -> None:
    rows = _as_evidence_rows(session)
    summary_lines: list[str] = []
    copied_any = False
    used_rel: set[str] = set()
    allowed = _allowed_categories(preset, include_logs)

    for required in ("eventlogs", "system", "network", "updates", "crash", "printer", "evidence"):
        (evidence_root / required).mkdir(parents=True, exist_ok=True)

    for row in rows:
        src = Path(row.get("path", ""))
        category = str(row.get("category", "evidence")).strip().lower()
        if category not in allowed:
            summary_lines.append(f"skipped by preset: {src.name}")
            continue
        if not src.exists() or not src.is_file():
            summary_lines.append(f"missing: {src}")
            continue
        target_dir = evidence_root / _target_subdir(category)
        task_prefix = str(row.get("task_id", "")).strip()
        base_name = src.name if not task_prefix else f"{task_prefix}_{src.name}"
        stem = Path(base_name).stem
        suffix = Path(base_name).suffix
        candidate = base_name
        counter = 1
        while (target_dir / candidate).exists() or f"{target_dir.name}/{candidate}".lower() in used_rel:
            candidate = f"{stem}_{counter}{suffix}"
            counter += 1
        used_rel.add(f"{target_dir.name}/{candidate}".lower())
        dst = target_dir / candidate
        try:
            if src.suffix.lower() in TEXT_EXPORT_EXTS:
                text = src.read_text(encoding="utf-8", errors="ignore")
                dst.write_text(mask_text(text, mask_options), encoding="utf-8")
            else:
                shutil.copy2(src, dst)
            copied_any = True
            summary_lines.append(f"copied: evidence/{target_dir.name}/{candidate}")
        except OSError as exc:
            summary_lines.append(f"copy failed: {src} ({exc})")

    if not copied_any:
        summary_lines.append("No evidence files were copied.")
    (evidence_root / "summary.txt").write_text("\n".join(summary_lines) + "\n", encoding="utf-8")


def _file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as f:
        while True:
            chunk = f.read(65536)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest()


def _build_manifest(folder: Path, preset: str, share_safe: bool, session_id: str) -> dict[str, Any]:
    files: list[dict[str, Any]] = []
    for f in sorted(folder.rglob("*")):
        if not f.is_file():
            continue
        rel = str(f.relative_to(folder)).replace("\\", "/")
        if rel == "manifest/hashes.txt":
            continue
        files.append({"path": rel, "size": f.stat().st_size})
    return {
        "session_id": session_id,
        "preset": preset,
        "share_safe": share_safe,
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "files": files,
    }


def _write_hashes(folder: Path) -> Path:
    lines: list[str] = []
    for f in sorted(folder.rglob("*")):
        if not f.is_file():
            continue
        rel = str(f.relative_to(folder)).replace("\\", "/")
        if rel == "manifest/hashes.txt":
            continue
        lines.append(f"{_file_sha256(f)}  {rel}")
    hashes_path = folder / "manifest" / "hashes.txt"
    hashes_path.parent.mkdir(parents=True, exist_ok=True)
    hashes_path.write_text("\n".join(lines) + ("\n" if lines else ""), encoding="utf-8")
    return hashes_path


def _share_safe_scan(folder: Path, raw_tokens: list[str]) -> list[str]:
    warnings: list[str] = []
    for f in folder.rglob("*"):
        if not f.is_file() or f.suffix.lower() not in SHARE_SAFE_SCAN_EXTS:
            continue
        rel = str(f.relative_to(folder)).replace("\\", "/")
        text = f.read_text(encoding="utf-8", errors="ignore")
        for token in raw_tokens:
            tok = str(token or "").strip()
            if tok and tok in text:
                warnings.append(f"Potential unmasked token in {rel}: {tok}")
                break
        if USER_PATH_RE.search(text):
            warnings.append(f"Potential unmasked user path in {rel}")
        if HOST_RE.search(text):
            warnings.append(f"Potential unmasked host in {rel}")
        for match in SSID_LINE_RE.findall(text):
            value = str(match).strip()
            if value and value not in {"<redacted>", "<pc-name>", "<user>"}:
                warnings.append(f"Potential unmasked SSID in {rel}: {value}")
                break
    return warnings


def validate_export_folder(
    folder: Path,
    manifest: dict[str, Any],
    share_safe: bool,
    raw_tokens: list[str],
) -> tuple[bool, list[str]]:
    warnings: list[str] = []
    required = {
        "report/report.html",
        "report/summary.md",
        "report/findings.csv",
        "data/session.json",
        "logs/actions.txt",
        "logs/diagnostics.txt",
        "manifest/manifest.json",
        "manifest/hashes.txt",
    }
    actual_files = {
        str(p.relative_to(folder)).replace("\\", "/")
        for p in folder.rglob("*")
        if p.is_file()
    }
    missing_required = sorted(required - actual_files)
    if missing_required:
        warnings.append(f"Missing required files: {', '.join(missing_required)}")

    manifest_files = {
        str(item.get("path", "")).replace("\\", "/")
        for item in manifest.get("files", [])
        if isinstance(item, dict)
    }
    for rel in actual_files:
        if rel == "manifest/hashes.txt":
            continue
        if rel not in manifest_files:
            warnings.append(f"Manifest missing file entry: {rel}")
            break
    for rel in manifest_files:
        if rel not in actual_files:
            warnings.append(f"Manifest entry missing on disk: {rel}")
            break

    hashes_path = folder / "manifest" / "hashes.txt"
    hash_rows: dict[str, str] = {}
    if hashes_path.exists():
        for line in hashes_path.read_text(encoding="utf-8", errors="ignore").splitlines():
            if "  " not in line:
                continue
            sha, rel = line.split("  ", 1)
            hash_rows[rel.strip()] = sha.strip()
    for rel in actual_files:
        if rel == "manifest/hashes.txt":
            continue
        if rel not in hash_rows:
            warnings.append(f"hashes.txt missing {rel}")
            break
        p = folder / rel
        if p.exists():
            sha = _file_sha256(p)
            if hash_rows.get(rel) != sha:
                warnings.append(f"Hash mismatch for {rel}")
                break

    preset = str(manifest.get("preset", "")).strip().lower()
    if preset == "ticket":
        for required_dir in ("evidence/eventlogs", "evidence/system", "evidence/network", "evidence/updates", "evidence/crash"):
            path = folder / required_dir
            if not path.exists() or not path.is_dir():
                warnings.append(f"Ticket preset missing required directory: {required_dir}")
    if share_safe:
        warnings.extend(_share_safe_scan(folder, raw_tokens))

    return len(warnings) == 0, warnings


def export_session(
    session: dict[str, Any],
    preset: str = "home_share",
    share_safe: bool = True,
    mask_ip: bool = False,
    include_logs: bool = False,
    allow_validator_override: bool = False,
) -> ExportResult:
    if preset not in PRESETS:
        raise ValueError(f"Unknown preset: {preset}")

    dirs = ensure_dirs()
    sid = str(session.get("session_id", "S_UNKNOWN"))
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    folder = dirs["exports"] / f"{sid}_{preset}_{stamp}"
    folder.mkdir(parents=True, exist_ok=True)

    report_dir = folder / "report"
    data_dir = folder / "data"
    logs_dir = folder / "logs"
    evidence_dir = folder / "evidence"
    manifest_dir = folder / "manifest"
    for p in (report_dir, data_dir, logs_dir, evidence_dir, manifest_dir):
        p.mkdir(parents=True, exist_ok=True)

    raw_tokens = [
        str(session.get("sysinfo", {}).get("hostname", "")),
        str(session.get("sysinfo", {}).get("user", "")),
        str(session.get("network", {}).get("ssid", "")),
    ]
    mask_options = MaskingOptions(
        enabled=share_safe,
        mask_ip=mask_ip,
        extra_tokens=tuple([t for t in raw_tokens if t]),
    )
    payload = _safe_session_payload(session, mask_options)

    short_summary, detailed_summary = _ticket_summaries(payload)
    icon_rel = _copy_brand_icon(report_dir)
    report_html = render_html(payload, icon_rel_path=icon_rel)
    (report_dir / "report.html").write_text(report_html, encoding="utf-8")
    (report_dir / "summary.md").write_text(_summary_markdown(payload), encoding="utf-8")
    _write_findings_csv(report_dir / "findings.csv", payload.get("findings", []))

    (data_dir / "session.json").write_text(json.dumps(payload, indent=2), encoding="utf-8")
    _write_actions_log(logs_dir / "actions.txt", payload.get("actions", []))
    _write_diagnostics_log(logs_dir / "diagnostics.txt", payload)
    (logs_dir / "ticket_summary_short.txt").write_text(short_summary + "\n", encoding="utf-8")
    (logs_dir / "ticket_summary_detailed.txt").write_text(detailed_summary + "\n", encoding="utf-8")

    _copy_evidence(payload, evidence_dir, mask_options, preset=preset, include_logs=include_logs)

    manifest_path = manifest_dir / "manifest.json"
    manifest = _build_manifest(folder, preset, share_safe, sid)
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    hashes_path = _write_hashes(folder)
    manifest = _build_manifest(folder, preset, share_safe, sid)
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    hashes_path = _write_hashes(folder)

    ok, warnings = validate_export_folder(folder, manifest, share_safe, raw_tokens)
    if (not ok) and (not allow_validator_override):
        raise RuntimeError("Export validation failed: " + " | ".join(warnings))

    zip_path = dirs["exports"] / f"{EXPORT_PREFIX}_{sid}_{preset}_{stamp}.zip"
    if zip_path.exists():
        zip_path.unlink()
    with zipfile.ZipFile(zip_path, "w", compression=zipfile.ZIP_DEFLATED) as zf:
        for file in folder.rglob("*"):
            if file.is_file():
                zf.write(file, arcname=str(file.relative_to(folder)))
    try:
        record_export_artifacts(sid, folder, zip_path, preset)
    except Exception:
        pass

    return ExportResult(
        zip_path=zip_path,
        folder_path=folder,
        manifest_path=manifest_path,
        hashes_path=hashes_path,
        validation_passed=ok,
        validation_warnings=warnings,
        ticket_summary_short=short_summary,
        ticket_summary_detailed=detailed_summary,
    )


def re_export_session(
    session: dict[str, Any],
    preset: str,
    share_safe: bool,
    mask_ip: bool = False,
    include_logs: bool = False,
) -> ExportResult:
    return export_session(
        session=session,
        preset=preset,
        share_safe=share_safe,
        mask_ip=mask_ip,
        include_logs=include_logs,
        allow_validator_override=False,
    )
