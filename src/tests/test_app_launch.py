from __future__ import annotations

import os
import time
import unittest

from PySide6.QtCore import QtMsgType, QTimer, qInstallMessageHandler
from PySide6.QtTest import QTest
from PySide6.QtWidgets import QApplication


class AppLaunchTests(unittest.TestCase):
    def setUp(self) -> None:
        os.environ["QT_QPA_PLATFORM"] = "offscreen"
        os.environ["FIXFOX_SKIP_ONBOARDING"] = "1"
        from src.core.qt_runtime import ensure_qt_runtime_env

        ensure_qt_runtime_env()

    def test_main_window_launches_without_qss_parse_warnings(self) -> None:
        from src.core.qt_runtime import is_fatal_qt_warning

        messages: list[str] = []

        def _handler(msg_type: QtMsgType, _context: object, message: str) -> None:
            lower = str(message or "").lower()
            if is_fatal_qt_warning(lower):
                messages.append(f"{msg_type}: {message}")

        prev = qInstallMessageHandler(_handler)
        app = QApplication.instance() or QApplication([])
        window = None
        ticks = {"count": 0}
        timer = QTimer()
        timer.setInterval(15)
        timer.timeout.connect(lambda: ticks.__setitem__("count", int(ticks["count"]) + 1))
        try:
            from src.ui.main_window import MainWindow

            window = MainWindow()
            window.show()
            timer.start()
            deadline = time.monotonic() + 0.8
            max_step_ms = 0.0
            while time.monotonic() < deadline:
                step_started = time.perf_counter()
                app.processEvents()
                max_step_ms = max(max_step_ms, (time.perf_counter() - step_started) * 1000.0)
                QTest.qWait(20)
            self.assertTrue(window.isVisible(), "main window was not visible after startup event processing")
            self.assertLessEqual(max_step_ms, 1500.0, f"event loop blocked too long during launch: {max_step_ms:.1f}ms")
            self.assertGreaterEqual(int(ticks["count"]), 1, f"event loop heartbeat too low: {ticks['count']}")
            self.assertFalse(bool(messages), f"unexpected QSS/Qt warnings: {messages[:4]}")
        finally:
            timer.stop()
            if window is not None:
                window.close()
                app.processEvents()
            qInstallMessageHandler(prev)


if __name__ == "__main__":
    raise SystemExit(unittest.main())
