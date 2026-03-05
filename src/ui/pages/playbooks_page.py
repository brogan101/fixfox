from __future__ import annotations

from typing import Any

from PySide6.QtWidgets import (
    QCheckBox,
    QComboBox,
    QGridLayout,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QSpinBox,
    QStackedWidget,
    QTextEdit,
    QVBoxLayout,
    QWidget,
)

from ..components.feed_renderer import FeedRenderer
from ..style import spacing
from ..widgets import Card, DrawerCard, InlineCallout, PrimaryButton, SoftButton
from .common import PageScroll, build_page_header


class PlaybooksPage(PageScroll):
    def __init__(self, services: Any) -> None:
        super().__init__(page_id="playbooks", object_name="PagePlaybooks")
        self.services = services
        self._build_ui()

    def _build_ui(self) -> None:
        w = self.services
        layout = QVBoxLayout(self.content)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(spacing("md"))
        layout.addWidget(
            build_page_header(
                "Playbooks",
                "Tools and runbooks are separated into clean directories.",
                help_text="Use Tools for one-off actions and Runbooks for guided sequences.",
                on_help=w._show_page_help,
            )
        )
        w.pb_callout = InlineCallout("Playbooks", "", level="info", density=w.settings_state.density)
        layout.addWidget(w.pb_callout)

        w.pb_basic_container = QWidget()
        basic_layout = QVBoxLayout(w.pb_basic_container)
        basic_layout.setContentsMargins(0, 0, 0, 0)
        basic_layout.setSpacing(spacing("md"))
        basic_intro = Card("Guided Goals", "Pick a goal, run the guided path, and review results in ToolRunner.")
        basic_layout.addWidget(basic_intro)
        w.pb_basic_goal_grid = QGridLayout()
        w.pb_basic_goal_grid.setContentsMargins(0, 0, 0, 0)
        w.pb_basic_goal_grid.setSpacing(spacing("md"))
        basic_layout.addLayout(w.pb_basic_goal_grid, 1)
        layout.addWidget(w.pb_basic_container, 1)

        w.pb_pro_console = QWidget()
        pro_layout = QVBoxLayout(w.pb_pro_console)
        pro_layout.setContentsMargins(0, 0, 0, 0)
        pro_layout.setSpacing(spacing("md"))

        controls_shell = QWidget()
        controls_outer = QVBoxLayout(controls_shell)
        controls_outer.setContentsMargins(0, 0, 0, 0)
        controls_outer.setSpacing(spacing("xs"))
        controls_row1 = QHBoxLayout()
        controls_row1.setContentsMargins(0, 0, 0, 0)
        controls_row1.setSpacing(spacing("sm"))
        controls_row2 = QHBoxLayout()
        controls_row2.setContentsMargins(0, 0, 0, 0)
        controls_row2.setSpacing(spacing("sm"))
        w.tb_search = QLineEdit()
        w.tb_search.setObjectName("SearchInput")
        w.tb_search.setPlaceholderText("Search tools, script tasks, and runbooks")
        w.tb_search.textChanged.connect(w._refresh_toolbox)
        w.pb_segment = QComboBox()
        w.pb_segment.addItems(["Tools", "Runbooks"])
        w.pb_segment.currentTextChanged.connect(lambda _: w._switch_playbooks_segment())
        w.pb_chip_safe = QCheckBox("Safe")
        w.pb_chip_safe.setChecked(True)
        w.pb_chip_admin = QCheckBox("Admin")
        w.pb_chip_admin.setChecked(True)
        w.pb_chip_advanced = QCheckBox("Advanced")
        w.pb_chip_advanced.setChecked(True)
        for chip in (w.pb_chip_safe, w.pb_chip_admin, w.pb_chip_advanced):
            chip.stateChanged.connect(w._refresh_toolbox)
        w.pb_advanced_toggle = SoftButton("Show advanced script tasks")
        w.pb_advanced_toggle.clicked.connect(w._toggle_advanced_script_tasks)
        controls_row1.addWidget(w.tb_search, 1)
        controls_row1.addWidget(w.pb_segment, 0)
        controls_row2.addWidget(w.pb_chip_safe, 0)
        controls_row2.addWidget(w.pb_chip_admin, 0)
        controls_row2.addWidget(w.pb_chip_advanced, 0)
        controls_row2.addStretch(1)
        controls_row2.addWidget(w.pb_advanced_toggle, 0)
        controls_outer.addLayout(controls_row1)
        controls_outer.addLayout(controls_row2)
        pro_layout.addWidget(controls_shell)

        w.pb_stack = QStackedWidget()

        tools_view = QWidget()
        tools_layout = QHBoxLayout(tools_view)
        tools_layout.setContentsMargins(0, 0, 0, 0)
        tools_layout.setSpacing(spacing("md"))

        left = QWidget()
        left_layout = QVBoxLayout(left)
        left_layout.setContentsMargins(0, 0, 0, 0)
        left_layout.setSpacing(spacing("md"))
        w.tb_filter = QComboBox()
        w.tb_filter.addItems(
            [
                "All Categories",
                "Windows Links",
                "Evidence",
                "Network",
                "Updates",
                "Printer",
                "Integrity",
                "Browser",
                "Audio",
                "Privacy",
                "Cloud",
                "Devices",
                "Office",
                "Services",
                "Security",
                "WMI",
            ]
        )
        w.tb_filter.currentTextChanged.connect(w._refresh_toolbox)
        w.tb_top = FeedRenderer(w._make_tool_row, density=w.settings_state.density, empty_icon="tool", empty_message="No top tools.")
        w.tb_top.item_activated.connect(lambda tid: w._launch_tool_payload(str(tid)))
        w.tb_top.item_selected.connect(lambda tid: w._set_selected_tool(str(tid)))
        w.tb_top.context_requested.connect(w._tool_menu)
        pinned = Card("Pinned", "Top tools and favorites.")
        pinned.body_layout().addWidget(w.tb_filter)
        pinned.body_layout().addWidget(QLabel("Top tools"))
        pinned.body_layout().addWidget(w.tb_top, 1)

        w.tb_favorites = FeedRenderer(w._make_tool_row, density=w.settings_state.density, empty_icon="star", empty_message="No favorite tools.")
        w.tb_favorites.item_activated.connect(lambda tid: w._launch_tool_payload(str(tid)))
        w.tb_favorites.item_selected.connect(lambda tid: w._set_selected_tool(str(tid)))
        w.tb_favorites.context_requested.connect(w._tool_menu)
        pinned.body_layout().addWidget(QLabel("Favorites"))
        pinned.body_layout().addWidget(w.tb_favorites, 1)
        left_layout.addWidget(pinned, 1)

        w.tb_all = FeedRenderer(w._make_tool_row, density=w.settings_state.density, empty_icon="tool", empty_message="No tools match search.")
        w.tb_all.item_activated.connect(lambda tid: w._launch_tool_payload(str(tid)))
        w.tb_all.item_selected.connect(lambda tid: w._set_selected_tool(str(tid)))
        w.tb_all.context_requested.connect(w._tool_menu)
        directory = Card("Tool Directory", "Browse with search and category filters.")
        directory.body_layout().addWidget(w.tb_all)
        left_layout.addWidget(directory, 2)

        right = QWidget()
        right_layout = QVBoxLayout(right)
        right_layout.setContentsMargins(0, 0, 0, 0)
        right_layout.setSpacing(spacing("md"))
        w.pb_tool_detail = Card("Tool Detail", "Select a tool to see details and run it.")
        w.pb_tool_detail_text = QLabel("Select a tool to see details and run it.")
        w.pb_tool_detail_text.setWordWrap(True)
        w.pb_tool_detail.body_layout().addWidget(w.pb_tool_detail_text)
        w.pb_detail_steps = DrawerCard("What it runs")
        w.pb_tool_detail.body_layout().addWidget(w.pb_detail_steps)
        detail_btn_row = QWidget()
        detail_btn_layout = QHBoxLayout(detail_btn_row)
        detail_btn_layout.setContentsMargins(0, 0, 0, 0)
        detail_btn_layout.setSpacing(spacing("xs"))
        w.pb_detail_run = PrimaryButton("Run Tool")
        w.pb_detail_dry = SoftButton("Dry-Run Tool")
        w.pb_detail_export = SoftButton("Export Pack")
        w.pb_detail_run.clicked.connect(w._run_selected_tool)
        w.pb_detail_dry.clicked.connect(w._dry_run_selected_tool)
        w.pb_detail_export.clicked.connect(lambda: w.nav.setCurrentRow(w.NAV_ITEMS.index("Reports")))
        detail_btn_layout.addWidget(w.pb_detail_run)
        detail_btn_layout.addWidget(w.pb_detail_dry)
        detail_btn_layout.addWidget(w.pb_detail_export)
        detail_btn_layout.addStretch(1)
        w.pb_tool_detail.body_layout().addWidget(detail_btn_row)
        right_layout.addWidget(w.pb_tool_detail)

        w.task_filter = QComboBox()
        w.task_filter.addItems(
            [
                "All Task Categories",
                "system",
                "eventlogs",
                "network",
                "updates",
                "printer",
                "integrity",
                "performance",
                "crash",
                "evidence",
                "storage",
                "repair",
                "hardware",
                "browser",
                "audio",
                "privacy",
                "cloud",
                "devices",
                "office",
                "services",
                "security",
                "wmi",
            ]
        )
        w.task_filter.currentTextChanged.connect(w._refresh_script_tasks)
        w.task_feed = FeedRenderer(w._make_task_row, density=w.settings_state.density, empty_icon="task", empty_message="No script tasks match search.")
        w.task_feed.item_activated.connect(lambda tid: w._run_script_task(str(tid), dry_run=False))
        w.task_feed.item_selected.connect(lambda tid: w._set_selected_script_task(str(tid)))
        w.task_feed.context_requested.connect(w._task_menu)
        w.task_card = Card("Advanced Script Tasks", "Hidden by default to reduce clutter for home users.")
        w.task_card.body_layout().addWidget(w.task_filter)
        w.task_card.body_layout().addWidget(w.task_feed)
        task_actions = QHBoxLayout()
        run_task = SoftButton("Run Selected Task")
        run_task.clicked.connect(w._run_selected_script_task)
        collect_evidence = SoftButton("Collect Core Evidence")
        collect_evidence.clicked.connect(w._collect_core_evidence)
        task_actions.addWidget(run_task)
        task_actions.addWidget(collect_evidence)
        task_actions.addStretch(1)
        w.task_card.body_layout().addLayout(task_actions)
        w.task_card.setVisible(False)
        right_layout.addWidget(w.task_card, 1)

        w.file_index_card = Card("Fast File Search", "Build a local index for instant file lookups (Pro).")
        w.file_index_roots = QLineEdit()
        w.file_index_roots.setObjectName("SearchInput")
        w.file_index_roots.setPlaceholderText("Roots (semicolon-separated), e.g. C:\\Users\\You\\Downloads;C:\\Users\\You\\Desktop")
        if w.settings_state.file_index_roots:
            w.file_index_roots.setText(";".join(w.settings_state.file_index_roots))
        w.file_index_budget = QSpinBox()
        w.file_index_budget.setRange(10, 600)
        w.file_index_budget.setValue(90)
        w.file_index_budget.setSuffix(" s")
        index_top = QHBoxLayout()
        index_top.setContentsMargins(0, 0, 0, 0)
        index_top.setSpacing(spacing("xs"))
        w.btn_file_index_add_root = SoftButton("Add Root")
        w.btn_file_index_build = SoftButton("Build Index")
        w.btn_file_index_add_root.clicked.connect(w._pick_file_index_root)
        w.btn_file_index_build.clicked.connect(w._build_file_index)
        index_top.addWidget(w.btn_file_index_add_root)
        index_top.addWidget(QLabel("Budget"))
        index_top.addWidget(w.file_index_budget)
        index_top.addStretch(1)
        index_top.addWidget(w.btn_file_index_build)

        w.file_index_query = QLineEdit()
        w.file_index_query.setObjectName("SearchInput")
        w.file_index_query.setPlaceholderText("Search indexed files")
        w.btn_file_index_search = SoftButton("Search")
        w.btn_file_index_export = SoftButton("Export CSV")
        w.btn_file_index_search.clicked.connect(w._search_file_index)
        w.btn_file_index_export.clicked.connect(w._export_file_index_results)
        w.file_index_query.returnPressed.connect(w._search_file_index)
        index_search = QHBoxLayout()
        index_search.setContentsMargins(0, 0, 0, 0)
        index_search.setSpacing(spacing("xs"))
        index_search.addWidget(w.file_index_query, 1)
        index_search.addWidget(w.btn_file_index_search)
        index_search.addWidget(w.btn_file_index_export)
        w.file_index_status = QLabel("Index not built yet.")
        w.file_index_results = QTextEdit()
        w.file_index_results.setReadOnly(True)
        w.file_index_results.setMinimumHeight(150)
        w.file_index_card.body_layout().addWidget(w.file_index_roots)
        w.file_index_card.body_layout().addLayout(index_top)
        w.file_index_card.body_layout().addLayout(index_search)
        w.file_index_card.body_layout().addWidget(w.file_index_status)
        w.file_index_card.body_layout().addWidget(w.file_index_results)
        right_layout.addWidget(w.file_index_card, 1)

        tools_layout.addWidget(left, 3)
        tools_layout.addWidget(right, 2)

        runbooks_view = QWidget()
        runbooks_layout = QVBoxLayout(runbooks_view)
        runbooks_layout.setContentsMargins(0, 0, 0, 0)
        runbooks_layout.setSpacing(spacing("md"))
        rb_controls = QHBoxLayout()
        w.rb_audience = QComboBox()
        w.rb_audience.addItems(["All Audiences", "home", "it"])
        w.rb_audience.currentTextChanged.connect(w._refresh_runbooks)
        rb_controls.addWidget(QLabel("Audience"))
        rb_controls.addWidget(w.rb_audience, 0)
        rb_controls.addStretch(1)
        runbooks_layout.addLayout(rb_controls)

        w.rb_curated = FeedRenderer(w._make_runbook_row, density=w.settings_state.density, empty_icon="book", empty_message="No curated runbooks.")
        w.rb_curated.item_selected.connect(lambda rid: w._set_runbook_selection(str(rid) if rid else ""))
        w.rb_curated.item_activated.connect(lambda rid: w._set_runbook_selection(str(rid) if rid else ""))
        w.rb_curated.context_requested.connect(w._runbook_menu)
        curated = Card("Curated Runbooks", "3 home playbooks + key IT runbooks (max 6 visible).")
        curated.body_layout().addWidget(w.rb_curated)
        runbooks_layout.addWidget(curated)

        w.rb_card = Card("Runbook Directory", "Dry-run first, then execute with checkpoints.")
        w.rb_feed = FeedRenderer(w._make_runbook_row, density=w.settings_state.density, empty_icon="book", empty_message="No runbooks match search.")
        w.rb_feed.item_selected.connect(lambda rid: w._set_runbook_selection(str(rid) if rid else ""))
        w.rb_feed.item_activated.connect(lambda rid: w._set_runbook_selection(str(rid) if rid else ""))
        w.rb_feed.context_requested.connect(w._runbook_menu)
        rb_actions = QHBoxLayout()
        run_dry = SoftButton("Run Dry-Run")
        run_live = SoftButton("Run Runbook")
        run_dry.clicked.connect(lambda: w.run_selected_runbook(True))
        run_live.clicked.connect(lambda: w.run_selected_runbook(False))
        rb_actions.addWidget(run_dry)
        rb_actions.addWidget(run_live)
        rb_actions.addStretch(1)
        w.rb_detail = Card("Runbook Detail", "Select a runbook to view sequence and safety context.")
        w.rb_steps = DrawerCard("Steps Preview")
        w.rb_detail.body_layout().addWidget(w.rb_steps)
        w.rb_card.body_layout().addWidget(w.rb_feed, 1)
        w.rb_card.body_layout().addLayout(rb_actions)
        w.rb_card.body_layout().addWidget(w.rb_detail)
        runbooks_layout.addWidget(w.rb_card, 1)

        w.pb_stack.addWidget(tools_view)
        w.pb_stack.addWidget(runbooks_view)
        pro_layout.addWidget(w.pb_stack, 1)
        layout.addWidget(w.pb_pro_console, 1)
        w._refresh_basic_playbooks_cards()
        w._switch_playbooks_segment()
