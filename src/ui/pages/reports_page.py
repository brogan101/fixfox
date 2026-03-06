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
        details_btn = SoftButton("Details")
        details_btn.clicked.connect(lambda: w._set_concierge_collapsed(False, persist=True))
        layout.addWidget(
            build_page_header(
                "Reports",
                "Review the active case, validate masking, and build a support-ready export bundle.",
                help_text="Use the 3-step flow: configure, preview evidence/redaction, then create and validate the bundle.",
                on_help=w._show_page_help,
                cta=details_btn,
            )
        )
        w.rep_callout = InlineCallout("Reports", "", level="warn", density=w.settings_state.density)
        layout.addWidget(w.rep_callout)

        overview = QWidget()
        overview_layout = QVBoxLayout(overview)
        overview_layout.setContentsMargins(0, 0, 0, 0)
        overview_layout.setSpacing(spacing("md"))
        overview_row = QWidget()
        overview_row_layout = QVBoxLayout(overview_row)
        overview_row_layout.setContentsMargins(0, 0, 0, 0)
        overview_row_layout.setSpacing(spacing("md"))
        w.rep_session_summary = Card("Active Session", "No active session loaded yet.")
        w.rep_session_summary.body_layout().addWidget(QLabel("Load or create a session to unlock live bundle validation."))
        w.rep_issue_summary = Card("Issue-family Reporting", "No issue-family context selected yet.")
        w.rep_issue_summary.body_layout().addWidget(QLabel("When you pick an issue in Playbooks or Fixes, Reports summarizes mapped playbooks, evidence plans, and escalation posture here."))
        w.rep_handoff_summary = Card("Handoff Checklist", "Masking preview, evidence review, validation, and summary copy all live here.")
        w.rep_handoff_summary.body_layout().addWidget(QLabel("Use Reports as the final review station before sharing anything externally."))
        overview_row_layout.addWidget(w.rep_session_summary)
        overview_row_layout.addWidget(w.rep_issue_summary)
        overview_row_layout.addWidget(w.rep_handoff_summary)
        overview_layout.addWidget(overview_row)
        layout.addWidget(overview)

        export_flow = Card(
            "Support Bundle Flow",
            "1) Choose session  2) Choose bundle type  3) Configure masking  4) Create and validate",
        )
        export_flow.body_layout().addWidget(QLabel("Reports stays useful even before a session exists: configure defaults, review the checklist, then generate once a case is loaded."))
        layout.addWidget(export_flow)

        w.rep_empty_state = EmptyState(
            "No active session yet",
            "Start from Home or reopen a case from History, then return here to review masking, validate evidence, and export.",
            icon="!",
        )
        layout.addWidget(w.rep_empty_state)

        w.rep_steps = QTabWidget()
        w.rep_steps.setDocumentMode(True)

        step1 = QWidget()
        step1_layout = QVBoxLayout(step1)
        step1_layout.setContentsMargins(0, 0, 0, 0)
        step1_layout.setSpacing(spacing("md"))
        step1_layout.addWidget(Card("Step 1: Choose Session", "Open a session from History, then return to Reports."))
        open_history = SoftButton("Open History")
        open_history.clicked.connect(lambda: w.nav.setCurrentRow(w.NAV_ITEMS.index("History")))
        step1_layout.addWidget(open_history)
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
        step1_layout.addWidget(Card("Step 2: Choose Bundle Type", "Pick support bundle policy and masking defaults.", right_widget=w.rep_preset))
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
        redaction_card = Card("Step 3: Configure Masking", "Before/after masking preview with token map.")
        w.rep_token_map = QLabel("Token map: PC_1 / USER_1 / SSID_1")
        w.rep_token_map.setWordWrap(True)
        redaction_card.body_layout().addWidget(w.rep_preview)
        redaction_card.body_layout().addWidget(w.rep_token_map)
        w.rep_tree = QTreeWidget()
        w.rep_tree.setHeaderLabels(["Bundle Preview", "Value"])
        w.rep_tree.currentItemChanged.connect(lambda item, _prev: w._on_report_tree_item_selected(item))
        tree_card = Card("Bundle Tree Preview", "Review what will be included.")
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
        w.rep_generate = PrimaryButton("Create Support Bundle")
        w.rep_generate.clicked.connect(w.export_current_session)
        w.rep_generate_override = SoftButton("Create Bundle (Allow Warnings)")
        w.rep_generate_override.clicked.connect(w.export_current_session_allow_warnings)
        w.rep_status = Card("Step 4: Create and Validate", "No support bundle yet.")
        w.rep_actions = Card("Post-bundle Actions", "Available after the bundle is created.")
        open_folder = SoftButton("Open Bundle Folder")
        open_folder.clicked.connect(w.open_last_export_folder)
        copy_path = SoftButton("Copy Bundle Path")
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

        w.rep_steps.addTab(step1, "1-2. Session + Type")
        w.rep_steps.addTab(step2, "3. Masking + Preview")
        w.rep_steps.addTab(step3, "4. Generate")
        layout.addWidget(w.rep_steps, 1)
        w._sync_reports_empty_state()
