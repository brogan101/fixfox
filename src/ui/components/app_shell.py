from __future__ import annotations

from PySide6.QtCore import Qt
from PySide6.QtWidgets import QFrame, QHBoxLayout, QLabel, QSplitter, QVBoxLayout, QWidget

from .nav import NavShell
from .toolbar import AppToolbar


class BottomStatusBar(QFrame):
    def __init__(self) -> None:
        super().__init__()
        self.setObjectName("BottomStatusBar")
        layout = QHBoxLayout(self)
        layout.setContentsMargins(8, 4, 8, 4)
        layout.setSpacing(10)
        self.session_label = QLabel("Session: none")
        self.mode_label = QLabel("Mode: Basic")
        self.safety_label = QLabel("Safety: Safe")
        for label in (self.session_label, self.mode_label, self.safety_label):
            label.setObjectName("BottomStatusText")
            layout.addWidget(label, 0)
        layout.addStretch(1)


class AppShellFrame(QFrame):
    def __init__(self) -> None:
        super().__init__()
        self.setObjectName("Shell")
        root = QVBoxLayout(self)
        root.setContentsMargins(12, 12, 12, 12)
        root.setSpacing(10)

        self.toolbar = AppToolbar()
        root.addWidget(self.toolbar)

        self.splitter = QSplitter(Qt.Horizontal)
        self.splitter.setChildrenCollapsible(False)
        root.addWidget(self.splitter, 1)

        self.nav_shell = NavShell()
        self.splitter.addWidget(self.nav_shell)

        self.center = QWidget()
        self.center_layout = QVBoxLayout(self.center)
        self.center_layout.setContentsMargins(0, 0, 0, 0)
        self.center_layout.setSpacing(10)
        self.splitter.addWidget(self.center)

        self.details_drawer = QWidget()
        self.details_drawer.setObjectName("DetailsDrawer")
        details_layout = QVBoxLayout(self.details_drawer)
        details_layout.setContentsMargins(0, 0, 0, 0)
        details_layout.setSpacing(8)
        self.details_layout = details_layout
        self.splitter.addWidget(self.details_drawer)

        self.bottom_status = BottomStatusBar()
        root.addWidget(self.bottom_status, 0)

