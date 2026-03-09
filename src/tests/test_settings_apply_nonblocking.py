from __future__ import annotations

import os
import time
import unittest
from dataclasses import asdict

from PySide6.QtCore import QTimer
from PySide6.QtWidgets import QApplication

from src.core.settings import AppSettings, load_settings, save_settings
from src.ui.main_window import MainWindow
from src.ui.runtime_bootstrap import apply_runtime_ui_bootstrap


def _drain(app: QApplication, cycles: int = 6, delay: float = 0.03) -> None:
    for _ in range(cycles):
        app.processEvents()
        time.sleep(delay)


class SettingsApplyNonblockingTests(unittest.TestCase):
    def test_settings_toggle_keeps_event_loop_responsive(self) -> None:
        os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
        os.environ["FIXFOX_SKIP_ONBOARDING"] = "1"
        app = QApplication.instance() or QApplication([])
        apply_runtime_ui_bootstrap(app)
        original = AppSettings(**asdict(load_settings().normalized()))
        window = MainWindow()
        window.show()
        _drain(app, cycles=10)
        try:
            window.nav.setCurrentRow(window.NAV_ITEMS.index("Settings"))
            _drain(app, cycles=4)

            ticks = {"count": 0}
            timer = QTimer()
            timer.setInterval(100)
            timer.timeout.connect(lambda: ticks.__setitem__("count", int(ticks["count"]) + 1))
            timer.start()

            window.s_share.toggle()
            app.processEvents()
            window.s_ip.toggle()
            app.processEvents()
            window.s_weekly.toggle()
            debounce_started = bool(window._settings_save_timer.isActive())

            deadline = time.perf_counter() + 0.75
            while time.perf_counter() < deadline:
                app.processEvents()
                time.sleep(0.02)
            timer.stop()

            self.assertTrue(debounce_started, "settings debounce timer did not start")
            self.assertGreaterEqual(ticks["count"], 3, f"heartbeat stalled while applying settings (ticks={ticks['count']})")
            self.assertFalse(window._settings_save_timer.isActive(), "settings debounce timer never flushed")
        finally:
            window.close()
            _drain(app, cycles=3)
            save_settings(original)


if __name__ == "__main__":
    raise SystemExit(unittest.main())
