from __future__ import annotations

from typing import Any

from PySide6.QtCore import QPoint, QSize, Qt, Signal
from PySide6.QtGui import QFontMetrics
from PySide6.QtWidgets import QFrame, QHBoxLayout, QLabel, QListWidget, QListWidgetItem, QVBoxLayout, QWidget

from ..style import spacing


class _SearchResultRow(QFrame):
    def __init__(self, payload: dict[str, str]) -> None:
        super().__init__()
        self.setObjectName("GlobalSearchRow")
        self.setProperty("state", "normal")
        self._payload = payload

        shell = QHBoxLayout(self)
        shell.setContentsMargins(spacing("sm"), spacing("sm"), spacing("sm"), spacing("sm"))
        shell.setSpacing(spacing("sm"))

        text_col = QVBoxLayout()
        text_col.setContentsMargins(0, 0, 0, 0)
        text_col.setSpacing(2)
        self.title = QLabel(str(payload.get("title", "")).strip())
        self.title.setObjectName("GlobalSearchTitle")
        self.title.setWordWrap(False)
        self.subtitle = QLabel(str(payload.get("subtitle", "")).strip())
        self.subtitle.setObjectName("GlobalSearchSubtitle")
        self.subtitle.setWordWrap(False)
        text_col.addWidget(self.title)
        text_col.addWidget(self.subtitle)

        self.kind = QLabel(str(payload.get("kind", "")).strip().upper())
        self.kind.setObjectName("GlobalSearchTag")
        self.kind.setAlignment(Qt.AlignCenter)

        shell.addLayout(text_col, 1)
        shell.addWidget(self.kind, 0, Qt.AlignTop)

    def resizeEvent(self, event) -> None:  # type: ignore[override]
        super().resizeEvent(event)
        metrics = QFontMetrics(self.title.font())
        title_width = max(80, self.title.width() - 2)
        subtitle_width = max(80, self.subtitle.width() - 2)
        raw_title = str(self._payload.get("title", "")).strip()
        raw_subtitle = str(self._payload.get("subtitle", "")).strip()
        self.title.setText(metrics.elidedText(raw_title, Qt.ElideRight, title_width))
        sub_metrics = QFontMetrics(self.subtitle.font())
        self.subtitle.setText(sub_metrics.elidedText(raw_subtitle, Qt.ElideRight, subtitle_width))
        self.subtitle.setVisible(bool(raw_subtitle))

    def set_selected(self, selected: bool) -> None:
        self.setProperty("state", "selected" if selected else "normal")
        self.style().unpolish(self)
        self.style().polish(self)


class GlobalSearchPopup(QFrame):
    result_activated = Signal(object)

    def __init__(self, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self.setObjectName("GlobalSearchPopup")
        self.setAttribute(Qt.WA_StyledBackground, True)
        self.setMinimumWidth(460)
        self.setMaximumHeight(460)
        self._anchor: QWidget | None = None

        root = QVBoxLayout(self)
        root.setContentsMargins(spacing("sm"), spacing("sm"), spacing("sm"), spacing("sm"))
        root.setSpacing(spacing("xs"))

        self.empty = QLabel("Type to search.")
        self.empty.setObjectName("GlobalSearchEmpty")
        self.empty.setWordWrap(True)
        root.addWidget(self.empty)

        self.results = QListWidget()
        self.results.setObjectName("GlobalSearchList")
        self.results.setSelectionMode(QListWidget.SingleSelection)
        self.results.itemClicked.connect(self._activate_item)
        self.results.itemDoubleClicked.connect(self._activate_item)
        self.results.currentItemChanged.connect(self._sync_row_states)
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
            header.setSizeHint(QSize(0, 24))
            self.results.addItem(header)
            for row in rows:
                item = QListWidgetItem()
                item.setData(Qt.UserRole, row)
                item.setSizeHint(QSize(0, 68))
                self.results.addItem(item)
                widget = _SearchResultRow(row)
                self.results.setItemWidget(item, widget)
                total += 1

        if total == 0:
            q = query.strip()
            self.empty.setText(
                "No matches. Try a goal, tool, runbook, fix, or session id."
                if q
                else "Type to search FixFox actions."
            )
            self.empty.show()
            self.results.hide()
        else:
            self.empty.hide()
            self.results.show()
            self._select_first_result()
            self._sync_row_states()

        desired_height = self._desired_height(total)
        self.setFixedHeight(desired_height)
        self._reposition()
        self.show()
        self.raise_()

    def _reposition(self) -> None:
        if self._anchor is None or self.parentWidget() is None:
            return
        parent = self.parentWidget()
        bottom_left = self._anchor.mapTo(parent, self._anchor.rect().bottomLeft())
        width = max(self._anchor.width(), 560)
        max_width = max(360, parent.width() - bottom_left.x() - spacing("lg"))
        width = min(width, max_width)
        self.setFixedWidth(width)
        self.move(bottom_left.x(), bottom_left.y() + 2)

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

    def _sync_row_states(self, *_args: object) -> None:
        current = self.results.currentItem()
        for index in range(self.results.count()):
            item = self.results.item(index)
            row = self.results.itemWidget(item)
            if isinstance(row, _SearchResultRow):
                row.set_selected(item is current)

    def _desired_height(self, result_count: int) -> int:
        margins = self.layout().contentsMargins() if self.layout() is not None else None
        chrome = 0 if margins is None else margins.top() + margins.bottom()
        if result_count <= 0:
            return min(self.maximumHeight(), max(96, self.empty.sizeHint().height() + chrome + spacing("md")))
        rows_height = 0
        for index in range(self.results.count()):
            hint = self.results.sizeHintForRow(index)
            if hint <= 0:
                item = self.results.item(index)
                hint = item.sizeHint().height() if item is not None else 0
            rows_height += max(0, hint)
        rows_height += self.results.frameWidth() * 2
        return min(self.maximumHeight(), max(180, rows_height + chrome + spacing("sm")))
