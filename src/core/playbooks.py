from __future__ import annotations

from dataclasses import dataclass

from .runbooks import Runbook, runbook_map
from .script_tasks import script_task_map
from .support_catalog import SupportPlaybook, support_playbook_map
from .support_playbooks import deep_support_playbook_map


@dataclass(frozen=True)
class PlaybookSafetyMetadata:
    risk_level: str
    admin_required: bool
    reboot_required: bool
    rollback_supported: bool
    evidence_capture: tuple[str, ...]


def _rollback_supported(text: str) -> bool:
    value = str(text or "").strip().lower()
    if not value:
        return False
    return "no rollback" not in value and "not reversible" not in value


def support_playbook_safety(playbook_id: str) -> PlaybookSafetyMetadata:
    playbook = support_playbook_map()[playbook_id]
    deep = deep_support_playbook_map().get(playbook_id)
    tasks = script_task_map()
    admin_required = False
    reboot_required = False
    if deep is not None:
        for binding in (*deep.diagnostics, *deep.remediations, *deep.validations):
            meta = tasks.get(binding.task_id)
            if meta is None:
                continue
            admin_required = admin_required or bool(meta.admin_required)
            reboot_required = reboot_required or bool(meta.reboot_likely)
    return PlaybookSafetyMetadata(
        risk_level=str(playbook.risk or "Safe"),
        admin_required=admin_required,
        reboot_required=reboot_required,
        rollback_supported=_rollback_supported(playbook.rollback),
        evidence_capture=tuple(str(row) for row in playbook.evidence),
    )


def runbook_safety(runbook_id: str) -> PlaybookSafetyMetadata:
    runbook = runbook_map()[runbook_id]
    tasks = script_task_map()
    admin_required = False
    reboot_required = False
    evidence_capture: list[str] = []
    for step in runbook.steps:
        meta = tasks.get(step.task_id)
        if meta is None:
            continue
        admin_required = admin_required or bool(meta.admin_required)
        reboot_required = reboot_required or bool(meta.reboot_likely)
        evidence_capture.append(meta.category)
    return PlaybookSafetyMetadata(
        risk_level=str(getattr(runbook, "risk_level", "Safe") or "Safe"),
        admin_required=admin_required,
        reboot_required=reboot_required,
        rollback_supported=bool(getattr(runbook, "rollback_supported", True)),
        evidence_capture=tuple(dict.fromkeys(evidence_capture)),
    )


def get_playbook(playbook_id: str) -> SupportPlaybook:
    return support_playbook_map()[playbook_id]


def get_runbook(runbook_id: str) -> Runbook:
    return runbook_map()[runbook_id]
