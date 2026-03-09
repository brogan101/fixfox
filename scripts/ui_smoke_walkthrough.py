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
from PySide6.QtWidgets import QAbstractButton, QApplication, QLabel, QLineEdit, QWidget


REPO_ROOT = Path(__file__).resolve().parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from src.core.diagnostics.font_sanity import probe_font_render
from src.ui.main_window import MainWindow
from src.ui.runtime_bootstrap import apply_runtime_ui_bootstrap


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


def _visible_text_profile(root: QWidget) -> dict[str, object]:
    texts: list[str] = []
    for widget in root.findChildren(QWidget):
        if not widget.isVisible():
            continue
        value = ""
        if isinstance(widget, QLabel):
            value = widget.text().strip()
        elif isinstance(widget, QAbstractButton):
            value = f"{widget.text()} {widget.toolTip()}".strip()
        elif isinstance(widget, QLineEdit):
            value = widget.text().strip() or widget.placeholderText().strip()
        if not value:
            continue
        collapsed = " ".join(value.split())
        if len(collapsed) < 3:
            continue
        texts.append(collapsed)
    deduped = list(dict.fromkeys(texts))
    return {"count": len(deduped), "samples": deduped[:10]}


def _page_persistence_probe(window: MainWindow, page: str, *, app: QApplication) -> dict[str, object]:
    current_idx = window.pages.currentIndex() if getattr(window, "pages", None) is not None else -1
    target = window.pages.currentWidget() if getattr(window, "pages", None) is not None else window
    samples: list[dict[str, object]] = []
    for delay_ms in (0, 500, 1000, 2000):
        if delay_ms:
            QTest.qWait(delay_ms)
            _drain(app, cycles=2, delay=0.03)
        profile = _visible_text_profile(target)
        samples.append({"delay_ms": delay_ms, **profile})
    transitions: list[dict[str, object]] = []
    if current_idx >= 0 and len(getattr(window, "NAV_ITEMS", ())) > 1:
        alternate_idx = 0 if current_idx != 0 else 1
        if alternate_idx < len(window.NAV_ITEMS):
            window.nav.setCurrentRow(alternate_idx)
            _drain(app, cycles=3, delay=0.03)
            window.nav.setCurrentRow(current_idx)
            _drain(app, cycles=4, delay=0.03)
            transitions.append({"phase": "nav_roundtrip", **_visible_text_profile(window.pages.currentWidget())})
    if hasattr(window, "top_search") and hasattr(window, "_search_popup"):
        window._focus_top_search()
        window.top_search.setText("quick")
        window._schedule_global_search()
        _drain(app, cycles=4, delay=0.03)
        window.top_search.clear()
        window._search_popup.hide_popup()
        if current_idx >= 0:
            window.nav.setCurrentRow(current_idx)
        _drain(app, cycles=4, delay=0.03)
        transitions.append({"phase": "search_roundtrip", **_visible_text_profile(window.pages.currentWidget())})
    if not window.isMaximized():
        original_size = window.size()
        shrink_width = max(1024, original_size.width() - 160)
        shrink_height = max(720, original_size.height() - 100)
        if shrink_width != original_size.width() or shrink_height != original_size.height():
            window.resize(shrink_width, shrink_height)
            _drain(app, cycles=3, delay=0.03)
            window.resize(original_size)
            if current_idx >= 0:
                window.nav.setCurrentRow(current_idx)
            _drain(app, cycles=4, delay=0.03)
            transitions.append({"phase": "resize_roundtrip", **_visible_text_profile(window.pages.currentWidget())})
    baseline = int(samples[0]["count"]) if samples else 0
    ok = baseline >= 5 and all(int(sample["count"]) >= max(5, baseline // 2) for sample in samples[1:])
    ok = ok and all(int(sample["count"]) >= max(5, baseline // 2) for sample in transitions)
    return {"page": page, "ok": ok, "samples": samples, "transitions": transitions}


def main() -> int:
    os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
    os.environ["FIXFOX_SKIP_ONBOARDING"] = "1"
    app = QApplication.instance() or QApplication([])
    bootstrap = apply_runtime_ui_bootstrap(app)
    runtime_font_failures = probe_font_render(app.font())
    window = MainWindow()
    window.show()
    _drain(app, cycles=10)

    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    out_dir = REPO_ROOT / "docs" / "screenshots" / f"stability_{stamp}"
    failures: list[str] = []
    captures: list[str] = []
    search_visible_ms = 0
    page_persistence: dict[str, dict[str, object]] = {}

    try:
        if runtime_font_failures:
            failures.extend(runtime_font_failures)
        for page_name in ("Home", "Playbooks", "Diagnose", "Fixes", "Reports", "History", "Settings"):
            if not _open_page(window, page_name):
                failures.append(f"failed to open page: {page_name}")
                continue
            _drain(app, cycles=4)
            shot = out_dir / f"{page_name.lower()}.png"
            _capture(window, shot)
            captures.append(str(shot.relative_to(REPO_ROOT)).replace("\\", "/"))
            persistence = _page_persistence_probe(window, page_name, app=app)
            page_persistence[page_name] = persistence
            if not persistence["ok"]:
                failures.append(f"page persistence failed: {page_name}")
            if page_name == "Home":
                delay_shot = out_dir / "home_persistence_2s.png"
                _capture(window, delay_shot)
                captures.append(str(delay_shot.relative_to(REPO_ROOT)).replace("\\", "/"))

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
        "runtime_bootstrap": {
            "font_family": bootstrap.font_family,
            "stylesheet_length": bootstrap.stylesheet_length,
            "ui_scale_pct": getattr(bootstrap.settings, "ui_scale_pct", 100),
        },
        "search_dropdown_visible_ms": search_visible_ms,
        "page_persistence": page_persistence,
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
