from __future__ import annotations

from PySide6.QtGui import QIcon
from PySide6.QtWidgets import QStyle, QWidget


_ICON_ALIASES: dict[str, QStyle.StandardPixmap] = {
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
    "play": QStyle.SP_MediaPlay,
    "preview": QStyle.SP_FileDialogInfoView,
}


def get_icon(name: str, widget: QWidget) -> QIcon:
    pixmap = _ICON_ALIASES.get(name, QStyle.SP_FileIcon)
    return widget.style().standardIcon(pixmap)
