from __future__ import annotations

from typing import Any

from PySide6.QtWidgets import QCheckBox, QComboBox, QHBoxLayout, QLineEdit, QVBoxLayout, QWidget

from ..components.feed_renderer import SkeletonLoader
from ..style import spacing
from ..widgets import Card, EmptyState, InlineCallout, PrimaryButton, SoftButton
from .common import PageScroll, build_page_header


class DiagnosePage(PageScroll):
    def __init__(self, services: Any) -> None:
        super().__init__(page_id="diagnose", object_name="PageDiagnose")
        self.services = services
        self._build_ui()

    def _build_ui(self) -> None:
        w = self.services
        layout = QHBoxLayout(self.content)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(spacing("md"))

        left = QWidget()
        left_layout = QVBoxLayout(left)
        left_layout.setContentsMargins(0, 0, 0, 0)
        left_layout.setSpacing(spacing("md"))
        left_layout.addWidget(
            build_page_header(
                "Diagnose",
                "Grouped findings feed with context and next actions.",
                help_text="Select a finding to see meaning, evidence impact, and next deterministic actions in the right detail pane.",
                on_help=w._show_page_help,
            )
        )
        w.diag_callout = InlineCallout("Diagnose", "", level="warn", density=w.settings_state.density)
        left_layout.addWidget(w.diag_callout)
        w.diag_issue_summary = Card("Issue-family Triage", "Issue-aware triage bundles, diagnostics, and next-best actions.")
        w.diag_issue_summary_text = QLineEdit()
        w.diag_issue_summary_text.setObjectName("SearchInput")
        w.diag_issue_summary_text.setPlaceholderText("Search issue families or symptoms")
        w.diag_issue_summary_text.textChanged.connect(w._refresh_support_diagnose)
        w.diag_issue_family = QComboBox()
        w.diag_issue_family.addItem("All Families")
        w.diag_issue_family.currentTextChanged.connect(w._refresh_support_diagnose)
        w.diag_issue_summary.body_layout().addWidget(w.diag_issue_summary_text)
        w.diag_issue_summary.body_layout().addWidget(w.diag_issue_family)
        left_layout.addWidget(w.diag_issue_summary)
        w.diag_summary = Card("No active session", "Run Quick Check from Home.")
        w.diag_counts = Card("Severity Snapshot", "CRIT 0 | WARN 0 | OK 0 | INFO 0")
        w.diag_top3 = Card("Top 3 Findings", "No findings yet.")
        w.diag_next_btn = PrimaryButton("Run Quick Check")
        w.diag_next_btn.clicked.connect(w._run_next_best_action)
        w.diag_next = Card("Next Best Action", "Run Quick Check to generate findings.", right_widget=w.diag_next_btn)
        left_layout.addWidget(w.diag_summary)
        left_layout.addWidget(w.diag_counts)
        left_layout.addWidget(w.diag_top3)
        left_layout.addWidget(w.diag_next)
        left_layout.addStretch(1)

        center = QWidget()
        center_layout = QVBoxLayout(center)
        center_layout.setContentsMargins(0, 0, 0, 0)
        center_layout.setSpacing(spacing("md"))
        toolbar = Card("Findings Toolbar", "Search and filter findings.")
        toolbar_row = QWidget()
        toolbar_row_layout = QHBoxLayout(toolbar_row)
        toolbar_row_layout.setContentsMargins(0, 0, 0, 0)
        toolbar_row_layout.setSpacing(spacing("sm"))
        w.diag_search = QLineEdit()
        w.diag_search.setObjectName("SearchInput")
        w.diag_search.setPlaceholderText("Search findings")
        w.diag_severity = QComboBox()
        w.diag_severity.addItems(["Any Severity", "CRIT", "WARN", "OK", "INFO"])
        w.diag_recommended = QCheckBox("Recommended only")
        w.diag_sort = QComboBox()
        w.diag_sort.addItems(["Sort: Severity", "Sort: Title"])
        w.diag_search.textChanged.connect(w._apply_diagnose_filters)
        w.diag_severity.currentTextChanged.connect(w._apply_diagnose_filters)
        w.diag_recommended.stateChanged.connect(w._apply_diagnose_filters)
        w.diag_sort.currentTextChanged.connect(w._apply_diagnose_filters)
        details_btn = SoftButton("Details")
        details_btn.clicked.connect(lambda: w._set_concierge_collapsed(False, persist=True))
        toolbar_row_layout.addWidget(w.diag_search, 1)
        toolbar_row_layout.addWidget(w.diag_severity, 0)
        toolbar_row_layout.addWidget(w.diag_recommended, 0)
        toolbar_row_layout.addWidget(w.diag_sort, 0)
        toolbar_row_layout.addWidget(details_btn, 0)
        toolbar.body_layout().addWidget(toolbar_row)
        center_layout.addWidget(toolbar)
        w.diag_issue_list = QWidget()
        w.diag_issue_list_layout = QVBoxLayout(w.diag_issue_list)
        w.diag_issue_list_layout.setContentsMargins(0, 0, 0, 0)
        w.diag_issue_list_layout.setSpacing(spacing("sm"))
        center_layout.addWidget(w.diag_issue_list)
        w.diag_loading = SkeletonLoader(rows=5, density=w.settings_state.density)
        w.diag_loading.hide()
        center_layout.addWidget(w.diag_loading)
        w.diag_feed = QWidget()
        w.diag_feed_layout = QVBoxLayout(w.diag_feed)
        w.diag_feed_layout.setContentsMargins(0, 0, 0, 0)
        w.diag_feed_layout.setSpacing(spacing("sm"))
        w.diag_feed_layout.addWidget(EmptyState("No Findings", "Run a scan from the top app bar to populate findings.", icon="!"))
        center_layout.addWidget(w.diag_feed, 1)

        layout.addWidget(left, 1)
        layout.addWidget(center, 3)
