from __future__ import annotations

from typing import Callable

from PySide6.QtCore import QSize, Qt, Signal
from PySide6.QtWidgets import QHBoxLayout, QLabel, QListWidget, QListWidgetItem, QToolButton, QVBoxLayout, QWidget


class NavShell(QWidget):
    collapsed_changed = Signal(bool)

    def __init__(self) -> None:
        super().__init__()
        self._collapsed = False
        outer = QHBoxLayout(self)
        outer.setContentsMargins(0, 0, 0, 0)
        outer.setSpacing(8)

        self.rail = QWidget()
        self.rail.setObjectName("IconRail")
        rail_layout = QVBoxLayout(self.rail)
        rail_layout.setContentsMargins(6, 8, 6, 8)
        rail_layout.setSpacing(8)
        self.toggle_btn = QToolButton()
        self.toggle_btn.setObjectName("IconButton")
        self.toggle_btn.setText("<<")
        self.toggle_btn.setToolTip("Collapse or expand navigation")
        self.toggle_btn.clicked.connect(self.toggle_collapsed)
        self.icon_list = QListWidget()
        self.icon_list.setObjectName("IconRailList")
        self.icon_list.setVerticalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
        self.icon_list.setHorizontalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
        self.icon_list.setFocusPolicy(Qt.TabFocus)
        self.icon_list.setMaximumWidth(56)
        rail_layout.addWidget(self.toggle_btn, 0, Qt.AlignTop)
        rail_layout.addWidget(self.icon_list, 1)

        self.nav_list = QListWidget()
        self.nav_list.setObjectName("MainNav")
        self.nav_list.setMinimumWidth(220)
        self.nav_list.setMaximumWidth(260)
        self.nav_list.setFocusPolicy(Qt.TabFocus)

        self.icon_list.currentRowChanged.connect(self._on_icon_row_changed)
        self.nav_list.currentRowChanged.connect(self._on_nav_row_changed)

        outer.addWidget(self.rail, 0)
        outer.addWidget(self.nav_list, 1)

    def set_items(
        self,
        labels: tuple[str, ...] | list[str],
        icon_resolver: Callable[[str], str],
        row_height: int,
        row_widget_factory: Callable[[str], QWidget],
    ) -> None:
        self.nav_list.clear()
        self.icon_list.clear()
        for label in labels:
            nav_item = QListWidgetItem()
            nav_item.setData(Qt.UserRole, label)
            nav_item.setSizeHint(QSize(0, row_height))
            self.nav_list.addItem(nav_item)
            self.nav_list.setItemWidget(nav_item, row_widget_factory(label))

            icon_item = QListWidgetItem()
            icon_item.setData(Qt.UserRole, label)
            icon_item.setSizeHint(QSize(42, row_height))
            self.icon_list.addItem(icon_item)
            icon_widget = QWidget()
            icon_layout = QHBoxLayout(icon_widget)
            icon_layout.setContentsMargins(6, 0, 6, 0)
            icon_layout.setSpacing(0)
            icon = QLabel(icon_resolver(label))
            icon.setObjectName("IconRailGlyph")
            icon.setAlignment(Qt.AlignCenter)
            icon_layout.addWidget(icon, 1)
            self.icon_list.setItemWidget(icon_item, icon_widget)

    @property
    def collapsed(self) -> bool:
        return self._collapsed

    def set_collapsed(self, collapsed: bool) -> None:
        self._collapsed = bool(collapsed)
        self.nav_list.setVisible(not self._collapsed)
        self.toggle_btn.setText(">>" if self._collapsed else "<<")
        self.collapsed_changed.emit(self._collapsed)

    def toggle_collapsed(self) -> None:
        self.set_collapsed(not self._collapsed)

    def _on_icon_row_changed(self, row: int) -> None:
        if row >= 0 and self.nav_list.currentRow() != row:
            self.nav_list.setCurrentRow(row)

    def _on_nav_row_changed(self, row: int) -> None:
        if row >= 0 and self.icon_list.currentRow() != row:
            self.icon_list.setCurrentRow(row)

