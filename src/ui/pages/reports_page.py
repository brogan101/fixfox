from __future__ import annotations

from typing import Any

from PySide6.QtWidgets import QCheckBox, QComboBox, QLabel, QTabWidget, QTextEdit, QTreeWidget, QVBoxLayout, QWidget

from ...core.exporter import PRESETS
from ..components.feed_renderer import FeedRenderer
from ..style import spacing
from ..widgets import Card, EmptyState, InlineCallout, PrimaryButton, SoftButton
from .common import PageScroll, build_page_header


class ReportsPage(PageScroll):
    def __init__(self, services: Any) -> None:
        super().__init__(page_id="reports", object_name="PageReports")
        self.services = services
        self._build_ui()

    def _build_ui(self) -> None:
        w = self.services
        layout = QVBoxLayout(self.content)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(spacing("md"))
        layout.addWidget(
            build_page_header(
                "Reports",
                "Export validated packs with share-safe masking.",
                help_text="Use the 3-step flow: configure, preview evidence/redaction, then generate and validate.",
                on_help=w._show_page_help,
            )
        )
        w.rep_callout = InlineCallout("Reports", "", level="warn", density=w.settings_state.density)
        layout.addWidget(w.rep_callout)

        w.rep_empty_state = EmptyState(
            "Run a goal first",
            "Start from Home or the top app bar to generate a session, then return here to configure and export.",
            icon="!",
        )
        layout.addWidget(w.rep_empty_state)

        w.rep_steps = QTabWidget()
        w.rep_steps.setDocumentMode(True)

        step1 = QWidget()
        step1_layout = QVBoxLayout(step1)
        step1_layout.setContentsMargins(0, 0, 0, 0)
        step1_layout.setSpacing(spacing("md"))
        w.rep_preset = QComboBox()
        w.rep_preset.addItems(list(PRESETS))
        w.rep_preset.currentTextChanged.connect(w._update_context_labels)
        w.rep_safe = QCheckBox("Share-safe masking enabled")
        w.rep_safe.setChecked(w.settings_state.share_safe_default)
        w.rep_safe.stateChanged.connect(w._update_context_labels)
        w.rep_ip = QCheckBox("Mask IP addresses")
        w.rep_ip.setChecked(w.settings_state.mask_ip_default)
        w.rep_ip.stateChanged.connect(w._update_redaction_preview)
        w.rep_logs = QCheckBox("Include evidence logs")
        w.rep_logs.setChecked(False)
        w.rep_preset_hint = QLabel("Basic mode is locked to Home Share preset.")
        w.rep_preset_hint.setObjectName("SubTitle")
        w.rep_preset_hint.setWordWrap(True)
        step1_layout.addWidget(Card("Step 1: Configure", "Pick export policy and masking defaults.", right_widget=w.rep_preset))
        step1_layout.addWidget(w.rep_preset_hint)
        step1_layout.addWidget(w.rep_safe)
        step1_layout.addWidget(w.rep_ip)
        step1_layout.addWidget(w.rep_logs)
        step1_layout.addStretch(1)

        step2 = QWidget()
        step2_layout = QVBoxLayout(step2)
        step2_layout.setContentsMargins(0, 0, 0, 0)
        step2_layout.setSpacing(spacing("md"))
        w.rep_preview = QTextEdit()
        w.rep_preview.setReadOnly(True)
        w.rep_preview.setMinimumHeight(180)
        redaction_card = Card("Step 2: Redaction Preview", "Before/after masking preview with token map.")
        w.rep_token_map = QLabel("Token map: PC_1 / USER_1 / SSID_1")
        w.rep_token_map.setWordWrap(True)
        redaction_card.body_layout().addWidget(w.rep_preview)
        redaction_card.body_layout().addWidget(w.rep_token_map)
        w.rep_tree = QTreeWidget()
        w.rep_tree.setHeaderLabels(["Export Preview", "Value"])
        w.rep_tree.currentItemChanged.connect(lambda item, _prev: w._on_report_tree_item_selected(item))
        tree_card = Card("Export Tree Preview", "Review what will be included.")
        tree_card.body_layout().addWidget(w.rep_tree)
        w.rep_evidence_status = QTreeWidget()
        w.rep_evidence_status.setHeaderLabels(["Evidence", "Status", "Notes"])
        collect_now = SoftButton("Collect Now")
        collect_now.clicked.connect(w._collect_core_evidence)
        w.rep_evidence_card = Card("Evidence Checklist", "Collected/missing/optional evidence by status.", right_widget=collect_now)
        w.rep_evidence_checklist = QLabel("Included: none")
        w.rep_evidence_checklist.setWordWrap(True)
        w.rep_evidence = FeedRenderer(w._make_evidence_row, density=w.settings_state.density, empty_icon="file", empty_message="No evidence collected yet.")
        w.rep_evidence.context_requested.connect(w._evidence_menu)
        w.rep_evidence.item_activated.connect(lambda payload: w._open_evidence_path(payload))
        w.rep_evidence_card.body_layout().addWidget(w.rep_evidence_checklist)
        w.rep_evidence_card.body_layout().addWidget(w.rep_evidence_status)
        w.rep_evidence_card.body_layout().addWidget(w.rep_evidence)
        step2_layout.addWidget(redaction_card)
        step2_layout.addWidget(tree_card)
        step2_layout.addWidget(w.rep_evidence_card, 1)

        step3 = QWidget()
        step3_layout = QVBoxLayout(step3)
        step3_layout.setContentsMargins(0, 0, 0, 0)
        step3_layout.setSpacing(spacing("md"))
        w.rep_generate = PrimaryButton("Generate Export")
        w.rep_generate.clicked.connect(w.export_current_session)
        w.rep_generate_override = SoftButton("Generate (Allow Warnings)")
        w.rep_generate_override.clicked.connect(w.export_current_session_allow_warnings)
        w.rep_status = Card("Step 3: Generate and Validate", "No export yet.")
        w.rep_actions = Card("Post-export Actions", "Available after export.")
        open_folder = SoftButton("Open Report Folder")
        open_folder.clicked.connect(w.open_last_export_folder)
        copy_path = SoftButton("Copy Export Path")
        copy_path.clicked.connect(w.copy_last_export_path)
        copy_short = SoftButton("Copy Ticket Summary (Short)")
        copy_short.clicked.connect(lambda: w.copy_ticket_summary(True))
        copy_long = SoftButton("Copy Ticket Summary (Detailed)")
        copy_long.clicked.connect(lambda: w.copy_ticket_summary(False))
        for btn in (open_folder, copy_path, copy_short, copy_long):
            w.rep_actions.body_layout().addWidget(btn)
        step3_layout.addWidget(w.rep_generate)
        step3_layout.addWidget(w.rep_generate_override)
        step3_layout.addWidget(w.rep_status)
        step3_layout.addWidget(w.rep_actions, 1)

        w.rep_steps.addTab(step1, "1. Configure")
        w.rep_steps.addTab(step2, "2. Preview")
        w.rep_steps.addTab(step3, "3. Generate")
        layout.addWidget(w.rep_steps, 1)
        w._sync_reports_empty_state()
