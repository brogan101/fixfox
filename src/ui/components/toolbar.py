from __future__ import annotations

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import QFrame, QHBoxLayout, QLabel, QLineEdit, QStackedWidget, QVBoxLayout, QWidget

from ...core.brand import APP_DISPLAY_NAME
from ..components.rows import IconButton
from ..widgets import SoftButton


class RunStatusPanel(QFrame):
    clicked = Signal()

    def mousePressEvent(self, event) -> None:  # type: ignore[override]
        self.clicked.emit()
        super().mousePressEvent(event)


class AppToolbar(QFrame):
    def __init__(self) -> None:
        super().__init__()
        self.setObjectName("TopBar")
        layout = QHBoxLayout(self)
        layout.setContentsMargins(10, 8, 10, 8)
        layout.setSpacing(8)

        self.run_status_panel = RunStatusPanel()
        self.run_status_panel.setObjectName("RunStatusCard")
        status_l = QHBoxLayout(self.run_status_panel)
        status_l.setContentsMargins(10, 6, 10, 6)
        status_l.setSpacing(8)
        status_text = QVBoxLayout()
        status_text.setContentsMargins(0, 0, 0, 0)
        status_text.setSpacing(1)
        self.app_identity = QLabel(APP_DISPLAY_NAME)
        self.app_identity.setObjectName("RunStatusTitle")
        self.run_status_title = QLabel("Ready")
        self.run_status_title.setObjectName("RunStatusDetail")
        self.run_status_detail = QLabel("No active run.")
        self.run_status_detail.setObjectName("RunStatusDetail")
        self.run_status_detail.setWordWrap(False)
        self.app_identity.setMinimumHeight(24)
        self.run_status_title.setMinimumHeight(22)
        self.run_status_detail.setMinimumHeight(22)
        status_text.addWidget(self.app_identity)
        status_text.addWidget(self.run_status_title)
        status_text.addWidget(self.run_status_detail)
        status_l.addLayout(status_text, 1)
        self.run_status_chip = QLabel("Idle")
        self.run_status_chip.setObjectName("RunnerStatusChip")
        self.run_status_chip.setProperty("kind", "info")
        status_l.addWidget(self.run_status_chip, 0, Qt.AlignVCenter)

        self.top_search = QLineEdit()
        self.top_search.setObjectName("SearchInput")
        self.top_search.setPlaceholderText("Search goals, tools, runbooks, fixes, and sessions")
        self.compact_search_btn = IconButton("search", self, "Search")
        self.search_stack = QStackedWidget()
        self.search_stack.setObjectName("HeaderSearchStack")
        self.search_stack.addWidget(self.top_search)
        self.search_stack.addWidget(self.compact_search_btn)

        self.btn_cancel_task = SoftButton("Cancel Task")
        self.btn_cancel_task.setEnabled(False)
        self.btn_open_runner = IconButton("run", self, "Open ToolRunner")
        self.btn_export = IconButton("export", self, "Open Reports")
        self.btn_panel_toggle = IconButton("panel_open", self, "Toggle details drawer")
        self.btn_overflow = IconButton("menu", self, "More actions")

        self.mode_shell = QWidget()
        mode_l = QHBoxLayout(self.mode_shell)
        mode_l.setContentsMargins(0, 0, 0, 0)
        mode_l.setSpacing(6)
        self.mode_basic_btn = SoftButton("Basic")
        self.mode_pro_btn = SoftButton("Pro")
        for btn in (self.mode_basic_btn, self.mode_pro_btn):
            btn.setCheckable(True)
            btn.setAutoExclusive(True)
        mode_l.addWidget(self.mode_basic_btn)
        mode_l.addWidget(self.mode_pro_btn)

        layout.addWidget(self.run_status_panel, 0)
        layout.addWidget(self.search_stack, 1)
        layout.addWidget(self.mode_shell, 0)
        layout.addWidget(self.btn_cancel_task, 0)
        layout.addWidget(self.btn_open_runner, 0)
        layout.addWidget(self.btn_export, 0)
        layout.addWidget(self.btn_panel_toggle, 0)
        layout.addWidget(self.btn_overflow, 0)

    def set_search_collapsed(self, collapsed: bool) -> None:
        self.search_stack.setCurrentIndex(1 if collapsed else 0)
