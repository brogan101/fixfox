from __future__ import annotations

from functools import partial
from typing import Any

from PySide6.QtCore import Qt
from PySide6.QtGui import QPixmap
from PySide6.QtWidgets import QGridLayout, QHBoxLayout, QLabel, QSizePolicy, QVBoxLayout, QWidget

from ...core.utils import resource_path
from ..components.feed_renderer import FeedRenderer
from ..style import spacing
from ..widgets import Card, InlineCallout, Pill, PrimaryButton, SoftButton
from .common import PageScroll, build_page_header


class HomePage(PageScroll):
    def __init__(self, services: Any) -> None:
        super().__init__(page_id="home", object_name="PageHome")
        self.services = services
        self._build_ui()

    def _build_ui(self) -> None:
        w = self.services
        layout = QVBoxLayout(self.content)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(spacing("md"))

        layout.addWidget(
            build_page_header(
                "Home",
                "Command center for diagnostics, session recovery, and support export readiness.",
                help_text="Start from a goal card or recent session, then export when done.",
                on_help=w._show_page_help,
            )
        )
        w.home_callout = InlineCallout("Status", "", level="info", density=w.settings_state.density)
        layout.addWidget(w.home_callout)

        hero_actions = QWidget()
        hero_actions_layout = QHBoxLayout(hero_actions)
        hero_actions_layout.setContentsMargins(0, 0, 0, 0)
        hero_actions_layout.setSpacing(spacing("sm"))
        hero_run = PrimaryButton("Quick Check")
        hero_run.clicked.connect(lambda: w.run_quick_check("Quick Check"))
        hero_export = SoftButton("Prepare Bundle")
        hero_export.clicked.connect(lambda: w.nav.setCurrentRow(w.NAV_ITEMS.index("Reports")))
        hero_settings = SoftButton("Settings")
        hero_settings.clicked.connect(lambda: w.nav.setCurrentRow(w.NAV_ITEMS.index("Settings")))
        hero_actions_layout.addWidget(hero_run)
        hero_actions_layout.addWidget(hero_export)
        hero_actions_layout.addWidget(hero_settings)
        hero_actions_layout.addStretch(1)

        hero = Card("FixFox Control Center", "Run local diagnostics, recover a session, and prepare a share-safe support bundle without leaving the shell.", right_widget=hero_actions)
        mark = QLabel()
        pix = QPixmap(resource_path("assets/brand/fixfox_mark.png"))
        if not pix.isNull():
            mark.setPixmap(pix.scaled(40, 40, Qt.KeepAspectRatio, Qt.SmoothTransformation))
        hero.body_layout().addWidget(mark)
        hero.body_layout().addWidget(QLabel("Local-only by default. No telemetry, no background uploads, and no hidden network steps."))
        layout.addWidget(hero)

        status = Card("System Readiness", "Live summary of the current machine state and export posture.")
        w.home_last = status.sub
        strip = QWidget()
        strip_layout = QHBoxLayout(strip)
        strip_layout.setContentsMargins(0, 0, 0, 0)
        strip_layout.setSpacing(spacing("sm"))
        w.p_disk = Pill("Disk: ...")
        w.p_cpu = Pill("CPU: ...")
        w.p_mem = Pill("Memory: ...")
        w.p_update = Pill("Update: review")
        w.p_reboot = Pill("Reboot: unknown")
        for chip in (w.p_disk, w.p_cpu, w.p_mem, w.p_update, w.p_reboot):
            strip_layout.addWidget(chip)
        strip_layout.addStretch(1)
        status.body_layout().addWidget(strip)
        layout.addWidget(status)

        prep_row = QWidget()
        prep_row_layout = QHBoxLayout(prep_row)
        prep_row_layout.setContentsMargins(0, 0, 0, 0)
        prep_row_layout.setSpacing(spacing("md"))
        triage = Card("What to do first", "Start with a safe baseline, then move to guided diagnostics and fixes.")
        triage.body_layout().addWidget(QLabel("1. Run Quick Check to create a working session."))
        triage.body_layout().addWidget(QLabel("2. Review top findings in Diagnose."))
        triage.body_layout().addWidget(QLabel("3. Use Fixes or Playbooks for deterministic next steps."))
        export_ready = Card("Support Bundle Readiness", "Reports becomes the handoff station once a session exists.")
        export_ready.body_layout().addWidget(QLabel("Includes masking preview, bundle tree, evidence checklist, and ticket-summary copy actions."))
        prep_row_layout.addWidget(triage, 1)
        prep_row_layout.addWidget(export_ready, 1)
        layout.addWidget(prep_row)

        goals = QWidget()
        goals_layout = QGridLayout(goals)
        goals_layout.setContentsMargins(0, 0, 0, 0)
        goals_layout.setSpacing(spacing("md"))
        goals_meta = [
            (
                "speed",
                "Speed Up PC",
                "Find the biggest performance bottlenecks with safe checks.",
                ["Performance sample", "Startup pressure review", "Pending reboot signals"],
                "Safe",
            ),
            (
                "space",
                "Free Up Space",
                "Identify high-impact storage cleanup opportunities safely.",
                ["Storage radar", "Large file scan", "Downloads cleanup preview"],
                "Safe",
            ),
            (
                "wifi",
                "Fix Wi-Fi",
                "Collect connectivity evidence and stabilize common network issues.",
                ["Wi-Fi report", "DNS/proxy checks", "Network evidence bundle"],
                "Safe/Admin Optional",
            ),
        ]
        for i, (goal_id, title, desc, runs, badge) in enumerate(goals_meta):
            actions = QWidget()
            actions_layout = QHBoxLayout(actions)
            actions_layout.setContentsMargins(0, 0, 0, 0)
            actions_layout.setSpacing(spacing("xs"))
            start_btn = SoftButton("Start")
            start_btn.clicked.connect(partial(w.run_quick_check, title))
            learn_btn = SoftButton("Learn More")
            learn_btn.clicked.connect(lambda _checked=False, t=title, d=desc: w._show_page_help(t, d))
            actions_layout.addWidget(start_btn)
            actions_layout.addWidget(learn_btn)
            actions_layout.addStretch(1)
            card = Card(title, desc, right_widget=actions)
            card.setProperty("goal", goal_id)
            card.body_layout().addWidget(QLabel("Runs:"))
            for row in runs:
                card.body_layout().addWidget(QLabel(f"- {row}"))
            card.body_layout().addWidget(QLabel(f"Safety: {badge}"))
            goals_layout.addWidget(card, i, 0)
        layout.addWidget(goals)

        w.home_changes = Card(
            "What Changed Since Last Run",
            "Pending reboot: unknown | Recent updates: n/a | Reliability critical events: n/a",
        )
        layout.addWidget(w.home_changes)

        w.home_recommended = Card(
            "Recommended Next",
            "Run Quick Check first, then review top findings in Diagnose and export a share-safe report.",
        )
        layout.addWidget(w.home_recommended)

        w.weekly_card = Card("Weekly Check Reminder", "Reminder is off.")
        layout.addWidget(w.weekly_card)

        favorites_card = Card("Quick Actions", "Pinned actions (max 6).")
        w.home_favorites = FeedRenderer(
            w._make_home_favorite_row,
            density=w.settings_state.density,
            empty_icon="quick_check",
            empty_message="Pin tools, fixes, or runbooks from their menus to build a reusable command deck.",
        )
        w.home_favorites.item_activated.connect(w._launch_home_favorite)
        w.home_favorites.context_requested.connect(w._home_favorite_menu)
        manage_btn = SoftButton("Choose Quick Actions")
        manage_btn.clicked.connect(lambda: w.nav.setCurrentRow(w.NAV_ITEMS.index("Settings")))
        manage_btn.setSizePolicy(QSizePolicy.Fixed, QSizePolicy.Fixed)
        favorites_card.body_layout().addWidget(manage_btn)
        w.home_favorites_hint = QLabel("If you have not pinned anything yet, FixFox will surface suggested home runbooks here.")
        w.home_favorites_hint.setWordWrap(True)
        favorites_card.body_layout().addWidget(w.home_favorites_hint)
        favorites_card.body_layout().addWidget(w.home_favorites)
        layout.addWidget(favorites_card)

        row = QWidget()
        row_layout = QHBoxLayout(row)
        row_layout.setContentsMargins(0, 0, 0, 0)
        row_layout.setSpacing(spacing("md"))
        w.home_recent = FeedRenderer(w._make_session_row, density=w.settings_state.density, empty_icon="clock", empty_message="No sessions yet.")
        w.home_recent.item_activated.connect(lambda payload: w._load_session(str((payload or {}).get("session_id", ""))) if isinstance(payload, dict) else None)
        w.home_recent.context_requested.connect(w._session_menu)
        history_card = Card("Recent Sessions", "Reopen prior sessions, compare outcomes, or continue toward support export.")
        history_card.body_layout().addWidget(w.home_recent)
        export_btn = SoftButton("Prepare Bundle")
        export_btn.clicked.connect(lambda: w.nav.setCurrentRow(w.NAV_ITEMS.index("Reports")))
        quick_action = Card("Bundle Workflow", "Move to Reports to validate masking, review included evidence, and generate a support bundle.", right_widget=export_btn)
        row_layout.addWidget(history_card, 1)
        row_layout.addWidget(quick_action, 0)
        layout.addWidget(row, 1)
