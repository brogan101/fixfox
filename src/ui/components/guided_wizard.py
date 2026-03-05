from __future__ import annotations

from dataclasses import dataclass
from typing import Callable

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import (
    QHBoxLayout,
    QLabel,
    QListWidget,
    QListWidgetItem,
    QPushButton,
    QTextEdit,
    QVBoxLayout,
    QWidget,
)

from ..widgets import Card, PrimaryButton, SoftButton
from ..style import spacing


@dataclass(frozen=True)
class GuidedStep:
    id: str
    title: str
    action_label: str
    details: str


class GuidedFixWizard(Card):
    step_action_requested = Signal(str)
    finished_requested = Signal()

    def __init__(self, title: str = "Guided Fix Wizard") -> None:
        super().__init__(title, "One concrete action per step with evidence capture.")
        self.setObjectName("GuidedFixWizard")
        self._steps: list[GuidedStep] = []
        self._action_callbacks: dict[str, Callable[[], None]] = {}

        root = QHBoxLayout()
        root.setContentsMargins(0, 0, 0, 0)
        root.setSpacing(spacing("md"))

        self.step_list = QListWidget()
        self.step_list.setObjectName("GuidedWizardStepList")
        self.step_list.currentRowChanged.connect(self._sync_step_detail)
        root.addWidget(self.step_list, 1)

        right = QWidget()
        right_layout = QVBoxLayout(right)
        right_layout.setContentsMargins(0, 0, 0, 0)
        right_layout.setSpacing(spacing("sm"))
        self.step_title = QLabel("Select a step")
        self.step_title.setObjectName("GuidedWizardStepTitle")
        self.step_title.setWordWrap(True)
        self.step_body = QLabel("Choose a step on the left to see the exact action.")
        self.step_body.setWordWrap(True)
        self.step_body.setObjectName("GuidedWizardStepBody")
        self.step_action = PrimaryButton("Run Step Action")
        self.step_action.clicked.connect(self._run_current_step_action)
        self.evidence_notes = QTextEdit()
        self.evidence_notes.setObjectName("GuidedWizardEvidenceNotes")
        self.evidence_notes.setPlaceholderText("Step evidence notes")
        self.evidence_notes.setMinimumHeight(96)
        finish_row = QHBoxLayout()
        self.btn_generate_pack = SoftButton("Generate Support Pack")
        self.btn_copy_summary = SoftButton("Copy Summary")
        self.btn_finish = QPushButton("Finish Wizard")
        self.btn_finish.setObjectName("PrimaryButton")
        self.btn_finish.clicked.connect(self.finished_requested.emit)
        finish_row.addWidget(self.btn_generate_pack)
        finish_row.addWidget(self.btn_copy_summary)
        finish_row.addStretch(1)
        finish_row.addWidget(self.btn_finish)
        right_layout.addWidget(self.step_title)
        right_layout.addWidget(self.step_body)
        right_layout.addWidget(self.step_action, 0, Qt.AlignLeft)
        right_layout.addWidget(self.evidence_notes, 1)
        right_layout.addLayout(finish_row)
        root.addWidget(right, 2)

        self.body_layout().addLayout(root)

    def set_steps(self, steps: list[GuidedStep], callbacks: dict[str, Callable[[], None]] | None = None) -> None:
        self._steps = list(steps)
        self._action_callbacks = callbacks or {}
        self.step_list.clear()
        for idx, step in enumerate(self._steps, start=1):
            item = QListWidgetItem(f"{idx}. {step.title}")
            item.setData(Qt.UserRole, step.id)
            self.step_list.addItem(item)
        if self.step_list.count():
            self.step_list.setCurrentRow(0)
        else:
            self.step_title.setText("No steps configured")
            self.step_body.setText("Add steps to enable guided actions.")
            self.step_action.setEnabled(False)

    def current_step_id(self) -> str:
        item = self.step_list.currentItem()
        if item is None:
            return ""
        return str(item.data(Qt.UserRole) or "").strip()

    def _sync_step_detail(self, row: int) -> None:
        if row < 0 or row >= len(self._steps):
            self.step_title.setText("Select a step")
            self.step_body.setText("Choose a step on the left to see the exact action.")
            self.step_action.setEnabled(False)
            return
        step = self._steps[row]
        self.step_title.setText(step.title)
        self.step_body.setText(step.details)
        self.step_action.setText(step.action_label or "Run Step Action")
        self.step_action.setEnabled(True)

    def _run_current_step_action(self) -> None:
        sid = self.current_step_id()
        if not sid:
            return
        callback = self._action_callbacks.get(sid)
        if callback is not None:
            callback()
        self.step_action_requested.emit(sid)

