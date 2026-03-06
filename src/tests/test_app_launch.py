from __future__ import annotations

import os
import time
import unittest

from PySide6.QtCore import QtMsgType, qInstallMessageHandler
from PySide6.QtWidgets import QApplication


class AppLaunchTests(unittest.TestCase):
    def setUp(self) -> None:
        os.environ["QT_QPA_PLATFORM"] = "offscreen"
        os.environ["FIXFOX_SKIP_ONBOARDING"] = "1"

    def test_main_window_launches_without_qss_parse_warnings(self) -> None:
        messages: list[str] = []

        def _handler(msg_type: QtMsgType, _context: object, message: str) -> None:
            lower = str(message or "").lower()
            if "could not parse application stylesheet" in lower:
                messages.append(f"{msg_type}: {message}")
            if "unknown property" in lower:
                messages.append(f"{msg_type}: {message}")

        prev = qInstallMessageHandler(_handler)
        app = QApplication.instance() or QApplication([])
        window = None
        try:
            from src.ui.main_window import MainWindow

            window = MainWindow()
            window.show()
            deadline = time.monotonic() + 0.8
            while time.monotonic() < deadline:
                app.processEvents()
                time.sleep(0.02)
            self.assertTrue(window.isVisible(), "main window was not visible after startup event processing")
            self.assertFalse(bool(messages), f"unexpected QSS/Qt warnings: {messages[:4]}")
        finally:
            if window is not None:
                window.close()
                app.processEvents()
            qInstallMessageHandler(prev)


if __name__ == "__main__":
    raise SystemExit(unittest.main())
