from __future__ import annotations

from PySide6.QtWidgets import QFrame, QHBoxLayout, QLabel, QVBoxLayout, QWidget

from .app_bar import AppToolbar as TopAppBar
from .nav import NavRail
from .side_sheet import SideSheet
from ..style import spacing


class StatusBar(QFrame):
    def __init__(self) -> None:
        super().__init__()
        self.setObjectName("BottomStatusBar")
        layout = QHBoxLayout(self)
        layout.setContentsMargins(spacing("sm"), spacing("xs"), spacing("sm"), spacing("xs"))
        layout.setSpacing(spacing("md"))
        self.session_label = QLabel("Session: none")
        self.mode_label = QLabel("Mode: Basic")
        self.safety_label = QLabel("Safe-only: on")
        for label in (self.session_label, self.mode_label, self.safety_label):
            label.setObjectName("BottomStatusText")
            layout.addWidget(label, 0)
        layout.addStretch(1)


class PageHost(QWidget):
    def __init__(self) -> None:
        super().__init__()
        self.setObjectName("PageHost")
        self.layout_main = QVBoxLayout(self)
        self.layout_main.setContentsMargins(0, 0, 0, 0)
        self.layout_main.setSpacing(spacing("md"))


class AppShellFrame(QFrame):
    def __init__(self) -> None:
        super().__init__()
        self.setObjectName("Shell")
        root = QVBoxLayout(self)
        root.setContentsMargins(spacing("md"), spacing("md"), spacing("md"), spacing("md"))
        root.setSpacing(spacing("md"))

        self.toolbar = TopAppBar()
        root.addWidget(self.toolbar, 0)

        self.content_row = QWidget()
        self.content_row.setObjectName("ShellContentRow")
        row_layout = QHBoxLayout(self.content_row)
        row_layout.setContentsMargins(0, 0, 0, 0)
        row_layout.setSpacing(spacing("md"))
        root.addWidget(self.content_row, 1)

        self.nav_rail = NavRail()
        row_layout.addWidget(self.nav_rail, 0)

        self.page_host = PageHost()
        row_layout.addWidget(self.page_host, 1)

        self.side_sheet = SideSheet()
        self.side_sheet.set_preferred_width(340)
        row_layout.addWidget(self.side_sheet, 0)

        self.bottom_status = StatusBar()
        root.addWidget(self.bottom_status, 0)

        self.center = self.page_host
        self.center_layout = self.page_host.layout_main
        self.details_drawer = self.side_sheet
        self.details_layout = self.side_sheet.content_layout
        self.nav_shell = self.nav_rail

    def details_open(self) -> bool:
        return not self.side_sheet.collapsed

    def set_details_open(self, opened: bool) -> None:
        self.side_sheet.set_collapsed(not bool(opened))
        self.toolbar.set_details_open(bool(opened))

    def set_details_width(self, width: int) -> None:
        self.side_sheet.set_preferred_width(width)

    def details_width(self) -> int:
        return self.side_sheet.preferred_width
