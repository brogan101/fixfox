from __future__ import annotations

import json
import os
import sys
import time
from datetime import datetime
from pathlib import Path

from PySide6.QtCore import QPoint, Qt
from PySide6.QtGui import QPainter, QPixmap
from PySide6.QtTest import QTest
from PySide6.QtWidgets import QApplication, QWidget


REPO_ROOT = Path(__file__).resolve().parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from src.ui.main_window import MainWindow


def _drain(app: QApplication, cycles: int = 5, delay: float = 0.05) -> None:
    for _ in range(cycles):
        app.processEvents()
        time.sleep(delay)


def _capture(window: MainWindow, out_path: Path, include_popup: bool = False) -> None:
    popup = getattr(window, "_search_popup", None)
    frame = window.frameGeometry()
    region = frame
    if include_popup and isinstance(popup, QWidget) and popup.isVisible():
        region = region.united(popup.frameGeometry())
    base = window.grab()
    if include_popup and isinstance(popup, QWidget) and popup.isVisible():
        canvas = QPixmap(region.size())
        canvas.fill(Qt.transparent)
        painter = QPainter(canvas)
        painter.drawPixmap(window.frameGeometry().topLeft() - region.topLeft(), base)
        painter.drawPixmap(popup.frameGeometry().topLeft() - region.topLeft(), popup.grab())
        painter.end()
        pix = canvas
    else:
        pix = base
    out_path.parent.mkdir(parents=True, exist_ok=True)
    pix.save(str(out_path), "PNG")


def _open_page(window: MainWindow, page_name: str) -> bool:
    if page_name not in getattr(window, "NAV_ITEMS", ()):
        return False
    idx = window.NAV_ITEMS.index(page_name)
    window.nav.setCurrentRow(idx)
    return window.pages.currentIndex() == idx


def main() -> int:
    os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
    os.environ["FIXFOX_SKIP_ONBOARDING"] = "1"
    app = QApplication.instance() or QApplication([])
    window = MainWindow()
    window.show()
    _drain(app, cycles=10)

    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    out_dir = REPO_ROOT / "docs" / "screenshots" / f"stability_{stamp}"
    failures: list[str] = []
    captures: list[str] = []
    search_visible_ms = 0

    try:
        for page_name in ("Home", "Settings", "Playbooks", "Reports", "Diagnose"):
            if not _open_page(window, page_name):
                failures.append(f"failed to open page: {page_name}")
                continue
            _drain(app, cycles=4)
            shot = out_dir / f"{page_name.lower()}.png"
            _capture(window, shot)
            captures.append(str(shot.relative_to(REPO_ROOT)).replace("\\", "/"))

        window._focus_top_search()
        _drain(app, cycles=2)
        window.top_search.setText("quick")
        window._schedule_global_search()
        deadline = time.monotonic() + 1.8
        while time.monotonic() < deadline and not window._search_popup.isVisible():
            _drain(app, cycles=1, delay=0.03)
        if not window._search_popup.isVisible():
            failures.append("search dropdown did not open")
        started = time.perf_counter()
        QTest.qWait(550)
        _drain(app, cycles=3)
        search_visible_ms = int((time.perf_counter() - started) * 1000.0)
        if not window._search_popup.isVisible():
            failures.append("search dropdown disappeared before 500ms")
        search_shot = out_dir / "search_dropdown.png"
        _capture(window, search_shot, include_popup=True)
        captures.append(str(search_shot.relative_to(REPO_ROOT)).replace("\\", "/"))
    finally:
        window.close()
        _drain(app, cycles=2)

    manifest = {
        "timestamp": stamp,
        "search_dropdown_visible_ms": search_visible_ms,
        "screenshots": captures,
        "failures": failures,
    }
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "MANIFEST.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    if failures:
        print("UI smoke walkthrough: FAIL")
        for row in failures:
            print(f"- {row}")
        print(f"screenshots_dir={out_dir}")
        return 1

    print("UI smoke walkthrough: PASS")
    print(f"screenshots_dir={out_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
