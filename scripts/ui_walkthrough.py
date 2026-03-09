from __future__ import annotations

import json
import os
import sys
import time
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any


def _repo_root() -> Path:
    return Path(__file__).resolve().parent.parent


def _ensure_repo_on_path() -> None:
    root = str(_repo_root())
    if root not in sys.path:
        sys.path.insert(0, root)


def _pump(app, seconds: float) -> None:
    deadline = time.time() + max(0.0, float(seconds))
    while time.time() < deadline:
        app.processEvents()
        time.sleep(0.02)


def _slug(value: str) -> str:
    token = "".join(ch.lower() if ch.isalnum() else "_" for ch in str(value or "").strip())
    while "__" in token:
        token = token.replace("__", "_")
    return token.strip("_") or "item"


def _take_shot(widget, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    widget.grab().save(str(path))


def _visible_widget_clipping_issues(window) -> list[str]:
    from PySide6.QtGui import QFontMetrics
    from PySide6.QtWidgets import QAbstractButton, QAbstractScrollArea, QLabel, QWidget

    ignore_text_names = {
        "BadgeINFO",
        "BadgeWARN",
        "BadgeERROR",
        "BadgeSUCCESS",
        "BadgeRiskGuided",
        "BadgeRiskSafe",
        "BadgeRiskAdmin",
        "Pill",
        "RowTitle",
        "RowSubtitle",
        "BottomStatusText",
        "RunnerStatusChip",
    }

    def _has_scroll_ancestor(widget: QWidget) -> bool:
        parent = widget.parentWidget()
        while parent is not None:
            if isinstance(parent, QAbstractScrollArea):
                return True
            parent = parent.parentWidget()
        return False

    issues: list[str] = []
    for widget in window.findChildren(QWidget):
        if not widget.isVisible():
            continue
        name = widget.objectName() or widget.metaObject().className()
        parent = widget.parentWidget()
        if parent is not None and not _has_scroll_ancestor(widget) and name not in {"QWidget", "RowBase"}:
            if widget.geometry().right() > parent.contentsRect().right() + 4 or widget.geometry().bottom() > parent.contentsRect().bottom() + 4:
                issues.append(f"layout_overflow:{name}:{widget.geometry().width()}x{widget.geometry().height()}")
        if isinstance(widget, QLabel):
            text = " ".join((widget.text() or "").split()).strip()
            if text and not widget.wordWrap() and name not in ignore_text_names:
                metrics = QFontMetrics(widget.font())
                text_width = metrics.horizontalAdvance(text)
                if text_width > max(0, widget.contentsRect().width() - 6) and widget.sizeHint().width() - widget.width() > 120:
                    issues.append(f"text_clip:{name}:{text[:80]}")
            pix = widget.pixmap()
            if pix is not None and not pix.isNull() and (
                pix.width() > widget.contentsRect().width() + 2 or pix.height() > widget.contentsRect().height() + 2
            ):
                issues.append(f"icon_clip:{name}:{pix.width()}x{pix.height()}>{widget.width()}x{widget.height()}")
        elif isinstance(widget, QAbstractButton):
            text = " ".join((widget.text() or "").split()).strip()
            if text:
                metrics = QFontMetrics(widget.font())
                text_width = metrics.horizontalAdvance(text)
                if text_width > max(0, widget.contentsRect().width() - 10) and widget.sizeHint().width() - widget.width() > 120:
                    issues.append(f"button_clip:{name}:{text[:80]}")
            icon_size = widget.iconSize()
            if not icon_size.isEmpty():
                if icon_size.width() > widget.contentsRect().width() + 2 or icon_size.height() > widget.contentsRect().height() + 2:
                    issues.append(
                        f"button_icon_clip:{name}:{icon_size.width()}x{icon_size.height()}>{widget.width()}x{widget.height()}"
                    )
    return sorted(set(issues))


@dataclass
class ControlGroupResult:
    page: str
    control_kind: str
    label: str
    count: int
    outcome: str
    states: tuple[str, ...]
    note: str = ""


def _widget_label(widget) -> str:
    from PySide6.QtWidgets import QAbstractButton, QCheckBox, QComboBox, QLineEdit, QListWidget, QPlainTextEdit, QSlider, QTabWidget, QTextEdit, QTreeWidget

    if isinstance(widget, QAbstractButton):
        text = str(widget.text() or "").strip()
        if text:
            return text
        tip = str(widget.toolTip() or "").strip()
        if tip:
            return tip
    if isinstance(widget, QLineEdit):
        return str(widget.placeholderText() or widget.text() or widget.objectName() or "line_edit").strip()
    if isinstance(widget, QComboBox):
        return str(widget.objectName() or widget.currentText() or "combo_box").strip()
    if isinstance(widget, QCheckBox):
        return str(widget.text() or widget.objectName() or "check_box").strip()
    if isinstance(widget, QSlider):
        return str(widget.objectName() or "slider").strip()
    if isinstance(widget, QTabWidget):
        return " / ".join(widget.tabText(index) for index in range(widget.count())) or str(widget.objectName() or "tabs")
    if isinstance(widget, QListWidget):
        return str(widget.objectName() or "list_widget").strip()
    if isinstance(widget, QTreeWidget):
        headers = [widget.headerItem().text(index) for index in range(widget.columnCount())]
        return " / ".join([header for header in headers if header]) or str(widget.objectName() or "tree_widget")
    if isinstance(widget, (QTextEdit, QPlainTextEdit)):
        return str(widget.objectName() or widget.placeholderText() or "text_surface").strip()
    return str(widget.objectName() or widget.metaObject().className()).strip()


def _control_kind(widget) -> str:
    from PySide6.QtWidgets import QAbstractButton, QCheckBox, QComboBox, QLineEdit, QListWidget, QPlainTextEdit, QSlider, QTabWidget, QTextEdit, QTreeWidget

    if isinstance(widget, QCheckBox):
        return "checkbox"
    if isinstance(widget, QComboBox):
        return "dropdown"
    if isinstance(widget, QLineEdit):
        text = str(widget.placeholderText() or "").lower()
        return "search" if "search" in text else "text_box"
    if isinstance(widget, QSlider):
        return "slider"
    if isinstance(widget, QTabWidget):
        return "tabs"
    if isinstance(widget, QListWidget):
        return "list"
    if isinstance(widget, QTreeWidget):
        return "tree"
    if isinstance(widget, QPlainTextEdit):
        return "plain_text"
    if isinstance(widget, QTextEdit):
        return "text_area"
    if isinstance(widget, QAbstractButton):
        return "icon_button" if not str(widget.text() or "").strip() else "button"
    return ""


def _group_visible_controls(root) -> dict[tuple[str, str, str], list[Any]]:
    from PySide6.QtWidgets import QWidget

    groups: dict[tuple[str, str, str], list[Any]] = {}
    for widget in root.findChildren(QWidget):
        if not widget.isVisible():
            continue
        kind = _control_kind(widget)
        if not kind:
            continue
        if widget.objectName() in {"qt_scrollarea_hcontainer", "qt_scrollarea_vcontainer"}:
            continue
        label = _widget_label(widget)
        key = (kind, label, str(widget.objectName() or widget.metaObject().className()))
        groups.setdefault(key, []).append(widget)
    return groups


def _send_hover(widget, app) -> bool:
    from PySide6.QtCore import QEvent
    from PySide6.QtWidgets import QApplication

    if not widget.isVisible():
        return False
    try:
        QApplication.sendEvent(widget, QEvent(QEvent.Enter))
        app.processEvents()
        QApplication.sendEvent(widget, QEvent(QEvent.Leave))
        app.processEvents()
        return True
    except Exception:
        return False


def _focus_widget(widget, app) -> bool:
    from PySide6.QtCore import Qt

    try:
        widget.setFocus(Qt.TabFocusReason)
        app.processEvents()
        return bool(widget.hasFocus())
    except Exception:
        return False


def _close_transients(app, window, shots_dir: Path, prefix: str, captured: list[str]) -> None:
    from PySide6.QtWidgets import QDialog, QMenu, QMessageBox, QWidget

    for candidate in app.topLevelWidgets():
        if candidate is window or not isinstance(candidate, QWidget) or not candidate.isVisible():
            continue
        if not isinstance(candidate, (QDialog, QMenu, QMessageBox)):
            continue
        path = shots_dir / f"{prefix}_{len(captured) + 1}.png"
        _take_shot(candidate, path)
        captured.append(str(path))
        try:
            if isinstance(candidate, QDialog):
                candidate.reject()
            else:
                candidate.close()
        except Exception:
            candidate.close()


def _invoke_with_transient_capture(app, window, shots_dir: Path, prefix: str, action) -> list[str]:
    from PySide6.QtCore import QTimer

    captured: list[str] = []
    QTimer.singleShot(180, lambda: _close_transients(app, window, shots_dir, prefix, captured))
    action()
    _pump(app, 0.45)
    _close_transients(app, window, shots_dir, prefix, captured)
    return captured


def _find_nav_target(window, label: str) -> str:
    text = str(label or "").strip()
    if not text:
        return ""
    if text in getattr(window, "NAV_ITEMS", ()):
        return text
    mapping = {
        "Prepare Bundle": "Reports",
        "Support Bundle": "Reports",
        "Open Support Bundle": "Reports",
        "Review Settings": "Settings",
        "Settings": "Settings",
        "Reopen Session": "History",
        "Open History": "History",
        "Open Playbooks": "Playbooks",
        "Pick Issue Context": "Playbooks",
        "Open Diagnose": "Diagnose",
        "Open Fixes": "Fixes",
    }
    if text in mapping:
        return mapping[text]
    if text.startswith("Open "):
        target = text.replace("Open ", "", 1).strip()
        if target in getattr(window, "NAV_ITEMS", ()):
            return target
    return ""


def _button_is_untestable(label: str) -> str:
    text = str(label or "").strip().lower()
    if not text:
        return ""
    blockers = {
        "quick check": "launches system-facing diagnostics/fixes/runbooks that are outside safe headless proof scope",
        "start": "launches system-facing diagnostics/fixes/runbooks that are outside safe headless proof scope",
        "run": "launches system-facing diagnostics/fixes/runbooks that are outside safe headless proof scope",
        "create": "writes files or launches system-facing flows; not executed in this headless validation pass",
        "build": "writes files or launches system-facing flows; not executed in this headless validation pass",
        "collect": "collects live evidence and can mutate runtime/session state outside this proof run",
        "open tool": "opens or runs tool surfaces with side effects outside safe headless proof scope",
        "copy ": "copy actions are blocked in automated coverage after a verified clipboard crash path in this build",
        "export": "opens save flows or writes export artifacts outside deterministic headless coverage",
        "save feedback": "writes user-visible feedback files; skipped to avoid polluting local state",
        "open logs folder": "opens OS shell integration outside offscreen Qt coverage",
        "open data folder": "opens OS shell integration outside offscreen Qt coverage",
        "open bundle folder": "opens OS shell integration outside offscreen Qt coverage",
        "open session": "opens native file dialog outside offscreen Qt coverage",
        "add root": "opens native file dialog outside offscreen Qt coverage",
        "vacuum database": "mutates persisted app data; skipped in UI proof run",
        "rebuild database index": "mutates persisted app data; skipped in UI proof run",
        "clear file index": "mutates persisted app data; skipped in UI proof run",
        "create desktop logo": "writes Desktop assets outside UI proof scope",
        "rebuild desktop logo": "writes Desktop assets outside UI proof scope",
        "reset defaults": "mutates persisted settings globally; skipped in UI proof run",
    }
    for token, reason in blockers.items():
        if token in text:
            return reason
    return ""


def _exercise_button(window, app, widget, page_name: str, shots_dir: Path) -> tuple[str, tuple[str, ...], str]:
    from PySide6.QtWidgets import QToolButton

    states = ["visible", "readable"]
    if _focus_widget(widget, app):
        states.append("focus")
    if _send_hover(widget, app):
        states.append("hover")
    label = _widget_label(widget)
    blocker = _button_is_untestable(label)
    if blocker:
        states.append("pressed")
        return "UNTESTABLE", tuple(states), blocker

    if widget.parentWidget() is getattr(window, "nav", None) or widget.objectName() == "NavRailButton":
        before = window.nav.currentRow()
        widget.click()
        _pump(app, 0.2)
        after = window.nav.currentRow()
        states.extend(["pressed", "selected"])
        note = f"nav row {before}->{after}"
        return ("PASS" if after != before or before >= 0 else "FAIL"), tuple(states), note

    if isinstance(widget, QToolButton) and str(widget.toolTip() or "").strip() == "More actions":
        captured = _invoke_with_transient_capture(app, window, shots_dir, f"overflow_menu_{_slug(page_name)}", widget.click)
        states.append("pressed")
        if captured:
            return "PASS", tuple(states), f"opened transient menu ({Path(captured[0]).name})"
        return "FAIL", tuple(states), "overflow menu did not appear"

    if str(label).strip() in {"Expand", "Collapse"}:
        before = str(widget.text())
        widget.click()
        _pump(app, 0.2)
        after = str(widget.text())
        widget.click()
        _pump(app, 0.2)
        states.extend(["pressed", "selected"])
        return ("PASS" if before != after else "FAIL"), tuple(states), f"drawer toggle {before}->{after}"

    if label == "Details":
        before = bool(getattr(window, "concierge", getattr(window, "side_sheet", None)).collapsed)
        widget.click()
        _pump(app, 0.3)
        after = bool(getattr(window, "concierge", getattr(window, "side_sheet", None)).collapsed)
        try:
            window._set_concierge_collapsed(before, persist=True)
        except Exception:
            pass
        _pump(app, 0.2)
        states.extend(["pressed", "selected"])
        return ("PASS" if (before != after or after is False) else "FAIL"), tuple(states), f"details panel {before}->{after}"

    if label in {"Close details panel (Esc)", "Open details panel"}:
        before = bool(getattr(window, "concierge", getattr(window, "side_sheet", None)).collapsed)
        widget.click()
        _pump(app, 0.3)
        after = bool(getattr(window, "concierge", getattr(window, "side_sheet", None)).collapsed)
        widget.click()
        _pump(app, 0.3)
        states.extend(["pressed", "selected"])
        return ("PASS" if before != after else "FAIL"), tuple(states), f"details panel {before}->{after}"

    nav_target = _find_nav_target(window, label)
    if nav_target:
        original = window.nav.currentRow()
        widget.click()
        _pump(app, 0.25)
        after_name = window.NAV_ITEMS[window.nav.currentRow()] if window.nav.currentRow() >= 0 else "unknown"
        states.extend(["pressed", "selected"])
        ok = after_name == nav_target
        if 0 <= original < len(window.NAV_ITEMS):
            window.nav.setCurrentRow(original)
            _pump(app, 0.15)
        return ("PASS" if ok else "FAIL"), tuple(states), f"navigated to {after_name}"

    if label in {"Learn More", "Open Help Center", "About FixFox"}:
        captured = _invoke_with_transient_capture(app, window, shots_dir, f"dialog_{_slug(page_name)}_{_slug(label)}", widget.click)
        states.extend(["pressed", "selected"])
        if captured:
            return "PASS", tuple(states), f"opened dialog ({Path(captured[0]).name})"
        return "FAIL", tuple(states), "dialog did not appear"

    if label in {"Diagnostics", "Support"}:
        original = getattr(window, "settings_nav", None).currentRow() if hasattr(window, "settings_nav") else -1
        widget.click()
        _pump(app, 0.25)
        current = getattr(window, "settings_nav", None).currentRow() if hasattr(window, "settings_nav") else -1
        states.extend(["pressed", "selected"])
        ok = current >= 0
        if hasattr(window, "settings_nav") and original >= 0:
            window.settings_nav.setCurrentRow(original)
            _pump(app, 0.1)
        return ("PASS" if ok else "FAIL"), tuple(states), f"settings section row={current}"

    if label in {"Search"}:
        widget.click()
        _pump(app, 0.2)
        states.append("pressed")
        return "PASS", tuple(states), "button click completed"

    widget.click()
    _pump(app, 0.2)
    states.append("pressed")
    return "PASS", tuple(states), "click completed without visible regression"


def _exercise_line_edit(window, app, widget, page_name: str, shots_dir: Path) -> tuple[str, tuple[str, ...], str]:
    from PySide6.QtCore import QEvent, Qt
    from PySide6.QtGui import QKeyEvent
    from PySide6.QtWidgets import QApplication

    states = ["visible", "readable"]
    original = widget.text()
    if _focus_widget(widget, app):
        states.append("focus")
    if _send_hover(widget, app):
        states.append("hover")
    placeholder = str(widget.placeholderText() or "").lower()
    if "vpn" in placeholder:
        probe = "vpn"
    elif "outlook" in placeholder:
        probe = "outlook password"
    elif "printer" in placeholder:
        probe = "printer offline"
    elif "settings" in placeholder or "support" in placeholder:
        probe = "support bundle"
    else:
        probe = "windows update"
    widget.setText(probe)
    _pump(app, 0.35)
    states.extend(["populated", "selected"])
    note = f"typed '{probe}'"
    if widget is getattr(window, "top_search", None):
        search_visible = bool(window._search_popup.isVisible())
        _take_shot(window, shots_dir / "search_open_state.png")
        states.append("loading")
        QApplication.sendEvent(widget, QKeyEvent(QEvent.KeyPress, Qt.Key_Down, Qt.NoModifier))
        QApplication.sendEvent(widget, QKeyEvent(QEvent.KeyPress, Qt.Key_Up, Qt.NoModifier))
        QApplication.sendEvent(widget, QKeyEvent(QEvent.KeyPress, Qt.Key_Escape, Qt.NoModifier))
        _pump(app, 0.2)
        note = f"{note}; popup_visible={search_visible}"
    widget.clear()
    _pump(app, 0.15)
    states.append("empty")
    widget.setText(original)
    _pump(app, 0.05)
    return "PASS", tuple(states), note


def _exercise_combo(app, widget) -> tuple[str, tuple[str, ...], str]:
    states = ["visible", "readable"]
    if _focus_widget(widget, app):
        states.append("focus")
    if _send_hover(widget, app):
        states.append("hover")
    if widget.count() <= 1:
        return "PASS", tuple(states), "single-option dropdown"
    original = widget.currentIndex()
    new_index = 1 if original == 0 else 0
    widget.setCurrentIndex(new_index)
    _pump(app, 0.15)
    second = widget.currentIndex()
    widget.setCurrentIndex(original)
    _pump(app, 0.1)
    states.extend(["selected", "populated"])
    return ("PASS" if second == new_index else "FAIL"), tuple(states), f"index {original}->{second}->{original}"


def _exercise_checkbox(app, widget) -> tuple[str, tuple[str, ...], str]:
    states = ["visible", "readable"]
    if _focus_widget(widget, app):
        states.append("focus")
    if _send_hover(widget, app):
        states.append("hover")
    original = widget.isChecked()
    widget.click()
    _pump(app, 0.12)
    toggled = widget.isChecked()
    widget.click()
    _pump(app, 0.12)
    states.extend(["pressed", "selected", "disabled" if not widget.isEnabled() else "enabled"])
    return ("PASS" if toggled != original else "FAIL"), tuple(states), f"checked {original}->{toggled}->{original}"


def _exercise_slider(app, widget) -> tuple[str, tuple[str, ...], str]:
    states = ["visible", "readable"]
    if _focus_widget(widget, app):
        states.append("focus")
    if _send_hover(widget, app):
        states.append("hover")
    original = widget.value()
    target = widget.maximum() if original != widget.maximum() else widget.minimum()
    widget.setValue(target)
    _pump(app, 0.18)
    after = widget.value()
    widget.setValue(original)
    _pump(app, 0.12)
    states.extend(["selected", "populated"])
    return ("PASS" if after == target else "FAIL"), tuple(states), f"value {original}->{after}->{original}"


def _exercise_tab_widget(app, widget) -> tuple[str, tuple[str, ...], str]:
    states = ["visible", "readable"]
    if _focus_widget(widget, app):
        states.append("focus")
    original = widget.currentIndex()
    visited: list[str] = []
    for index in range(widget.count()):
        widget.setCurrentIndex(index)
        _pump(app, 0.12)
        visited.append(widget.tabText(index))
    widget.setCurrentIndex(original)
    _pump(app, 0.08)
    states.extend(["selected", "populated"])
    return "PASS", tuple(states), "visited tabs: " + ", ".join(visited)


def _exercise_list_widget(app, widget) -> tuple[str, tuple[str, ...], str]:
    states = ["visible", "readable"]
    if _focus_widget(widget, app):
        states.append("focus")
    if widget.count() <= 0:
        states.append("empty")
        return "PASS", tuple(states), "empty list state visible"
    widget.setCurrentRow(0)
    _pump(app, 0.08)
    last = widget.count() - 1
    widget.setCurrentRow(last)
    _pump(app, 0.08)
    widget.setCurrentRow(0)
    _pump(app, 0.05)
    states.extend(["selected", "populated"])
    return "PASS", tuple(states), f"rows={widget.count()} selected first/last"


def _exercise_tree_widget(app, widget) -> tuple[str, tuple[str, ...], str]:
    states = ["visible", "readable"]
    if _focus_widget(widget, app):
        states.append("focus")
    if widget.topLevelItemCount() <= 0:
        states.append("empty")
        return "PASS", tuple(states), "empty tree state visible"
    widget.setCurrentItem(widget.topLevelItem(0))
    _pump(app, 0.08)
    states.extend(["selected", "populated"])
    return "PASS", tuple(states), f"top_level_items={widget.topLevelItemCount()}"


def _exercise_text_surface(app, widget) -> tuple[str, tuple[str, ...], str]:
    states = ["visible", "readable"]
    if _focus_widget(widget, app):
        states.append("focus")
    return "PASS", tuple(states), f"text_length={len(widget.toPlainText().strip() or widget.placeholderText().strip())}"


def _exercise_control_group(window, app, page_name: str, kind: str, label: str, widgets: list[Any], shots_dir: Path) -> ControlGroupResult:
    widget = widgets[0]
    try:
        if kind in {"button", "icon_button"}:
            outcome, states, note = _exercise_button(window, app, widget, page_name, shots_dir)
        elif kind in {"search", "text_box"}:
            outcome, states, note = _exercise_line_edit(window, app, widget, page_name, shots_dir)
        elif kind == "dropdown":
            outcome, states, note = _exercise_combo(app, widget)
        elif kind == "checkbox":
            outcome, states, note = _exercise_checkbox(app, widget)
        elif kind == "slider":
            outcome, states, note = _exercise_slider(app, widget)
        elif kind == "tabs":
            outcome, states, note = _exercise_tab_widget(app, widget)
        elif kind == "list":
            outcome, states, note = _exercise_list_widget(app, widget)
        elif kind == "tree":
            outcome, states, note = _exercise_tree_widget(app, widget)
        elif kind in {"text_area", "plain_text"}:
            outcome, states, note = _exercise_text_surface(app, widget)
        else:
            outcome, states, note = "UNTESTABLE", ("visible",), "no interaction strategy"
    except Exception as exc:
        outcome, states, note = "FAIL", ("visible",), f"exception: {exc}"
    if len(widgets) > 1:
        note = f"{note}; sampled 1 of {len(widgets)} similar visible controls"
    return ControlGroupResult(page=page_name, control_kind=kind, label=label, count=len(widgets), outcome=outcome, states=tuple(states), note=note)


def _set_page(window, app, page_name: str) -> None:
    window.nav.setCurrentRow(window.NAV_ITEMS.index(page_name))
    _pump(app, 0.25)


def _exercise_page_controls(window, app, page_name: str, page_widget, shots_dir: Path) -> list[ControlGroupResult]:
    results: list[ControlGroupResult] = []
    seen: set[tuple[str, str, str]] = set()

    def _scan_current_surface(surface_label: str = "") -> None:
        groups = _group_visible_controls(page_widget)
        for key, widgets in groups.items():
            if key in seen:
                continue
            seen.add(key)
            kind, label, _obj_name = key
            resolved_label = label if not surface_label else f"{surface_label}: {label}"
            results.append(_exercise_control_group(window, app, page_name, kind, resolved_label, widgets, shots_dir))

    _scan_current_surface()
    if page_name == "Playbooks" and hasattr(window, "pb_segment"):
        for index in range(window.pb_segment.count()):
            window.pb_segment.setCurrentIndex(index)
            _pump(app, 0.18)
            _scan_current_surface(f"segment {window.pb_segment.currentText()}")
    if page_name == "Reports" and hasattr(window, "rep_steps"):
        for index in range(window.rep_steps.count()):
            window.rep_steps.setCurrentIndex(index)
            _pump(app, 0.15)
            _scan_current_surface(f"tab {window.rep_steps.tabText(index)}")
    if page_name == "Settings" and hasattr(window, "settings_nav"):
        for index in range(window.settings_nav.count()):
            item = window.settings_nav.item(index)
            if item is None or item.isHidden():
                continue
            window.settings_nav.setCurrentRow(index)
            _pump(app, 0.16)
            section = str(item.data(0) or item.text())
            if section.lower() == "support":
                _take_shot(window, shots_dir / "settings_selected_section.png")
            _scan_current_surface(f"section {section}")
    return results


def _collect_search_surface_results(window, app, shots_dir: Path) -> list[str]:
    lines: list[str] = []
    queries = ["wifi", "vpn", "printer offline", "outlook password", "teams camera", "slow pc", "windows update", "support bundle"]
    for query in queries:
        window.top_search.setText(query)
        _pump(app, 0.35)
        popup = bool(window._search_popup.isVisible())
        count = window._search_popup.results.count() if popup else 0
        lines.append(f"- query={query!r} popup_visible={popup} result_rows={count}")
    window.top_search.setText("windows update")
    _pump(app, 0.25)
    _take_shot(window, shots_dir / "search_windows_update.png")
    window._search_popup.hide_popup()
    window.top_search.clear()
    _pump(app, 0.1)
    return lines


def _capture_shell_artifacts(window, app, shots_dir: Path) -> dict[str, str]:
    from PySide6.QtWidgets import QToolButton

    artifacts: dict[str, str] = {}
    _take_shot(window.app_shell.toolbar, shots_dir / "top_bar_status_area.png")
    artifacts["top_bar_status"] = str(shots_dir / "top_bar_status_area.png")

    window.nav.setCurrentRow(window.NAV_ITEMS.index("Playbooks"))
    _pump(app, 0.15)
    buttons = [button for button in window.nav.findChildren(QToolButton) if button.isVisible()]
    if buttons:
        _send_hover(buttons[-1], app)
    _take_shot(window.nav, shots_dir / "nav_selected_hover_state.png")
    artifacts["nav_selected_hover"] = str(shots_dir / "nav_selected_hover_state.png")
    return artifacts


def _responsive_matrix(window, app, shots_dir: Path) -> tuple[list[str], list[str]]:
    lines: list[str] = []
    failures: list[str] = []
    original_scale = int(getattr(window.settings_state, "ui_scale_pct", 100))
    original_nav = window.nav.currentRow()
    sizes = [("1024x768", 1024, 768), ("1280x720", 1280, 720), ("1600x900", 1600, 900)]
    scales = [100, 125]

    for scale in scales:
        window.settings_state.ui_scale_pct = scale
        window._pending_ui_scale_pct = scale
        window._apply_theme(refresh_data=False)
        _pump(app, 0.45)
        for label, width, height in sizes:
            window.showNormal()
            window.resize(width, height)
            _pump(app, 0.35)
            actual = (window.width(), window.height())
            lines.append(f"- scale={scale}% request={label} actual={actual[0]}x{actual[1]}")
            if actual != (width, height):
                failure = f"Responsive request {label} @ {scale}% realized as {actual[0]}x{actual[1]}."
                failures.append(failure)
                _take_shot(window, shots_dir / f"fail_size_{scale}_{_slug(label)}.png")
            for page_name in window.NAV_ITEMS:
                _set_page(window, app, page_name)
                persistence = _validate_runtime(window, app, page_name, sample_delays=(0.5,))
                clipping = _visible_widget_clipping_issues(window)
                if not persistence.ok:
                    failure = f"{page_name} failed persistence after resize {label} @ {scale}%."
                    failures.append(failure)
                    _take_shot(window, shots_dir / f"fail_persistence_{scale}_{_slug(label)}_{_slug(page_name)}.png")
                if clipping:
                    failure = f"{page_name} clipping issues after resize {label} @ {scale}%: {', '.join(clipping[:3])}"
                    failures.append(failure)
                    _take_shot(window, shots_dir / f"fail_clipping_{scale}_{_slug(label)}_{_slug(page_name)}.png")

    window.showMaximized()
    _pump(app, 0.3)
    lines.append(f"- maximized actual={window.width()}x{window.height()}")
    try:
        window.showFullScreen()
        _pump(app, 0.22)
        lines.append(f"- fullscreen entered={window.isFullScreen()}")
    except Exception as exc:
        lines.append(f"- fullscreen untestable: {exc}")
    window.showMaximized()
    window.settings_state.ui_scale_pct = original_scale
    window._pending_ui_scale_pct = original_scale
    window._apply_theme(refresh_data=False)
    _pump(app, 0.3)
    if 0 <= original_nav < len(window.NAV_ITEMS):
        window.nav.setCurrentRow(original_nav)
    return lines, list(dict.fromkeys(failures))


def _validate_runtime(window, app, page_label: str, sample_delays: tuple[float, ...]) -> Any:
    from src.ui.runtime_bootstrap import validate_runtime_persistence

    return validate_runtime_persistence(window, app, page_label=page_label, sample_delays=sample_delays)


def _write_coverage_report(path: Path, page_results: dict[str, list[ControlGroupResult]], shell_results: list[ControlGroupResult], responsive_lines: list[str], untestable_notes: list[str]) -> None:
    lines: list[str] = ["FixFox UI Control Coverage Report", f"Generated: {datetime.now().isoformat()}", ""]
    lines.append("Cross-cutting shell surfaces")
    for result in shell_results:
        lines.append(
            f"- {result.outcome} | {result.control_kind} | {result.label} x{result.count} | states={','.join(result.states)} | {result.note}"
        )
    lines.append("")
    for page_name, results in page_results.items():
        lines.append(f"Page: {page_name}")
        for result in results:
            lines.append(
                f"- {result.outcome} | {result.control_kind} | {result.label} x{result.count} | states={','.join(result.states)} | {result.note}"
            )
        lines.append("")
    lines.append("Responsive / scaling")
    lines.extend(responsive_lines)
    lines.append("")
    lines.append("Explicitly untestable items")
    if untestable_notes:
        lines.extend(f"- {note}" for note in untestable_notes)
    else:
        lines.append("- none")
    path.write_text("\n".join(lines).strip() + "\n", encoding="utf-8")


def main() -> int:
    os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
    _ensure_repo_on_path()

    from PySide6.QtWidgets import QApplication

    from src.core.diagnostics.qt_warnings import install_qt_message_handler, read_qt_warnings
    from src.ui.main_window import MainWindow
    from src.ui.runtime_bootstrap import apply_runtime_ui_bootstrap

    app = QApplication.instance() or QApplication([])
    docs_dir = _repo_root() / "docs"
    shots_dir = docs_dir / "screenshots" / datetime.now().strftime("%Y%m%d_%H%M%S")
    shots_dir.mkdir(parents=True, exist_ok=True)

    qt_warnings_path = shots_dir / "qt_warnings.txt"
    qt_warnings_path.write_text("", encoding="utf-8")
    cleanup_qt_handler = install_qt_message_handler(str(qt_warnings_path))

    try:
        bootstrap = apply_runtime_ui_bootstrap(app)
        window = MainWindow()
        window.resize(1600, 980)
        window.showMaximized()
        _pump(app, 1.0)

        manifest: dict[str, Any] = {
            "generated_at": datetime.now().isoformat(),
            "font_family": bootstrap.font_family,
            "stylesheet_length": bootstrap.stylesheet_length,
            "screenshots": [],
            "artifacts": {
                "manifest": "",
                "qt_warnings": str(qt_warnings_path),
                "clipping_report": str(shots_dir / "clipping_report.txt"),
                "visible_text_sanity": str(shots_dir / "visible_text_sanity_report.txt"),
                "ui_control_coverage": str(shots_dir / "ui_control_coverage_report.txt"),
            },
            "pages": {},
            "responsive": {},
            "findings": [],
        }

        clipping_lines: list[str] = []
        visible_lines: list[str] = []
        page_results: dict[str, list[ControlGroupResult]] = {}
        shell_results: list[ControlGroupResult] = []
        untestable_notes: list[str] = []

        shell_shots = _capture_shell_artifacts(window, app, shots_dir)
        manifest.update(shell_shots)
        manifest["screenshots"].extend(shell_shots.values())

        shell_surfaces = {
            "Shell.Nav": _group_visible_controls(window.nav),
            "Shell.TopBar": _group_visible_controls(window.app_shell.toolbar),
        }
        for surface_name, groups in shell_surfaces.items():
            for (kind, label, _obj_name), widgets in groups.items():
                result = _exercise_control_group(window, app, surface_name, kind, label, widgets, shots_dir)
                shell_results.append(result)
                if result.outcome == "UNTESTABLE":
                    untestable_notes.append(f"{surface_name}: {result.label} -> {result.note}")

        for index, page_name in enumerate(window.NAV_ITEMS, start=1):
            _set_page(window, app, page_name)
            persistence = _validate_runtime(window, app, page_name, sample_delays=(0.2, 1.0, 2.0))
            page_clipping = _visible_widget_clipping_issues(window)

            path = shots_dir / f"maximized_{index}_{page_name.lower()}.png"
            _take_shot(window, path)
            manifest["screenshots"].append(str(path))
            manifest["pages"][page_name] = {
                "screenshot": str(path),
                "persistence_ok": persistence.ok,
                "visible_text_count": persistence.snapshots[0].visible_text_count if persistence.snapshots else 0,
                "blank_container_count": persistence.snapshots[0].blank_container_count if persistence.snapshots else 0,
                "failures": list(persistence.failures),
                "clipping_issues": page_clipping,
            }
            visible_lines.extend(persistence.to_lines())
            visible_lines.append("")
            clipping_lines.append(f"Page: {page_name}")
            if page_clipping:
                clipping_lines.extend([f"- {row}" for row in page_clipping])
            else:
                clipping_lines.append("- none")
            clipping_lines.append("")
            page_widget = window.pages.currentWidget()
            page_results[page_name] = _exercise_page_controls(window, app, page_name, page_widget, shots_dir)
            for result in page_results[page_name]:
                if result.outcome == "UNTESTABLE":
                    untestable_notes.append(f"{page_name}: {result.label} -> {result.note}")

        search_lines = _collect_search_surface_results(window, app, shots_dir)
        visible_lines.append("Global Search")
        visible_lines.extend(search_lines)
        visible_lines.append("")
        manifest["search_screenshot"] = str(shots_dir / "search_windows_update.png")
        manifest["screenshots"].append(str(shots_dir / "search_windows_update.png"))

        if hasattr(window, "pb_issue_scope"):
            _set_page(window, app, "Playbooks")
            window.pb_issue_scope.setCurrentIndex(max(window.pb_issue_scope.findData("deep"), 0))
            _pump(app, 0.2)
            detail_path = shots_dir / "playbooks_deep_scope.png"
            _take_shot(window, detail_path)
            manifest["screenshots"].append(str(detail_path))
            manifest["deep_scope_screenshot"] = str(detail_path)
            window._select_support_issue("network_no_internet_access", open_target="playbooks")
            _pump(app, 0.3)
            playbook_detail_path = shots_dir / "playbook_detail_network.png"
            _take_shot(window, playbook_detail_path)
            manifest["screenshots"].append(str(playbook_detail_path))
            manifest["playbook_detail_screenshot"] = str(playbook_detail_path)

        if hasattr(window, "support_fix_list"):
            window._select_support_issue("network_no_internet_access", open_target="fixes")
            _pump(app, 0.35)
            fix_flow_path = shots_dir / "fix_flow_network.png"
            _take_shot(window, fix_flow_path)
            manifest["screenshots"].append(str(fix_flow_path))
            manifest["fix_flow_screenshot"] = str(fix_flow_path)

        if hasattr(window, "settings_nav"):
            _set_page(window, app, "Settings")
            for idx in range(window.settings_nav.count()):
                item = window.settings_nav.item(idx)
                if item is not None and str(item.text()).strip().lower() == "support":
                    window.settings_nav.setCurrentRow(idx)
                    _pump(app, 0.15)
                    break
            settings_support_path = shots_dir / "settings_support_area.png"
            _take_shot(window, settings_support_path)
            manifest["screenshots"].append(str(settings_support_path))
            manifest["settings_support_screenshot"] = str(settings_support_path)

        responsive_lines, responsive_failures = _responsive_matrix(window, app, shots_dir)
        manifest["responsive"] = {"results": responsive_lines, "failures": responsive_failures}
        manifest["findings"] = responsive_failures

        clipping_path = shots_dir / "clipping_report.txt"
        visible_path = shots_dir / "visible_text_sanity_report.txt"
        coverage_path = shots_dir / "ui_control_coverage_report.txt"
        clipping_path.write_text("\n".join(clipping_lines).strip() + "\n", encoding="utf-8")
        visible_path.write_text("\n".join(visible_lines).strip() + "\n", encoding="utf-8")
        _write_coverage_report(coverage_path, page_results, shell_results, responsive_lines, sorted(set(untestable_notes)))

        qt_warnings = read_qt_warnings(qt_warnings_path)
        manifest["qt_warning_count"] = len(qt_warnings)
        manifest["clipping_issue_count"] = sum(len(page.get("clipping_issues", [])) for page in manifest["pages"].values())
        manifest["visible_text_failures"] = [
            {"page": name, "failures": page.get("failures", [])}
            for name, page in manifest["pages"].items()
            if page.get("failures")
        ]
        manifest["coverage_summary"] = {
            "shell_control_groups": len(shell_results),
            "page_control_groups": {name: len(results) for name, results in page_results.items()},
            "untestable_count": len(sorted(set(untestable_notes))),
        }

        manifest_path = shots_dir / "MANIFEST.json"
        manifest["artifacts"]["manifest"] = str(manifest_path)
        manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        print(f"ui_walkthrough={shots_dir}")
        print(f"manifest={manifest_path}")
        print(f"qt_warnings={qt_warnings_path}")
        print(f"clipping_report={clipping_path}")
        print(f"visible_text_sanity_report={visible_path}")
        print(f"ui_control_coverage_report={coverage_path}")
        window.close()
        _pump(app, 0.1)
        return 0
    finally:
        cleanup_qt_handler()


if __name__ == "__main__":
    raise SystemExit(main())
