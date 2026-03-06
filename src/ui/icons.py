from __future__ import annotations

import logging
from pathlib import Path

from PySide6.QtCore import QByteArray, Qt
from PySide6.QtGui import QColor, QIcon, QPainter, QPixmap
from PySide6.QtSvg import QSvgRenderer
from PySide6.QtWidgets import QApplication, QWidget

from ..core.utils import resource_path

_ICON_DIR = Path(resource_path("assets/icons"))
_ICON_CACHE: dict[tuple[str, str, int], QIcon] = {}
LOGGER = logging.getLogger("fixfox.ui.icons")

_ICON_ALIASES: dict[str, str] = {
    "home": "home",
    "playbooks": "open_book",
    "open_book": "open_book",
    "toolbox": "playbooks",
    "tool": "playbooks",
    "book": "open_book",
    "task": "run",
    "star": "quick_check",
    "i": "info",
    "?": "info",
    "!": "info",
    "diagnose": "diagnose",
    "fixes": "wrench",
    "wrench": "wrench",
    "reports": "reports",
    "history": "history",
    "settings": "gear",
    "gear": "gear",
    "settings_gear": "settings_gear",
    "help": "help",
    "search": "search",
    "quick_check": "quick_check",
    "run": "run",
    "play": "play",
    "stop": "stop",
    "cancel": "stop",
    "export": "export",
    "download": "export",
    "panel": "panel_open",
    "details": "details",
    "panel_open": "panel_open",
    "panel_closed": "panel_closed",
    "menu": "menu",
    "overflow": "overflow",
    "pin": "pin",
    "close": "close",
    "info": "info",
    "privacy": "privacy",
    "shield": "shield",
    "preview": "preview",
    "chevron_down": "chevron_down",
    "chevron_right": "chevron_right",
    "chevron_left": "chevron_left",
    "next": "chevron_right",
    "back": "chevron_left",
    "check_circle": "check_circle",
}

def _icon_color(widget: QWidget | None) -> QColor:
    if widget is not None:
        return widget.palette().color(widget.foregroundRole())
    app = QApplication.instance()
    if app is not None:
        return app.palette().color(app.palette().Text)
    return QColor("#5E6A7D")


def _asset_path(icon_name: str) -> Path | None:
    slug = _ICON_ALIASES.get(icon_name, icon_name)
    for suffix in (".svg", ".png"):
        path = _ICON_DIR / f"{slug}{suffix}"
        if path.exists():
            return path
    return None


def render_svg(path: str | Path, size: int) -> QPixmap:
    target = max(14, int(size))
    pixmap = QPixmap(target, target)
    pixmap.fill(Qt.transparent)
    renderer = QSvgRenderer(str(path))
    if not renderer.isValid():
        return QPixmap()
    painter = QPainter(pixmap)
    painter.setRenderHint(QPainter.Antialiasing, True)
    painter.setRenderHint(QPainter.SmoothPixmapTransform, True)
    renderer.render(painter)
    painter.end()
    return pixmap


def render_svg_from_bytes(svg_data: bytes, size: int) -> QPixmap:
    target = max(14, int(size))
    pixmap = QPixmap(target, target)
    pixmap.fill(Qt.transparent)
    renderer = QSvgRenderer(QByteArray(svg_data))
    if not renderer.isValid():
        return QPixmap()
    painter = QPainter(pixmap)
    painter.setRenderHint(QPainter.Antialiasing, True)
    painter.setRenderHint(QPainter.SmoothPixmapTransform, True)
    renderer.render(painter)
    painter.end()
    return pixmap


def _rasterize_asset(path: Path, size: int) -> QPixmap:
    if path.suffix.lower() == ".svg":
        return render_svg(path, size)
    base = QPixmap(str(path))
    if base.isNull():
        return QPixmap()
    return base.scaled(size, size, Qt.KeepAspectRatio, Qt.SmoothTransformation)


def _tint_pixmap(base: QPixmap, color: QColor) -> QPixmap:
    if base.isNull():
        return QPixmap()
    tinted = QPixmap(base.size())
    tinted.fill(Qt.transparent)
    painter = QPainter(tinted)
    painter.setRenderHint(QPainter.Antialiasing, True)
    painter.setRenderHint(QPainter.SmoothPixmapTransform, True)
    painter.drawPixmap(0, 0, base)
    painter.setCompositionMode(QPainter.CompositionMode_SourceIn)
    painter.fillRect(tinted.rect(), color)
    painter.end()
    return tinted


def get_icon(name: str, widget: QWidget | None = None, size: int = 20) -> QIcon:
    icon_name = str(name or "").strip().lower() or "menu"
    color = _icon_color(widget)
    color_key = color.name(QColor.HexArgb)
    target_size = max(14, int(size))
    cache_key = (icon_name, color_key, target_size)
    cached = _ICON_CACHE.get(cache_key)
    if cached is not None:
        return cached

    path = _asset_path(icon_name)
    if path is not None:
        pixmap = _rasterize_asset(path, target_size)
        if not pixmap.isNull():
            icon = QIcon(_tint_pixmap(pixmap, color))
            if not icon.isNull():
                _ICON_CACHE[cache_key] = icon
                return icon
    LOGGER.error("missing_or_invalid_icon_asset name=%s icon_dir=%s", icon_name, _ICON_DIR)
    empty = QIcon()
    _ICON_CACHE[cache_key] = empty
    return empty


def clear_icon_cache() -> None:
    _ICON_CACHE.clear()
