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
from PySide6.QtWidgets import QAbstractButton, QApplication, QLabel, QToolButton, QWidget


REPO_ROOT = Path(__file__).resolve().parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from src.core.diagnostics.font_sanity import run_font_sanity
from src.core.diagnostics.qt_warnings import install_qt_message_handler, read_qt_warnings
from src.core.qt_runtime import ensure_qt_runtime_env, is_fatal_qt_warning, is_font_warning, is_qss_warning
from src.ui.main_window import MainWindow


def _drain(app: QApplication, cycles: int = 5, delay: float = 0.05) -> None:
    for _ in range(cycles):
        app.processEvents()
        time.sleep(delay)


def _slug(text: str) -> str:
    return "".join(ch.lower() if ch.isalnum() else "_" for ch in text).strip("_")


def _capture_shell(window: MainWindow, path: Path, include_popup: bool = False) -> None:
    popup = getattr(window, "_search_popup", None)
    win_geo = window.frameGeometry()
    region = win_geo
    if include_popup and isinstance(popup, QWidget) and popup.isVisible():
        region = region.united(popup.frameGeometry())

    screen = QApplication.primaryScreen()
    pix = QPixmap()
    if screen is not None and os.environ.get("QT_QPA_PLATFORM", "").strip().lower() not in {"offscreen", "minimal"}:
        pix = screen.grabWindow(0, region.x(), region.y(), region.width(), region.height())
    else:
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
    path.parent.mkdir(parents=True, exist_ok=True)
    pix.save(str(path), "PNG")


def _measure_content(window: MainWindow) -> tuple[int, int]:
    pages = getattr(window, "pages", None)
    if pages is None:
        return (0, 0)
    return (pages.width(), pages.height())


def _detect_clipping(window: MainWindow) -> list[str]:
    issues: list[str] = []
    for widget in window.findChildren(QWidget):
        if not widget.isVisible():
            continue
        if widget.window() is not window:
            continue
        parent = widget.parentWidget()
        if parent is None or not parent.isVisible():
            continue
        if widget.objectName() in {"NavRailButton", "NavRailAuxButton", "SideSheet"}:
            continue
        skip_bounds = False
        ancestor = parent
        while ancestor is not None:
            if ancestor.objectName() in {"PageViewport", "qt_scrollarea_viewport"}:
                skip_bounds = True
                break
            ancestor = ancestor.parentWidget()
        if skip_bounds:
            continue
        if widget.width() == 640 and widget.height() == 480 and widget.objectName() in {"Card", "Drawer", "EmptyState"}:
            continue
        top_left = widget.mapTo(parent, QPoint(0, 0))
        bottom_right = widget.mapTo(parent, QPoint(max(0, widget.width()), max(0, widget.height())))
        if top_left.x() < -2 or top_left.y() < -2:
            issues.append(f"outside-parent-start:{widget.objectName() or widget.__class__.__name__}")
            continue
        if bottom_right.x() > parent.width() + 2 or bottom_right.y() > parent.height() + 2:
            issues.append(f"outside-parent-end:{widget.objectName() or widget.__class__.__name__}")
            continue
        if isinstance(widget, QLabel):
            text = widget.text().strip()
            if text and (not widget.wordWrap()) and widget.height() < widget.fontMetrics().height():
                issues.append(f"label-clipped:{widget.objectName() or text[:24]}")
        if isinstance(widget, QAbstractButton):
            text = widget.text().strip()
            if text and widget.height() < widget.fontMetrics().height() + 2:
                issues.append(f"button-clipped:{widget.objectName() or text[:24]}")
    return issues


def _settings_nav_issues(window: MainWindow) -> list[str]:
    issues: list[str] = []
    nav = getattr(window, "settings_nav", None)
    if nav is None or not nav.isVisible():
        return issues
    rects = []
    for index in range(nav.count()):
        item = nav.item(index)
        if item is None or item.isHidden():
            continue
        rect = nav.visualItemRect(item)
        if rect.height() <= 0:
            issues.append(f"settings-nav-zero-height:{index}")
            continue
        if item.icon().isNull():
            issues.append(f"settings-nav-null-icon:{item.text()}")
        rects.append((item.text(), rect))
    for index in range(len(rects) - 1):
        current_name, current_rect = rects[index]
        next_name, next_rect = rects[index + 1]
        if current_rect.bottom() > next_rect.top() + 1:
            issues.append(f"settings-nav-overlap:{current_name}->{next_name}")
    return issues


