from __future__ import annotations

from functools import partial
from typing import Any

from PySide6.QtWidgets import QGridLayout, QHBoxLayout, QLabel, QSizePolicy, QVBoxLayout, QWidget

from ..components.feed_renderer import FeedRenderer
from ..widgets import Card, Pill, PrimaryButton, SoftButton
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
        layout.setSpacing(10)

        quick_check = PrimaryButton("Start Quick Check")
        quick_check.clicked.connect(lambda: w.run_quick_check("Quick Check"))
        layout.addWidget(
            build_page_header(
                "Home",
                "Status, goals, favorites, and recent sessions.",
                cta=quick_check,
                help_text="Start from a goal card or recent session, then export when done.",
                on_help=w._show_page_help,
            )
        )

        status = Card("System Status", "Last checked: n/a")
        w.home_last = status.sub
        strip = QWidget()
        strip_layout = QHBoxLayout(strip)
        strip_layout.setContentsMargins(0, 0, 0, 0)
        strip_layout.setSpacing(8)
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

        goals = QWidget()
        goals_layout = QGridLayout(goals)
        goals_layout.setContentsMargins(0, 0, 0, 0)
        goals_layout.setSpacing(10)
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
            actions_layout.setSpacing(6)
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

        w.weekly_card = Card("Weekly Check Reminder", "Reminder is off.")
        layout.addWidget(w.weekly_card)

        favorites_card = Card("Quick Actions", "Pinned actions (max 6).")
        w.home_favorites = FeedRenderer(
            w._make_home_favorite_row,
            density=w.settings_state.density,
            empty_icon="*",
            empty_message="No favorites yet.",
        )
        w.home_favorites.item_activated.connect(w._launch_home_favorite)
        w.home_favorites.context_requested.connect(w._home_favorite_menu)
        manage_btn = SoftButton("Manage Favorites")
        manage_btn.clicked.connect(lambda: w.nav.setCurrentRow(w.NAV_ITEMS.index("Settings")))
        manage_btn.setSizePolicy(QSizePolicy.Fixed, QSizePolicy.Fixed)
        favorites_card.body_layout().addWidget(manage_btn)
        favorites_card.body_layout().addWidget(w.home_favorites)
        layout.addWidget(favorites_card)

        row = QWidget()
        row_layout = QHBoxLayout(row)
        row_layout.setContentsMargins(0, 0, 0, 0)
        row_layout.setSpacing(10)
        w.home_recent = FeedRenderer(w._make_session_row, density=w.settings_state.density, empty_icon="clock", empty_message="No sessions yet.")
        w.home_recent.item_activated.connect(lambda payload: w._load_session(str((payload or {}).get("session_id", ""))) if isinstance(payload, dict) else None)
        w.home_recent.context_requested.connect(w._session_menu)
        history_card = Card("Recent Sessions", "Reopen or export previous sessions.")
        history_card.body_layout().addWidget(w.home_recent)
        export_btn = SoftButton("Export Last Pack")
        export_btn.clicked.connect(w.export_last_session)
        quick_action = Card("Quick Action", "Export latest session without rerun.", right_widget=export_btn)
        row_layout.addWidget(history_card, 1)
        row_layout.addWidget(quick_action, 0)
        layout.addWidget(row, 1)
