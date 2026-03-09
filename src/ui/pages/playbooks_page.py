from __future__ import annotations

from typing import Any

from PySide6.QtCore import Qt
from PySide6.QtWidgets import (
    QCheckBox,
    QComboBox,
    QGridLayout,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QListWidget,
    QListWidgetItem,
    QSpinBox,
    QStackedWidget,
    QTextEdit,
    QVBoxLayout,
    QWidget,
)

from ..components.feed_renderer import FeedRenderer
from ..components.guided_wizard import GuidedFixWizard, GuidedStep
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
                "Guided workflows, deterministic tools, and runbooks with clear risk and execution context.",
                help_text="Use filters and category facets to find the right play quickly.",
                on_help=w._show_page_help,
            )
        )
        w.pb_callout = InlineCallout("Playbooks", "", level="info", density=w.settings_state.density)
        layout.addWidget(w.pb_callout)

        issue_shell = QWidget()
        issue_shell_layout = QHBoxLayout(issue_shell)
        issue_shell_layout.setContentsMargins(0, 0, 0, 0)
        issue_shell_layout.setSpacing(spacing("md"))

        issue_left = QWidget()
        issue_left_layout = QVBoxLayout(issue_left)
        issue_left_layout.setContentsMargins(0, 0, 0, 0)
        issue_left_layout.setSpacing(spacing("md"))

        w.pb_issue_summary = Card("Issue Library", "200 issue classes routed through shared playbooks, diagnostics, evidence plans, and fix flows.")
        w.pb_issue_stats = QLabel("Loading issue coverage...")
        w.pb_issue_stats.setWordWrap(True)
        w.pb_issue_summary.body_layout().addWidget(w.pb_issue_stats)
        issue_left_layout.addWidget(w.pb_issue_summary)

        issue_filters = Card("Issue Filters", "Search plain-English symptoms or browse by family.")
        filter_row = QHBoxLayout()
        filter_row.setContentsMargins(0, 0, 0, 0)
        filter_row.setSpacing(spacing("sm"))
        w.pb_issue_search = QLineEdit()
        w.pb_issue_search.setObjectName("SearchInput")
        w.pb_issue_search.setPlaceholderText("Search issues: internet not working, outlook password, teams camera...")
        w.pb_issue_family = QComboBox()
        w.pb_issue_family.addItem("All Families")
        w.pb_issue_scope = QComboBox()
        w.pb_issue_scope.addItem("All Coverage", "all")
        w.pb_issue_scope.addItem("Deep Playbooks", "deep")
        w.pb_issue_scope.addItem("Admin Required", "admin")
        w.pb_issue_scope.addItem("Network Needed", "network")
        w.pb_issue_scope.addItem("Restart Risk", "restart")
        w.pb_issue_search.textChanged.connect(w._refresh_support_issue_library)
        w.pb_issue_family.currentTextChanged.connect(w._refresh_support_issue_library)
        w.pb_issue_scope.currentTextChanged.connect(w._refresh_support_issue_library)
        filter_row.addWidget(w.pb_issue_search, 1)
        filter_row.addWidget(w.pb_issue_family, 0)
        filter_row.addWidget(w.pb_issue_scope, 0)
        issue_filters.body_layout().addLayout(filter_row)
        issue_left_layout.addWidget(issue_filters)

        w.pb_issue_list = FeedRenderer(w._make_support_issue_row, density=w.settings_state.density, empty_icon="diagnose", empty_message="No support issues match current filters.")
        w.pb_issue_list.item_selected.connect(lambda issue_id: w._select_support_issue(str(issue_id) if issue_id else ""))
        w.pb_issue_list.item_activated.connect(lambda issue_id: w._select_support_issue(str(issue_id) if issue_id else "", open_target="playbooks"))
        issue_catalog = Card("Issue Catalog", "Searchable issue classes with family and playbook mapping.")
        issue_catalog.body_layout().addWidget(w.pb_issue_list)
        issue_left_layout.addWidget(issue_catalog, 1)

        issue_right = QWidget()
        issue_right_layout = QVBoxLayout(issue_right)
        issue_right_layout.setContentsMargins(0, 0, 0, 0)
        issue_right_layout.setSpacing(spacing("md"))

        w.pb_issue_detail = Card("Issue Detail", "Choose an issue to review mapped diagnostics, playbooks, and recommended fixes.")
        w.pb_issue_detail_text = QLabel("Choose an issue to review mapped diagnostics, playbooks, and recommended fixes.")
        w.pb_issue_detail_text.setWordWrap(True)
        w.pb_issue_playbooks = DrawerCard("Playbooks")
        w.pb_issue_diagnostics = DrawerCard("Diagnostics")
        w.pb_issue_fixes = DrawerCard("Fixes")
        w.pb_issue_detail.body_layout().addWidget(w.pb_issue_detail_text)
        w.pb_issue_detail.body_layout().addWidget(w.pb_issue_playbooks)
        w.pb_issue_detail.body_layout().addWidget(w.pb_issue_diagnostics)
        w.pb_issue_detail.body_layout().addWidget(w.pb_issue_fixes)
        issue_actions = QHBoxLayout()
        issue_actions.setContentsMargins(0, 0, 0, 0)
        issue_actions.setSpacing(spacing("xs"))
        w.pb_issue_run = PrimaryButton("Run Primary Playbook")
        w.pb_issue_open_diag = SoftButton("Open Diagnose")
        w.pb_issue_open_fixes = SoftButton("Open Fixes")
        w.pb_issue_run.clicked.connect(w._run_selected_support_playbook)
        w.pb_issue_open_diag.clicked.connect(lambda: w._select_support_issue(getattr(w, "selected_support_issue_id", ""), open_target="diagnose"))
        w.pb_issue_open_fixes.clicked.connect(lambda: w._select_support_issue(getattr(w, "selected_support_issue_id", ""), open_target="fixes"))
        issue_actions.addWidget(w.pb_issue_run)
        issue_actions.addWidget(w.pb_issue_open_diag)
        issue_actions.addWidget(w.pb_issue_open_fixes)
        issue_actions.addStretch(1)
        w.pb_issue_detail.body_layout().addLayout(issue_actions)
        issue_right_layout.addWidget(w.pb_issue_detail)

        w.pb_playbook_detail = Card("Deep Playbook Detail", "Choose an issue or mapped playbook to inspect the actual script-backed workflow.")
        w.pb_playbook_detail_text = QLabel("Choose an issue or mapped playbook to inspect the actual script-backed workflow.")
        w.pb_playbook_detail_text.setWordWrap(True)
        w.pb_playbook_scripts = DrawerCard("Scripts and Checks")
        w.pb_playbook_guided = DrawerCard("Guided / Manual Steps")
        w.pb_playbook_validation = DrawerCard("Validation / Escalation")
        w.pb_playbook_detail.body_layout().addWidget(w.pb_playbook_detail_text)
        w.pb_playbook_detail.body_layout().addWidget(w.pb_playbook_scripts)
        w.pb_playbook_detail.body_layout().addWidget(w.pb_playbook_guided)
        w.pb_playbook_detail.body_layout().addWidget(w.pb_playbook_validation)
        playbook_actions = QHBoxLayout()
        playbook_actions.setContentsMargins(0, 0, 0, 0)
        playbook_actions.setSpacing(spacing("xs"))
        w.pb_playbook_run_diag = SoftButton("Run Diagnostics")
        w.pb_playbook_run_full = PrimaryButton("Run Full Playbook")
        w.pb_playbook_collect = SoftButton("Open Reports")
        w.pb_playbook_run_diag.clicked.connect(w._run_selected_support_playbook_diagnostics)
        w.pb_playbook_run_full.clicked.connect(w._run_selected_support_playbook)
        w.pb_playbook_collect.clicked.connect(lambda: w.nav.setCurrentRow(w.NAV_ITEMS.index("Reports")))
        playbook_actions.addWidget(w.pb_playbook_run_diag)
        playbook_actions.addWidget(w.pb_playbook_run_full)
        playbook_actions.addWidget(w.pb_playbook_collect)
        playbook_actions.addStretch(1)
        w.pb_playbook_detail.body_layout().addLayout(playbook_actions)
        issue_right_layout.addWidget(w.pb_playbook_detail)

        issue_shell_layout.addWidget(issue_left, 3)
        issue_shell_layout.addWidget(issue_right, 2)
        layout.addWidget(issue_shell)

        summary_row = QWidget()
        summary_row_layout = QGridLayout(summary_row)
        summary_row_layout.setContentsMargins(0, 0, 0, 0)
        summary_row_layout.setSpacing(spacing("md"))
        catalog_summary = Card("Workflow Hub", "Pick the right surface for the job: guided goal, direct tool, script task, or runbook.")
        catalog_summary.body_layout().addWidget(QLabel("Tools are best for one-off actions. Runbooks are best when you need ordered, repeatable steps."))
        confidence_summary = Card("Execution posture", "Every row exposes risk, timing, and what will actually run before you commit.")
        confidence_summary.body_layout().addWidget(QLabel("Use Dry-Run, Tool Detail, or the side sheet to understand impact before making changes."))
        summary_row_layout.addWidget(catalog_summary, 0, 0)
        summary_row_layout.addWidget(confidence_summary, 0, 1)
        layout.addWidget(summary_row)

        w.pb_basic_container = QWidget()
        basic_layout = QVBoxLayout(w.pb_basic_container)
        basic_layout.setContentsMargins(0, 0, 0, 0)
        basic_layout.setSpacing(spacing("md"))
        basic_intro = Card("Guided Goals", "Pick a goal, run the guided path, and review results in ToolRunner.")
        basic_layout.addWidget(basic_intro)
        w.pb_guided_wizard = GuidedFixWizard("Guided Fix Wizard")
        w.pb_guided_wizard.set_steps(
            [
                GuidedStep(
                    id="step_quick_check",
                    title="Run Quick Check",
                    action_label="Run Quick Check",
                    details="Collect baseline diagnostics to identify current risk and bottlenecks.",
                ),
                GuidedStep(
                    id="step_open_diagnose",
                    title="Review Findings",
                    action_label="Open Diagnose",
                    details="Open Diagnose and review findings with risk labels before changing anything.",
                ),
                GuidedStep(
                    id="step_apply_fix",
                    title="Apply One Safe Fix",
                    action_label="Open Fixes",
                    details="Run one safe fix and capture outcome evidence in ToolRunner.",
                ),
            ],
            callbacks={
                "step_quick_check": lambda: w.run_quick_check("Quick Check"),
                "step_open_diagnose": lambda: w.nav.setCurrentRow(w.NAV_ITEMS.index("Diagnose")),
                "step_apply_fix": lambda: w.nav.setCurrentRow(w.NAV_ITEMS.index("Fixes")),
            },
        )
        w.pb_guided_wizard.btn_generate_pack.clicked.connect(lambda: w.nav.setCurrentRow(w.NAV_ITEMS.index("Reports")))
        w.pb_guided_wizard.btn_copy_summary.clicked.connect(w.copy_session_summary)
        basic_layout.addWidget(w.pb_guided_wizard, 0)
        w.pb_basic_goal_grid = QGridLayout()
        w.pb_basic_goal_grid.setContentsMargins(0, 0, 0, 0)
        w.pb_basic_goal_grid.setSpacing(spacing("md"))
        basic_layout.addLayout(w.pb_basic_goal_grid, 1)
        layout.addWidget(w.pb_basic_container, 1)

        w.pb_pro_console = QWidget()
        pro_layout = QVBoxLayout(w.pb_pro_console)
        pro_layout.setContentsMargins(0, 0, 0, 0)
        pro_layout.setSpacing(spacing("md"))

        controls_shell = Card("Catalog Filters", "Search and facet the play catalog.")
        controls_outer = controls_shell.body_layout()
        controls_row1 = QHBoxLayout()
        controls_row1.setContentsMargins(0, 0, 0, 0)
        controls_row1.setSpacing(spacing("sm"))
        controls_row2 = QHBoxLayout()
        controls_row2.setContentsMargins(0, 0, 0, 0)
        controls_row2.setSpacing(spacing("sm"))
        w.tb_search = QLineEdit()
        w.tb_search.setObjectName("SearchInput")
        w.tb_search.setPlaceholderText("Search playbooks, tools, and runbooks")
        w.tb_search.textChanged.connect(w._refresh_toolbox)
        w.pb_segment = QComboBox()
        w.pb_segment.addItems(["Tools", "Runbooks"])
        w.pb_segment.currentTextChanged.connect(lambda _: w._switch_playbooks_segment())
        w.pb_chip_safe = QCheckBox("Safe")
        w.pb_chip_safe.setChecked(True)
        w.pb_chip_admin = QCheckBox("Admin")
        w.pb_chip_admin.setChecked(True)
        w.pb_chip_restart = QCheckBox("Restart")
        w.pb_chip_restart.setChecked(True)
        w.pb_chip_time = QCheckBox("Long")
        w.pb_chip_time.setChecked(True)
        for chip in (w.pb_chip_safe, w.pb_chip_admin, w.pb_chip_restart, w.pb_chip_time):
            chip.stateChanged.connect(w._refresh_toolbox)
        w.pb_advanced_toggle = SoftButton("Show advanced script tasks")
        w.pb_advanced_toggle.clicked.connect(w._toggle_advanced_script_tasks)
        controls_row1.addWidget(w.tb_search, 1)
        controls_row1.addWidget(w.pb_segment, 0)
        controls_row2.addWidget(w.pb_chip_safe, 0)
        controls_row2.addWidget(w.pb_chip_admin, 0)
        controls_row2.addWidget(w.pb_chip_restart, 0)
        controls_row2.addWidget(w.pb_chip_time, 0)
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
        categories_card = Card("Categories", "Single-select category facet.")
        w.pb_category_list = QListWidget()
        w.pb_category_list.setObjectName("PlaybookCategoryList")
        for label in (
            "All Categories",
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
        ):
            w.pb_category_list.addItem(QListWidgetItem(label))
        w.pb_category_list.setCurrentRow(0)
        w.pb_category_list.currentTextChanged.connect(lambda text: w.tb_filter.setCurrentText(str(text or "All Categories")))
        categories_card.body_layout().addWidget(w.pb_category_list)
        left_layout.addWidget(categories_card, 1)

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
        w.tb_filter.hide()
        w.tb_filter.currentTextChanged.connect(w._refresh_toolbox)
        def _sync_category_list(text: str) -> None:
            if not hasattr(w, "pb_category_list"):
                return
            matches = w.pb_category_list.findItems(str(text or ""), Qt.MatchExactly)
            if not matches:
                return
            row = w.pb_category_list.row(matches[0])
            if row >= 0 and w.pb_category_list.currentRow() != row:
                w.pb_category_list.setCurrentRow(row)
        w.tb_filter.currentTextChanged.connect(_sync_category_list)
        w.tb_top = FeedRenderer(w._make_tool_row, density=w.settings_state.density, empty_icon="tool", empty_message="No top tools.")
        w.tb_top.item_activated.connect(lambda tid: w._launch_tool_payload(str(tid)))
        w.tb_top.item_selected.connect(lambda tid: w._set_selected_tool(str(tid)))
        w.tb_top.context_requested.connect(w._tool_menu)
        pinned = Card("Recommended", "Fast actions and pinned tools.")
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
        directory = Card("Playbook Catalog", "Rows include risk badges, timing hints, and run actions.")
        directory.body_layout().addWidget(w.tb_all)
        left_layout.addWidget(directory, 2)

        right = QWidget()
        right_layout = QVBoxLayout(right)
        right_layout.setContentsMargins(0, 0, 0, 0)
        right_layout.setSpacing(spacing("md"))
        w.pb_tool_detail = Card("Tool Detail", "Choose a tool to review what it runs and when to use it.")
        w.pb_tool_detail_text = QLabel("Choose a tool to review what it runs and when to use it.")
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
