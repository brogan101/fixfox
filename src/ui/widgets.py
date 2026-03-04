from __future__ import annotations

from PySide6.QtCore import QTimer, Qt, Signal
from PySide6.QtWidgets import (
    QFrame,
    QHBoxLayout,
    QLabel,
    QPushButton,
    QStackedLayout,
    QTextEdit,
    QVBoxLayout,
    QWidget,
)

from .theme import resolve_density_tokens


class Card(QFrame):
    def __init__(self, title: str, subtitle: str = "", right_widget: QWidget | None = None, object_name: str = "Card", density: str = "comfortable"):
        super().__init__()
        self._density = density
        self.setObjectName(object_name)
        self.setFrameShape(QFrame.NoFrame)
        self.layout_main = QVBoxLayout(self)
        self.layout_main.setContentsMargins(14, 12, 14, 12)
        self.layout_main.setSpacing(8)
        self.set_density(density)

        top = QHBoxLayout()
        self.title = QLabel(title)
        self.title.setObjectName("CardTitle")
        self.title.setWordWrap(True)
        top.addWidget(self.title, 1)
        if right_widget is not None:
            top.addWidget(right_widget, 0, Qt.AlignRight)
        self.layout_main.addLayout(top)

        self.sub = QLabel(subtitle)
        self.sub.setObjectName("CardSubtitle")
        self.sub.setWordWrap(True)
        self.layout_main.addWidget(self.sub)

    def body_layout(self) -> QVBoxLayout:
        return self.layout_main

    def set_density(self, density: str) -> None:
        self._density = density
        d = resolve_density_tokens(density)
        self.layout_main.setContentsMargins(d.card_padding_h, d.card_padding_v, d.card_padding_h, d.card_padding_v)
        self.layout_main.setSpacing(8 if density == "comfortable" else 6)
        self.update()


class Pill(QLabel):
    def __init__(self, text: str):
        super().__init__(text)
        self.setObjectName("Pill")
        self.setAlignment(Qt.AlignCenter)
        self.setMinimumHeight(26)


class PrimaryButton(QPushButton):
    def __init__(self, text: str):
        super().__init__(text)
        self.setObjectName("PrimaryButton")
        self.setMinimumHeight(36)

    def set_density(self, density: str) -> None:
        self.setMinimumHeight(resolve_density_tokens(density).button_height)


class SoftButton(QPushButton):
    def __init__(self, text: str):
        super().__init__(text)
        self.setObjectName("SoftButton")
        self.setMinimumHeight(34)

    def set_density(self, density: str) -> None:
        self.setMinimumHeight(resolve_density_tokens(density).button_height - 2)


class DrawerCard(Card):
    def __init__(self, title: str = "Details"):
        self.toggle_btn = SoftButton("Show details")
        super().__init__(title, "", right_widget=self.toggle_btn, object_name="Drawer")
        self.text = QTextEdit()
        self.text.setReadOnly(True)
        self.text.setMinimumHeight(120)
        self.text.hide()
        self.body_layout().addWidget(self.text)
        self.toggle_btn.clicked.connect(self._toggle)

    def set_text(self, text: str) -> None:
        self.text.setPlainText(text)

    def _toggle(self) -> None:
        visible = self.text.isVisible()
        self.text.setVisible(not visible)
        self.toggle_btn.setText("Hide details" if not visible else "Show details")


class ToastHost(QWidget):
    def __init__(self) -> None:
        super().__init__()
        self.setAttribute(Qt.WA_TransparentForMouseEvents)
        self.layout_main = QVBoxLayout(self)
        self.layout_main.setContentsMargins(0, 0, 0, 0)
        self.layout_main.setSpacing(8)
        self.layout_main.addStretch(1)
        self.setMaximumWidth(360)

    def show_toast(self, text: str, timeout_ms: int = 2600) -> None:
        frame = QFrame()
        frame.setObjectName("Toast")
        lay = QHBoxLayout(frame)
        lay.setContentsMargins(12, 8, 12, 8)
        label = QLabel(text)
        label.setWordWrap(True)
        lay.addWidget(label)
        self.layout_main.addWidget(frame)
        QTimer.singleShot(timeout_ms, frame.deleteLater)


class ConciergePanel(Card):
    collapsed_changed = Signal(bool)

    def __init__(self) -> None:
        self.collapse_btn = SoftButton("Collapse")
        super().__init__("Concierge Panel", "Context help and next action.", right_widget=self.collapse_btn, object_name="ConciergePanel")
        self.content = QWidget()
        self.content_layout = QVBoxLayout(self.content)
        self.content_layout.setContentsMargins(0, 4, 0, 0)
        self.content_layout.setSpacing(8)
        self.body_layout().addWidget(self.content)
        self._collapsed = False
        self.collapse_btn.clicked.connect(self.toggle_collapsed)

    def add_widget(self, widget: QWidget) -> None:
        self.content_layout.addWidget(widget)

    def clear_widgets(self) -> None:
        while self.content_layout.count():
            item = self.content_layout.takeAt(0)
            widget = item.widget()
            if widget:
                widget.deleteLater()

    def set_collapsed(self, collapsed: bool) -> None:
        self._collapsed = collapsed
        self.content.setVisible(not collapsed)
        self.collapse_btn.setText("Expand" if collapsed else "Collapse")
        self.collapsed_changed.emit(collapsed)

    def toggle_collapsed(self) -> None:
        self.set_collapsed(not self._collapsed)

    @property
    def collapsed(self) -> bool:
        return self._collapsed


class EmptyState(Card):
    def __init__(self, title: str, subtitle: str, cta: QWidget | None = None, icon: str = "i"):
        super().__init__(title, subtitle, right_widget=cta, object_name="EmptyState")
        badge = QLabel(icon)
        badge.setObjectName("CardTitle")
        badge.setAlignment(Qt.AlignHCenter)
        self.layout_main.insertWidget(0, badge)