def _icon_runtime_issues(window: MainWindow) -> list[str]:
    issues: list[str] = []
    placeholder_tokens = {"^", "[]", "!", "ex"}
    for button in window.findChildren(QToolButton):
        icon_name = str(button.property("icon_name") or "").strip().lower()
        if not icon_name:
            continue
        if icon_name in placeholder_tokens:
            issues.append(f"placeholder-icon-token:{icon_name}")
        if button.icon().isNull():
            issues.append(f"null-icon:{button.objectName() or button.toolTip() or icon_name}")
    nav = getattr(window, "settings_nav", None)
    if nav is not None:
        for index in range(nav.count()):
            item = nav.item(index)
            if item is None or item.isHidden():
                continue
            if item.icon().isNull():
                issues.append(f"settings-nav-null-icon:{item.text()}")
    return issues


def _record_issues(
    *,
    window: MainWindow,
    page: str,
    size_label: str,
    failures: list[str],
    clipping_issues: list[str],
) -> None:
    issues = _detect_clipping(window)
    if page == "Settings":
        issues.extend(_settings_nav_issues(window))
    if issues:
        clipping_issues.extend([f"{size_label}:{page}:{issue}" for issue in issues])
        failures.append(f"text/layout clipping at {size_label} page={page}: {', '.join(issues[:4])}")


def _write_outputs(
    *,
    out_dir: Path,
    manifest: dict[str, object],
    clipping_issues: list[str],
    qt_warnings: list[str],
) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "MANIFEST.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    clipping_text = "\n".join(clipping_issues) + "\n" if clipping_issues else "OK: no clipping issues detected.\n"
    (out_dir / "clipping_report.txt").write_text(clipping_text, encoding="utf-8")
    warnings_text = "\n".join(qt_warnings) + ("\n" if qt_warnings else "OK: no Qt warnings detected.\n")
    (out_dir / "qt_warnings.txt").write_text(warnings_text, encoding="utf-8")


