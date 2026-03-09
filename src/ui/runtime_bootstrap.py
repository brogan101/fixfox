from __future__ import annotations

import logging
import os
import sys
import time
from dataclasses import dataclass

from PySide6.QtCore import Qt
from PySide6.QtCore import QByteArray
from PySide6.QtGui import QFont, QFontDatabase
from PySide6.QtWidgets import QApplication, QAbstractButton, QLabel, QLineEdit, QTextEdit, QWidget

from ..core.qt_runtime import ensure_qt_runtime_env
from ..core.settings import AppSettings, load_settings
from .app_qss import build_qss
from .font_utils import font_asset_candidates
from .theme import resolve_theme_tokens, set_ui_scale_percent


@dataclass(frozen=True)
class RuntimeBootstrapResult:
    settings: AppSettings
    font_family: str
    stylesheet_length: int


@dataclass(frozen=True)
class VisibleTextSnapshot:
    label: str
    nav_row: int
    stack_index: int
    current_page_id: str
    current_widget_token: str
    page_widget_tokens: tuple[str, ...]
    visible_text_count: int
    visible_text_examples: tuple[str, ...]
    blank_container_count: int


@dataclass(frozen=True)
class RuntimePersistenceReport:
    page_label: str
    failures: tuple[str, ...]
    snapshots: tuple[VisibleTextSnapshot, ...]

    @property
    def ok(self) -> bool:
        return not self.failures

    def to_lines(self) -> list[str]:
        lines = [f"Page: {self.page_label}", f"Result: {'PASS' if self.ok else 'FAIL'}"]
        for snapshot in self.snapshots:
            lines.append(
                f"- {snapshot.label}: nav={snapshot.nav_row} stack={snapshot.stack_index} "
                f"text={snapshot.visible_text_count} blank_containers={snapshot.blank_container_count} "
                f"page={snapshot.current_page_id or 'n/a'}"
            )
            if snapshot.visible_text_examples:
                lines.append("  sample: " + " | ".join(snapshot.visible_text_examples[:3]))
        if self.failures:
            lines.append("Failures:")
            lines.extend([f"- {row}" for row in self.failures])
        return lines


def _is_valid_font_blob(blob: bytes) -> bool:
    if len(blob) <= 50 * 1024:
        return False
    magic = blob[:4]
    return magic == b"\x00\x01\x00\x00" or magic == b"OTTO"


def load_bundled_font(
    *,
    app: QApplication | None = None,
    logger: logging.Logger | None = None,
) -> str:
    resolved_app = app or QApplication.instance()
    if resolved_app is None:
        return "Segoe UI"

    resolved_logger = logger or logging.getLogger("fixfox.runtime_bootstrap")
    qpa_platform = os.environ.get("QT_QPA_PLATFORM", "").strip().lower()
    if sys.platform == "win32" and qpa_platform not in {"offscreen", "minimal"}:
        family = "Segoe UI"
        resolved_app.setFont(QFont(family))
        resolved_logger.info("Selected UI font: %s (system)", family)
        print(f"[FixFox] UI font: {family}")
        return family

    for path in font_asset_candidates("NotoSans-Regular.ttf"):
        try:
            if not path.exists():
                continue
            raw = path.read_bytes()
            if not _is_valid_font_blob(raw):
                resolved_logger.warning("Font validation failed for %s", path)
                continue
            font_id = QFontDatabase.addApplicationFontFromData(QByteArray(raw))
            if font_id < 0:
                resolved_logger.warning("Qt rejected font data from %s", path)
                continue
            families = QFontDatabase.applicationFontFamilies(font_id)
            if not families:
                resolved_logger.warning("Qt returned no families for %s", path)
                continue
            family = str(families[0]).strip() or "Segoe UI"
            resolved_app.setFont(QFont(family))
            resolved_logger.info("Selected UI font: %s (%s)", family, path)
            print(f"[FixFox] UI font: {family}")
            return family
        except Exception as exc:
            resolved_logger.warning("Font load failed for %s: %s", path, exc)

    fallback = "Segoe UI"
    resolved_app.setFont(QFont(fallback))
    resolved_logger.info("Selected UI font fallback: %s", fallback)
    print(f"[FixFox] UI font: {fallback}")
    return fallback


def apply_runtime_ui_bootstrap(
    app: QApplication | None = None,
    *,
    logger: logging.Logger | None = None,
    settings: AppSettings | None = None,
) -> RuntimeBootstrapResult:
    resolved_logger = logger or logging.getLogger("fixfox.runtime_bootstrap")
    ensure_qt_runtime_env(resolved_logger)
    resolved_app = app or QApplication.instance() or QApplication([])
    resolved_settings = (settings or load_settings()).normalized()
    set_ui_scale_percent(getattr(resolved_settings, "ui_scale_pct", 100))
    font_family = load_bundled_font(app=resolved_app, logger=resolved_logger)
    tokens = resolve_theme_tokens(resolved_settings.theme_palette, resolved_settings.theme_mode)
    stylesheet = build_qss(tokens, resolved_settings.theme_mode, resolved_settings.density)
    resolved_app.setStyleSheet(stylesheet)
    return RuntimeBootstrapResult(
        settings=resolved_settings,
        font_family=font_family,
        stylesheet_length=len(stylesheet),
    )


def _pump_events(app: QApplication, seconds: float) -> None:
    deadline = time.time() + max(0.0, float(seconds))
    while time.time() < deadline:
        app.processEvents()
        time.sleep(0.02)


