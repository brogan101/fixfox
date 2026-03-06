from __future__ import annotations

from PySide6.QtCore import QEasingCurve, QPropertyAnimation, Signal
from PySide6.QtWidgets import QFrame, QHBoxLayout, QLabel, QScrollArea, QTextEdit, QToolButton, QVBoxLayout, QWidget

from ..icons import get_icon
from .motion import animate_opacity
from ..style import spacing


class SideSheet(QFrame):
    collapsed_changed = Signal(bool)
    pin_changed = Signal(bool)

    def __init__(self) -> None:
        super().__init__()
        self.setObjectName("SideSheet")
        self._pinned = False
        self._collapsed = False
        self._preferred_width = 340
        self.setMinimumWidth(0)
        self.setMaximumWidth(520)
        self._collapse_anim = QPropertyAnimation(self, b"maximumWidth", self)
        self._collapse_anim.setDuration(210)
        self._collapse_anim.setEasingCurve(QEasingCurve.OutCubic)
        self._collapse_anim.finished.connect(self._on_anim_finished)
        self._collapse_target_hidden = False

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
        target = max(300, min(self.maximumWidth(), int(width)))
        self._preferred_width = target
        if not self._collapsed:
            self.setMinimumWidth(target)
            self.setMaximumWidth(target)

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
        self._collapse_anim.stop()
        self._collapse_target_hidden = next_state
        if next_state:
            self.setMinimumWidth(0)
            animate_opacity(self, start=1.0, end=0.0, duration_ms=180)
            self._collapse_anim.setStartValue(max(1, self.width()))
            self._collapse_anim.setEndValue(1)
            self._collapse_anim.start()
        else:
            self.setVisible(True)
            self.setMaximumWidth(1)
            self.setMinimumWidth(0)
            animate_opacity(self, start=0.0, end=1.0, duration_ms=190)
            self._collapse_anim.setStartValue(1)
            self._collapse_anim.setEndValue(self._preferred_width)
            self._collapse_anim.start()
        self.collapsed_changed.emit(next_state)

    def _on_anim_finished(self) -> None:
        if self._collapse_target_hidden:
            self.setVisible(False)
            self.setMinimumWidth(0)
            self.setMaximumWidth(self._preferred_width)
            return
        self.setVisible(True)
        self.setMinimumWidth(self._preferred_width)
        self.setMaximumWidth(self._preferred_width)

    def set_pinned(self, pinned: bool) -> None:
        self.pin_btn.setChecked(bool(pinned))

    def _on_pin_toggled(self, checked: bool) -> None:
        self._pinned = bool(checked)
        self.pin_changed.emit(self._pinned)

    def refresh_icons(self) -> None:
        self.pin_btn.setIcon(get_icon("pin", self.pin_btn))
        self.close_btn.setIcon(get_icon("close", self.close_btn))
