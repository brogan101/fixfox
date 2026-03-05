from __future__ import annotations

from pathlib import Path

from PySide6.QtCore import QByteArray, Qt
from PySide6.QtGui import QColor, QIcon, QPainter, QPixmap
from PySide6.QtSvg import QSvgRenderer
from PySide6.QtWidgets import QApplication, QStyle, QWidget

from ..core.utils import resource_path

_ICON_DIR = Path(resource_path("assets/icons"))
_ICON_CACHE: dict[tuple[str, str, int], QIcon] = {}

_ICON_ALIASES: dict[str, str] = {
    "home": "home",
    "playbooks": "open_book",
    "open_book": "open_book",
    "toolbox": "playbooks",
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
}

_FALLBACK_STYLE: dict[str, QStyle.StandardPixmap] = {
    "home": QStyle.SP_DirHomeIcon,
    "open_book": QStyle.SP_DirOpenIcon,
    "diagnose": QStyle.SP_FileDialogDetailedView,
    "fixes": QStyle.SP_BrowserReload,
    "wrench": QStyle.SP_BrowserReload,
    "reports": QStyle.SP_DialogSaveButton,
    "history": QStyle.SP_FileDialogListView,
    "settings": QStyle.SP_ComputerIcon,
    "gear": QStyle.SP_ComputerIcon,
    "settings_gear": QStyle.SP_ComputerIcon,
    "search": QStyle.SP_FileDialogContentsView,
    "quick_check": QStyle.SP_MediaPlay,
    "export": QStyle.SP_DialogSaveButton,
    "help": QStyle.SP_DialogHelpButton,
    "menu": QStyle.SP_TitleBarMenuButton,
    "panel_open": QStyle.SP_TitleBarShadeButton,
    "panel_closed": QStyle.SP_TitleBarUnshadeButton,
    "details": QStyle.SP_TitleBarShadeButton,
    "run": QStyle.SP_MediaPlay,
    "play": QStyle.SP_MediaPlay,
    "preview": QStyle.SP_FileDialogInfoView,
    "close": QStyle.SP_TitleBarCloseButton,
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

    style_key = _FALLBACK_STYLE.get(icon_name, QStyle.SP_FileIcon)
    if widget is not None:
        icon = widget.style().standardIcon(style_key)
    else:
        app = QApplication.instance()
        icon = app.style().standardIcon(style_key) if app is not None else QIcon()
    _ICON_CACHE[cache_key] = icon
    return icon


def clear_icon_cache() -> None:
    _ICON_CACHE.clear()
