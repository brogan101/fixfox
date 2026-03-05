from __future__ import annotations

from typing import Callable

from PySide6.QtCore import Qt
from PySide6.QtGui import QPixmap
from PySide6.QtWidgets import QComboBox, QDialog, QHBoxLayout, QLabel, QStackedWidget, QVBoxLayout, QWidget

from ...core.brand import APP_DISPLAY_NAME
from ...core.utils import resource_path
from ..style import spacing
from ..widgets import Card, PrimaryButton, SoftButton


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
        self.resize(820, 520)

        self.apply_preferences = apply_preferences
        self.result_action = "none"
        self._completed = False

        root = QVBoxLayout(self)
        root.setContentsMargins(spacing("lg"), spacing("md"), spacing("lg"), spacing("md"))
        root.setSpacing(spacing("md"))

        header = Card("Welcome", "Set essentials in under a minute.", object_name="OnboardingCard")
        self.step_indicator = QLabel("Step 1 of 3")
        self.step_indicator.setObjectName("SubTitle")
        header.body_layout().addWidget(self.step_indicator)
        root.addWidget(header, 0)

        self.stack = QStackedWidget()
        self.stack.setObjectName("OnboardingStack")
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
        self.back_btn = SoftButton("Back")
        self.next_btn = SoftButton("Next")
        self.skip_btn = SoftButton("Skip")
        self.finish_btn = PrimaryButton("Finish")
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

        card = Card("Welcome to Fix Fox", "Local-first desktop diagnostics and support workflows.", object_name="OnboardingCard")
        mark = QLabel()
        mark.setObjectName("BrandMark")
        pix = QPixmap(resource_path("assets/branding/fixfox_mark.svg"))
        if not pix.isNull():
            mark.setPixmap(pix.scaled(54, 54, Qt.KeepAspectRatio, Qt.SmoothTransformation))
        copy = QLabel("Runs locally on this machine. No telemetry and no cloud sync.")
        copy.setObjectName("SubTitle")
        copy.setWordWrap(True)
        start = PrimaryButton("Start")
        start.clicked.connect(lambda: self.stack.setCurrentIndex(1))
        card.body_layout().addWidget(mark, 0, Qt.AlignLeft)
        card.body_layout().addWidget(copy)
        card.body_layout().addWidget(start, 0, Qt.AlignLeft)
        layout.addWidget(card)
        layout.addStretch(1)
        return page

    def _build_step2(self, theme_mode: str, density: str, ui_mode: str) -> tuple[QWidget, QComboBox, QComboBox, QComboBox]:
        page = QWidget()
        layout = QVBoxLayout(page)
        layout.setContentsMargins(spacing("md"), spacing("sm"), spacing("md"), spacing("sm"))
        layout.setSpacing(spacing("md"))

        card = Card("Preferences", "Theme, density, and mode update live.", object_name="OnboardingCard")
        mode_combo = QComboBox()
        mode_combo.addItems(["light", "dark"])
        mode_combo.setCurrentText("dark" if str(theme_mode).lower() == "dark" else "light")

        density_combo = QComboBox()
        density_combo.addItems(["comfortable", "compact"])
        density_combo.setCurrentText("compact" if str(density).lower() == "compact" else "comfortable")

        ui_mode_combo = QComboBox()
        ui_mode_combo.addItems(["basic", "pro"])
        ui_mode_combo.setCurrentText("pro" if str(ui_mode).lower() == "pro" else "basic")

        mode_note = QLabel("Basic is guided. Pro exposes advanced tools in overflow and expanded sections.")
        mode_note.setWordWrap(True)
        mode_note.setObjectName("SubTitle")

        card.body_layout().addWidget(QLabel("Theme"))
        card.body_layout().addWidget(mode_combo)
        card.body_layout().addWidget(QLabel("Density"))
        card.body_layout().addWidget(density_combo)
        card.body_layout().addWidget(QLabel("Mode"))
        card.body_layout().addWidget(ui_mode_combo)
        card.body_layout().addWidget(mode_note)
        layout.addWidget(card)
        layout.addStretch(1)
        return page, mode_combo, density_combo, ui_mode_combo

    def _build_step3(self, can_resume_session: bool) -> QWidget:
        page = QWidget()
        layout = QVBoxLayout(page)
        layout.setContentsMargins(spacing("md"), spacing("sm"), spacing("md"), spacing("sm"))
        layout.setSpacing(spacing("md"))

        card = Card("First Action", "Choose where you want to begin.", object_name="OnboardingCard")
        quick_btn = PrimaryButton("Quick Check")
        settings_btn = SoftButton("Open Settings")
        resume_btn = SoftButton("Import/Resume Session")
        resume_btn.setEnabled(can_resume_session)

        quick_btn.clicked.connect(lambda: self._complete_with_action("quick_check"))
        settings_btn.clicked.connect(lambda: self._complete_with_action("settings"))
        resume_btn.clicked.connect(lambda: self._complete_with_action("resume"))

        card.body_layout().addWidget(quick_btn, 0, Qt.AlignLeft)
        card.body_layout().addWidget(settings_btn, 0, Qt.AlignLeft)
        card.body_layout().addWidget(resume_btn, 0, Qt.AlignLeft)
        layout.addWidget(card)
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
        self.step_indicator.setText(f"Step {index + 1} of 3")

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

