from __future__ import annotations

from typing import Any

from PySide6.QtCore import QPoint, QSize, Qt, Signal
from PySide6.QtWidgets import QFrame, QLabel, QListWidget, QListWidgetItem, QVBoxLayout, QWidget


class GlobalSearchPopup(QFrame):
    result_activated = Signal(object)

    def __init__(self, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self.setObjectName("GlobalSearchPopup")
        self.setAttribute(Qt.WA_StyledBackground, True)
        self.setWindowFlags(Qt.FramelessWindowHint | Qt.Tool)
        self.setMinimumWidth(460)
        self.setMaximumHeight(440)
        self._anchor: QWidget | None = None

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
        self.results.itemClicked.connect(self._activate_item)
        self.results.itemDoubleClicked.connect(self._activate_item)
        root.addWidget(self.results, 1)

        self.hide()

    def show_results(self, anchor: QWidget, groups: list[tuple[str, list[dict[str, str]]]], query: str) -> None:
        self._anchor = anchor
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
                item = QListWidgetItem()
                item.setData(Qt.UserRole, row)
                item.setSizeHint(QSize(0, 58))
                self.results.addItem(item)

                label = QLabel()
                label.setObjectName("SearchResultLabel")
                title = str(row.get("title_html") or row.get("title", "")).strip()
                subtitle = str(row.get("subtitle_html") or row.get("subtitle", "")).strip()
                body = title if not subtitle else f"{title}<br><span style='opacity:0.72'>{subtitle}</span>"
                label.setTextFormat(Qt.RichText)
                label.setText(body)
                label.setWordWrap(True)
                label.setContentsMargins(8, 6, 8, 6)
                self.results.setItemWidget(item, label)
                total += 1

        if total == 0:
            q = query.strip()
            if q:
                self.empty.setText("No matches. Try a goal, tool, runbook, fix, or session id.")
            else:
                self.empty.setText("Type to search FixFox actions.")
            self.empty.show()
            self.results.hide()
        else:
            self.empty.hide()
            self.results.show()
            self._select_first_result()

        self._reposition()
        self.show()
        self.raise_()

    def _reposition(self) -> None:
        if self._anchor is None:
            return
        bottom_left = self._anchor.mapToGlobal(self._anchor.rect().bottomLeft())
        width = max(self._anchor.width(), 520)
        self.setFixedWidth(width)
        self.move(bottom_left)

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

    def contains_global_pos(self, global_pos: QPoint) -> bool:
        if not self.isVisible():
            return False
        local = self.mapFromGlobal(global_pos)
        return self.rect().contains(local)

    def sync_anchor(self) -> None:
        if self.isVisible():
            self._reposition()
