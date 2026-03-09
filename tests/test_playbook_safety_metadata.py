from __future__ import annotations

from src.core.playbooks import runbook_safety, support_playbook_safety
from src.core.support_catalog import support_playbook_map


def test_support_playbook_safety_metadata_is_populated() -> None:
    for playbook_id in list(support_playbook_map())[:10]:
        safety = support_playbook_safety(playbook_id)
        assert safety.risk_level
        assert isinstance(safety.admin_required, bool)
        assert isinstance(safety.reboot_required, bool)
        assert isinstance(safety.rollback_supported, bool)
        assert isinstance(safety.evidence_capture, tuple)


def test_runbook_safety_metadata_defaults_exist() -> None:
    safety = runbook_safety("it_network_stack_repair")
    assert safety.risk_level
    assert isinstance(safety.admin_required, bool)
    assert isinstance(safety.reboot_required, bool)
