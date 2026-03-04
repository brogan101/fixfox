from __future__ import annotations

from typing import Any

from PySide6.QtCore import QEasingCurve, QPoint, QPropertyAnimation, Qt, Signal
from PySide6.QtWidgets import (
    QFrame,
    QHBoxLayout,
    QLabel,
    QListWidget,
    QToolButton,
    QVBoxLayout,
    QWidget,
)

from ..theme import resolve_density_tokens
from .rows import Badge


class AccordionSection(QFrame):
    finding_context_requested = Signal(QListWidget, QPoint)
    finding_selected = Signal(dict)

    def __init__(self, title: str, count: int, severity_chips: dict[str, int], density: str = "comfortable") -> None:
        super().__init__()
        self.setObjectName("AccordionSection")
        self._collapsed = False
        d = resolve_density_tokens(density)

        outer = QVBoxLayout(self)
        outer.setContentsMargins(8, 8, 8, 8)
        outer.setSpacing(8)

        header_frame = QFrame()
        header_frame.setObjectName("AccordionHeader")
        header = QHBoxLayout(header_frame)
        header.setContentsMargins(10, 8, 10, 8)
        header.setSpacing(8)

        self.toggle = QToolButton()
        self.toggle.setCheckable(True)
        self.toggle.setChecked(True)
        self.toggle.setArrowType(Qt.DownArrow)
        self.toggle.setToolButtonStyle(Qt.ToolButtonIconOnly)
        self.toggle.clicked.connect(self.toggle_collapsed)
        header.addWidget(self.toggle)

        self.title = QLabel(title)
        self.title.setObjectName("SectionTitle")
        header.addWidget(self.title, 1)
        self.count_label = Badge(str(count), "INFO")
        header.addWidget(self.count_label)

        for key in ("CRIT", "WARN", "OK", "INFO"):
            qty = severity_chips.get(key, 0)
            if qty > 0:
                header.addWidget(Badge(f"{key} {qty}", key))

        outer.addWidget(header_frame)

        self.body = QWidget()
        body_layout = QVBoxLayout(self.body)
        body_layout.setContentsMargins(0, 0, 0, 0)
        body_layout.setSpacing(6)

        self.list_widget = QListWidget()
        self.list_widget.setSelectionMode(QListWidget.SingleSelection)
        self.list_widget.setContextMenuPolicy(Qt.CustomContextMenu)
        self.list_widget.customContextMenuRequested.connect(self._emit_context)
        self.list_widget.currentItemChanged.connect(self._emit_selection)
        body_layout.addWidget(self.list_widget, 1)

        outer.addWidget(self.body)

        self.anim = QPropertyAnimation(self.body, b"maximumHeight", self)
        self.anim.setDuration(170)
        self.anim.setEasingCurve(QEasingCurve.InOutCubic)
        self.body.setMaximumHeight(320)
        self.body.setMinimumHeight(max(180, d.list_row_height * 2))

    def _emit_context(self, pos: QPoint) -> None:
        self.finding_context_requested.emit(self.list_widget, pos)

    def _emit_selection(self) -> None:
        item = self.list_widget.currentItem()
        payload = item.data(Qt.UserRole) if item is not None else {}
        if isinstance(payload, dict):
            self.finding_selected.emit(payload)

    def set_counts(self, count: int) -> None:
        self.count_label.setText(str(count))

    def toggle_collapsed(self) -> None:
        self.set_collapsed(not self._collapsed)

    def set_collapsed(self, collapsed: bool) -> None:
        self._collapsed = collapsed
        self.toggle.setChecked(not collapsed)
        self.toggle.setArrowType(Qt.RightArrow if collapsed else Qt.DownArrow)
        start = self.body.maximumHeight()
        end = 0 if collapsed else max(self._content_height(), self.body.minimumHeight())
        self.anim.stop()
        self.anim.setStartValue(start)
        self.anim.setEndValue(end)
        self.anim.start()

    def _content_height(self) -> int:
        count = self.list_widget.count()
        if count <= 0:
            return 80
        row_h = self.list_widget.sizeHintForRow(0)
        if row_h <= 0:
            row_h = 58
        return min(max(140, row_h * count + 8), 440)
