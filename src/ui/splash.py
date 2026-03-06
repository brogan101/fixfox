from __future__ import annotations

from pathlib import Path

from PySide6.QtCore import Qt, QRectF
from PySide6.QtGui import QColor, QFont, QGuiApplication, QPainter, QPainterPath, QPen, QPixmap
from PySide6.QtWidgets import QSplashScreen

from ..core.brand import APP_DISPLAY_NAME, APP_TAGLINE
from ..core.utils import resource_path


def build_splash_pixmap(*, width: int = 620, height: int = 340, status_text: str = "Loading workspace...") -> QPixmap:
    pixmap = QPixmap(width, height)
    pixmap.fill(Qt.transparent)
    painter = QPainter(pixmap)
    painter.setRenderHint(QPainter.Antialiasing, True)
    painter.setRenderHint(QPainter.SmoothPixmapTransform, True)

    outer = QRectF(0.5, 0.5, width - 1.0, height - 1.0)
    card = QPainterPath()
    card.addRoundedRect(outer, 28.0, 28.0)
    painter.fillPath(card, QColor("#F6F1E8"))
    painter.setPen(QPen(QColor("#E6D7C2"), 1.2))
    painter.drawPath(card)

    painter.fillRect(0, 0, width, 120, QColor("#1E2735"))
    painter.fillRect(0, 120, width, height - 120, QColor("#FBF7F1"))

    painter.setPen(QColor("#FEA643"))
    painter.setBrush(QColor("#FEA643"))
    painter.drawEllipse(46, 48, 14, 14)
    painter.drawEllipse(66, 48, 14, 14)
    painter.drawEllipse(86, 48, 14, 14)

    brand_path = Path(resource_path("assets/brand/fixfox_mark.png"))
    brand = QPixmap(str(brand_path))
    if not brand.isNull():
        painter.drawPixmap(44, 132, brand.scaled(64, 64, Qt.KeepAspectRatio, Qt.SmoothTransformation))

    title_font = QFont("Segoe UI Semibold", 20)
    subtitle_font = QFont("Segoe UI", 10)
    status_font = QFont("Segoe UI Semibold", 10)

    painter.setPen(QColor("#FFFFFF"))
    painter.setFont(title_font)
    painter.drawText(44, 88, APP_DISPLAY_NAME)
    painter.setFont(subtitle_font)
    painter.setPen(QColor("#DCE4F0"))
    painter.drawText(44, 108, APP_TAGLINE)

    painter.setPen(QColor("#223043"))
    painter.setFont(QFont("Segoe UI Semibold", 18))
    painter.drawText(124, 166, "Launching the real workspace")
    painter.setFont(subtitle_font)
    painter.setPen(QColor("#586779"))
    painter.drawText(124, 190, "Loading shell, search index, diagnostics context, and saved settings.")

    bar_rect = QRectF(44.0, 246.0, width - 88.0, 16.0)
    painter.setBrush(QColor("#E4D7C9"))
    painter.setPen(Qt.NoPen)
    painter.drawRoundedRect(bar_rect, 8.0, 8.0)
    fill_rect = QRectF(bar_rect.x(), bar_rect.y(), bar_rect.width() * 0.42, bar_rect.height())
    painter.setBrush(QColor("#FEA643"))
    painter.drawRoundedRect(fill_rect, 8.0, 8.0)

    painter.setFont(status_font)
    painter.setPen(QColor("#223043"))
    painter.drawText(44, 292, status_text)
    painter.setFont(subtitle_font)
    painter.setPen(QColor("#7A8695"))
    painter.drawText(44, 314, "No onboarding. No blank shell. Just a direct launch into FixFox.")
    painter.end()
    return pixmap


class FixFoxSplashScreen(QSplashScreen):
    def __init__(self, *, status_text: str = "Loading workspace...") -> None:
        super().__init__(build_splash_pixmap(status_text=status_text))
        self.setObjectName("FixFoxSplash")
        self.setWindowFlag(Qt.WindowStaysOnTopHint, True)
        self.setEnabled(False)

    def update_status(self, text: str) -> None:
        self.setPixmap(build_splash_pixmap(status_text=text))
        self.show()
        app = QGuiApplication.instance()
        if app is not None:
            app.processEvents()
