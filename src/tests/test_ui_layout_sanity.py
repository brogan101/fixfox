from __future__ import annotations

import ast
import os
import time
import unittest
from pathlib import Path

from PySide6.QtCore import QPoint, Qt
from PySide6.QtTest import QTest
from PySide6.QtWidgets import QAbstractButton, QApplication, QLabel, QSplitter, QWidget

from src.ui.main_window import MainWindow

LEGACY_NAV_OBJECTS = {"Nav", "NavList", "DrawerNav", "LegacyNav", "MainNav"}


def _assert_within_parent(widget: QWidget, tolerance: int = 2) -> None:
    parent = widget.parentWidget()
    if parent is None or (not parent.isVisible()):
        return
    if widget.objectName() in {"NavRailButton", "NavRailAuxButton"}:
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

    def test_no_legacy_main_navigation_widgets(self) -> None:
        nav_rails = [w for w in self.window.findChildren(QWidget) if w.objectName() == "NavRail"]
        self.assertEqual(len(nav_rails), 1, "Expected exactly one NavRail widget.")

        legacy = [w for w in self.window.findChildren(QWidget) if w.objectName() in LEGACY_NAV_OBJECTS]
        self.assertFalse(legacy, f"Legacy navigation widgets found: {[w.objectName() for w in legacy]}")

        list_nav = [
            w
            for w in self.window.findChildren(QWidget)
            if w.__class__.__name__ in {"QListWidget", "QTreeView"} and w.objectName() in LEGACY_NAV_OBJECTS
        ]
        self.assertFalse(list_nav, "Legacy list/tree main navigation widgets are not allowed.")

    def test_no_qsplitter_in_shell(self) -> None:
        splitters = self.window.findChildren(QSplitter)
        self.assertFalse(splitters, "QSplitter is not allowed in the app shell.")

    def test_search_popup_persists_and_details_toggle_works(self) -> None:
        self.window.top_search.setText("quick")
        self.window._refresh_global_search_results()
        self._drain_events(cycles=4)
        self.assertTrue(self.window._search_popup.isVisible(), "Search popup should be visible after typing.")

        time.sleep(0.15)
        self._drain_events(cycles=3)
        self.assertTrue(self.window._search_popup.isVisible(), "Search popup should remain open while typing.")

        self.assertTrue(self.window.concierge.collapsed, "Details sheet should be collapsed by default.")
        self.window.btn_panel_toggle.click()
        self._drain_events(cycles=3)
        self.assertFalse(self.window.concierge.collapsed, "Details sheet should open from app bar toggle.")

        QTest.keyClick(self.window, Qt.Key_Escape)
        self._drain_events(cycles=3)
        self.assertTrue(self.window.concierge.collapsed, "Details sheet should close with ESC when unpinned.")

    def test_stylesheet_overrides_combo_and_tree_arrows(self) -> None:
        qss = self.app.styleSheet()
        self.assertIn("QComboBox::down-arrow", qss)
        self.assertIn("QTreeWidget::branch:closed:has-children", qss)
        self.assertIn("QTreeView::branch:open:has-children", qss)
        icons_py = Path("src/ui/icons.py").read_text(encoding="utf-8")
        self.assertNotIn("standardIcon(", icons_py)
        self.assertNotIn("QStyle", icons_py)

    def test_no_ascii_arrow_labels_in_ui_strings(self) -> None:
        targets = (
            Path("src/ui/components/onboarding.py"),
            Path("src/ui/pages/playbooks_page.py"),
            Path("src/ui/pages/reports_page.py"),
            Path("src/ui/pages/settings_page.py"),
        )
        offenders: list[str] = []
        for file_path in targets:
            tree = ast.parse(file_path.read_text(encoding="utf-8"))
            for node in ast.walk(tree):
                if not isinstance(node, ast.Constant) or not isinstance(node.value, str):
                    continue
                value = str(node.value)
                lower = value.lower()
                if "next >" in lower or "->" in value:
                    offenders.append(f"{file_path.name}:{value[:50]}")
        self.assertFalse(offenders, f"ASCII arrow labels found: {offenders[:6]}")


if __name__ == "__main__":
    raise SystemExit(unittest.main())