def main() -> int:
    os.environ.setdefault("FIXFOX_SKIP_ONBOARDING", "1")
    os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
    ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    out_dir = REPO_ROOT / "docs" / "screenshots" / ts
    qt_warning_path = out_dir / "qt_warnings.txt"
    out_dir.mkdir(parents=True, exist_ok=True)

    failures: list[str] = []
    clipping_issues: list[str] = []
    captured: list[str] = []
    sizes = [(1024, 768), (1280, 720), (1600, 900)]
    search_visible_ms = 0
    normal_page_sizes: dict[str, tuple[int, int]] = {}
    maximized_growth: dict[str, dict[str, list[int]]] = {}

    font_result = run_font_sanity(
        report_path=out_dir / "font_sanity_report.txt",
        warning_log_path=qt_warning_path,
        verbose=False,
    )
    if not font_result.ok:
        failures.extend(font_result.failures)
        qt_warnings = read_qt_warnings(qt_warning_path)
        manifest = {
            "timestamp": ts,
            "sizes": [f"{width}x{height}" for width, height in sizes],
            "pages": [],
            "screenshots": captured,
            "search_dropdown_visible_ms": search_visible_ms,
            "clipping_issue_count": len(clipping_issues),
            "qt_warning_count": len(qt_warnings),
            "fatal_qt_warning_count": len(qt_warnings),
            "font_sanity_ok": False,
            "font_sanity_failures": font_result.failures,
            "maximized_growth": maximized_growth,
            "failure_count": len(failures),
            "failures": failures,
        }
        _write_outputs(out_dir=out_dir, manifest=manifest, clipping_issues=clipping_issues, qt_warnings=qt_warnings)
        print("UI walkthrough: FAIL")
        for row in failures:
            print(f"- {row}")
        print(f"- screenshots_dir={out_dir}")
        print(f"- manifest={out_dir / 'MANIFEST.json'}")
        print(f"- clipping_report={out_dir / 'clipping_report.txt'}")
        print(f"- qt_warnings={out_dir / 'qt_warnings.txt'}")
        return 1

    ensure_qt_runtime_env()
    cleanup = install_qt_message_handler(str(qt_warning_path))
    app = QApplication.instance() or QApplication([])
    window: MainWindow | None = None
    try:
        window = MainWindow()
        window.show()
        _drain(app, cycles=8)
        style_sheet = app.styleSheet()
        if "QComboBox::down-arrow" not in style_sheet:
            failures.append("Global stylesheet missing custom combobox arrow override.")
        if "QTreeWidget::branch:closed:has-children" not in style_sheet:
            failures.append("Global stylesheet missing custom tree expander override.")

        for width, height in sizes:
            window.resize(width, height)
            _drain(app, cycles=6)
            for idx, page in enumerate(getattr(window, "NAV_ITEMS", ())):
                window.nav.setCurrentRow(idx)
                _drain(app, cycles=5)
                if hasattr(window, "pages") and window.pages.currentIndex() != idx:
                    failures.append(f"page failed to load: {page} at {width}x{height}")
                    continue
                if width == 1024 and height == 768:
                    normal_page_sizes[page] = _measure_content(window)
                shot = out_dir / f"{width}x{height}_{idx + 1}_{_slug(page)}.png"
                _capture_shell(window, shot)
                captured.append(str(shot.relative_to(REPO_ROOT)).replace("\\", "/"))
                _record_issues(
                    window=window,
                    page=page,
                    size_label=f"{width}x{height}",
                    failures=failures,
                    clipping_issues=clipping_issues,
                )

        icon_issues = _icon_runtime_issues(window)
        if icon_issues:
            failures.append(f"icon runtime failures: {', '.join(icon_issues[:4])}")

        screen = QApplication.primaryScreen()
        if screen is not None:
            available = screen.availableGeometry().size()
            target_width = max(available.width(), window.width() + 240, 1680)
            target_height = max(available.height(), window.height() + 160, 980)
            if os.environ.get("QT_QPA_PLATFORM", "").strip().lower() in {"offscreen", "minimal"}:
                window.showNormal()
                window.resize(target_width, target_height)
            else:
                window.showMaximized()
            _drain(app, cycles=10)
            for idx, page in enumerate(getattr(window, "NAV_ITEMS", ())):
                window.nav.setCurrentRow(idx)
                _drain(app, cycles=4)
                before = normal_page_sizes.get(page, (0, 0))
                after = _measure_content(window)
                maximized_growth[page] = {"before": [before[0], before[1]], "after": [after[0], after[1]]}
                growth_w = after[0] - before[0]
                growth_h = after[1] - before[1]
                if growth_w < 120 or growth_h < 60:
                    failures.append(
                        f"Maximized content failed to expand for {page}: before={before} after={after}"
                    )
                shot = out_dir / f"maximized_{idx + 1}_{_slug(page)}.png"
                _capture_shell(window, shot)
                captured.append(str(shot.relative_to(REPO_ROOT)).replace("\\", "/"))
                _record_issues(
                    window=window,
                    page=page,
                    size_label="maximized",
                    failures=failures,
                    clipping_issues=clipping_issues,
                )
            window.showNormal()
            window.resize(1024, 768)
            _drain(app, cycles=5)
        else:
            failures.append("No primary screen available for maximize/fullscreen resize validation.")

        window._focus_top_search()
        window.top_search.setText("quick")
        window._refresh_global_search_results()
        _drain(app, cycles=4)
        if not window._search_popup.isVisible():
            failures.append("Search dropdown did not open.")
        started = time.perf_counter()
        QTest.qWait(550)
        _drain(app, cycles=3)
        search_visible_ms = int((time.perf_counter() - started) * 1000.0)
        if not window._search_popup.isVisible():
            failures.append("Search dropdown disappears before 500ms.")
        search_shot = out_dir / "search_dropdown_open.png"
        _capture_shell(window, search_shot, include_popup=True)
        captured.append(str(search_shot.relative_to(REPO_ROOT)).replace("\\", "/"))

        if getattr(window, "concierge", None) is not None and window.concierge.collapsed:
            window.btn_panel_toggle.click()
            _drain(app, cycles=4)
        if getattr(window, "concierge", None) is None or window.concierge.collapsed:
            failures.append("Details side sheet failed to open.")
        detail_shot = out_dir / "details_side_sheet_open.png"
        _capture_shell(window, detail_shot)
        captured.append(str(detail_shot.relative_to(REPO_ROOT)).replace("\\", "/"))
        if getattr(window, "concierge", None) is not None and not window.concierge.collapsed:
            window.btn_panel_toggle.click()
            _drain(app, cycles=3)

        window.run_quick_check("Quick Check")
        deadline = time.time() + 75
        while time.time() < deadline:
            _drain(app, cycles=3, delay=0.08)
            runner = getattr(window, "tool_runner", None)
            if runner is not None and runner.isVisible():
                runner_shot = out_dir / "tool_runner_quick_check.png"
                runner.grab().save(str(runner_shot), "PNG")
                captured.append(str(runner_shot.relative_to(REPO_ROOT)).replace("\\", "/"))
                break
        else:
            failures.append("Tool Runner screenshot not captured from Quick Check.")
        if getattr(window, "concierge", None) is not None and not window.concierge.collapsed:
            window.btn_panel_toggle.click()
            _drain(app, cycles=4)
    finally:
        try:
            if window is not None:
                deadline = time.time() + 90
                while getattr(window, "active_worker", None) is not None and time.time() < deadline:
                    _drain(app, cycles=3, delay=0.08)
                if getattr(window, "active_worker", None) is not None:
                    window._cancel_task()
                    _drain(app, cycles=12, delay=0.08)
        except Exception:
            pass
        if window is not None:
            window.close()
            _drain(app, cycles=2)
        cleanup()

    qt_warnings = read_qt_warnings(qt_warning_path)
    fatal_qt_warnings = []
    for line in qt_warnings:
        message = line.split("] ", 1)[1] if "] " in line else line
        if is_qss_warning(message) or is_font_warning(message) or is_fatal_qt_warning(message):
            fatal_qt_warnings.append(line)
    if fatal_qt_warnings:
        failures.append(f"Qt warnings detected: {', '.join(fatal_qt_warnings[:3])}")

    manifest = {
        "timestamp": ts,
        "sizes": [f"{width}x{height}" for width, height in sizes],
        "pages": list(getattr(window, "NAV_ITEMS", ())) if window is not None else [],
        "screenshots": captured,
        "search_dropdown_visible_ms": search_visible_ms,
        "clipping_issue_count": len(clipping_issues),
        "qt_warning_count": len(qt_warnings),
        "fatal_qt_warning_count": len(fatal_qt_warnings),
        "font_sanity_ok": font_result.ok,
        "font_sanity_failures": font_result.failures,
        "maximized_growth": maximized_growth,
        "failure_count": len(failures),
        "failures": failures,
    }
    _write_outputs(out_dir=out_dir, manifest=manifest, clipping_issues=clipping_issues, qt_warnings=qt_warnings)

    if failures:
        print("UI walkthrough: FAIL")
        for row in failures:
            print(f"- {row}")
        print(f"- screenshots_dir={out_dir}")
        print(f"- manifest={out_dir / 'MANIFEST.json'}")
        print(f"- clipping_report={out_dir / 'clipping_report.txt'}")
        print(f"- qt_warnings={out_dir / 'qt_warnings.txt'}")
        return 1

    print("UI walkthrough: PASS")
    print(f"screenshots_dir={out_dir}")
    print(f"manifest={out_dir / 'MANIFEST.json'}")
    print(f"clipping_report={out_dir / 'clipping_report.txt'}")
    print(f"qt_warnings={out_dir / 'qt_warnings.txt'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
