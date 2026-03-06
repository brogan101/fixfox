from __future__ import annotations

from dataclasses import dataclass, field
from typing import Iterable


@dataclass
class ToolError(Exception):
    user_message: str
    technical_message: str = ""
    suggested_next_steps: list[str] = field(default_factory=list)

    def __post_init__(self) -> None:
        super().__init__(self.user_message)

    def as_payload(self) -> dict[str, object]:
        return {
            "user_message": self.user_message,
            "technical_message": self.technical_message,
            "suggested_next_steps": list(self.suggested_next_steps),
        }


class ToolNotSupported(ToolError):
    ...


class ToolPermissionDenied(ToolError):
    ...


class ToolTimeout(ToolError):
    ...


class ToolCanceled(ToolError):
    ...


class ToolFailed(ToolError):
    ...


class EvidenceCollectionFailed(ToolError):
    ...


def ensure_next_steps(steps: Iterable[str] | None) -> list[str]:
    out = [str(step).strip() for step in (steps or []) if str(step).strip()]
    if out:
        return out
    return [
        "Review the technical details in the Details tab.",
        "Try the related safe tool or runbook from Playbooks.",
        "Create a support bundle for escalation.",
    ]


def classify_exit(code: int, stderr: str = "") -> ToolError | None:
    if code == 0:
        return None
    details = (stderr or "").strip()
    if code == 130:
        return ToolCanceled(
            user_message="The operation was cancelled.",
            technical_message=details or "Cancellation token set.",
            suggested_next_steps=[
                "Re-run when ready.",
                "Try a smaller scoped tool first.",
                "Export partial evidence if needed.",
            ],
        )
    if code == 124:
        return ToolTimeout(
            user_message="The operation timed out before completion.",
            technical_message=details or "Command timed out.",
            suggested_next_steps=[
                "Re-run with fewer concurrent tools.",
                "Try the same tool again after reboot.",
                "Export partial evidence for review.",
            ],
        )
    if "administrator" in details.lower() or "access is denied" in details.lower():
        return ToolPermissionDenied(
            user_message="This operation needs administrator permissions.",
            technical_message=details or "Permission denied.",
            suggested_next_steps=[
                "Run FixFox as administrator.",
                "Use dry-run first to preview commands.",
                "Use a non-admin safe alternative when possible.",
            ],
        )
    return ToolFailed(
        user_message="The operation did not complete successfully.",
        technical_message=details or f"Exit code {code}.",
        suggested_next_steps=ensure_next_steps(None),
    )
