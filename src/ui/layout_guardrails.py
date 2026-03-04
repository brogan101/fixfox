from __future__ import annotations

from dataclasses import dataclass

from PySide6.QtCore import QPoint, QRect, QSize, Qt
from PySide6.QtGui import QColor, QPainter, QPen
from PySide6.QtWidgets import QPushButton, QWidget


MIN_WINDOW_SIZE = QSize(1100, 700)
MIN_RIGHT_PANEL_WIDTH = 260
MIN_NAV_WIDTH = 210
RIGHT_PANEL_COLLAPSE_WIDTH = 1240

MIN_BUTTON_SIZE_BY_DENSITY: dict[str, QSize] = {
    "comfortable": QSize(110, 34),
    "compact": QSize(96, 30),
}

MIN_ROW_HEIGHT_BY_DENSITY: dict[str, int] = {
    "comfortable": 64,
    "compact": 56,
}


@dataclass(frozen=True)
class GuardrailConfig:
    min_window_size: QSize = MIN_WINDOW_SIZE
    min_right_panel_width: int = MIN_RIGHT_PANEL_WIDTH
    min_nav_width: int = MIN_NAV_WIDTH
    collapse_threshold_width: int = RIGHT_PANEL_COLLAPSE_WIDTH


def min_button_size(density: str) -> QSize:
    return MIN_BUTTON_SIZE_BY_DENSITY.get(density, MIN_BUTTON_SIZE_BY_DENSITY["comfortable"])


def min_row_height(density: str) -> int:
    return MIN_ROW_HEIGHT_BY_DENSITY.get(density, MIN_ROW_HEIGHT_BY_DENSITY["comfortable"])


def should_auto_collapse_right_panel(width: int, threshold_width: int = RIGHT_PANEL_COLLAPSE_WIDTH) -> bool:
    return int(width) < int(threshold_width)


def apply_button_guardrails(root: QWidget, density: str) -> None:
    button_size = min_button_size(density)
    for button in root.findChildren(QPushButton):
        button.setMinimumWidth(max(button.minimumWidth(), button_size.width()))
        button.setMinimumHeight(max(button.minimumHeight(), button_size.height()))


class LayoutDebugOverlay(QWidget):
    def __init__(self, host: QWidget) -> None:
        super().__init__(host)
        self._host = host
        self.setAttribute(Qt.WA_TransparentForMouseEvents, True)
        self.setAttribute(Qt.WA_NoSystemBackground, True)
        self.hide()

    def sync_geometry(self) -> None:
        self.setGeometry(self._host.rect())
        self.raise_()
        self.update()

    def paintEvent(self, _event) -> None:  # type: ignore[override]
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing, False)
        host_rect = self.rect()
        for widget in self._host.findChildren(QWidget):
            if widget is self or not widget.isVisible():
                continue
            global_top_left = widget.mapToGlobal(QPoint(0, 0))
            local_top_left = self.mapFromGlobal(global_top_left)
            rect = QRect(local_top_left, widget.size())
            if rect.width() < 6 or rect.height() < 6:
                continue
            clipped = not host_rect.contains(rect)
            color = QColor(220, 80, 80, 180) if clipped else QColor(80, 170, 255, 110)
            pen = QPen(color)
            pen.setWidth(1 if not clipped else 2)
            painter.setPen(pen)
            painter.drawRect(rect)
        painter.end()