def _widget_token(widget: QWidget | None) -> str:
    if widget is None:
        return ""
    return f"{widget.metaObject().className()}::{widget.objectName()}::{id(widget)}"


def _visible_text_for_widget(widget: QWidget) -> str:
    if not widget.isVisible():
        return ""
    text = ""
    if isinstance(widget, QLabel):
        text = widget.text()
    elif isinstance(widget, QAbstractButton):
        text = widget.text()
    elif isinstance(widget, QLineEdit):
        text = widget.text().strip() or widget.placeholderText().strip()
    elif isinstance(widget, QTextEdit):
        text = widget.toPlainText().strip() or widget.placeholderText().strip()
    elif hasattr(widget, "text") and callable(getattr(widget, "text")):
        try:
            text = str(widget.text() or "")
        except Exception:
            text = ""
    return " ".join(str(text or "").split()).strip()


def _blank_container_count(root: QWidget) -> int:
    count = 0
    for widget in root.findChildren(QWidget):
        if not widget.isVisible():
            continue
        object_name = str(widget.objectName() or "").lower()
        if object_name not in {"card", "drawer", "emptystate", "inlinecallout"}:
            continue
        visible_text = 0
        for child in widget.findChildren(QWidget):
            text = _visible_text_for_widget(child)
            if text:
                visible_text += 1
                if visible_text >= 2:
                    break
        if visible_text == 0:
            count += 1
    return count


def capture_visible_text_snapshot(window: QWidget, *, label: str = "") -> VisibleTextSnapshot:
    nav_row = -1
    stack_index = -1
    current_page_id = ""
    current_widget_token = ""
    page_widget_tokens: list[str] = []
    if hasattr(window, "nav"):
        try:
            nav_row = int(window.nav.currentRow())
        except Exception:
            nav_row = -1
    if hasattr(window, "pages"):
        try:
            stack_index = int(window.pages.currentIndex())
            current_widget = window.pages.currentWidget()
            current_widget_token = _widget_token(current_widget)
            current_page_id = str(current_widget.property("page_id") or current_widget.objectName() or "")
            for idx in range(window.pages.count()):
                page_widget_tokens.append(_widget_token(window.pages.widget(idx)))
        except Exception:
            stack_index = -1
    visible_text: list[str] = []
    seen: set[str] = set()
    for widget in window.findChildren(QWidget):
        text = _visible_text_for_widget(widget)
        if not text:
            continue
        key = text.lower()
        if key in seen:
            continue
        seen.add(key)
        visible_text.append(text)
    return VisibleTextSnapshot(
        label=label or "snapshot",
        nav_row=nav_row,
        stack_index=stack_index,
        current_page_id=current_page_id,
        current_widget_token=current_widget_token,
        page_widget_tokens=tuple(page_widget_tokens),
        visible_text_count=len(visible_text),
        visible_text_examples=tuple(visible_text[:8]),
        blank_container_count=_blank_container_count(window),
    )


def validate_runtime_persistence(
    window: QWidget,
    app: QApplication | None = None,
    *,
    page_label: str = "",
    sample_delays: tuple[float, ...] = (0.2, 1.0, 2.0),
) -> RuntimePersistenceReport:
    resolved_app = app or QApplication.instance()
    if resolved_app is None:
        raise RuntimeError("QApplication instance is required for runtime persistence validation.")
    snapshots: list[VisibleTextSnapshot] = [capture_visible_text_snapshot(window, label="baseline")]
    for delay in sample_delays:
        _pump_events(resolved_app, delay)
        snapshots.append(capture_visible_text_snapshot(window, label=f"+{delay:.1f}s"))

    failures: list[str] = []
    baseline = snapshots[0]
    if baseline.visible_text_count <= 0:
        failures.append("Baseline page rendered with no visible human-readable text.")
    if baseline.blank_container_count >= 4:
        failures.append(f"Baseline page shows {baseline.blank_container_count} blank visible containers.")

    for snapshot in snapshots[1:]:
        if baseline.nav_row >= 0 and snapshot.nav_row != baseline.nav_row:
            failures.append(f"{snapshot.label}: nav row changed from {baseline.nav_row} to {snapshot.nav_row}.")
        if baseline.stack_index >= 0 and snapshot.stack_index != baseline.stack_index:
            failures.append(f"{snapshot.label}: page stack index changed from {baseline.stack_index} to {snapshot.stack_index}.")
        if baseline.current_widget_token and snapshot.current_widget_token != baseline.current_widget_token:
            failures.append(f"{snapshot.label}: current page widget was replaced after first paint.")
        if baseline.page_widget_tokens and snapshot.page_widget_tokens != baseline.page_widget_tokens:
            failures.append(f"{snapshot.label}: stacked page widget set changed after first paint.")
        if baseline.visible_text_count >= 4 and snapshot.visible_text_count < max(3, int(baseline.visible_text_count * 0.6)):
            failures.append(
                f"{snapshot.label}: visible text dropped from {baseline.visible_text_count} to {snapshot.visible_text_count}."
            )
        if snapshot.blank_container_count > max(baseline.blank_container_count + 2, 4):
            failures.append(
                f"{snapshot.label}: blank visible containers increased from {baseline.blank_container_count} to {snapshot.blank_container_count}."
            )
    return RuntimePersistenceReport(
        page_label=page_label or (baseline.current_page_id or "window"),
        failures=tuple(dict.fromkeys(failures)),
        snapshots=tuple(snapshots),
    )
