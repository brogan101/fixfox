from __future__ import annotations

import unittest

from src.core.support_catalog import support_playbook_map
from src.core.support_playbooks import (
    deep_support_playbook_map,
    deep_support_playbook_stats,
    execute_support_playbook,
)
from src.core.script_tasks import script_task_map


class DeepSupportPlaybookTests(unittest.TestCase):
    def test_priority_family_coverage_and_registry_integrity(self) -> None:
        plans = deep_support_playbook_map()
        stats = deep_support_playbook_stats()
        self.assertGreaterEqual(stats.playbook_count, 10)
        required_families = {
            "identity",
            "network",
            "remote",
            "print",
            "email",
            "collab",
            "browser",
            "performance",
            "windows_update",
            "shell",
            "recovery",
        }
        self.assertTrue(required_families.issubset({row.family for row in plans.values()}))
        tasks = script_task_map()
        catalog = support_playbook_map()
        for playbook_id, plan in plans.items():
            self.assertIn(playbook_id, catalog)
            self.assertTrue(plan.diagnostics, msg=playbook_id)
            self.assertTrue(plan.validations, msg=playbook_id)
            self.assertTrue(plan.evidence_plan_ids, msg=playbook_id)
            for bucket in (plan.diagnostics, plan.remediations, plan.validations):
                for row in bucket:
                    self.assertIn(row.task_id, tasks, msg=f"{playbook_id}:{row.task_id}")

    def test_execute_safe_deep_playbook_returns_normalized_result(self) -> None:
        payload = execute_support_playbook("identity_credential_repair", mode="diagnose", timeout_s=120)
        self.assertEqual(payload["support_playbook_id"], "identity_credential_repair")
        self.assertIn("summary_text", payload)
        self.assertIn("findings", payload)
        self.assertTrue(isinstance(payload["findings"], list) and payload["findings"])
        self.assertIn("evidence_files", payload)
        self.assertTrue(all("status" in row for row in payload["findings"] if isinstance(row, dict)))


if __name__ == "__main__":
    unittest.main()
