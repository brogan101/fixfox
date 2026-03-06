from __future__ import annotations

from typing import Callable

from PySide6.QtCore import Qt
from PySide6.QtGui import QPixmap
from PySide6.QtWidgets import QComboBox, QDialog, QGridLayout, QHBoxLayout, QLabel, QStackedWidget, QVBoxLayout, QWidget

from ...core.brand import APP_DISPLAY_NAME
from ...core.utils import resource_path
from ..icons import get_icon
from .stepper import StepIndicator
from ..style import spacing
from ..widgets import Card, PrimaryButton, SoftButton


class OnboardingFlow(QDialog):
    _STEP_TITLES = ("Welcome", "Preferences", "First Action", "Finish")

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
        self.resize(900, 560)

        self.apply_preferences = apply_preferences
        self.result_action = "none"
        self._completed = False

        root = QVBoxLayout(self)
        root.setContentsMargins(spacing("lg"), spacing("md"), spacing("lg"), spacing("md"))
        root.setSpacing(spacing("md"))

        header = Card("Welcome", "Set essentials in under a minute.", object_name="OnboardingCard")
        self.stepper = StepIndicator(self._STEP_TITLES, self)
        header.body_layout().addWidget(self.stepper)
        root.addWidget(header, 0)

        self.stack = QStackedWidget()
        self.stack.setObjectName("OnboardingStack")
        root.addWidget(self.stack, 1)

        self.step1 = self._build_step1()
        self.step2, self.mode_combo, self.density_combo, self.ui_mode_combo = self._build_step2(theme_mode, density, ui_mode)
        self.step3 = self._build_step3(can_resume_session)
        self.step4 = self._build_step4()
        self.stack.addWidget(self.step1)
        self.stack.addWidget(self.step2)
        self.stack.addWidget(self.step3)
        self.stack.addWidget(self.step4)

        controls = QHBoxLayout()
        controls.setContentsMargins(0, 0, 0, 0)
        controls.setSpacing(spacing("sm"))
        self.back_btn = SoftButton("Back")
        self.next_btn = SoftButton("Next")
        self.skip_btn = SoftButton("Skip")
        self.finish_btn = PrimaryButton("Finish")
        self.back_btn.setIcon(get_icon("back", self.back_btn))
        self.next_btn.setIcon(get_icon("next", self.next_btn))
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
        pix = QPixmap(resource_path("assets/brand/fixfox_mark.png")).scaled(60, 60, Qt.KeepAspectRatio, Qt.SmoothTransformation)
        if not pix.isNull():
            mark.setPixmap(pix)
        copy = QLabel("Runs locally on this machine. No telemetry and no cloud sync.")
        copy.setObjectName("SubTitle")
        copy.setWordWrap(True)
        start = PrimaryButton("Start Setup")
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
        form = QGridLayout()
        form.setContentsMargins(0, 0, 0, 0)
        form.setSpacing(spacing("sm"))

        mode_combo = QComboBox()
        mode_combo.addItems(["light", "dark"])
        mode_combo.setCurrentText("dark" if str(theme_mode).lower() == "dark" else "light")

        density_combo = QComboBox()
        density_combo.addItems(["comfortable", "compact"])
        density_combo.setCurrentText("compact" if str(density).lower() == "compact" else "comfortable")

        ui_mode_combo = QComboBox()
        ui_mode_combo.addItems(["basic", "pro"])
        ui_mode_combo.setCurrentText("pro" if str(ui_mode).lower() == "pro" else "basic")

        form.addWidget(QLabel("Theme"), 0, 0)
        form.addWidget(mode_combo, 0, 1)
        form.addWidget(QLabel("Density"), 1, 0)
        form.addWidget(density_combo, 1, 1)
        form.addWidget(QLabel("UI Mode"), 2, 0)
        form.addWidget(ui_mode_combo, 2, 1)

        card.body_layout().addLayout(form)
        card.body_layout().addWidget(QLabel("Basic is guided. Pro exposes the full tool and runbook catalog."))
        layout.addWidget(card)
        layout.addStretch(1)
        return page, mode_combo, density_combo, ui_mode_combo

    def _build_step3(self, can_resume_session: bool) -> QWidget:
        page = QWidget()
        layout = QVBoxLayout(page)
        layout.setContentsMargins(spacing("md"), spacing("sm"), spacing("md"), spacing("sm"))
        layout.setSpacing(spacing("md"))

        card = Card("First Action", "Choose where you want to begin.", object_name="OnboardingCard")
        grid = QGridLayout()
        grid.setContentsMargins(0, 0, 0, 0)
        grid.setSpacing(spacing("sm"))

        quick_btn = PrimaryButton("Quick Check")
        settings_btn = SoftButton("Open Settings")
        resume_btn = SoftButton("Resume Session")
        resume_btn.setEnabled(can_resume_session)

        quick_btn.clicked.connect(lambda: self._complete_with_action("quick_check"))
        settings_btn.clicked.connect(lambda: self._complete_with_action("settings"))
        resume_btn.clicked.connect(lambda: self._complete_with_action("resume"))

        grid.addWidget(self._action_card("Quick Check", "Run a safe first diagnostic scan.", quick_btn, "run"), 0, 0)
        grid.addWidget(self._action_card("Open Settings", "Fine-tune safety and appearance options.", settings_btn, "settings"), 0, 1)
        grid.addWidget(self._action_card("Resume Session", "Return to a previous support case.", resume_btn, "history"), 1, 0, 1, 2)

        card.body_layout().addLayout(grid)
        layout.addWidget(card)
        layout.addStretch(1)
        return page

    def _build_step4(self) -> QWidget:
        page = QWidget()
        layout = QVBoxLayout(page)
        layout.setContentsMargins(spacing("md"), spacing("sm"), spacing("md"), spacing("sm"))
        layout.setSpacing(spacing("md"))
        card = Card("Finish", "You are ready to start.", object_name="OnboardingCard")
        card.body_layout().addWidget(QLabel("Preferences are already applied. Click Finish to continue."))
        layout.addWidget(card)
        layout.addStretch(1)
        return page

    def _action_card(self, title: str, subtitle: str, button: QWidget, icon_name: str) -> QWidget:
        card = Card(title, subtitle)
        icon = QLabel()
        icon.setPixmap(get_icon(icon_name, card, size=20).pixmap(20, 20))
        icon.setFixedSize(20, 20)
        card.body_layout().addWidget(icon, 0, Qt.AlignLeft)
        card.body_layout().addWidget(button, 0, Qt.AlignLeft)
        return card

    def _go_back(self) -> None:
        self.stack.setCurrentIndex(max(0, self.stack.currentIndex() - 1))
        self._sync_nav_buttons()

    def _go_next(self) -> None:
        self.stack.setCurrentIndex(min(3, self.stack.currentIndex() + 1))
        self._sync_nav_buttons()

    def _skip(self) -> None:
        self.result_action = "none"
        self._completed = True
        self.accept()

    def _finish(self) -> None:
        self.result_action = self.result_action or "none"
        self._completed = True
        self.accept()

    def _complete_with_action(self, action: str) -> None:
        self.result_action = action
        self.stack.setCurrentIndex(3)
        self._sync_nav_buttons()

    def _sync_nav_buttons(self) -> None:
        index = self.stack.currentIndex()
        self.back_btn.setEnabled(index > 0)
        self.next_btn.setEnabled(index < 3)
        self.finish_btn.setEnabled(index == 3)
        self.stepper.set_current_step(index)

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
