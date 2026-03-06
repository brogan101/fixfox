from __future__ import annotations

import os
import time
import unittest

from PySide6.QtCore import QTimer
from PySide6.QtTest import QTest
from PySide6.QtWidgets import QApplication


class SearchNonBlockingTests(unittest.TestCase):
    def setUp(self) -> None:
        os.environ["QT_QPA_PLATFORM"] = "offscreen"
        os.environ["FIXFOX_SKIP_ONBOARDING"] = "1"

    def test_search_typing_is_responsive_and_dropdown_stays_open(self) -> None:
        from src.core.search import get_search_cache_stats, reset_search_cache_for_tests
        from src.ui.main_window import MainWindow

        app = QApplication.instance() or QApplication([])
        window = MainWindow()
        window.show()
        ticks = {"count": 0}
        timer = QTimer()
        timer.setInterval(15)
        timer.timeout.connect(lambda: ticks.__setitem__("count", int(ticks["count"]) + 1))

        try:
            warmup_deadline = time.monotonic() + 35.0
            while getattr(window, "_startup_warmup_active", False) and time.monotonic() < warmup_deadline:
                app.processEvents()
                time.sleep(0.05)
            self.assertFalse(getattr(window, "_startup_warmup_active", True), "startup warmup did not complete in time")

            reset_search_cache_for_tests()
            before = get_search_cache_stats()
            timer.start()
            query = "quickcheck"
            started = time.perf_counter()

            window._focus_top_search()
            for i in range(1, min(10, len(query)) + 1):
                window.top_search.setText(query[:i])
                window._schedule_global_search()
                app.processEvents()
                time.sleep(0.018)

            popup_deadline = time.perf_counter() + 1.2
            while time.perf_counter() < popup_deadline and not window._search_popup.isVisible():
                app.processEvents()
                time.sleep(0.01)
            self.assertTrue(window._search_popup.isVisible(), "search popup did not open")

            QTest.qWait(550)
            app.processEvents()
            self.assertTrue(window._search_popup.isVisible(), "search popup closed before 500ms")

            wait_deadline = time.perf_counter() + 0.6
            while time.perf_counter() < wait_deadline:
                app.processEvents()
                time.sleep(0.01)

            elapsed_ms = (time.perf_counter() - started) * 1000.0
            after = get_search_cache_stats()
            static_delta = int(after.get("static_builds", 0.0) - before.get("static_builds", 0.0))
            self.assertLessEqual(static_delta, 1, f"static search index rebuilt too often: delta={static_delta}")
            self.assertGreaterEqual(int(ticks["count"]), 16, f"event loop stalled while typing: ticks={ticks['count']}")
            self.assertLessEqual(elapsed_ms, 2400.0, f"search typing exceeded budget: {elapsed_ms:.1f}ms")
        finally:
            timer.stop()
            window.close()
            app.processEvents()


if __name__ == "__main__":
    raise SystemExit(unittest.main())
