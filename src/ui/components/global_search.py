from __future__ import annotations

from typing import Any

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import QFrame, QLabel, QListWidget, QListWidgetItem, QVBoxLayout, QWidget


class GlobalSearchPopup(QFrame):
    result_activated = Signal(object)

    def __init__(self, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self.setObjectName("GlobalSearchPopup")
        self.setWindowFlags(Qt.Popup | Qt.FramelessWindowHint)
        self.setMinimumWidth(460)
        self.setMaximumHeight(440)

        root = QVBoxLayout(self)
        root.setContentsMargins(8, 8, 8, 8)
        root.setSpacing(6)

        self.empty = QLabel("Type to search.")
        self.empty.setObjectName("GlobalSearchEmpty")
        self.empty.setWordWrap(True)
        root.addWidget(self.empty)

        self.results = QListWidget()
        self.results.setObjectName("GlobalSearchList")
        self.results.setSelectionMode(QListWidget.SingleSelection)
        self.results.itemDoubleClicked.connect(self._activate_item)
        root.addWidget(self.results, 1)

    def show_results(self, anchor: QWidget, groups: list[tuple[str, list[dict[str, str]]]], query: str) -> None:
        self.results.clear()
        total = 0
        for group_name, rows in groups:
            if not rows:
                continue
            header = QListWidgetItem(group_name.upper())
            header.setFlags(Qt.NoItemFlags)
            header.setData(Qt.UserRole, None)
            self.results.addItem(header)
            for row in rows:
                title = str(row.get("title", "")).strip()
                subtitle = str(row.get("subtitle", "")).strip()
                label = title if not subtitle else f"{title}\n{subtitle}"
                item = QListWidgetItem(label)
                item.setData(Qt.UserRole, row)
                item.setToolTip(subtitle)
                self.results.addItem(item)
                total += 1
        if total == 0:
            q = query.strip()
            if q:
                self.empty.setText("No results. Try a goal name, fix keyword, runbook title, or session id.")
            else:
                self.empty.setText("Type to search Fix Fox actions.")
            self.empty.show()
            self.results.hide()
        else:
            self.empty.hide()
            self.results.show()
            self._select_first_result()
        global_pos = anchor.mapToGlobal(anchor.rect().bottomLeft())
        self.setFixedWidth(max(anchor.width(), 460))
        self.move(global_pos)
        self.show()
        self.raise_()

    def hide_popup(self) -> None:
        self.hide()

    def has_visible_results(self) -> bool:
        return self.isVisible() and self.results.isVisible() and self.results.count() > 0

    def move_selection(self, delta: int) -> None:
        if not self.has_visible_results():
            return
        count = self.results.count()
        if count <= 0:
            return
        current = self.results.currentRow()
        if current < 0:
            self._select_first_result()
            return
        idx = current
        for _ in range(count):
            idx = (idx + delta + count) % count
            item = self.results.item(idx)
            if item is not None and item.flags() != Qt.NoItemFlags and item.data(Qt.UserRole) is not None:
                self.results.setCurrentItem(item)
                self.results.scrollToItem(item)
                return

    def activate_current(self) -> bool:
        if not self.has_visible_results():
            return False
        item = self.results.currentItem()
        if item is None:
            return False
        payload = item.data(Qt.UserRole)
        if not isinstance(payload, dict):
            return False
        self.result_activated.emit(payload)
        self.hide_popup()
        return True

    def _activate_item(self, item: QListWidgetItem) -> None:
        payload = item.data(Qt.UserRole)
        if isinstance(payload, dict):
            self.result_activated.emit(payload)
            self.hide_popup()

    def _select_first_result(self) -> None:
        for index in range(self.results.count()):
            item = self.results.item(index)
            if item is not None and item.flags() != Qt.NoItemFlags and item.data(Qt.UserRole) is not None:
                self.results.setCurrentItem(item)
                return
