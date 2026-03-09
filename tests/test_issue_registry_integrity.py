from __future__ import annotations

from src.core.support_catalog import issue_map, list_families, playbooks_for_issue


def test_issue_registry_has_required_fields_and_mappings() -> None:
    families = {family.id for family in list_families()}
    issues = issue_map()
    assert len(issues) == 200
    for issue in issues.values():
        assert issue.family_id in families
        assert issue.subfamily.strip()
        assert issue.title.strip()
        assert issue.severity.strip()
        assert issue.symptom_labels
        assert issue.playbook_ids
        assert playbooks_for_issue(issue.id)
        assert issue.diagnostic_ids
        assert issue.fix_ids or issue.workflow.lower() in {"guided", "escalate"}
        assert issue.evidence_plan_ids

