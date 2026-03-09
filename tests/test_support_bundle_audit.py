from __future__ import annotations

from src.core.support_bundle import audit_support_bundle


def test_support_bundle_audit_detects_execution_logs_and_warnings() -> None:
    session = {
        "actions": [
            {
                "title": "Network Baseline",
                "final_status": "COMPLETED_WITH_ISSUES",
                "warnings": ["DNS mismatch"],
                "errors": [],
                "actions_performed": ["network_baseline"],
            }
        ],
        "findings": [{"title": "DNS server mismatch"}],
        "support_playbook_runs": [
            {
                "title": "Network Baseline and Repair",
                "evidence_files": ["C:/tmp/network.txt"],
                "warnings": ["DNS mismatch"],
                "errors": [],
            }
        ],
        "sysinfo": {"hostname": "TEST-PC"},
        "evidence": {"files": [{"path": "C:/tmp/network.txt"}]},
    }
    audit = audit_support_bundle(session)
    assert audit.execution_logs_included is True
    assert audit.diagnostics_included is True
    assert audit.system_info_included is True
    assert audit.playbook_results_included is True
    assert audit.warnings_errors_included is True
    assert audit.evidence_file_count == 1
