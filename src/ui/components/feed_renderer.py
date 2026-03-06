from __future__ import annotations

from collections.abc import Callable
from dataclasses import dataclass
from typing import Any

from PySide6.QtCore import QSize, Qt, Signal
from PySide6.QtWidgets import (
    QFrame,
    QLabel,
    QListWidget,
    QListWidgetItem,
    QStackedLayout,
    QVBoxLayout,
    QWidget,
)

from ..theme import resolve_density_tokens
from ..icons import get_icon
from .rows import BaseRow, row_height_for_density


@dataclass(frozen=True)
class FeedItemAdapter:
    key: str
    title: str
    subtitle: str
    payload: Any
    category: str = ""
    status: str = ""
    timestamp: str = ""
    export_status: str = ""

    def match_blob(self) -> str:
        return f"{self.title} {self.subtitle} {self.category} {self.status} {self.key}".lower()


class EmptyState(QWidget):
    def __init__(self, icon: str, message: str, cta: QWidget | None = None) -> None:
        super().__init__()
        layout = QVBoxLayout(self)
        layout.setContentsMargins(16, 16, 16, 16)
        layout.setSpacing(8)
        frame = QFrame()
        frame.setObjectName("EmptyState")
        inner = QVBoxLayout(frame)
        inner.setContentsMargins(18, 16, 18, 16)
        inner.setSpacing(6)
        ico = QLabel(icon)
        ico.setObjectName("CardTitle")
        pix = get_icon(icon, self, size=20).pixmap(20, 20)
        if not pix.isNull():
            ico.setPixmap(pix)
            ico.setAlignment(Qt.AlignHCenter)
        txt = QLabel(message)
        txt.setObjectName("CardSubtitle")
        txt.setWordWrap(True)
        inner.addWidget(ico, 0, Qt.AlignHCenter)
        inner.addWidget(txt, 0, Qt.AlignHCenter)
        if cta is not None:
            inner.addWidget(cta, 0, Qt.AlignHCenter)
        layout.addWidget(frame, 0, Qt.AlignTop)

    def update_content(self, icon: str, message: str) -> None:
        labels = self.findChildren(QLabel)
        if labels:
            pix = get_icon(icon, self, size=20).pixmap(20, 20)
            if not pix.isNull():
                labels[0].setPixmap(pix)
                labels[0].setText("")
            else:
                labels[0].setPixmap(pix)
                labels[0].setText(icon)
        if len(labels) > 1:
            labels[1].setText(message)


class SkeletonLoader(QWidget):
    def __init__(self, rows: int = 5, density: str = "comfortable") -> None:
        super().__init__()
        d = resolve_density_tokens(density)
        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(8)
        for _ in range(rows):
            bar = QFrame()
            bar.setObjectName("Skeleton")
            bar.setMinimumHeight(max(30, d.list_row_height - 18))
            layout.addWidget(bar)
        layout.addStretch(1)


