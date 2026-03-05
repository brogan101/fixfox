from __future__ import annotations

from typing import Any

from PySide6.QtWidgets import QComboBox, QHBoxLayout, QLineEdit, QVBoxLayout, QWidget

from ..components.feed_renderer import FeedRenderer
from ..style import spacing
from ..widgets import Card, DrawerCard, InlineCallout, SoftButton
from .common import PageScroll, build_page_header


class HistoryPage(PageScroll):
    def __init__(self, services: Any) -> None:
        super().__init__(page_id="history", object_name="PageHistory")
        self.services = services
        self._build_ui()

    def _build_ui(self) -> None:
        w = self.services
        layout = QHBoxLayout(self.content)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(spacing("md"))

        center = QWidget()
        center_layout = QVBoxLayout(center)
        center_layout.setContentsMargins(0, 0, 0, 0)
        center_layout.setSpacing(spacing("md"))
        details_btn = SoftButton("Details")
        details_btn.clicked.connect(lambda: w._set_concierge_collapsed(False, persist=True))
        center_layout.addWidget(
            build_page_header(
                "History",
                "Timeline of sessions with reopen and re-export.",
                help_text="Search, reopen prior sessions, compare with active, or re-export without rerunning diagnostics.",
                on_help=w._show_page_help,
                cta=details_btn,
            )
        )
        w.hist_callout = InlineCallout("History", "", level="info", density=w.settings_state.density)
        center_layout.addWidget(w.hist_callout)
        w.hist_search = QLineEdit()
        w.hist_search.setObjectName("SearchInput")
        w.hist_search.setPlaceholderText("Search sessions")
        w.hist_search.textChanged.connect(w._refresh_history)
        w.hist_scope = QComboBox()
        w.hist_scope.addItems(["All Sessions", "With Exports", "Failures"])
        w.hist_scope.currentTextChanged.connect(w._refresh_history)
        w.hist_list = FeedRenderer(w._make_session_row, density=w.settings_state.density, empty_icon="clock", empty_message="No sessions found.")
        w.hist_list.item_activated.connect(lambda payload: w._load_session(str((payload or {}).get("session_id", ""))) if isinstance(payload, dict) else None)
        w.hist_list.context_requested.connect(w._session_menu)
        w.hist_list.item_selected.connect(lambda _: w._update_history_detail())
        center_layout.addWidget(w.hist_search)
        center_layout.addWidget(w.hist_scope)
        center_layout.addWidget(w.hist_list, 1)

        right = QWidget()
        right_layout = QVBoxLayout(right)
        right_layout.setContentsMargins(0, 0, 0, 0)
        right_layout.setSpacing(spacing("md"))
        w.hist_detail = Card("Case Summary", "Select a session.")
        reopen_btn = SoftButton("Reopen Session")
        reopen_btn.clicked.connect(w.reopen_selected_session)
        compare_btn = SoftButton("Compare to Active")
        compare_btn.clicked.connect(w.compare_with_active_session)
        reexport_btn = SoftButton("Re-export")
        reexport_btn.clicked.connect(w.reexport_selected_session)
        for btn in (reopen_btn, compare_btn, reexport_btn):
            w.hist_detail.body_layout().addWidget(btn)
        right_layout.addWidget(w.hist_detail)
        w.hist_compare = DrawerCard("Compare View")
        right_layout.addWidget(w.hist_compare)
        w.run_center = FeedRenderer(w._make_run_center_row, density=w.settings_state.density, empty_icon="run", empty_message="No recent runs.")
        w.run_center.item_activated.connect(w._run_center_activate)
        w.run_center.context_requested.connect(w._run_center_menu)
        w.run_card_widget = Card("Run Center", "Last 20 tool/fix/runbook runs.")
        w.run_card_widget.body_layout().addWidget(w.run_center)
        right_layout.addWidget(w.run_card_widget, 1)
        right_layout.addStretch(1)

        layout.addWidget(center, 2)
        layout.addWidget(right, 1)
