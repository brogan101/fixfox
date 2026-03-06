from __future__ import annotations

import sys
import time
from pathlib import Path

from PySide6.QtCore import QTimer, Qt
from PySide6.QtGui import QIcon
from PySide6.QtTest import QTest
from PySide6.QtWidgets import QApplication, QSplitter, QWidget

REPO_ROOT = Path(__file__).resolve().parents[1]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from src.ui.main_window import MainWindow
from src.ui.components.tool_runner import ToolRunnerWindow
from src.ui.icons import get_icon
from src.ui.splash import build_splash_pixmap

REQUIRED_ICONS = [
    "home",
    "playbooks",
    "diagnose",
    "fixes",
    "reports",
    "history",
    "settings_gear",
    "help",
    "search",
    "quick_check",
    "export",
    "overflow",
    "details",
    "pin",
    "close",
    "info",
    "privacy",
    "shield",
]


def _is_clipped(widget: QWidget) -> bool:
    if not widget.isVisible() or widget.width() <= 0 or widget.height() <= 0:
        return False
    hint = widget.sizeHint()
    return widget.width() + 8 < hint.width() or widget.height() + 6 < hint.height()


def _assert(condition: bool, message: str, failures: list[str]) -> None:
    if not condition:
        failures.append(message)


def main() -> int:
    failures: list[str] = []
    icon_dir = REPO_ROOT / "src" / "assets" / "icons"

    for icon_name in REQUIRED_ICONS:
        candidate_svg = icon_dir / f"{icon_name}.svg"
        candidate_png = icon_dir / f"{icon_name}.png"
        _assert(candidate_svg.exists() or candidate_png.exists(), f"Missing icon asset: {icon_name}", failures)

    app = QApplication.instance() or QApplication([])
    window = MainWindow()
    window.show()
    app.processEvents()

    for icon_name in REQUIRED_ICONS[:12]:
        icon: QIcon = get_icon(icon_name, window, size=20)
        _assert(not icon.isNull(), f"Null QIcon for {icon_name}", failures)

    splitters = [w for w in window.findChildren(QSplitter)]
    _assert(len(splitters) == 0, "QSplitter still present in main layout", failures)

    details_btn = getattr(window, "btn_panel_toggle", None)
    _assert(details_btn is not None and details_btn.isVisible(), "Details toggle button is not visible", failures)

    was_collapsed = bool(getattr(window.concierge, "collapsed", True))
    if details_btn is not None:
        details_btn.click()
        app.processEvents()
        _assert((not window.concierge.collapsed), "Details panel did not open from visible button", failures)
        QTest.keyClick(window, Qt.Key_Escape)
        app.processEvents()
        _assert(window.concierge.collapsed, "Details panel did not close with Esc", failures)
        if not was_collapsed:
            details_btn.click()
            app.processEvents()

    window.top_search.setText("quick")
    deadline = time.time() + 1.2
    while time.time() < deadline and not window._search_popup.isVisible():
        window._refresh_global_search_results()
        app.processEvents()
        QTest.qWait(40)
    _assert(window._search_popup.isVisible(), "Search popup did not open", failures)
    QTest.qWait(180)
    app.processEvents()
    _assert(window._search_popup.isVisible(), "Search popup closed unexpectedly while typing", failures)

    splash_pixmap = build_splash_pixmap(status_text="Audit probe")
    _assert(not splash_pixmap.isNull(), "Splash pixmap did not render", failures)

    for btn in (window.btn_quick_check, window.btn_panel_toggle, window.btn_overflow):
        _assert(not _is_clipped(btn), f"Top bar control clipped: {btn.objectName() or btn.text()}", failures)

    if hasattr(window, "settings_nav"):
        _assert(not _is_clipped(window.settings_nav), "Settings left nav clipped", failures)

    runner = ToolRunnerWindow("Audit Probe", parent=window)
    runner.show()
    app.processEvents()
    _assert(not _is_clipped(runner.tabs.tabBar()), "Tool runner tabs clipped", failures)
    _assert(not _is_clipped(runner.btn_export), "Tool runner export button clipped", failures)

    runner.close()
    window.close()
    app.processEvents()

    if failures:
        print("UI audit failed:")
        for failure in failures:
            print(f"- {failure}")
        return 1

    print("UI audit passed: shell, icons, details panel, search popup, and clipping checks are healthy.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
