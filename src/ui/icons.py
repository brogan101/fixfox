from __future__ import annotations

from pathlib import Path

from PySide6.QtCore import Qt
from PySide6.QtGui import QColor, QIcon, QPainter, QPixmap
from PySide6.QtWidgets import QApplication, QStyle, QWidget

from ..core.utils import resource_path


_ICON_DIR = Path(resource_path("assets/icons"))
_ICON_CACHE: dict[tuple[str, str, int], QIcon] = {}

_ICON_ALIASES: dict[str, str] = {
    "home": "home",
    "playbooks": "playbooks",
    "toolbox": "toolbox",
    "diagnose": "diagnose",
    "fixes": "fixes",
    "reports": "reports",
    "history": "history",
    "settings": "settings",
    "help": "help",
    "search": "search",
    "run": "run",
    "play": "run",
    "stop": "stop",
    "cancel": "cancel",
    "export": "export",
    "panel": "panel",
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
    "diagnose": QStyle.SP_FileDialogDetailedView,
    "fixes": QStyle.SP_BrowserReload,
    "reports": QStyle.SP_DialogSaveButton,
    "history": QStyle.SP_FileDialogListView,
    "toolbox": QStyle.SP_ComputerIcon,
    "settings": QStyle.SP_FileDialogInfoView,
    "search": QStyle.SP_FileDialogContentsView,
    "export": QStyle.SP_DialogSaveButton,
    "help": QStyle.SP_DialogHelpButton,
    "menu": QStyle.SP_TitleBarMenuButton,
    "panel_open": QStyle.SP_TitleBarShadeButton,
    "panel_closed": QStyle.SP_TitleBarUnshadeButton,
    "run": QStyle.SP_MediaPlay,
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
    svg = _ICON_DIR / f"{slug}.svg"
    if svg.exists():
        return svg
    png = _ICON_DIR / f"{slug}.png"
    if png.exists():
        return png
    return None


def _tinted_icon(path: Path, color: QColor, size: int) -> QIcon:
    base = QPixmap(str(path))
    if base.isNull():
        return QIcon()
    scaled = base.scaled(size, size, Qt.KeepAspectRatio, Qt.SmoothTransformation)
    tinted = QPixmap(scaled.size())
    tinted.fill(Qt.transparent)
    painter = QPainter(tinted)
    painter.setRenderHint(QPainter.Antialiasing, True)
    painter.drawPixmap(0, 0, scaled)
    painter.setCompositionMode(QPainter.CompositionMode_SourceIn)
    painter.fillRect(tinted.rect(), color)
    painter.end()
    return QIcon(tinted)


def get_icon(name: str, widget: QWidget | None = None, size: int = 20) -> QIcon:
    icon_name = str(name or "").strip().lower()
    if not icon_name:
        icon_name = "menu"
    color = _icon_color(widget)
    color_key = color.name(QColor.HexArgb)
    cache_key = (icon_name, color_key, int(size))
    cached = _ICON_CACHE.get(cache_key)
    if cached is not None:
        return cached

    path = _asset_path(icon_name)
    if path is not None:
        icon = _tinted_icon(path, color, max(14, int(size)))
        if not icon.isNull():
            _ICON_CACHE[cache_key] = icon
            return icon

    style_key = _FALLBACK_STYLE.get(icon_name, QStyle.SP_FileIcon)
    if widget is not None:
        return widget.style().standardIcon(style_key)
    app = QApplication.instance()
    if app is not None:
        return app.style().standardIcon(style_key)
    return QIcon()

