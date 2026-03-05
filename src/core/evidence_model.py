from __future__ import annotations

from dataclasses import asdict, dataclass
from typing import Any


@dataclass(frozen=True)
class EvidenceItem:
    id: str
    title: str
    status: str
    summary: str
    details: str
    attachments: tuple[str, ...]

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


def coerce_evidence_items(payload: dict[str, Any] | None) -> list[EvidenceItem]:
    data = payload or {}
    rows = data.get("evidence_items", [])
    out: list[EvidenceItem] = []
    if isinstance(rows, list):
        for index, row in enumerate(rows):
            if not isinstance(row, dict):
                continue
            out.append(
                EvidenceItem(
                    id=str(row.get("id", f"evidence_{index+1}")).strip() or f"evidence_{index+1}",
                    title=str(row.get("title", "Evidence")).strip() or "Evidence",
                    status=str(row.get("status", "info")).strip() or "info",
                    summary=str(row.get("summary", "")).strip(),
                    details=str(row.get("details", "")).strip(),
                    attachments=tuple(str(p) for p in row.get("attachments", []) if str(p).strip()),
                )
            )
    if out:
        return out

    output_files = [str(p) for p in data.get("output_files", []) if str(p).strip()]
    evidence_files = [str(p) for p in data.get("evidence_files", []) if str(p).strip()]
    merged = output_files + [p for p in evidence_files if p not in output_files]
    if merged:
        out.append(
            EvidenceItem(
                id="artifacts",
                title="Artifacts",
                status="ok",
                summary=f"{len(merged)} artifact(s) captured.",
                details="Artifacts were generated and attached to this run.",
                attachments=tuple(merged),
            )
        )
    summary_text = str(data.get("summary_text", "")).strip()
    if summary_text:
        out.append(
            EvidenceItem(
                id="summary",
                title="Summary",
                status="info",
                summary=summary_text.splitlines()[0][:140] if summary_text else "",
                details=summary_text,
                attachments=tuple(),
            )
        )
    return out

