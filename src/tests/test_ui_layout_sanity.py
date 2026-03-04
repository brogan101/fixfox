from __future__ import annotations

import os
import time
import unittest

from PySide6.QtCore import QPoint
from PySide6.QtWidgets import QAbstractButton, QApplication, QLabel, QWidget

from src.ui.main_window import MainWindow


def _assert_within_parent(widget: QWidget, tolerance: int = 2) -> None:
    parent = widget.parentWidget()
    if parent is None or (not parent.isVisible()):
        return
    ancestor = parent
    while ancestor is not None:
        if ancestor.objectName() in {"PageViewport", "qt_scrollarea_viewport"}:
            return
        ancestor = ancestor.parentWidget()
    # Qt defaults some not-yet-laid-out child widgets to 640x480 before layout pass.
    if widget.width() == 640 and widget.height() == 480 and widget.objectName() in {"Card", "Drawer", "EmptyState"}:
        return
    top_left = widget.mapTo(parent, QPoint(0, 0))
    bottom_right = widget.mapTo(parent, QPoint(max(0, widget.width()), max(0, widget.height())))
    if top_left.x() < -tolerance or top_left.y() < -tolerance:
        raise AssertionError(f"{widget.__class__.__name__} starts outside parent bounds: {widget.objectName()}")
    if bottom_right.x() > parent.width() + tolerance or bottom_right.y() > parent.height() + tolerance:
        raise AssertionError(f"{widget.__class__.__name__} exceeds parent bounds: {widget.objectName()}")


def _assert_text_not_clipped(widget: QWidget, padding: int = 2) -> None:
    if isinstance(widget, QLabel):
        text = widget.text().strip()
        if not text:
            return
        if widget.wordWrap():
            return
        min_h = widget.fontMetrics().height()
        if widget.height() < min_h:
            raise AssertionError(f"QLabel clipped: {widget.objectName()} text='{text[:40]}' h={widget.height()} need>={min_h}")
    if isinstance(widget, QAbstractButton):
        text = widget.text().strip()
        if not text:
            return
        min_h = widget.fontMetrics().height() + padding
        if widget.height() < min_h:
            raise AssertionError(
                f"QAbstractButton clipped: {widget.objectName()} text='{text[:40]}' h={widget.height()} need>={min_h}"
            )


class UiLayoutSanityTests(unittest.TestCase):
    def setUp(self) -> None:
        os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
        os.environ["FIXFOX_SKIP_ONBOARDING"] = "1"
        self.app = QApplication.instance() or QApplication([])
        self.window = MainWindow()
        self.window.show()
        self._drain_events()

    def tearDown(self) -> None:
        self.window.close()
        self._drain_events()

    def _drain_events(self, cycles: int = 4) -> None:
        for _ in range(cycles):
            self.app.processEvents()
            time.sleep(0.03)

    def test_layout_bounds_and_text_clipping(self) -> None:
        sizes = [(1024, 768), (1280, 720), (1600, 900)]
        for width, height in sizes:
            self.window.resize(width, height)
            self._drain_events(cycles=6)
            for widget in self.window.findChildren(QWidget):
                if not widget.isVisible():
                    continue
                if widget.window() is not self.window:
                    continue
                _assert_within_parent(widget, tolerance=2)
                _assert_text_not_clipped(widget, padding=2)


if __name__ == "__main__":
    raise SystemExit(unittest.main())
