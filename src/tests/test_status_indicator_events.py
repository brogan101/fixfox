from __future__ import annotations

import os
import time
import unittest

from PySide6.QtWidgets import QApplication

from src.core.run_events import RunEventType, get_run_event_bus
from src.ui.main_window import MainWindow


def _drain(app: QApplication, cycles: int = 6, delay: float = 0.02) -> None:
    for _ in range(cycles):
        app.processEvents()
        time.sleep(delay)


class StatusIndicatorEventTests(unittest.TestCase):
    def test_status_indicator_tracks_run_events_without_polling(self) -> None:
        os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
        os.environ["FIXFOX_SKIP_ONBOARDING"] = "1"
        app = QApplication.instance() or QApplication([])
        bus = get_run_event_bus()
        window = MainWindow()
        window.show()
        _drain(app, cycles=10)
        try:
            run_id = "test_status_events"
            bus.clear_run(run_id)

            bus.publish(run_id, RunEventType.START, message="Quick Check started.", data={"name": "Quick Check"})
            _drain(app, cycles=3)
            self.assertEqual(window.run_status_chip.text(), "Running")
            self.assertIn("Quick Check", window.run_status_title.text())

            bus.publish(run_id, RunEventType.PROGRESS, message="Collecting data", progress=55)
            _drain(app, cycles=3)
            self.assertIn("Collecting data", window._run_status_detail_raw)
            self.assertTrue(window._run_status_active)

            bus.publish(run_id, RunEventType.WARNING, message="One issue needs review.")
            bus.publish(run_id, RunEventType.END, message="Run finished with code 0.", data={"code": 0})
            _drain(app, cycles=3)
            self.assertEqual(window.run_status_chip.text(), "Needs attention")

            error_run_id = "test_status_events_error"
            bus.clear_run(error_run_id)
            bus.publish(error_run_id, RunEventType.START, message="Repair started.", data={"name": "Repair"})
            bus.publish(error_run_id, RunEventType.ERROR, message="Repair failed.")
            _drain(app, cycles=3)
            self.assertEqual(window.run_status_chip.text(), "Error")
            self.assertIn("failed", window.run_status_title.text().lower())

            idle_run_id = "test_status_events_idle"
            bus.clear_run(idle_run_id)
            bus.publish(idle_run_id, RunEventType.START, message="Inventory started.", data={"name": "Inventory"})
            bus.publish(idle_run_id, RunEventType.END, message="Run finished with code 0.", data={"code": 0})
            _drain(app, cycles=3)
            self.assertEqual(window.run_status_chip.text(), "Idle")
        finally:
            window.close()
            _drain(app, cycles=3)
            bus.clear_run("test_status_events")
            bus.clear_run("test_status_events_error")
            bus.clear_run("test_status_events_idle")


if __name__ == "__main__":
    raise SystemExit(unittest.main())