class FeedRenderer(QWidget):
    context_requested = Signal(QListWidget, object)
    item_activated = Signal(object)
    item_selected = Signal(object)

    def __init__(
        self,
        row_factory: Callable[[FeedItemAdapter, str], QWidget],
        density: str = "comfortable",
        empty_icon: str = "i",
        empty_message: str = "No rows found.",
    ) -> None:
        super().__init__()
        self._row_factory = row_factory
        self._density = density
        self._raw_items: list[FeedItemAdapter] = []
        self._sort_key: Callable[[FeedItemAdapter], Any] = lambda item: item.title.lower()
        self._reverse = False
        self._filter = ""

        layout = QStackedLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        self.stack = layout

        self.list_widget = QListWidget()
        self.list_widget.setSelectionMode(QListWidget.SingleSelection)
        self.list_widget.setContextMenuPolicy(Qt.CustomContextMenu)
        self.list_widget.customContextMenuRequested.connect(self._on_list_context)
        self.list_widget.itemDoubleClicked.connect(self._on_item_double_clicked)
        self.list_widget.currentItemChanged.connect(self._sync_row_states)
        layout.addWidget(self.list_widget)

        self.empty_state = EmptyState(empty_icon, empty_message)
        layout.addWidget(self.empty_state)

        self.skeleton = SkeletonLoader(rows=5, density=density)
        layout.addWidget(self.skeleton)

        self.show_empty()

    def set_density(self, density: str) -> None:
        self._density = density
        self.stack.removeWidget(self.skeleton)
        self.skeleton.deleteLater()
        self.skeleton = SkeletonLoader(rows=5, density=density)
        self.stack.addWidget(self.skeleton)

    def show_loading(self, rows: int = 5) -> None:
        self.stack.removeWidget(self.skeleton)
        self.skeleton.deleteLater()
        self.skeleton = SkeletonLoader(rows=rows, density=self._density)
        self.stack.addWidget(self.skeleton)
        self.stack.setCurrentWidget(self.skeleton)

    def show_empty(self, icon: str = "i", message: str = "No rows found.") -> None:
        self.empty_state.update_content(icon, message)
        self.stack.setCurrentWidget(self.empty_state)

    def set_items(
        self,
        items: list[FeedItemAdapter],
        *,
        sort_key: Callable[[FeedItemAdapter], Any] | None = None,
        reverse: bool = False,
        filter_text: str = "",
    ) -> None:
        self._raw_items = list(items)
        self._sort_key = sort_key or self._sort_key
        self._reverse = reverse
        self._filter = filter_text.strip().lower()
        self._render()

    def _filtered_items(self) -> list[FeedItemAdapter]:
        rows = self._raw_items
        if self._filter:
            rows = [row for row in rows if self._filter in row.match_blob()]
        rows.sort(key=self._sort_key, reverse=self._reverse)
        return rows

    def _render(self) -> None:
        self.list_widget.clear()
        rows = self._filtered_items()
        if not rows:
            self.show_empty()
            return

        height = row_height_for_density(self._density)
        for row in rows:
            item = QListWidgetItem()
            item.setData(Qt.UserRole, row.payload)
            item.setData(Qt.UserRole + 1, row.key)
            item.setSizeHint(QSize(0, height))
            self.list_widget.addItem(item)
            widget = self._row_factory(row, self._density)
            if isinstance(widget, BaseRow):
                widget.bind_to_list(self.list_widget, item)
                widget.activated.connect(self.item_activated.emit)
                widget.context_requested.connect(self._on_row_context)
            self.list_widget.setItemWidget(item, widget)

        self.stack.setCurrentWidget(self.list_widget)
        if self.list_widget.count():
            self.list_widget.setCurrentRow(0)

    def _on_row_context(self, payload: object, global_pos: object) -> None:
        del payload
        pos = global_pos
        if hasattr(global_pos, "x") and hasattr(global_pos, "y"):
            pos = self.list_widget.mapFromGlobal(global_pos)
        self.context_requested.emit(self.list_widget, pos)

    def _on_list_context(self, pos: object) -> None:
        self.context_requested.emit(self.list_widget, pos)

    def _on_item_double_clicked(self, item: QListWidgetItem) -> None:
        # BaseRow handles activation itself on double-click; avoid duplicate emits.
        row = self.list_widget.itemWidget(item)
        if isinstance(row, BaseRow):
            return
        self.item_activated.emit(item.data(Qt.UserRole))

    def _sync_row_states(self, *_args: object) -> None:
        current = self.list_widget.currentItem()
        payload = current.data(Qt.UserRole) if current is not None else None
        self.item_selected.emit(payload)
        for idx in range(self.list_widget.count()):
            it = self.list_widget.item(idx)
            row = self.list_widget.itemWidget(it)
            if isinstance(row, BaseRow):
                row.set_selected(it is current)
