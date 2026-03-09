from __future__ import annotations

from PySide6.QtCore import QEasingCurve, QPropertyAnimation, QTimer, Qt
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

from .style import control_height, spacing, tight_spacing
from .theme import ELEVATION_SCALE, resolve_density_tokens
from .icons import get_icon
from .components.motion import animate_opacity


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
        self.layout_main.setContentsMargins(spacing("lg"), spacing("md"), spacing("lg"), spacing("md"))
        self.layout_main.setSpacing(spacing("sm"))
        self.set_density(density)
        self._hovered = False

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

    def enterEvent(self, event) -> None:  # type: ignore[override]
        self._hovered = True
        self.setProperty("hovered", "true")
        self.style().unpolish(self)
        self.style().polish(self)
        super().enterEvent(event)

    def leaveEvent(self, event) -> None:  # type: ignore[override]
        self._hovered = False
        self.setProperty("hovered", "false")
        self.style().unpolish(self)
        self.style().polish(self)
        super().leaveEvent(event)


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
        self.toggle_btn = SoftButton("Expand")
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
            self.toggle_btn.setText("Expand")
            return
        self.text.setVisible(True)
        self._anim.stop()
        self._anim.setStartValue(0)
        self._anim.setEndValue(180)
        self._anim.start()
        self.toggle_btn.setText("Collapse")

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
        self._toast_anims: list[QPropertyAnimation] = []

    def show_toast(self, text: str, timeout_ms: int = 2600) -> None:
        frame = QFrame()
        frame.setObjectName("Toast")
        frame.setMaximumHeight(0)
        lay = QHBoxLayout(frame)
        lay.setContentsMargins(spacing("sm"), spacing("xs"), spacing("sm"), spacing("xs"))
        label = QLabel(text)
        label.setWordWrap(True)
        lay.addWidget(label)
        self.layout_main.insertWidget(max(0, self.layout_main.count() - 1), frame)
        open_height = max(28, frame.sizeHint().height() + 2)
        height_anim = QPropertyAnimation(frame, b"maximumHeight", frame)
        height_anim.setDuration(180)
        height_anim.setEasingCurve(QEasingCurve.OutCubic)
        height_anim.setStartValue(0)
        height_anim.setEndValue(open_height)
        height_anim.start()
        self._toast_anims.append(height_anim)
        fade_in = animate_opacity(frame, start=0.0, end=1.0, duration_ms=170)
        self._toast_anims.append(fade_in)
        QTimer.singleShot(timeout_ms, lambda ref=frame: self._dismiss_toast(ref))

    def _dismiss_toast(self, frame: QFrame) -> None:
        if frame is None or not frame.isVisible():
            return
        fade_out = animate_opacity(frame, start=1.0, end=0.0, duration_ms=170)
        self._toast_anims.append(fade_out)
        close_anim = QPropertyAnimation(frame, b"maximumHeight", frame)
        close_anim.setDuration(170)
        close_anim.setEasingCurve(QEasingCurve.InCubic)
        close_anim.setStartValue(max(18, frame.height()))
        close_anim.setEndValue(0)
        close_anim.finished.connect(frame.deleteLater)
        close_anim.start()
        self._toast_anims.append(close_anim)


class EmptyState(Card):
    def __init__(self, title: str, subtitle: str, cta: QWidget | None = None, icon: str = "i"):
        super().__init__(title, subtitle, right_widget=cta, object_name="EmptyState")
        badge = QLabel()
        badge.setAlignment(Qt.AlignHCenter)
        pix = get_icon(icon, self, size=22).pixmap(22, 22)
        if not pix.isNull():
            badge.setPixmap(pix)
        else:
            badge.setText(str(icon or ""))
            badge.setObjectName("CardTitle")
        self.layout_main.insertWidget(0, badge)


class DrawerPanel(Card):
    def __init__(self, title: str, subtitle: str = "", density: str = "comfortable"):
        super().__init__(title, subtitle, object_name="Drawer", density=density, elevation=ELEVATION_SCALE["overlay"])


class InlineCallout(Card):
    def __init__(self, title: str = "Issue", message: str = "", level: str = "warn", density: str = "comfortable"):
        super().__init__(title, message, object_name="InlineCallout", density=density, elevation=ELEVATION_SCALE["raised"])
        self.setProperty("level", str(level or "warn").strip().lower())
        self.sub.setWordWrap(True)
        self.setVisible(bool(message))

    def set_message(self, title: str, message: str, level: str = "warn") -> None:
        self.title.setText(str(title or "Issue"))
        self.sub.setText(str(message or "").strip())
        self.setProperty("level", str(level or "warn").strip().lower())
        self.style().unpolish(self)
        self.style().polish(self)
        self.setVisible(bool(str(message or "").strip()))
