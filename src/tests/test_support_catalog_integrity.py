from __future__ import annotations

import unittest

from src.core.support_catalog import catalog_stats, issue_map, list_families, playbooks_for_issue


class SupportCatalogIntegrityTests(unittest.TestCase):
    def test_catalog_counts_and_family_presence(self) -> None:
        stats = catalog_stats()
        self.assertEqual(stats.issue_count, 200)
        self.assertEqual(stats.family_count, 20)
        self.assertGreaterEqual(stats.playbook_count, 31)

    def test_every_family_has_at_least_one_issue(self) -> None:
        issues = list(issue_map().values())
        family_ids = {issue.family_id for issue in issues}
        self.assertEqual(family_ids, {family.id for family in list_families()})

    def test_every_issue_has_a_playbook_path(self) -> None:
        missing = [issue.id for issue in issue_map().values() if not issue.playbook_ids or not playbooks_for_issue(issue.id)]
        self.assertEqual(missing, [])


if __name__ == "__main__":
    unittest.main()
