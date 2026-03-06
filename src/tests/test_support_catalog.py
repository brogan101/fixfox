from __future__ import annotations

import unittest

from src.core.support_catalog import (
    catalog_stats,
    query_issues,
    search_alias_examples,
    validate_support_catalog,
)


class SupportCatalogTests(unittest.TestCase):
    def test_catalog_counts_and_integrity(self) -> None:
        stats = catalog_stats()
        self.assertEqual(stats.issue_count, 200)
        self.assertEqual(stats.family_count, 20)
        self.assertGreaterEqual(stats.playbook_count, 21)
        self.assertGreaterEqual(stats.diagnostic_count, 20)
        self.assertGreaterEqual(stats.fix_count, 20)
        self.assertEqual(validate_support_catalog(), [])

    def test_common_helpdesk_queries_resolve(self) -> None:
        for query, expected_issue_ids in search_alias_examples().items():
            matches = query_issues(query, limit=10)
            found = {row.id for row in matches}
            self.assertTrue(found.intersection(expected_issue_ids), msg=f"query={query} found={found}")


if __name__ == "__main__":
    unittest.main()
