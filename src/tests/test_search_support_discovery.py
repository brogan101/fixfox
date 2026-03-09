from __future__ import annotations

import unittest

from src.core.search import query_index, reset_search_cache_for_tests


class SearchSupportDiscoveryTests(unittest.TestCase):
    def setUp(self) -> None:
        reset_search_cache_for_tests()

    def test_windows_update_prefers_support_results(self) -> None:
        rows = query_index("windows update", limit=5)
        titles = [row.title for row in rows]
        self.assertIn("Windows Update Repair", titles)

    def test_printer_offline_finds_support_playbook(self) -> None:
        rows = query_index("printer offline", limit=5)
        kinds = {(row.kind, row.title) for row in rows}
        self.assertIn(("support_playbook", "Printer / Spooler Repair"), kinds)

    def test_slow_pc_finds_deep_triage(self) -> None:
        rows = query_index("slow pc", limit=5)
        kinds = [(row.kind, row.title) for row in rows]
        self.assertIn(("support_playbook", "Slow PC Triage"), kinds)


if __name__ == "__main__":
    unittest.main()
