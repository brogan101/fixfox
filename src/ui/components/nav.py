from __future__ import annotations

from dataclasses import dataclass
from typing import Callable

from PySide6.QtCore import QSize, Qt, Signal
from PySide6.QtWidgets import QButtonGroup, QFrame, QSizePolicy, QToolButton, QVBoxLayout, QWidget

from ..icons import get_icon
from ..style import spacing


@dataclass
class _NavItemProxy:
    label: str
    button: QToolButton

    def text(self) -> str:
        return self.label

    def isHidden(self) -> bool:
        return self.button.isHidden()

    def setHidden(self, hidden: bool) -> None:
        self.button.setHidden(hidden)


class NavRail(QWidget):
    currentRowChanged = Signal(int)

    def __init__(self) -> None:
        super().__init__()
        self.setObjectName("NavRail")
        self.setSizePolicy(QSizePolicy.Fixed, QSizePolicy.Expanding)
        self.setMinimumWidth(88)
        self.setMaximumWidth(88)
        self._row_height = 58
        self._current_row = -1
        self._items: list[str] = []
        self._proxies: list[_NavItemProxy] = []
        self._buttons: dict[str, QToolButton] = {}

        shell = QVBoxLayout(self)
        shell.setContentsMargins(spacing("md"), spacing("md"), spacing("md"), spacing("md"))
        shell.setSpacing(spacing("md"))

        self.top_group_box = QWidget()
        self.top_group = QVBoxLayout(self.top_group_box)
        self.top_group.setContentsMargins(0, 0, 0, 0)
        self.top_group.setSpacing(spacing("xs"))

        self.bottom_group_box = QWidget()
        self.bottom_group = QVBoxLayout(self.bottom_group_box)
        self.bottom_group.setContentsMargins(0, 0, 0, 0)
        self.bottom_group.setSpacing(spacing("xs"))

        self.divider = QFrame()
        self.divider.setObjectName("NavRailDivider")
        self.divider.setFrameShape(QFrame.HLine)

        shell.addWidget(self.top_group_box, 0)
        shell.addStretch(1)
        shell.addWidget(self.divider, 0)
        shell.addWidget(self.bottom_group_box, 0)

        self._group = QButtonGroup(self)
        self._group.setExclusive(True)
        self._group.idClicked.connect(self._on_id_clicked)

    @property
    def collapsed(self) -> bool:
        # Rail-only shell is always compact.
        return True

    def set_collapsed(self, _collapsed: bool) -> None:
        return

    def set_items(
        self,
        labels: tuple[str, ...] | list[str],
        icon_resolver: Callable[[str], str],
        row_height: int,
        row_widget_factory: Callable[[str], QWidget] | None = None,
    ) -> None:
        del row_widget_factory
        self._row_height = max(42, int(row_height))
        self._items = [str(label) for label in labels]
        self._proxies.clear()
        self._buttons.clear()

        self._clear_layout(self.top_group)
        self._clear_layout(self.bottom_group)
        for button in self._group.buttons():
            self._group.removeButton(button)
            button.deleteLater()

        top_items = [label for label in self._items if label not in {"Settings"}]
        bottom_items = [label for label in self._items if label in {"Settings"}]
        top_items = [x for x in top_items if x != "Help"]

        for row, label in enumerate(self._items):
            button = self._make_button(label, icon_resolver(label), row)
            self._buttons[label] = button
            self._proxies.append(_NavItemProxy(label=label, button=button))

        for label in top_items:
            self.top_group.addWidget(self._buttons[label], 0)
        self.top_group.addStretch(1)
        for label in bottom_items:
            self.bottom_group.addWidget(self._buttons[label], 0)

        if self._items:
            self.setCurrentRow(0)

    def _make_button(self, label: str, icon_name: str, row: int) -> QToolButton:
        btn = QToolButton()
        btn.setObjectName("NavRailButton")
        btn.setToolButtonStyle(Qt.ToolButtonIconOnly)
        btn.setText("")
        btn.setToolTip(label)
        btn.setCheckable(True)
        btn.setAutoExclusive(False)
        btn.setFocusPolicy(Qt.TabFocus)
        btn.setCursor(Qt.PointingHandCursor)
        btn.setProperty("nav_label", label)
        btn.setProperty("icon_name", icon_name)
        btn.setFixedSize(64, self._row_height)
        icon_size = max(18, min(20, self._row_height - 26))
        btn.setIconSize(QSize(icon_size, icon_size))
        btn.setIcon(get_icon(icon_name, btn, size=icon_size))
        btn.setAccessibleName(f"Navigate to {label}")
        self._group.addButton(btn, row)
        return btn

    def _make_aux_button(self, label: str, icon_name: str) -> QToolButton:
        btn = QToolButton()
        btn.setObjectName("NavRailAuxButton")
        btn.setToolButtonStyle(Qt.ToolButtonIconOnly)
        btn.setText("")
        btn.setToolTip(label)
        btn.setFocusPolicy(Qt.TabFocus)
        btn.setCursor(Qt.PointingHandCursor)
        btn.setProperty("icon_name", icon_name)
        btn.setFixedSize(64, self._row_height)
        icon_size = max(18, min(20, self._row_height - 26))
        btn.setIconSize(QSize(icon_size, icon_size))
        btn.setIcon(get_icon(icon_name, btn, size=icon_size))
        return btn

    def _on_id_clicked(self, row: int) -> None:
        self.setCurrentRow(row)

    def currentRow(self) -> int:
        return self._current_row

    def setCurrentRow(self, row: int) -> None:
        if row < 0 or row >= len(self._items):
            return
        if row == self._current_row:
            return
        self._current_row = row
        for button in self._group.buttons():
            button_id = self._group.id(button)
            button.setChecked(button_id == row)
        self.currentRowChanged.emit(row)

    def count(self) -> int:
        return len(self._items)

    def item(self, index: int) -> _NavItemProxy | None:
        if index < 0 or index >= len(self._proxies):
            return None
        return self._proxies[index]

    def width(self) -> int:  # type: ignore[override]
        return 88

    @staticmethod
    def _clear_layout(layout: QVBoxLayout) -> None:
        while layout.count():
            item = layout.takeAt(0)
            widget = item.widget()
            if widget is not None:
                widget.deleteLater()

    def refresh_icons(self) -> None:
        for button in self.findChildren(QToolButton):
            icon_name = str(button.property("icon_name") or "").strip().lower()
            if not icon_name:
                continue
            size = button.iconSize().width() or 20
            button.setIcon(get_icon(icon_name, button, size=size))
