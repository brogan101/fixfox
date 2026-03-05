from __future__ import annotations

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import QFrame, QHBoxLayout, QLabel, QScrollArea, QTextEdit, QToolButton, QVBoxLayout, QWidget

from .nav import NavRail
from .toolbar import AppToolbar as TopAppBar
from ..icons import get_icon
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


class SideSheet(QFrame):
    collapsed_changed = Signal(bool)
    pin_changed = Signal(bool)

    def __init__(self) -> None:
        super().__init__()
        self.setObjectName("SideSheet")
        self._pinned = False
        self._collapsed = False
        self._preferred_width = 340
        self.setMinimumWidth(300)
        self.setMaximumWidth(520)

        shell = QVBoxLayout(self)
        shell.setContentsMargins(spacing("sm"), spacing("sm"), spacing("sm"), spacing("sm"))
        shell.setSpacing(spacing("sm"))

        header = QWidget()
        header_layout = QHBoxLayout(header)
        header_layout.setContentsMargins(0, 0, 0, 0)
        header_layout.setSpacing(spacing("xs"))
        self.title = QLabel("Details")
        self.title.setObjectName("SideSheetTitle")
        self.pin_btn = QToolButton()
        self.pin_btn.setObjectName("AppBarIconButton")
        self.pin_btn.setCheckable(True)
        self.pin_btn.setText("")
        self.pin_btn.setIcon(get_icon("pin", self.pin_btn))
        self.pin_btn.setToolTip("Pin details panel")
        self.close_btn = QToolButton()
        self.close_btn.setObjectName("AppBarIconButton")
        self.close_btn.setText("")
        self.close_btn.setIcon(get_icon("close", self.close_btn))
        self.close_btn.setToolTip("Close details panel (Esc)")
        header_layout.addWidget(self.title, 1)
        header_layout.addWidget(self.pin_btn, 0)
        header_layout.addWidget(self.close_btn, 0)

        self.scroll = QScrollArea()
        self.scroll.setObjectName("SideSheetScroll")
        self.scroll.setWidgetResizable(True)
        self.scroll.setFrameShape(QFrame.NoFrame)
        self.content_host = QWidget()
        self.content_layout = QVBoxLayout(self.content_host)
        self.content_layout.setContentsMargins(0, 0, 0, 0)
        self.content_layout.setSpacing(spacing("sm"))
        self.scroll.setWidget(self.content_host)

        self.log_panel = QTextEdit()
        self.log_panel.setObjectName("SideSheetLog")
        self.log_panel.setReadOnly(True)
        self.log_panel.setMinimumHeight(90)
        self.log_panel.setPlaceholderText("Context details and diagnostics output appear here.")

        shell.addWidget(header, 0)
        shell.addWidget(self.scroll, 1)
        shell.addWidget(self.log_panel, 0)

        self.pin_btn.toggled.connect(self._on_pin_toggled)
        self.close_btn.clicked.connect(lambda: self.set_collapsed(True))
        self.set_collapsed(True)

    @property
    def pinned(self) -> bool:
        return self._pinned

    @property
    def collapsed(self) -> bool:
        return self._collapsed

    @property
    def preferred_width(self) -> int:
        return self._preferred_width

    def set_preferred_width(self, width: int) -> None:
        target = max(self.minimumWidth(), min(self.maximumWidth(), int(width)))
        self._preferred_width = target
        self.setFixedWidth(target)

    def clear_widgets(self) -> None:
        while self.content_layout.count():
            item = self.content_layout.takeAt(0)
            widget = item.widget()
            if widget is not None:
                widget.deleteLater()

    def add_widget(self, widget: QWidget) -> None:
        self.content_layout.addWidget(widget)

    def append_log(self, line: str) -> None:
        text = str(line or "").strip()
        if text:
            self.log_panel.append(text)

    def set_collapsed(self, collapsed: bool) -> None:
        next_state = bool(collapsed)
        if self._collapsed == next_state:
            return
        self._collapsed = next_state
        self.setVisible(not next_state)
        self.collapsed_changed.emit(next_state)

    def set_pinned(self, pinned: bool) -> None:
        self.pin_btn.setChecked(bool(pinned))

    def _on_pin_toggled(self, checked: bool) -> None:
        self._pinned = bool(checked)
        self.pin_changed.emit(self._pinned)

    def refresh_icons(self) -> None:
        self.pin_btn.setIcon(get_icon("pin", self.pin_btn))
        self.close_btn.setIcon(get_icon("close", self.close_btn))


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
