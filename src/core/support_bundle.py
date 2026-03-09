from __future__ import annotations

from dataclasses import dataclass
from typing import Any


@dataclass(frozen=True)
class SupportBundleAudit:
    execution_logs_included: bool
    diagnostics_included: bool
    system_info_included: bool
    playbook_results_included: bool
    warnings_errors_included: bool
    evidence_file_count: int
    action_count: int
    support_run_count: int

    def summary_lines(self) -> list[str]:
        return [
            f"Execution logs: {'yes' if self.execution_logs_included else 'no'}",
            f"Diagnostics: {'yes' if self.diagnostics_included else 'no'}",
            f"System info: {'yes' if self.system_info_included else 'no'}",
            f"Playbook results: {'yes' if self.playbook_results_included else 'no'}",
            f"Warnings / errors: {'yes' if self.warnings_errors_included else 'no'}",
            f"Evidence files: {self.evidence_file_count}",
            f"Action records: {self.action_count}",
            f"Deep playbook runs: {self.support_run_count}",
        ]


def audit_support_bundle(session: dict[str, Any] | None) -> SupportBundleAudit:
    payload = session if isinstance(session, dict) else {}
    actions = payload.get("actions", []) if isinstance(payload.get("actions", []), list) else []
    findings = payload.get("findings", []) if isinstance(payload.get("findings", []), list) else []
    support_runs = payload.get("support_playbook_runs", []) if isinstance(payload.get("support_playbook_runs", []), list) else []
    evidence = payload.get("evidence", {}).get("files", []) if isinstance(payload.get("evidence", {}), dict) else []
    execution_logs_included = any(
        isinstance(row, dict)
        and (
            row.get("execution", {})
            or row.get("start_time")
            or row.get("end_time")
            or row.get("actions_performed")
            or row.get("final_status")
        )
        for row in actions
    )
    diagnostics_included = bool(findings)
    system_info_included = isinstance(payload.get("sysinfo", {}), dict) and bool(payload.get("sysinfo"))
    playbook_results_included = bool(support_runs)
    warnings_errors_included = any(
        isinstance(row, dict)
        and (
            row.get("warnings")
            or row.get("errors")
            or str(row.get("final_status", "")).upper() in {"COMPLETED_WITH_ISSUES", "FAILED"}
        )
        for row in actions
    ) or any(
        isinstance(row, dict) and (row.get("warnings") or row.get("errors"))
        for row in support_runs
    )
    return SupportBundleAudit(
        execution_logs_included=execution_logs_included,
        diagnostics_included=diagnostics_included,
        system_info_included=system_info_included,
        playbook_results_included=playbook_results_included,
        warnings_errors_included=warnings_errors_included,
        evidence_file_count=len(evidence),
        action_count=len(actions),
        support_run_count=len(support_runs),
    )
