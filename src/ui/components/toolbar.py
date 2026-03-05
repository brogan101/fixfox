from __future__ import annotations

from PySide6.QtCore import Qt, Signal
from PySide6.QtGui import QPixmap
from PySide6.QtWidgets import QFrame, QHBoxLayout, QLabel, QLineEdit, QStackedWidget, QToolButton, QVBoxLayout, QWidget

from ...core.brand import APP_DISPLAY_NAME
from ...core.utils import resource_path
from ..style import spacing
from ..widgets import PrimaryButton, SoftButton


class RunStatusPanel(QFrame):
    clicked = Signal()

    def mousePressEvent(self, event) -> None:  # type: ignore[override]
        self.clicked.emit()
        super().mousePressEvent(event)


class AppToolbar(QFrame):
    def __init__(self) -> None:
        super().__init__()
        self.setObjectName("TopAppBar")
        layout = QHBoxLayout(self)
        layout.setContentsMargins(spacing("md"), spacing("md"), spacing("md"), spacing("md"))
        layout.setSpacing(spacing("md"))

        self.run_status_panel = RunStatusPanel()
        self.run_status_panel.setObjectName("BrandStatus")
        status_l = QHBoxLayout(self.run_status_panel)
        status_l.setContentsMargins(spacing("md"), spacing("xs"), spacing("md"), spacing("xs"))
        status_l.setSpacing(spacing("sm"))

        self.brand_mark = QLabel()
        self.brand_mark.setObjectName("BrandMark")
        mark = QPixmap(resource_path("assets/branding/fixfox_mark.svg"))
        if not mark.isNull():
            self.brand_mark.setPixmap(mark.scaled(26, 26, Qt.KeepAspectRatio, Qt.SmoothTransformation))
        self.brand_mark.setFixedSize(28, 28)

        text_col = QVBoxLayout()
        text_col.setContentsMargins(0, 0, 0, 0)
        text_col.setSpacing(0)
        self.app_identity = QLabel(APP_DISPLAY_NAME)
        self.app_identity.setObjectName("Wordmark")
        self.run_status_title = QLabel("Ready")
        self.run_status_title.setObjectName("TopStatusText")
        self.run_status_detail = QLabel("No active run.")
        self.run_status_detail.setObjectName("TopStatusSubtle")
        self.run_status_detail.setWordWrap(False)
        text_col.addWidget(self.app_identity)
        text_col.addWidget(self.run_status_title)
        text_col.addWidget(self.run_status_detail)

        self.run_status_chip = QLabel("Idle")
        self.run_status_chip.setObjectName("RunnerStatusChip")
        self.run_status_chip.setProperty("kind", "info")

        status_l.addWidget(self.brand_mark, 0, Qt.AlignVCenter)
        status_l.addLayout(text_col, 1)
        status_l.addWidget(self.run_status_chip, 0, Qt.AlignVCenter)

        self.top_search = QLineEdit()
        self.top_search.setObjectName("SearchInput")
        self.top_search.setPlaceholderText("Search tools, fixes, sessions")
        self.compact_search_btn = QToolButton()
        self.compact_search_btn.setObjectName("AppBarIconButton")
        self.compact_search_btn.setText("S")
        self.compact_search_btn.setToolTip("Search")
        self.search_stack = QStackedWidget()
        self.search_stack.setObjectName("HeaderSearchStack")
        self.search_stack.addWidget(self.top_search)
        self.search_stack.addWidget(self.compact_search_btn)

        self.btn_quick_check = PrimaryButton("Run Quick Check")
        self.btn_quick_check.setToolTip("Run a safe quick diagnostic check (Ctrl+Shift+R)")
        self.btn_export = QToolButton()
        self.btn_export.setObjectName("AppBarIconButton")
        self.btn_export.setText("EX")
        self.btn_export.setToolTip("Open Reports")
        self.btn_overflow = QToolButton()
        self.btn_overflow.setObjectName("AppBarIconButton")
        self.btn_overflow.setText("...")
        self.btn_overflow.setToolTip("More actions")

        # Kept as hidden compatibility hooks for existing orchestration methods.
        self.btn_cancel_task = SoftButton("Cancel Task")
        self.btn_cancel_task.setVisible(False)
        self.btn_open_runner = QToolButton()
        self.btn_open_runner.setVisible(False)
        self.btn_panel_toggle = QToolButton()
        self.btn_panel_toggle.setVisible(False)
        self.mode_basic_btn = SoftButton("Basic")
        self.mode_pro_btn = SoftButton("Pro")
        self.mode_basic_btn.setVisible(False)
        self.mode_pro_btn.setVisible(False)

        layout.addWidget(self.run_status_panel, 0)
        layout.addWidget(self.search_stack, 1)
        layout.addWidget(self.btn_quick_check, 0)
        layout.addWidget(self.btn_export, 0)
        layout.addWidget(self.btn_overflow, 0)

    def set_search_collapsed(self, collapsed: bool) -> None:
        self.search_stack.setCurrentIndex(1 if collapsed else 0)
