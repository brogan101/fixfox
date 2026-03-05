from __future__ import annotations

from typing import Any

from PySide6.QtWidgets import QCheckBox, QComboBox, QHBoxLayout, QLabel, QVBoxLayout, QWidget

from ..components.feed_renderer import FeedRenderer
from ..style import spacing
from ..widgets import Card, DrawerCard, InlineCallout, PrimaryButton, SoftButton
from .common import PageScroll, build_page_header


class FixesPage(PageScroll):
    def __init__(self, services: Any) -> None:
        super().__init__(page_id="fixes", object_name="PageFixes")
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
                "Fixes",
                "Recommended fixes first, with rollback notes.",
                help_text="Select a fix, review risk and rollback, then run through ToolRunner.",
                on_help=w._show_page_help,
            )
        )
        w.fix_callout = InlineCallout("Fixes", "", level="warn", density=w.settings_state.density)
        left_layout.addWidget(w.fix_callout)
        w.fix_scope = QComboBox()
        w.fix_scope.addItems(["Recommended", "All"])
        w.fix_scope.currentTextChanged.connect(w._refresh_fixes)
        w.fix_chip_safe = QCheckBox("Safe")
        w.fix_chip_admin = QCheckBox("Admin")
        w.fix_chip_adv = QCheckBox("Advanced")
        w.fix_chip_safe.setChecked(True)
        w.fix_chip_admin.setChecked(w.settings_state.show_admin_tools)
        w.fix_chip_adv.setChecked(w.settings_state.show_advanced_tools)
        for chip in (w.fix_chip_safe, w.fix_chip_admin, w.fix_chip_adv):
            chip.stateChanged.connect(w._refresh_fixes)
        policy_card = Card("Policy Summary", "Safe-only mode and visible risk levels.", right_widget=w.fix_scope)
        w.fix_policy_text = QLabel("Safe-only mode ON")
        chip_row = QWidget()
        chip_layout = QHBoxLayout(chip_row)
        chip_layout.setContentsMargins(0, 0, 0, 0)
        chip_layout.setSpacing(spacing("sm"))
        chip_layout.addWidget(w.fix_chip_safe)
        chip_layout.addWidget(w.fix_chip_admin)
        chip_layout.addWidget(w.fix_chip_adv)
        chip_layout.addStretch(1)
        policy_card.body_layout().addWidget(w.fix_policy_text)
        policy_card.body_layout().addWidget(chip_row)
        left_layout.addWidget(policy_card)

        w.fix_list = FeedRenderer(w._make_fix_row, density=w.settings_state.density, empty_icon="wrench", empty_message="No fixes match current filters.")
        w.fix_list.item_selected.connect(lambda key: w._set_fix_selection(str(key)))
        w.fix_list.item_activated.connect(lambda key: w._set_fix_selection(str(key)))
        w.fix_list.context_requested.connect(w._fix_menu)
        directory = Card("Fix Directory", "Choose a fix to review details and rollback guidance.")
        directory.body_layout().addWidget(w.fix_list)
        left_layout.addWidget(directory, 1)

        right = QWidget()
        right_layout = QVBoxLayout(right)
        right_layout.setContentsMargins(0, 0, 0, 0)
        right_layout.setSpacing(spacing("md"))
        w.fix_detail = Card("Fix Detail", "Select a fix.")
        w.fix_detail_text = QLabel("Select a fix to see plain language, risk, and rollback guidance.")
        w.fix_detail_text.setWordWrap(True)
        w.fix_commands = DrawerCard("Commands")
        w.fix_detail.body_layout().addWidget(w.fix_detail_text)
        w.fix_detail.body_layout().addWidget(w.fix_commands)
        detail_actions = QWidget()
        detail_actions_layout = QHBoxLayout(detail_actions)
        detail_actions_layout.setContentsMargins(0, 0, 0, 0)
        detail_actions_layout.setSpacing(spacing("sm"))
        w.fix_preview_btn = SoftButton("Preview Fix")
        w.fix_run_btn = PrimaryButton("Run Fix")
        w.fix_preview_btn.clicked.connect(w._preview_selected_fix)
        w.fix_run_btn.clicked.connect(w._run_selected_fix)
        detail_actions_layout.addWidget(w.fix_preview_btn)
        detail_actions_layout.addWidget(w.fix_run_btn)
        detail_actions_layout.addStretch(1)
        w.fix_detail.body_layout().addWidget(detail_actions)
        right_layout.addWidget(w.fix_detail)

        rollback_btn = SoftButton("Undo Selected")
        rollback_btn.clicked.connect(w._run_selected_rollback)
        w.fix_rollback = Card("Rollback Center", "Reversible actions from this session.", right_widget=rollback_btn)
        w.rollback_feed = FeedRenderer(w._make_rollback_row, density=w.settings_state.density, empty_icon="undo", empty_message="No reversible actions yet.")
        w.fix_rollback.body_layout().addWidget(w.rollback_feed)
        right_layout.addWidget(w.fix_rollback, 1)

        w.fix_info = Card("Safety and Rollback", "All fixes require confirmation.")
        right_layout.addWidget(w.fix_info)

        layout.addWidget(left, 2)
        layout.addWidget(right, 2)
