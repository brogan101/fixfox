from __future__ import annotations

import logging
import os
import sys
from dataclasses import dataclass

from PySide6.QtCore import QByteArray
from PySide6.QtGui import QFont, QFontDatabase
from PySide6.QtWidgets import QApplication

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
