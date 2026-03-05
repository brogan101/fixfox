from __future__ import annotations

from collections.abc import Callable
from typing import Any

from PySide6.QtCore import QPoint, QSize, Qt, Signal
from PySide6.QtGui import QAction, QContextMenuEvent, QKeyEvent, QMouseEvent
from PySide6.QtWidgets import (
    QFrame,
    QHBoxLayout,
    QLabel,
    QListWidget,
    QListWidgetItem,
    QMenu,
    QSizePolicy,
    QToolButton,
    QVBoxLayout,
    QWidget,
)

from ..icons import get_icon
from ..style import spacing, tight_spacing
from ..theme import resolve_density_tokens


def row_height_for_density(density: str) -> int:
    return resolve_density_tokens(density).list_row_height


class Badge(QLabel):
    def __init__(self, text: str, kind: str = "INFO", risk: bool = False) -> None:
        super().__init__(text)
        self.setAlignment(Qt.AlignCenter)
        self.setObjectName("Badge")
        self.setProperty("kind", kind.upper())
        if risk:
            self.setObjectName(f"BadgeRisk{kind.title()}")
        else:
            self.setObjectName(f"Badge{kind.upper()}")


class IconButton(QToolButton):
    def __init__(self, icon_name: str, parent: QWidget | None = None, tooltip: str = "") -> None:
        super().__init__(parent)
        self._icon_name = icon_name
        self.setObjectName("IconButton")
        self.setToolTip(tooltip)
        self.setCursor(Qt.PointingHandCursor)
        self.setFocusPolicy(Qt.TabFocus)
        self.refresh_icon()
        self.set_density("comfortable")

    def refresh_icon(self) -> None:
        if self.parentWidget() is not None:
            self.setIcon(get_icon(self._icon_name, self.parentWidget()))

    def set_density(self, density: str) -> None:
        d = resolve_density_tokens(density)
        self.setFixedHeight(d.button_height)
        self.setIconSize(QSize(d.icon_size, d.icon_size))


