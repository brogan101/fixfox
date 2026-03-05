from __future__ import annotations

from typing import Callable

from PySide6.QtCore import Qt
from PySide6.QtGui import QPixmap
from PySide6.QtWidgets import (
    QComboBox,
    QDialog,
    QHBoxLayout,
    QLabel,
    QPushButton,
    QStackedWidget,
    QVBoxLayout,
    QWidget,
)

from ...core.brand import APP_DISPLAY_NAME
from ...core.utils import resource_path
from ..style import spacing


class OnboardingFlow(QDialog):
    def __init__(
        self,
        parent=None,
        theme_mode: str = "light",
        density: str = "comfortable",
        ui_mode: str = "basic",
        apply_preferences: Callable[[str, str, str], None] | None = None,
        can_resume_session: bool = True,
    ) -> None:
        super().__init__(parent)
        self.setObjectName("OnboardingFlow")
        self.setWindowTitle(f"Welcome to {APP_DISPLAY_NAME}")
        self.setModal(True)
        self.resize(760, 460)

        self.apply_preferences = apply_preferences
        self.result_action = "none"
        self._completed = False

        root = QVBoxLayout(self)
        root.setContentsMargins(spacing("lg"), spacing("md"), spacing("lg"), spacing("md"))
        root.setSpacing(spacing("md"))

        self.stack = QStackedWidget()
        root.addWidget(self.stack, 1)

        self.step1 = self._build_step1()
        self.step2, self.mode_combo, self.density_combo, self.ui_mode_combo = self._build_step2(theme_mode, density, ui_mode)
        self.step3 = self._build_step3(can_resume_session)
        self.stack.addWidget(self.step1)
        self.stack.addWidget(self.step2)
        self.stack.addWidget(self.step3)

        controls = QHBoxLayout()
        controls.setContentsMargins(0, 0, 0, 0)
        controls.setSpacing(spacing("sm"))
        self.back_btn = QPushButton("Back")
        self.next_btn = QPushButton("Next")
        self.skip_btn = QPushButton("Skip")
        self.finish_btn = QPushButton("Finish")
        self.finish_btn.setEnabled(False)
        controls.addWidget(self.back_btn, 0)
        controls.addWidget(self.next_btn, 0)
        controls.addStretch(1)
        controls.addWidget(self.skip_btn, 0)
        controls.addWidget(self.finish_btn, 0)
        root.addLayout(controls)

        self.back_btn.clicked.connect(self._go_back)
        self.next_btn.clicked.connect(self._go_next)
        self.skip_btn.clicked.connect(self._skip)
        self.finish_btn.clicked.connect(self._finish)
        self.mode_combo.currentTextChanged.connect(self._apply_live_preferences)
        self.density_combo.currentTextChanged.connect(self._apply_live_preferences)
        self.ui_mode_combo.currentTextChanged.connect(self._apply_live_preferences)
        self._sync_nav_buttons()

    def _build_step1(self) -> QWidget:
        page = QWidget()
        layout = QVBoxLayout(page)
        layout.setContentsMargins(spacing("md"), spacing("sm"), spacing("md"), spacing("sm"))
        layout.setSpacing(spacing("md"))

        mark = QLabel()
        pix = QPixmap(resource_path("assets/branding/fixfox_mark.svg"))
        if not pix.isNull():
            mark.setPixmap(pix.scaled(54, 54, Qt.KeepAspectRatio, Qt.SmoothTransformation))
        mark.setAlignment(Qt.AlignLeft)

        title = QLabel("Welcome to Fix Fox")
        title.setObjectName("Title")
        copy = QLabel("Runs locally on your machine.\nNo telemetry, no cloud sync.")
        copy.setWordWrap(True)
        copy.setObjectName("SubTitle")

        start = QPushButton("Start")
        start.clicked.connect(lambda: self.stack.setCurrentIndex(1))

        layout.addWidget(mark, 0)
        layout.addWidget(title, 0)
        layout.addWidget(copy, 0)
        layout.addWidget(start, 0, Qt.AlignLeft)
        layout.addStretch(1)
        return page

    def _build_step2(self, theme_mode: str, density: str, ui_mode: str) -> tuple[QWidget, QComboBox, QComboBox, QComboBox]:
        page = QWidget()
        layout = QVBoxLayout(page)
        layout.setContentsMargins(spacing("md"), spacing("sm"), spacing("md"), spacing("sm"))
        layout.setSpacing(spacing("md"))

        title = QLabel("Preferences")
        title.setObjectName("Title")
        subtitle = QLabel("Pick essentials now. You can change these later in Settings.")
        subtitle.setWordWrap(True)
        subtitle.setObjectName("SubTitle")

        mode_combo = QComboBox()
        mode_combo.addItems(["light", "dark"])
        mode_combo.setCurrentText("dark" if str(theme_mode).lower() == "dark" else "light")

        density_combo = QComboBox()
        density_combo.addItems(["comfortable", "compact"])
        density_combo.setCurrentText("compact" if str(density).lower() == "compact" else "comfortable")

        ui_mode_combo = QComboBox()
        ui_mode_combo.addItems(["basic", "pro"])
        ui_mode_combo.setCurrentText("pro" if str(ui_mode).lower() == "pro" else "basic")

        mode_note = QLabel("Basic is guided; Pro exposes advanced tools in overflow and sections.")
        mode_note.setWordWrap(True)
        mode_note.setObjectName("SubTitle")

        layout.addWidget(title)
        layout.addWidget(subtitle)
        layout.addWidget(QLabel("Theme"))
        layout.addWidget(mode_combo)
        layout.addWidget(QLabel("Density"))
        layout.addWidget(density_combo)
        layout.addWidget(QLabel("Mode"))
        layout.addWidget(ui_mode_combo)
        layout.addWidget(mode_note)
        layout.addStretch(1)
        return page, mode_combo, density_combo, ui_mode_combo

    def _build_step3(self, can_resume_session: bool) -> QWidget:
        page = QWidget()
        layout = QVBoxLayout(page)
        layout.setContentsMargins(spacing("md"), spacing("sm"), spacing("md"), spacing("sm"))
        layout.setSpacing(spacing("md"))

        title = QLabel("First action")
        title.setObjectName("Title")
        subtitle = QLabel("Start with a quick check or go to settings.")
        subtitle.setObjectName("SubTitle")

        quick_btn = QPushButton("Run Quick Check")
        settings_btn = QPushButton("Open Settings")
        resume_btn = QPushButton("Import/Resume Session")
        resume_btn.setEnabled(can_resume_session)

        quick_btn.clicked.connect(lambda: self._complete_with_action("quick_check"))
        settings_btn.clicked.connect(lambda: self._complete_with_action("settings"))
        resume_btn.clicked.connect(lambda: self._complete_with_action("resume"))

        layout.addWidget(title)
        layout.addWidget(subtitle)
        layout.addWidget(quick_btn, 0, Qt.AlignLeft)
        layout.addWidget(settings_btn, 0, Qt.AlignLeft)
        layout.addWidget(resume_btn, 0, Qt.AlignLeft)
        layout.addStretch(1)
        return page

    def _go_back(self) -> None:
        self.stack.setCurrentIndex(max(0, self.stack.currentIndex() - 1))
        self._sync_nav_buttons()

    def _go_next(self) -> None:
        self.stack.setCurrentIndex(min(2, self.stack.currentIndex() + 1))
        self._sync_nav_buttons()

    def _skip(self) -> None:
        self.result_action = "none"
        self._completed = True
        self.accept()

    def _finish(self) -> None:
        self.result_action = "none"
        self._completed = True
        self.accept()

    def _complete_with_action(self, action: str) -> None:
        self.result_action = action
        self._completed = True
        self.accept()

    def _sync_nav_buttons(self) -> None:
        index = self.stack.currentIndex()
        self.back_btn.setEnabled(index > 0)
        self.next_btn.setEnabled(index < 2)
        self.finish_btn.setEnabled(index == 2)

    def _apply_live_preferences(self) -> None:
        if self.apply_preferences is None:
            return
        self.apply_preferences(
            self.mode_combo.currentText().strip().lower(),
            self.density_combo.currentText().strip().lower(),
            self.ui_mode_combo.currentText().strip().lower(),
        )

    @property
    def completed(self) -> bool:
        return self._completed
