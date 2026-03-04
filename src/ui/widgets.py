from __future__ import annotations

from PySide6.QtCore import QEasingCurve, QPropertyAnimation, QTimer, Qt, Signal
from PySide6.QtWidgets import (
    QFrame,
    QHBoxLayout,
    QLabel,
    QPushButton,
    QScrollArea,
    QStackedLayout,
    QTextEdit,
    QVBoxLayout,
    QWidget,
)

from .style import control_height, spacing, tight_spacing
from .theme import resolve_density_tokens


class Card(QFrame):
    def __init__(
        self,
        title: str,
        subtitle: str = "",
        right_widget: QWidget | None = None,
        object_name: str = "Card",
        density: str = "comfortable",
        elevation: int = 1,
    ):
        super().__init__()
        self._density = density
        self.setObjectName(object_name)
        self.setProperty("elevation", max(0, min(2, int(elevation))))
        self.setFrameShape(QFrame.NoFrame)
        self.layout_main = QVBoxLayout(self)
        self.layout_main.setContentsMargins(14, 12, 14, 12)
        self.layout_main.setSpacing(spacing("sm"))
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
        self.layout_main.setSpacing(tight_spacing(density))
        self.update()


class Pill(QLabel):
    def __init__(self, text: str):
        super().__init__(text)
        self.setObjectName("Pill")
        self.setAlignment(Qt.AlignCenter)
        self.setMinimumHeight(24)


class Chip(QLabel):
    def __init__(self, text: str, selected: bool = False):
        super().__init__(text)
        self.setObjectName("Chip")
        self.setAlignment(Qt.AlignCenter)
        self.set_selected(selected)
        self.setMinimumHeight(24)

    def set_selected(self, selected: bool) -> None:
        self.setProperty("state", "selected" if selected else "normal")
        self.style().unpolish(self)
        self.style().polish(self)


class Badge(QLabel):
    def __init__(self, text: str):
        super().__init__(text)
        self.setObjectName("Badge")
        self.setAlignment(Qt.AlignCenter)
        self.setMinimumHeight(20)


class PrimaryButton(QPushButton):
    def __init__(self, text: str):
        super().__init__(text)
        self.setObjectName("PrimaryButton")
        self.setMinimumHeight(control_height("comfortable"))

    def set_density(self, density: str) -> None:
        self.setMinimumHeight(resolve_density_tokens(density).button_height)


class SoftButton(QPushButton):
    def __init__(self, text: str):
        super().__init__(text)
        self.setObjectName("SoftButton")
        self.setMinimumHeight(control_height("comfortable") - 2)

    def set_density(self, density: str) -> None:
        self.setMinimumHeight(resolve_density_tokens(density).button_height - 2)


class SecondaryButton(SoftButton):
    def __init__(self, text: str):
        super().__init__(text)
        self.setObjectName("SecondaryButton")


class TextButton(QPushButton):
    def __init__(self, text: str):
        super().__init__(text)
        self.setObjectName("TextButton")
        self.setMinimumHeight(control_height("comfortable") - 2)

    def set_density(self, density: str) -> None:
        self.setMinimumHeight(resolve_density_tokens(density).button_height - 2)


class DrawerCard(Card):
    def __init__(self, title: str = "Details"):
        self.toggle_btn = SoftButton("Show details")
        super().__init__(title, "", right_widget=self.toggle_btn, object_name="Drawer")
        self.text = QTextEdit()
        self.text.setReadOnly(True)
        self.text.setMinimumHeight(120)
        self.text.setMaximumHeight(0)
        self.text.hide()
        self.body_layout().addWidget(self.text)
        self._anim = QPropertyAnimation(self.text, b"maximumHeight", self)
        self._anim.setDuration(180)
        self._anim.setEasingCurve(QEasingCurve.OutCubic)
        self.toggle_btn.clicked.connect(self._toggle)

    def set_text(self, text: str) -> None:
        self.text.setPlainText(text)

    def _toggle(self) -> None:
        visible = self.text.isVisible()
        if visible:
            self._anim.stop()
            self._anim.setStartValue(self.text.maximumHeight())
            self._anim.setEndValue(0)
            self._anim.finished.connect(self._hide_drawer_once)
            self._anim.start()
            self.toggle_btn.setText("Show details")
            return
        self.text.setVisible(True)
        self._anim.stop()
        self._anim.setStartValue(0)
        self._anim.setEndValue(180)
        self._anim.start()
        self.toggle_btn.setText("Hide details")

    def _hide_drawer_once(self) -> None:
        if self.text.maximumHeight() <= 0:
            self.text.hide()
        try:
            self._anim.finished.disconnect(self._hide_drawer_once)
        except Exception:
            pass


class ToastHost(QWidget):
    def __init__(self) -> None:
        super().__init__()
        self.setAttribute(Qt.WA_TransparentForMouseEvents)
        self.layout_main = QVBoxLayout(self)
        self.layout_main.setContentsMargins(0, 0, 0, 0)
        self.layout_main.setSpacing(spacing("sm"))
        self.layout_main.addStretch(1)
        self.setMaximumWidth(360)

    def show_toast(self, text: str, timeout_ms: int = 2600) -> None:
        frame = QFrame()
        frame.setObjectName("Toast")
        lay = QHBoxLayout(frame)
        lay.setContentsMargins(spacing("sm"), spacing("xs"), spacing("sm"), spacing("xs"))
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
        self.content = QScrollArea()
        self.content.setObjectName("ConciergeScroll")
        self.content.setWidgetResizable(True)
        self.content.setFrameShape(QFrame.NoFrame)
        self.content_host = QWidget()
        self.content_layout = QVBoxLayout(self.content_host)
        self.content_layout.setContentsMargins(0, spacing("xs"), 0, 0)
        self.content_layout.setSpacing(spacing("sm"))
        self.content.setWidget(self.content_host)
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


class DrawerPanel(Card):
    def __init__(self, title: str, subtitle: str = "", density: str = "comfortable"):
        super().__init__(title, subtitle, object_name="Drawer", density=density, elevation=2)