class KebabMenuButton(QToolButton):
    menu_requested = Signal(QPoint)

    def __init__(self, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self.setObjectName("KebabMenuButton")
        self.setText("")
        self.setIcon(get_icon("overflow", self, size=16))
        self.setCursor(Qt.PointingHandCursor)
        self.setFocusPolicy(Qt.TabFocus)
        self.clicked.connect(self._emit_request)
        self.set_density("comfortable")

    def _emit_request(self) -> None:
        self.menu_requested.emit(self.mapToGlobal(self.rect().bottomLeft()))

    def set_density(self, density: str) -> None:
        d = resolve_density_tokens(density)
        self.setFixedSize(d.button_height, d.button_height)
        self.setIconSize(QSize(d.icon_size, d.icon_size))
        self.setIcon(get_icon("overflow", self, size=d.icon_size))


class BaseRow(QFrame):
    activated = Signal(object)
    context_requested = Signal(object, QPoint)

    def __init__(self, payload: Any, density: str = "comfortable") -> None:
        super().__init__()
        self.payload = payload
        self.list_widget: QListWidget | None = None
        self.list_item: QListWidgetItem | None = None
        self._selected = False
        self._hover = False
        self._density = density

        self.setObjectName("RowBase")
        self.setProperty("state", "normal")
        self.setFocusPolicy(Qt.StrongFocus)
        self.setCursor(Qt.PointingHandCursor)
        self.setAutoFillBackground(True)

        shell = QHBoxLayout(self)
        shell.setSpacing(spacing("sm"))

        self.icon_slot = QLabel()
        self.icon_slot.setObjectName("RowIconSlot")
        self.icon_slot.setFixedSize(24, 24)
        self.icon_slot.setAlignment(Qt.AlignCenter)

        left = QVBoxLayout()
        left.setContentsMargins(0, 0, 0, 0)
        left.setSpacing(3)
        self.title = QLabel("")
        self.title.setObjectName("RowTitle")
        self.subtitle = QLabel("")
        self.subtitle.setObjectName("RowSubtitle")
        self.subtitle.setWordWrap(False)
        left.addWidget(self.title)
        left.addWidget(self.subtitle)

        self.left_container = left
        self.right_container = QHBoxLayout()
        self.right_container.setContentsMargins(0, 0, 0, 0)
        self.right_container.setSpacing(spacing("xs"))

        shell.addWidget(self.icon_slot, 0, Qt.AlignTop)
        shell.addLayout(left, 1)
        shell.addLayout(self.right_container, 0)
        self.set_density(density)

    def set_density(self, density: str) -> None:
        self._density = density
        d = resolve_density_tokens(density)
        row_vpad = max(4, d.card_padding_v - 6)
        self.layout().setContentsMargins(d.card_padding_h, row_vpad, d.card_padding_h, row_vpad)
        self.layout().setSpacing(tight_spacing(density))

    def bind_to_list(self, list_widget: QListWidget, list_item: QListWidgetItem) -> None:
        self.list_widget = list_widget
        self.list_item = list_item

    def set_title(self, text: str) -> None:
        self.title.setText(text)

    def set_subtitle(self, text: str) -> None:
        self.subtitle.setText(text)
        self.subtitle.setToolTip(text)

    def set_leading_icon(self, icon_name: str) -> None:
        if not icon_name:
            self.icon_slot.clear()
            return
        icon = get_icon(icon_name, self)
        if icon.isNull():
            self.icon_slot.clear()
            return
        self.icon_slot.setPixmap(icon.pixmap(20, 20))

    def set_selected(self, selected: bool) -> None:
        self._selected = selected
        self._sync_state()

    def _sync_state(self) -> None:
        if self._selected:
            state = "selected"
        elif self._hover:
            state = "hover"
        else:
            state = "normal"
        self.setProperty("state", state)
        self.style().unpolish(self)
        self.style().polish(self)
        self.update()

    def enterEvent(self, event: Any) -> None:
        self._hover = True
        self._sync_state()
        super().enterEvent(event)

    def leaveEvent(self, event: Any) -> None:
        self._hover = False
        self._sync_state()
        super().leaveEvent(event)

    def mousePressEvent(self, event: QMouseEvent) -> None:
        if self.list_widget is not None and self.list_item is not None:
            self.list_widget.setCurrentItem(self.list_item)
        self.setFocus(Qt.MouseFocusReason)
        super().mousePressEvent(event)

    def mouseDoubleClickEvent(self, event: QMouseEvent) -> None:
        if self.list_widget is not None and self.list_item is not None:
            self.list_widget.setCurrentItem(self.list_item)
        self.setFocus(Qt.MouseFocusReason)
        self.activated.emit(self.payload)
        super().mouseDoubleClickEvent(event)

    def contextMenuEvent(self, event: QContextMenuEvent) -> None:
        if self.list_widget is not None and self.list_item is not None:
            self.list_widget.setCurrentItem(self.list_item)
        self.context_requested.emit(self.payload, event.globalPos())
        event.accept()

    def keyPressEvent(self, event: QKeyEvent) -> None:
        if event.key() in {Qt.Key_Return, Qt.Key_Enter}:
            self.activated.emit(self.payload)
            event.accept()
            return
        if event.key() == Qt.Key_Menu:
            self.context_requested.emit(self.payload, self.mapToGlobal(self.rect().center()))
            event.accept()
            return
        super().keyPressEvent(event)


class FindingRow(BaseRow):
    def __init__(
        self,
        title: str,
        subtitle: str,
        status_badge: str,
        payload: Any,
        density: str = "comfortable",
        actions: list[QAction] | None = None,
    ) -> None:
        super().__init__(payload, density=density)
        self.set_leading_icon("diagnose")
        self.set_title(title)
        self.set_subtitle(subtitle)
        self.status = Badge(status_badge, kind=status_badge)
        self.right_container.addWidget(self.status)
        if actions:
            menu_btn = KebabMenuButton(self)
            menu_btn.menu_requested.connect(lambda p: self._show_inline_menu(p, actions))
            self.right_container.addWidget(menu_btn)

    def _show_inline_menu(self, pos: QPoint, actions: list[QAction]) -> None:
        menu = QMenu(self)
        for action in actions:
            menu.addAction(action)
        menu.exec(pos)


class FixRow(BaseRow):
    preview_clicked = Signal(object)
    run_clicked = Signal(object)

    def __init__(
        self,
        title: str,
        subtitle: str,
        risk_badge: str,
        payload: Any,
        density: str = "comfortable",
    ) -> None:
        super().__init__(payload, density=density)
        self.set_leading_icon("fixes")
        self.set_title(title)
        self.set_subtitle(subtitle)
        self.risk = Badge(risk_badge, kind=risk_badge, risk=True)
        self.preview_btn = IconButton("preview", self, "Preview")
        self.run_btn = IconButton("play", self, "Run fix")
        self.preview_btn.clicked.connect(lambda: self.preview_clicked.emit(self.payload))
        self.run_btn.clicked.connect(lambda: self.run_clicked.emit(self.payload))
        self.right_container.addWidget(self.risk)
        self.right_container.addWidget(self.preview_btn)
        self.right_container.addWidget(self.run_btn)


class SessionRow(BaseRow):
    def __init__(
        self,
        session_id: str,
        symptom: str,
        summary: str,
        export_status: str,
        timestamp: str,
        payload: Any,
        density: str = "comfortable",
    ) -> None:
        super().__init__(payload, density=density)
        self.set_leading_icon("history")
        self.set_title(f"{symptom}  [{session_id}]")
        self.set_subtitle(summary)
        stamp = QLabel(timestamp or "n/a")
        stamp.setObjectName("RowSubtitle")
        stamp.setSizePolicy(QSizePolicy.Fixed, QSizePolicy.Preferred)
        self.export_badge = Badge(export_status, "INFO")
        self.right_container.addWidget(self.export_badge)
        self.right_container.addWidget(stamp)


class ToolRow(BaseRow):
    open_clicked = Signal(object)

    def __init__(
        self,
        title: str,
        category_chip: str,
        subtitle: str,
        payload: Any,
        density: str = "comfortable",
    ) -> None:
        super().__init__(payload, density=density)
        self.set_leading_icon("toolbox")
        self.set_title(title)
        self.set_subtitle(subtitle)
        self.category = Badge(category_chip, "INFO")
        self.open_btn = IconButton("play", self, "Open tool")
        self.open_btn.clicked.connect(lambda: self.open_clicked.emit(self.payload))
        self.right_container.addWidget(self.category)
        self.right_container.addWidget(self.open_btn)
