from __future__ import annotations

from PySide6.QtCore import QEasingCurve, QPropertyAnimation, Qt, Signal
from PySide6.QtGui import QPixmap
from PySide6.QtWidgets import QFrame, QHBoxLayout, QLabel, QLineEdit, QProgressBar, QStackedWidget, QToolButton, QVBoxLayout, QWidget

from ...core.brand import APP_DISPLAY_NAME
from ...core.utils import resource_path
from ..icons import get_icon
from .motion import animate_opacity, ensure_opacity_effect
from ..style import spacing
from ..widgets import PrimaryButton, SoftButton


class RunStatusPanel(QFrame):
    clicked = Signal()

    def mousePressEvent(self, event) -> None:  # type: ignore[override]
        self.clicked.emit()
        super().mousePressEvent(event)


class AppToolbar(QFrame):
    details_toggled = Signal(bool)

    def __init__(self) -> None:
        super().__init__()
        self.setObjectName("TopAppBar")
        self._active_anims: list[QPropertyAnimation] = []

        root = QVBoxLayout(self)
        root.setContentsMargins(spacing("md"), spacing("md"), spacing("md"), spacing("sm"))
        root.setSpacing(spacing("xs"))
        layout = QHBoxLayout()
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(spacing("md"))

        self.run_status_panel = RunStatusPanel()
        self.run_status_panel.setObjectName("BrandStatus")
        self.run_status_panel.setMinimumWidth(300)
        status_l = QHBoxLayout(self.run_status_panel)
        status_l.setContentsMargins(spacing("md"), spacing("sm"), spacing("md"), spacing("sm"))
        status_l.setSpacing(spacing("sm"))

        self.brand_mark = QLabel()
        self.brand_mark.setObjectName("BrandMark")
        self.brand_mark.setFixedSize(48, 48)
        self._refresh_brand_mark()

        text_col = QVBoxLayout()
        text_col.setContentsMargins(0, 0, 0, 0)
        text_col.setSpacing(2)
        self.app_identity = QLabel(APP_DISPLAY_NAME)
        self.app_identity.setObjectName("Wordmark")
        self.run_status_eyebrow = QLabel("SYSTEM STATUS")
        self.run_status_eyebrow.setObjectName("TopStatusEyebrow")
        self.run_status_title = QLabel("Ready for local diagnostics")
        self.run_status_title.setObjectName("TopStatusText")
        self.run_status_detail = QLabel("No active run. Search, launch a quick check, or reopen a session.")
        self.run_status_detail.setObjectName("TopStatusSubtle")
        self.run_status_detail.setWordWrap(False)
        self.run_status_meta = QWidget()
        self.run_status_meta.setObjectName("TopStatusMeta")
        meta_l = QHBoxLayout(self.run_status_meta)
        meta_l.setContentsMargins(0, 0, 0, 0)
        meta_l.setSpacing(spacing("xs"))
        text_col.addWidget(self.app_identity)
        text_col.addWidget(self.run_status_eyebrow)
        text_col.addWidget(self.run_status_title)
        text_col.addWidget(self.run_status_detail)
        text_col.addWidget(self.run_status_meta)

        self.run_status_chip = QLabel("Idle")
        self.run_status_chip.setObjectName("RunnerStatusChip")
        self.run_status_chip.setProperty("kind", "info")
        self.run_status_session_chip = QLabel("Session none")
        self.run_status_session_chip.setObjectName("RunnerStatusChip")
        self.run_status_session_chip.setProperty("kind", "muted")
        self.run_status_attention_chip = QLabel("Local-only")
        self.run_status_attention_chip.setObjectName("RunnerStatusChip")
        self.run_status_attention_chip.setProperty("kind", "muted")
        meta_l.addWidget(self.run_status_chip, 0)
        meta_l.addWidget(self.run_status_session_chip, 0)
        meta_l.addWidget(self.run_status_attention_chip, 0)
        meta_l.addStretch(1)

        status_l.addWidget(self.brand_mark, 0, Qt.AlignVCenter)
        status_l.addLayout(text_col, 1)

        self.top_search = QLineEdit()
        self.top_search.setObjectName("SearchInput")
        self.top_search.setPlaceholderText("Search goals, tools, runbooks, fixes, sessions (Ctrl+K)")
        self.compact_search_btn = QToolButton()
        self.compact_search_btn.setObjectName("AppBarIconButton")
        self.compact_search_btn.setText("")
        self.compact_search_btn.setIcon(get_icon("search", self.compact_search_btn))
        self.compact_search_btn.setToolTip("Search")
        self.search_stack = QStackedWidget()
        self.search_stack.setObjectName("HeaderSearchStack")
        self.search_stack.addWidget(self.top_search)
        self.search_stack.addWidget(self.compact_search_btn)
        ensure_opacity_effect(self.top_search)
        self._search_expand_anim = QPropertyAnimation(self.top_search, b"maximumWidth", self)
        self._search_expand_anim.setDuration(180)
        self._search_expand_anim.setEasingCurve(QEasingCurve.OutCubic)

        self.btn_quick_check = PrimaryButton("Quick Check")
        self.btn_quick_check.setToolTip("Run a safe quick diagnostic check (Ctrl+Shift+R)")
        self.btn_quick_check.setIcon(get_icon("quick_check", self.btn_quick_check))

        self.btn_panel_toggle = QToolButton()
        self.btn_panel_toggle.setObjectName("AppBarIconButton")
        self.btn_panel_toggle.setText("")
        self.btn_panel_toggle.setCheckable(True)
        self.btn_panel_toggle.setChecked(False)
        self.btn_panel_toggle.clicked.connect(lambda checked: self.details_toggled.emit(bool(checked)))

        self.btn_overflow = QToolButton()
        self.btn_overflow.setObjectName("AppBarIconButton")
        self.btn_overflow.setText("")
        self.btn_overflow.setIcon(get_icon("overflow", self.btn_overflow))
        self.btn_overflow.setToolTip("More actions")

        # Hidden compatibility hooks for existing orchestration methods.
        self.btn_cancel_task = SoftButton("Cancel Task")
        self.btn_cancel_task.setVisible(False)
        self.btn_open_runner = QToolButton()
        self.btn_open_runner.setVisible(False)
        self.mode_basic_btn = SoftButton("Basic")
        self.mode_pro_btn = SoftButton("Pro")
        self.mode_basic_btn.setVisible(False)
        self.mode_pro_btn.setVisible(False)

        layout.addWidget(self.run_status_panel, 0)
        layout.addWidget(self.search_stack, 1)
        layout.addWidget(self.btn_quick_check, 0)
        layout.addWidget(self.btn_panel_toggle, 0)
        layout.addWidget(self.btn_overflow, 0)
        root.addLayout(layout, 1)

        self.task_progress = QProgressBar()
        self.task_progress.setObjectName("AppBarProgress")
        self.task_progress.setTextVisible(False)
        self.task_progress.setFixedHeight(3)
        self.task_progress.setRange(0, 0)
        self.task_progress.hide()
        root.addWidget(self.task_progress, 0)

        self.set_details_open(False)

    def _refresh_brand_mark(self) -> None:
        pixmap = QPixmap(resource_path("assets/brand/fixfox_mark.png")).scaled(
            44, 44, Qt.KeepAspectRatio, Qt.SmoothTransformation
        )
        if not pixmap.isNull():
            self.brand_mark.setPixmap(pixmap)

    def set_details_open(self, opened: bool) -> None:
        checked = bool(opened)
        self.btn_panel_toggle.blockSignals(True)
        self.btn_panel_toggle.setChecked(checked)
        self.btn_panel_toggle.blockSignals(False)
        icon_name = "panel_closed" if checked else "panel_open"
        tip = "Close details panel" if checked else "Open details panel"
        self.btn_panel_toggle.setIcon(get_icon(icon_name, self.btn_panel_toggle))
        self.btn_panel_toggle.setToolTip(tip)

    def set_search_collapsed(self, collapsed: bool, *, animate: bool = False) -> None:
        if not animate:
            self.top_search.setMaximumWidth(16777215)
            self._search_expand_anim.stop()
            self.search_stack.setCurrentIndex(1 if collapsed else 0)
            effect = ensure_opacity_effect(self.top_search)
            effect.setOpacity(1.0)
            return
        if collapsed:
            self._search_expand_anim.stop()
            fade = animate_opacity(self.top_search, start=1.0, end=0.0, duration_ms=140)
            self._active_anims.append(fade)
            fade.finished.connect(lambda: self.search_stack.setCurrentIndex(1))
            return
        self.search_stack.setCurrentIndex(0)
        self.top_search.setMaximumWidth(1)
        ensure_opacity_effect(self.top_search).setOpacity(0.0)
        self._search_expand_anim.stop()
        target_width = max(280, self.search_stack.width() or 320)
        self._search_expand_anim.setStartValue(1)
        self._search_expand_anim.setEndValue(target_width)
        self._search_expand_anim.start()
        fade = animate_opacity(self.top_search, start=0.0, end=1.0, duration_ms=170)
        self._active_anims.append(fade)

    def expand_search(self, *, animate: bool = True) -> None:
        self.set_search_collapsed(False, animate=animate)

    def collapse_search(self, *, animate: bool = False) -> None:
        self.set_search_collapsed(True, animate=animate)

    def refresh_icons(self) -> None:
        self._refresh_brand_mark()
        self.compact_search_btn.setIcon(get_icon("search", self.compact_search_btn))
        self.btn_quick_check.setIcon(get_icon("quick_check", self.btn_quick_check))
        self.btn_overflow.setIcon(get_icon("overflow", self.btn_overflow))
        self.set_details_open(self.btn_panel_toggle.isChecked())

    def set_task_running(self, running: bool, *, progress: int | None = None) -> None:
        if not running:
            self.task_progress.hide()
            self.task_progress.setRange(0, 0)
            return
        self.task_progress.show()
        if progress is None or progress < 0:
            self.task_progress.setRange(0, 0)
        else:
            self.task_progress.setRange(0, 100)
            self.task_progress.setValue(max(0, min(100, int(progress))))
