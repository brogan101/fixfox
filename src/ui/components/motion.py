from __future__ import annotations

import os
from dataclasses import dataclass

from PySide6.QtCore import QEasingCurve, QPoint, QPropertyAnimation, QParallelAnimationGroup
from PySide6.QtGui import QEnterEvent
from PySide6.QtWidgets import QGraphicsOpacityEffect, QWidget


EASE_OUT = QEasingCurve.OutCubic
EASE_SOFT = QEasingCurve.InOutCubic
DEFAULT_DURATION_MS = 200


def _animations_enabled() -> bool:
    return os.environ.get("QT_QPA_PLATFORM", "").strip().lower() not in {"offscreen", "minimal"}


def _noop_animation(widget: QWidget) -> QPropertyAnimation:
    anim = QPropertyAnimation(widget, b"pos", widget)
    anim.setDuration(0)
    anim.setStartValue(widget.pos())
    anim.setEndValue(widget.pos())
    anim.start()
    return anim


def ensure_opacity_effect(widget: QWidget) -> QGraphicsOpacityEffect:
    effect = widget.graphicsEffect()
    if isinstance(effect, QGraphicsOpacityEffect):
        return effect
    opacity = QGraphicsOpacityEffect(widget)
    opacity.setOpacity(1.0)
    widget.setGraphicsEffect(opacity)
    return opacity


def animate_opacity(widget: QWidget, *, start: float, end: float, duration_ms: int = DEFAULT_DURATION_MS) -> QPropertyAnimation:
    if not _animations_enabled():
        return _noop_animation(widget)
    effect = ensure_opacity_effect(widget)
    anim = QPropertyAnimation(effect, b"opacity", widget)
    anim.setDuration(max(80, int(duration_ms)))
    anim.setEasingCurve(EASE_SOFT)
    anim.setStartValue(float(start))
    anim.setEndValue(float(end))
    anim.start()
    return anim


def animate_geometry(widget: QWidget, *, start: QPoint, end: QPoint, duration_ms: int = DEFAULT_DURATION_MS) -> QPropertyAnimation:
    anim = QPropertyAnimation(widget, b"pos", widget)
    anim.setDuration(max(90, int(duration_ms)))
    anim.setEasingCurve(EASE_OUT)
    anim.setStartValue(start)
    anim.setEndValue(end)
    anim.start()
    return anim


def animate_slide_fade(
    widget: QWidget,
    *,
    start_pos: QPoint,
    end_pos: QPoint,
    start_opacity: float,
    end_opacity: float,
    duration_ms: int = DEFAULT_DURATION_MS,
) -> QParallelAnimationGroup:
    if not _animations_enabled():
        widget.move(end_pos)
        group = QParallelAnimationGroup(widget)
        group.start()
        return group
    group = QParallelAnimationGroup(widget)
    pos = QPropertyAnimation(widget, b"pos", group)
    pos.setDuration(max(90, int(duration_ms)))
    pos.setEasingCurve(EASE_OUT)
    pos.setStartValue(start_pos)
    pos.setEndValue(end_pos)
    opacity_effect = ensure_opacity_effect(widget)
    fade = QPropertyAnimation(opacity_effect, b"opacity", group)
    fade.setDuration(max(90, int(duration_ms)))
    fade.setEasingCurve(EASE_SOFT)
    fade.setStartValue(float(start_opacity))
    fade.setEndValue(float(end_opacity))
    group.addAnimation(pos)
    group.addAnimation(fade)
    group.start()
    return group


@dataclass
class HoverLiftConfig:
    lift_px: int = 1
    duration_ms: int = 130


class HoverLiftMixin:
    _hover_lift_anim: QPropertyAnimation | None = None
    _hover_origin: QPoint | None = None
    _hover_config = HoverLiftConfig()

    def _animate_hover(self, entering: bool) -> None:
        if not isinstance(self, QWidget):
            return
        widget = self
        if self._hover_origin is None:
            self._hover_origin = widget.pos()
        start = widget.pos()
        target = self._hover_origin if not entering else QPoint(self._hover_origin.x(), self._hover_origin.y() - self._hover_config.lift_px)
        if self._hover_lift_anim is None:
            self._hover_lift_anim = QPropertyAnimation(widget, b"pos", widget)
        self._hover_lift_anim.stop()
        self._hover_lift_anim.setDuration(self._hover_config.duration_ms)
        self._hover_lift_anim.setEasingCurve(EASE_SOFT)
        self._hover_lift_anim.setStartValue(start)
        self._hover_lift_anim.setEndValue(target)
        self._hover_lift_anim.start()

    def enterEvent(self, event: QEnterEvent) -> None:  # type: ignore[override]
        self._animate_hover(True)
        super().enterEvent(event)  # type: ignore[misc]

    def leaveEvent(self, event) -> None:  # type: ignore[override]
        self._animate_hover(False)
        super().leaveEvent(event)  # type: ignore[misc]
