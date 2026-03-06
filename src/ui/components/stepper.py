from __future__ import annotations

from typing import Iterable

from PySide6.QtCore import QPointF, Qt
from PySide6.QtGui import QColor, QFont, QPainter, QPen
from PySide6.QtWidgets import QWidget

from ..icons import get_icon
from ..style import spacing, token_radius, token_weight
from ..theme import resolve_theme_tokens
from ...core.settings import load_settings


class StepIndicator(QWidget):
    def __init__(self, steps: Iterable[str] | None = None, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self._steps = [str(step or "").strip() for step in (steps or [])]
        self._current = 0
        self.setMinimumHeight(56)
        self.setMaximumHeight(72)

    def set_steps(self, steps: Iterable[str]) -> None:
        self._steps = [str(step or "").strip() for step in steps]
        self.update()

    def set_current_step(self, index: int) -> None:
        self._current = max(0, min(int(index), max(0, len(self._steps) - 1)))
        self.update()

    def _colors(self) -> tuple[QColor, QColor, QColor, QColor]:
        settings = load_settings().normalized()
        tokens = resolve_theme_tokens(settings.theme_palette, settings.theme_mode)
        border = QColor(tokens.border)
        muted = QColor(tokens.text_muted)
        accent = QColor(tokens.accent)
        ok = QColor(tokens.ok)
        return border, muted, accent, ok

    def paintEvent(self, event) -> None:  # type: ignore[override]
        del event
        if not self._steps:
            return
        border, muted, accent, ok = self._colors()
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing, True)

        radius = token_radius("sm")
        top = spacing("sm") + radius
        left = spacing("md") + radius
        width = max(1, self.width() - spacing("md") * 2 - radius * 2)
        step_gap = width / max(1, len(self._steps) - 1)

        # Connecting line.
        pen = QPen(border, 2)
        painter.setPen(pen)
        if len(self._steps) > 1:
            painter.drawLine(
                QPointF(left, top),
                QPointF(left + step_gap * (len(self._steps) - 1), top),
            )

        number_font = painter.font()
        number_font.setWeight(QFont.Weight(token_weight("demibold")))
        painter.setFont(number_font)

        for idx, title in enumerate(self._steps):
            x = left + (step_gap * idx)
            state = "upcoming"
            if idx < self._current:
                state = "done"
            elif idx == self._current:
                state = "current"

            fill = QColor("transparent")
            edge = border
            txt = muted
            if state == "done":
                fill = ok
                edge = ok
                txt = QColor("#FFFFFF")
            elif state == "current":
                fill = accent
                edge = accent
                txt = QColor("#FFFFFF")

            painter.setPen(QPen(edge, 2))
            painter.setBrush(fill)
            painter.drawEllipse(QPointF(x, top), radius, radius)

            if state == "done":
                check = get_icon("check_circle", self, size=12).pixmap(12, 12)
                if not check.isNull():
                    painter.drawPixmap(int(x - 6), int(top - 6), check)
                else:
                    painter.setPen(QPen(txt, 2))
                    painter.drawText(int(x - 3), int(top + 5), "v")
            else:
                painter.setPen(txt)
                painter.drawText(int(x - 4), int(top + 5), str(idx + 1))

            painter.setPen(muted if state == "upcoming" else QColor(self.palette().color(self.foregroundRole())))
            painter.drawText(int(x - 46), int(top + radius + spacing("sm")), 92, 20, Qt.AlignHCenter | Qt.AlignTop, title)

