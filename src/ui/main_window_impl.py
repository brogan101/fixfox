from __future__ import annotations

import json
import logging
import os
import re
import subprocess
import time
import traceback
import html
from pathlib import Path
from datetime import datetime
from functools import partial
from typing import Any, Callable

from PySide6.QtCore import QEvent, QPoint, QSize, Qt, QTimer, Signal
from PySide6.QtGui import QAction, QFontMetrics, QIcon, QKeySequence, QPixmap, QShortcut
from PySide6.QtWidgets import (
    QApplication,
    QCheckBox,
    QComboBox,
    QDialog,
    QDialogButtonBox,
    QFormLayout,
    QFrame,
    QFileDialog,
    QGridLayout,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QListWidget,
    QListWidgetItem,
    QMainWindow,
    QMenu,
    QMessageBox,
    QScrollArea,
    QSlider,
    QSpinBox,
    QStackedWidget,
    QTabWidget,
    QTextEdit,
    QTreeWidget,
    QTreeWidgetItem,
    QVBoxLayout,
    QWidget,
    QSizePolicy,
)

from ..core import diagnostics
from ..core.brand import APP_DISPLAY_NAME, APP_TAGLINE, ICON_ICO, ICON_PNG
from ..core.brand_assets import ensure_logo_on_desktop
from ..core.db import (
    db_stats,
    get_run,
    list_recent_runs,
    list_sessions as list_sessions_db,
    rebuild_from_sessions_folder,
    vacuum_database,
    clear_file_index as clear_file_index_db,
)
from ..core.evidence_collector import (
    collect_crash_bundle,
    collect_event_logs,
    collect_network_bundle,
    collect_system_snapshot,
    collect_update_bundle,
)
from ..core.errors import classify_exit, ensure_next_steps
from ..core.exporter import PRESETS, export_session
from ..core.feedback import save_feedback
from ..core.fixes import FIX_CATALOG, FixAction, list_fixes, run_fix
from ..core.file_index import export_results_csv, index_roots, search_files
from ..core.kb import KB_CARDS
from ..core.logging_setup import log_path, logs_dir
from ..core.masking import MaskingOptions, mask_text, redaction_preview
from ..core.paths import ensure_dirs
from ..core.registry import CAPABILITIES
from ..core.registry import get_visible_capabilities
from ..core.runbooks import RUNBOOKS, execute_runbook, runbook_map
from ..core.run_events import RunEventBus, RunEventType, get_run_event_bus
from ..core.safety import policy_from_settings
from ..core.search import SearchItem, get_search_cache_stats, query_index, refresh_dynamic_index_async, warm_static_index_async
from ..core.sessions import (
    SessionMeta,
    add_or_update_meta,
    load_index,
    load_session,
    new_session_id,
    persist_new_session,
    save_session,
    update_meta_export_path,
)
from ..core.settings import AppSettings, load_settings, save_settings
from ..core.script_tasks import list_script_tasks, run_script_task, script_task_map
from ..core.toolbox import TOP_TOOLS, TOOL_DIRECTORY, launch_tool, search_tools
from ..core.utils import resource_path
from ..core.version import APP_VERSION
from ..core.workers import TaskWorker, WorkerConfig, start_worker
from .app_qss import build_qss
from .components.accordion import AccordionSection
from .components.context_menu import ContextAction, show_context_menu
from .components.feed_renderer import FeedItemAdapter, FeedRenderer, SkeletonLoader
from .components.rows import BaseRow, FindingRow, FixRow, SessionRow, ToolRow, row_height_for_density
from .components.tool_runner import ToolRunnerWindow
from .components.global_search import GlobalSearchPopup
from .components.app_shell import AppShellFrame
from .components.onboarding import OnboardingFlow
from .icons import clear_icon_cache, get_icon
from .layout_guardrails import (
    MIN_NAV_WIDTH,
    MIN_RIGHT_PANEL_WIDTH,
    LayoutDebugOverlay,
    apply_button_guardrails,
    min_button_size,
    should_auto_collapse_right_panel,
    scaled_min_window_size,
)
from .ui_state import LayoutPolicy, is_basic, is_pro, layout_policy
from .pages import (
    DiagnosePage,
    FixesPage,
    HistoryPage,
    HomePage,
    PlaybooksPage,
    ReportsPage,
    SettingsPage,
)
from .style import spacing
from .theme import (
    available_palette_labels,
    clamp_ui_scale,
    normalize_density,
    normalize_mode,
    normalize_palette,
    palette_key_from_label,
    palette_label,
    resolve_density_tokens,
    resolve_theme_tokens,
    set_ui_scale_percent,
)
from .widgets import Card, DrawerCard, EmptyState, Pill, PrimaryButton, SoftButton, ToastHost

LOGGER = logging.getLogger("fixfox.ui")

try:
    from rapidfuzz import fuzz as _fuzz
except Exception:  # pragma: no cover - optional fallback
    _fuzz = None


def _now_local() -> str:
    return datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def _scroll(widget: QWidget) -> QScrollArea:
    area = QScrollArea()
    area.setObjectName("PageScroll")
    area.setWidgetResizable(True)
    area.setFrameShape(QFrame.NoFrame)
    area.viewport().setObjectName("PageViewport")
    area.setWidget(widget)
    return area


class CommandPaletteDialog(QDialog):
    def __init__(self, parent: QWidget | None = None, allowed_capability_ids: set[str] | None = None) -> None:
        super().__init__(parent)
        self.setObjectName("CommandPaletteDialog")
        self.setWindowTitle("Command Palette")
        self.resize(760, 520)
        self.selected_key = ""
        self.selected_kind = ""
        self.allowed_capability_ids = allowed_capability_ids
        layout = QVBoxLayout(self)
        self.search = QLineEdit()
        self.search.setObjectName("SearchInput")
        self.search.setPlaceholderText("Search capabilities, fixes, tools, runbooks, sessions, KB")
        self.feed = FeedRenderer(self._make_row, density="comfortable", empty_icon="?", empty_message="No commands found.")
        self.feed.item_activated.connect(self._accept_current)
        self.feed.item_selected.connect(lambda payload: self._set_selected(payload))
        layout.addWidget(self.search)
        layout.addWidget(self.feed, 1)
        self.search.textChanged.connect(self.refresh)
        self.search.returnPressed.connect(self._accept_current)
        self.refresh()

    def _make_row(self, item: FeedItemAdapter, density: str) -> QWidget:
        row = ToolRow(item.title, item.category, item.subtitle, payload=item.payload, density=density)
        row.open_clicked.connect(self._accept_current)
        return row

    def _set_selected(self, payload: Any) -> None:
        if not isinstance(payload, dict):
            return
        self.selected_kind = str(payload.get("kind", ""))
        self.selected_key = str(payload.get("key", ""))

    def refresh(self) -> None:
        adapters: list[FeedItemAdapter] = []
        for row in query_index(self.search.text(), allowed_capability_ids=self.allowed_capability_ids):
            adapters.append(
                FeedItemAdapter(
                    key=f"{row.kind}:{row.key}",
                    title=row.title,
                    subtitle=row.subtitle,
                    payload={"kind": row.kind, "key": row.key},
                    category=row.kind,
                )
            )
        order = {
            "fix": 0,
            "tool": 1,
            "runbook": 2,
            "task": 3,
            "session": 4,
            "run": 5,
            "finding": 6,
            "artifact": 7,
            "file": 8,
            "kb": 9,
            "capability": 10,
            "export": 11,
        }
        self.feed.set_items(adapters, sort_key=lambda item: (order.get(item.category.lower(), 99), item.title.lower()))
        if adapters:
            self.selected_kind = adapters[0].payload["kind"]
            self.selected_key = adapters[0].payload["key"]

    def _accept_current(self, payload: Any = None) -> None:
        if isinstance(payload, dict):
            self._set_selected(payload)
        elif self.feed.list_widget.currentItem() is not None:
            self._set_selected(self.feed.list_widget.currentItem().data(Qt.UserRole))
        if not self.selected_kind or not self.selected_key:
            return
        self.accept()


class FixConfirmDialog(QDialog):
    def __init__(self, fix: FixAction, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self.setWindowTitle(f"Confirm: {fix.title}")
        self.resize(620, 320)
        self.reboot_ack = False
        layout = QVBoxLayout(self)
        layout.addWidget(QLabel(f"Plain English: {fix.plain}"))
        layout.addWidget(QLabel(f"Risk: {fix.risk}"))
        layout.addWidget(QLabel(f"Rollback: {fix.rollback}"))
        drawer = DrawerCard("Commands")
        drawer.set_text("\n".join(fix.commands))
        layout.addWidget(drawer)
        self.reboot_check = QCheckBox("I understand this may require reboot.") if fix.admin_required else None
        if self.reboot_check:
            layout.addWidget(self.reboot_check)
        box = QDialogButtonBox(QDialogButtonBox.Ok | QDialogButtonBox.Cancel)
        layout.addWidget(box)
        box.accepted.connect(self._ok)
        box.rejected.connect(self.reject)

    def _ok(self) -> None:
        if self.reboot_check is not None:
            self.reboot_ack = self.reboot_check.isChecked()
            if not self.reboot_ack:
                QMessageBox.warning(self, "Required", "Please acknowledge reboot warning.")
                return
        self.accept()


class HelpCenterDialog(QDialog):
    HELP_TABS: tuple[tuple[str, str], ...] = (
        (
            "Start Here",
            (
                "Goals and sessions:\n"
                "- Start from Home goals to create a session.\n"
                "- Sessions track findings, actions, evidence, and exports.\n\n"
                "Exports and share-safe:\n"
                "- Reports can mask user/device/IP tokens before sharing.\n"
                "- Export packs are generated locally.\n\n"
                "Optional online actions:\n"
                "- Opening Microsoft Get Help is optional and clearly labeled.\n"
                "- Core diagnostics, fixes, runbooks, and exports work offline.\n\n"
                "Data location:\n"
                "- App data, sessions, and exports are stored under local FixFox app folders."
            ),
        ),
        (
            "Privacy",
            (
                "Collected:\n"
                "- Diagnostic findings, selected actions, and explicit evidence files.\n"
                "- Optional logs when Include Logs is enabled.\n\n"
                "Never collected by default:\n"
                "- Cloud uploads.\n"
                "- External telemetry services.\n"
                "- Background remote transfer."
            ),
        ),
        (
            "Safety",
            (
                "Risk levels:\n"
                "- Safe: low-risk reads or guided Windows surfaces.\n"
                "- Admin: elevated actions, explicit confirmation required.\n"
                "- Advanced: expert workflows with higher operational risk.\n\n"
                "Rollback guidance:\n"
                "- Review rollback notes in Fixes detail before running.\n"
                "- Use rollback actions from Fixes when available."
            ),
        ),
        (
            "KB Pattern",
            (
                "Every KB card follows:\n"
                "1) What it means\n"
                "2) Why it matters\n"
                "3) What FixFox can do\n"
                "4) What user can do now\n"
                "5) When to escalate"
            ),
        ),
    )

    def __init__(self, title: str, page_note: str, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self.setWindowTitle(title)
        self.resize(760, 520)
        layout = QVBoxLayout(self)
        self.tabs = QTabWidget()
        self.tabs.setDocumentMode(True)
        for tab_title, tab_body in self.HELP_TABS:
            page = QWidget()
            page_layout = QVBoxLayout(page)
            page_layout.setContentsMargins(8, 8, 8, 8)
            page_layout.setSpacing(8)
            body = QTextEdit()
            body.setReadOnly(True)
            body.setPlainText(tab_body)
            page_layout.addWidget(body, 1)
            self.tabs.addTab(page, tab_title)
        layout.addWidget(self.tabs, 1)
        note_card = Card("Page Guidance", page_note or "Use Home to start a goal-based session.")
        layout.addWidget(note_card, 0)
        buttons = QDialogButtonBox(QDialogButtonBox.Close)
        buttons.rejected.connect(self.reject)
        buttons.accepted.connect(self.accept)
        layout.addWidget(buttons)


def _rank_search_rows_fast(query: str, rows: list[SearchItem]) -> list[SearchItem]:
    q = str(query or "").strip().lower()
    if not q:
        return rows
    ranked: list[tuple[float, SearchItem]] = []
    for row in rows:
        title = str(row.title or "")
        subtitle = str(row.subtitle or "")
        key = str(row.key or "")
        haystack = f"{title} {subtitle} {key}".strip()
        if not haystack:
            continue
        if _fuzz is not None:
            score = max(
                float(_fuzz.WRatio(q, title.lower())),
                float(_fuzz.partial_ratio(q, haystack.lower())),
            )
        else:
            score = 100.0 if q in haystack.lower() else 0.0
            if title.lower().startswith(q):
                score += 30.0
            if key.lower().startswith(q):
                score += 20.0
        ranked.append((score, row))
    ranked.sort(key=lambda item: item[0], reverse=True)
    return [row for score, row in ranked if score > 0][:80]


def _highlight_search_match_html(text: str, query: str) -> str:
    raw = str(text or "")
    tokens = [token for token in re.split(r"\s+", str(query or "").strip()) if token]
    if not raw or not tokens:
        return html.escape(raw)
    pattern = re.compile("|".join(re.escape(token) for token in tokens), re.IGNORECASE)
    parts: list[str] = []
    last = 0
    for match in pattern.finditer(raw):
        start, end = match.span()
        if start < last:
            continue
        parts.append(html.escape(raw[last:start]))
        parts.append(f"<span style='font-weight:75'>{html.escape(raw[start:end])}</span>")
        last = end
    parts.append(html.escape(raw[last:]))
    return "".join(parts)


def _search_group_for_item_fast(row: SearchItem, *, basic: bool) -> str:
    kind = str(row.kind or "").strip().lower()
    key = str(row.key or "").strip().lower()
    title = str(row.title or "").strip().lower()
    if kind == "session" or kind == "run":
        return "Sessions"
    if kind == "fix" or key.startswith("fix_action."):
        return "Fixes"
    if kind == "tool" or kind == "task" or kind == "play":
        return "Tools"
    if kind == "route":
        return "Goals"
    if kind == "runbook":
        if key.startswith("home_"):
            return "Goals"
        return "Runbooks"
    if kind == "capability":
        if key.startswith("runbook.home_") or "goal" in title or "quick check" in title:
            return "Goals"
        if key.startswith("tool.") or key.startswith("script_task."):
            return "Tools"
        if key.startswith("runbook."):
            return "Runbooks"
        if key.startswith("fix_action."):
            return "Fixes"
    if basic and kind in {"finding", "kb"}:
        return "Goals"
    return "Tools"


class MainWindow(QMainWindow):
    run_bus_event = Signal(object)
    NAV_ITEMS = ("Home", "Playbooks", "Diagnose", "Fixes", "Reports", "History", "Settings")
    NAV_ICONS = {
        "Home": "home",
        "Playbooks": "open_book",
        "Diagnose": "diagnose",
        "Fixes": "wrench",
        "Reports": "reports",
        "History": "history",
        "Settings": "gear",
    }

    def __init__(self, *, startup_phase_cb: Callable[[str], None] | None = None) -> None:
        super().__init__()
        self._startup_phase_cb = startup_phase_cb
        self._event_filter_reentrant = False
        self._mark_startup_phase("mainwindow:init_begin")
        ensure_dirs()
        self.settings_state = load_settings()
        set_ui_scale_percent(getattr(self.settings_state, "ui_scale_pct", 100))
        self.layout_policy_state: LayoutPolicy = layout_policy(self.settings_state)
        if is_basic(self.settings_state):
            self.settings_state.show_advanced_tools = False
            if not self.settings_state.show_admin_tools:
                self.settings_state.safe_only_mode = True
        self.layout_policy_state = layout_policy(self.settings_state)
        self.safety_policy = policy_from_settings(self.settings_state)
        self.current_session: dict[str, Any] = {}
        self.last_export: dict[str, Any] = {}
        self._fast_file_results: list[dict[str, Any]] = []
        self.active_worker: TaskWorker | None = None
        self.tool_runner: ToolRunnerWindow | None = None
        self.run_event_bus: RunEventBus = get_run_event_bus()
        self.active_run_id = ""
        self.active_run_name = ""
        self.active_run_started = 0.0
        self._run_status_subscription_id = 0
        self._status_spinner_frames = ("|", "/", "-", "\\")
        self._status_spinner_index = 0
        self._last_run_status = "Ready"
        self._last_run_line = "Ready."
        self._last_run_log_line = ""
        self._run_status_detail_raw = "No active run."
        self.selected_finding: dict[str, Any] = {}
        self.selected_fix_key = ""
        self.selected_tool_id = ""
        self.selected_task_id = ""
        self.rb_selected_id = ""
        self._syncing_settings = False
        self._search_debounce_timer = QTimer(self)
        self._search_debounce_timer.setSingleShot(True)
        self._search_debounce_timer.setInterval(180)
        self._search_debounce_timer.timeout.connect(self._refresh_global_search_results)
        self._search_worker: TaskWorker | None = None
        self._search_request_id = 0
        self._scale_apply_timer = QTimer(self)
        self._scale_apply_timer.setSingleShot(True)
        self._scale_apply_timer.setInterval(80)
        self._scale_apply_timer.timeout.connect(self._apply_pending_ui_scale)
        self._pending_ui_scale_pct = int(getattr(self.settings_state, "ui_scale_pct", 100))
        self._search_popup = GlobalSearchPopup(self)
        self._search_popup.result_activated.connect(self._apply_global_search_result)
        self._persist_concierge_events = True
        self._auto_concierge_collapse = False
        self._startup_warmup_active = True
        self.layout_overlay: LayoutDebugOverlay | None = None
        self.run_bus_event.connect(self._handle_run_bus_event)
        self._run_status_subscription_id = self.run_event_bus.subscribe_global(
            lambda event: self.run_bus_event.emit(event),
            replay_buffered=False,
        )

        self.setWindowTitle("Fix Fox")
        window_icon = QIcon(resource_path(ICON_ICO))
        if window_icon.isNull():
            window_icon = QIcon(resource_path(ICON_PNG))
        self.setWindowIcon(window_icon)
        self.resize(1380, 900)
        self.setMinimumSize(scaled_min_window_size(self.settings_state.ui_scale_pct))
        self.setObjectName("RootWindow")

        self.app_shell = AppShellFrame()
        self.setCentralWidget(self.app_shell)
        self.nav_shell = self.app_shell.nav_shell
        self.nav = self.nav_shell
        self.nav.currentRowChanged.connect(self._on_nav)
        self.nav_shell.help_requested.connect(lambda: self._open_settings_section("Feedback"))

        self.run_status_panel = self.app_shell.toolbar.run_status_panel
        self.app_identity = self.app_shell.toolbar.app_identity
        self.run_status_title = self.app_shell.toolbar.run_status_title
        self.run_status_detail = self.app_shell.toolbar.run_status_detail
        self.run_status_chip = self.app_shell.toolbar.run_status_chip
        self.top_search = self.app_shell.toolbar.top_search
        self.compact_search_btn = self.app_shell.toolbar.compact_search_btn
        self.top_search_stack = self.app_shell.toolbar.search_stack
        self.btn_quick_check = self.app_shell.toolbar.btn_quick_check
        self._quick_check_default_label = self.btn_quick_check.text()
        self.btn_cancel_task = self.app_shell.toolbar.btn_cancel_task
        self.btn_open_runner = self.app_shell.toolbar.btn_open_runner
        self.btn_export = self.app_shell.toolbar.btn_export
        self.btn_panel_toggle = self.app_shell.toolbar.btn_panel_toggle
        self.btn_overflow = self.app_shell.toolbar.btn_overflow
        self.mode_basic_btn = self.app_shell.toolbar.mode_basic_btn
        self.mode_pro_btn = self.app_shell.toolbar.mode_pro_btn

        self.top_search.setToolTip("Global search (Ctrl+K)")
        self.top_search.textChanged.connect(self._schedule_global_search)
        self.top_search.returnPressed.connect(self._activate_global_search_or_palette)
        self.top_search.installEventFilter(self)
        self.compact_search_btn.clicked.connect(self._handle_compact_search)
        self.run_status_panel.clicked.connect(self.open_tool_runner)
        self.run_status_panel.setToolTip("Open ToolRunner (Ctrl+R)")
        self.btn_quick_check.clicked.connect(lambda: self.run_quick_check("Quick Check"))
        self.btn_quick_check.setShortcut("Ctrl+Shift+R")
        self.btn_open_runner.clicked.connect(self.open_tool_runner)
        self.btn_export.clicked.connect(lambda: self.nav.setCurrentRow(self.NAV_ITEMS.index("Reports")))
        self.app_shell.toolbar.details_toggled.connect(lambda checked: self._set_concierge_collapsed(not checked, persist=True))
        self.btn_overflow.clicked.connect(self._open_header_overflow_menu)
        self.mode_basic_btn.setVisible(False)
        self.mode_pro_btn.setVisible(False)

        self.context = self._build_context_bar()
        self.app_shell.center_layout.addWidget(self.context)
        self.pages = QStackedWidget()
        self.app_shell.center_layout.addWidget(self.pages, 1)

        self.details_drawer = self.app_shell.details_drawer
        self.details_drawer.setMinimumWidth(MIN_RIGHT_PANEL_WIDTH)
        self.side_sheet = self.app_shell.side_sheet
        self.concierge = self.side_sheet
        self.side_sheet.pin_changed.connect(self._on_side_sheet_pin_changed)
        self.toasts = ToastHost()
        self.app_shell.center_layout.addWidget(self.toasts, 0, Qt.AlignRight)

        self.shell_session_label = self.app_shell.bottom_status.session_label
        self.shell_mode_label = self.app_shell.bottom_status.mode_label
        self.shell_safety_label = self.app_shell.bottom_status.safety_label

        details_width = int((self.settings_state.splitter_sizes or [0, 0, 340])[2])
        self.app_shell.set_details_width(max(MIN_RIGHT_PANEL_WIDTH, details_width))
        self._mark_startup_phase("mainwindow:build_nav_pages")
        self._build_nav()
        self._build_pages()

        self.concierge.collapsed_changed.connect(self._on_concierge)
        self.btn_cancel_task.clicked.connect(self._cancel_task)
        QShortcut(QKeySequence("Ctrl+K"), self, self._focus_top_search)
        QShortcut(QKeySequence("Ctrl+Shift+R"), self, lambda: self.run_quick_check("Quick Check"))
        QShortcut(QKeySequence("Ctrl+Alt+L"), self, self._toggle_layout_debug_overlay)
        QShortcut(QKeySequence("Ctrl+Alt+T"), self, self._run_ui_self_check)
        app = QApplication.instance()
        if app is not None:
            app.installEventFilter(self)

        self._sync_settings_ui()
        self._mark_startup_phase("mainwindow:restore_layout")
        self._restore_layout_state()
        self._mark_startup_phase("mainwindow:apply_theme_deferred")
        QTimer.singleShot(0, lambda: self._apply_theme(refresh_data=False))
        self._mark_startup_phase("mainwindow:apply_button_guardrails")
        apply_button_guardrails(self, self.settings_state.density)
        default_open = False
        self._mark_startup_phase("mainwindow:set_concierge_initial")
        self._set_concierge_collapsed(not default_open, persist=False)
        self._mark_startup_phase("mainwindow:responsive_layout_pass")
        self._apply_responsive_concierge()
        self._apply_responsive_header()
        self.layout_overlay = LayoutDebugOverlay(self)
        self._mark_startup_phase("mainwindow:schedule_warmup")
        self._schedule_startup_warmup()
        self._sync_shell_status_bar()
        self._refresh_db_info_label()
        self._sync_ui_mode_controls()
        self._apply_mode_visibility()
        self.settings_state.right_panel_open = False
        self._set_concierge_collapsed(True, persist=False)
        # Keep startup deterministic: side sheet must remain hidden until user explicitly opens it.
        QTimer.singleShot(250, lambda: self._set_concierge_collapsed(True, persist=False))
        self._status_tick_timer = QTimer(self)
        self._status_tick_timer.timeout.connect(self._update_run_status_indicator)
        self._status_tick_timer.start(250)
        QTimer.singleShot(0, self._show_onboarding_if_needed)
        self._run_ui_structure_audit_once()
        self._mark_startup_phase("mainwindow:init_complete")

    def resizeEvent(self, event: Any) -> None:
        super().resizeEvent(event)
        self._apply_responsive_concierge()
        self._apply_responsive_header()
        self._search_popup.sync_anchor()
        self._clamp_concierge_card_widths()
        if hasattr(self, "run_status_title") and hasattr(self, "run_status_detail"):
            self._set_run_status(self.run_status_title.text(), self._run_status_detail_raw)
        if self.layout_overlay is not None and self.layout_overlay.isVisible():
            self.layout_overlay.sync_geometry()

    def _build_nav(self) -> None:
        nav_height = resolve_density_tokens(self.settings_state.density).nav_item_height
        self.nav_shell.set_items(
            list(self.NAV_ITEMS),
            lambda label: self.NAV_ICONS.get(label, str(label).strip().lower()),
            nav_height,
            None,
        )
        if hasattr(self.nav_shell, "refresh_icons"):
            self.nav_shell.refresh_icons()
    def _build_context_bar(self) -> QWidget:
        f = QFrame(); f.setObjectName("SessionContext")
        l = QVBoxLayout(f); l.setContentsMargins(10, 8, 10, 8); l.setSpacing(4)
        self.ctx_hint = QLabel("No active session - start a goal from Home.")
        self.ctx_hint.setObjectName("SubTitle")
        self.ctx_full = QWidget()
        full_l = QHBoxLayout(self.ctx_full); full_l.setContentsMargins(0, 0, 0, 0); full_l.setSpacing(8)
        self.ctx_session = QLabel("Session: none")
        self.ctx_symptom = QLabel("Symptom: n/a")
        self.ctx_share = QLabel("Share-safe: on")
        self.ctx_preset = QLabel("Preset: home_share")
        self.ctx_last = QLabel("Last run: n/a")
        b_export = SoftButton("Export"); b_export.clicked.connect(lambda: self.nav.setCurrentRow(self.NAV_ITEMS.index("Reports")))
        b_copy = SoftButton("Copy Summary"); b_copy.clicked.connect(self.copy_session_summary)
        b_end = SoftButton("End Session"); b_end.clicked.connect(self.end_session)
        min_btn = min_button_size(self.settings_state.density)
        for btn in (b_export, b_copy, b_end):
            btn.setMinimumSize(min_btn)
        for w in (self.ctx_session, self.ctx_symptom, self.ctx_share, self.ctx_preset, self.ctx_last):
            full_l.addWidget(w)
        full_l.addStretch(1); full_l.addWidget(b_export); full_l.addWidget(b_copy); full_l.addWidget(b_end)
        l.addWidget(self.ctx_hint)
        l.addWidget(self.ctx_full)
        return f

    def _mark_startup_phase(self, phase: str) -> None:
        cb = getattr(self, "_startup_phase_cb", None)
        if callable(cb):
            try:
                cb(str(phase))
            except Exception:
                LOGGER.debug("startup_phase_callback_failed phase=%s", phase)

    def _schedule_startup_warmup(self) -> None:
        # Keep first paint fast: defer expensive data refresh into staged warmup tasks.
        QTimer.singleShot(0, self._startup_warmup_stage_one)

    def _startup_warmup_stage_one(self) -> None:
        self._mark_startup_phase("mainwindow:warmup_stage_one")
        warm_static_index_async()
        refresh_dynamic_index_async(force=False)
        self._update_status_strip()
        self._update_weekly_status()
        self._update_context_labels()
        self._update_redaction_preview()
        self._update_diagnose_context({})
        self._update_concierge()
        QTimer.singleShot(0, self._startup_warmup_stage_two)

    def _startup_warmup_stage_two(self) -> None:
        self._mark_startup_phase("mainwindow:warmup_stage_two")
        self._refresh_home_history()
        self._refresh_history()
        self._refresh_run_center()
        QTimer.singleShot(0, self._startup_warmup_stage_three)

    def _startup_warmup_stage_three(self) -> None:
        self._mark_startup_phase("mainwindow:warmup_stage_three")
        self._refresh_fixes()
        self._refresh_toolbox()
        self._refresh_runbooks()
        self._refresh_home_favorites()
        self._refresh_evidence_items()
        self._startup_warmup_active = False
        self._mark_startup_phase("mainwindow:warmup_complete")

    def eventFilter(self, watched: Any, event: Any) -> bool:  # type: ignore[override]
        if self._event_filter_reentrant:
            return super().eventFilter(watched, event)
        self._event_filter_reentrant = True
        try:
            if watched is self.top_search and event.type() == QEvent.KeyPress:
                key = event.key()
                if key == Qt.Key_Down:
                    self._search_popup.move_selection(1)
                    return True
                if key == Qt.Key_Up:
                    self._search_popup.move_selection(-1)
                    return True
                if key in {Qt.Key_Return, Qt.Key_Enter} and self._search_popup.activate_current():
                    return True
                if key == Qt.Key_Escape:
                    self._search_popup.hide_popup()
                    return True
            if (
                self._search_popup.isVisible()
                and event.type() == QEvent.MouseButtonPress
                and isinstance(watched, QWidget)
                and os.environ.get("QT_QPA_PLATFORM", "").strip().lower() != "offscreen"
            ):
                in_popup = watched is self._search_popup or self._search_popup.isAncestorOf(watched)
                in_search = watched is self.top_search or self.top_search.isAncestorOf(watched)
                in_compact = watched is self.compact_search_btn or self.compact_search_btn.isAncestorOf(watched)
                if not any((in_popup, in_search, in_compact)):
                    self._search_popup.hide_popup()
            return super().eventFilter(watched, event)
        finally:
            self._event_filter_reentrant = False

    def _focus_top_search(self) -> None:
        if hasattr(self, "top_search_stack"):
            self.app_shell.toolbar.expand_search(animate=self.width() < 1120)
        self.top_search.setFocus(Qt.ShortcutFocusReason)
        self.top_search.selectAll()
        if self.top_search.text().strip():
            self._schedule_global_search()

    def keyPressEvent(self, event: Any) -> None:  # type: ignore[override]
        if event.key() == Qt.Key_Escape and hasattr(self, "concierge"):
            if not self.concierge.collapsed:
                self._set_concierge_collapsed(True, persist=True)
                event.accept()
                return
        super().keyPressEvent(event)

    def _schedule_global_search(self) -> None:
        query = self.top_search.text().strip() if hasattr(self, "top_search") else ""
        if not query:
            self._cancel_active_search_worker()
            self._search_popup.hide_popup()
            return
        if os.environ.get("QT_QPA_PLATFORM", "").strip().lower() == "offscreen":
            self._dispatch_global_search()
            return
        self._search_debounce_timer.start()

    def _handle_compact_search(self) -> None:
        if hasattr(self, "top_search_stack") and self.top_search_stack.currentIndex() == 1:
            self.app_shell.toolbar.expand_search(animate=True)
            self._focus_top_search()
            return
        self._focus_top_search()

    def _refresh_global_search_results(self) -> None:
        self._dispatch_global_search()

    def _cancel_active_search_worker(self) -> None:
        if self._search_worker is not None:
            self._search_worker.cancel()
            self._search_worker = None

    def _dispatch_global_search(self) -> None:
        started = time.perf_counter()
        query = self.top_search.text().strip() if hasattr(self, "top_search") else ""
        if not query:
            self._cancel_active_search_worker()
            self._search_popup.hide_popup()
            return
        refresh_dynamic_index_async(force=False)
        self._search_request_id += 1
        request_id = self._search_request_id
        self._cancel_active_search_worker()
        worker = TaskWorker(
            self._compute_global_search_payload,
            kwargs={
                "query": query,
                "visible": self._visible_capability_ids(),
                "basic_mode": self.layout_policy_state.mode == "basic",
            },
            config=WorkerConfig(timeout_s=20),
        )
        worker.signals.result.connect(lambda payload, rid=request_id: self._apply_global_search_payload(rid, payload))
        worker.signals.error.connect(lambda message, rid=request_id: LOGGER.debug("global_search_worker_error rid=%s msg=%s", rid, message[:160]))
        worker.signals.finished.connect(lambda rid=request_id, ref=worker: self._on_search_worker_finished(rid, ref))
        self._search_worker = worker
        start_worker(worker)
        elapsed_ms = (time.perf_counter() - started) * 1000.0
        LOGGER.debug("global_search_dispatch_ms=%.2f query=%s", elapsed_ms, query)

    def _compute_global_search_payload(
        self,
        *,
        query: str,
        visible: set[str],
        basic_mode: bool,
        cancel_event: Any = None,
        **_kwargs: Any,
    ) -> dict[str, Any]:
        rows = query_index(query, limit=120, allowed_capability_ids=visible)
        if cancel_event is not None and cancel_event.is_set():
            return {"cancelled": True, "query": query, "grouped_rows": [], "row_count": 0}
        ranked_rows = _rank_search_rows_fast(query, rows)
        if cancel_event is not None and cancel_event.is_set():
            return {"cancelled": True, "query": query, "grouped_rows": [], "row_count": len(ranked_rows)}
        groups: dict[str, list[dict[str, str]]] = {
            "Goals": [],
            "Tools": [],
            "Runbooks": [],
            "Fixes": [],
            "Sessions": [],
        }
        for row in ranked_rows:
            group = _search_group_for_item_fast(row, basic=basic_mode)
            payload = {
                "kind": row.kind,
                "key": row.key,
                "title": row.title,
                "subtitle": row.subtitle,
                "title_html": _highlight_search_match_html(row.title, query),
                "subtitle_html": _highlight_search_match_html(row.subtitle, query),
            }
            bucket = groups.get(group, groups["Tools"])
            if len(bucket) < 10:
                bucket.append(payload)
            if cancel_event is not None and cancel_event.is_set():
                return {"cancelled": True, "query": query, "grouped_rows": [], "row_count": len(ranked_rows)}
        order = ("Goals", "Fixes", "Runbooks", "Tools", "Sessions") if basic_mode else ("Goals", "Tools", "Runbooks", "Fixes", "Sessions")
        grouped_rows = [(name, groups[name]) for name in order]
        return {
            "cancelled": False,
            "query": query,
            "grouped_rows": grouped_rows,
            "row_count": len(ranked_rows),
            "cache_stats": get_search_cache_stats(),
        }

    def _apply_global_search_payload(self, request_id: int, payload: Any) -> None:
        if request_id != self._search_request_id:
            return
        if not isinstance(payload, dict):
            return
        if bool(payload.get("cancelled")):
            return
        query = str(payload.get("query", "")).strip()
        current = self.top_search.text().strip() if hasattr(self, "top_search") else ""
        if (not query) or query != current:
            return
        grouped_rows = payload.get("grouped_rows", [])
        if not isinstance(grouped_rows, list):
            return
        self._search_popup.show_results(self.top_search, grouped_rows, query)
        cache_stats = payload.get("cache_stats", {})
        LOGGER.debug(
            "global_search_result rid=%s query=%s rows=%s static_builds=%s dynamic_builds=%s",
            request_id,
            query,
            payload.get("row_count", 0),
            cache_stats.get("static_builds", "?") if isinstance(cache_stats, dict) else "?",
            cache_stats.get("dynamic_builds", "?") if isinstance(cache_stats, dict) else "?",
        )

    def _on_search_worker_finished(self, request_id: int, worker_ref: TaskWorker) -> None:
        if self._search_worker is worker_ref and request_id <= self._search_request_id:
            self._search_worker = None

    def _rank_search_rows(self, query: str, rows: list[SearchItem]) -> list[SearchItem]:
        return _rank_search_rows_fast(query, rows)

    def _highlight_search_match(self, text: str, query: str) -> str:
        return _highlight_search_match_html(text, query)

    def _search_group_for_item(self, row: SearchItem, *, basic: bool) -> str:
        return _search_group_for_item_fast(row, basic=basic)

    def _activate_global_search_or_palette(self) -> None:
        if self._search_popup.activate_current():
            return
        self._schedule_global_search()

    def _apply_global_search_result(self, payload: Any) -> None:
        if not isinstance(payload, dict):
            return
        kind = str(payload.get("kind", "")).strip()
        key = str(payload.get("key", "")).strip()
        if not kind or not key:
            return
        self._search_popup.hide_popup()
        self._dispatch_search_selection(kind, key)
        self._set_concierge_collapsed(False, persist=True)

    def open_tool_runner(self) -> None:
        if self.tool_runner is None:
            self.toasts.show_toast("No active run.")
            return
        self.tool_runner.show()
        self.tool_runner.raise_()
        self.tool_runner.activateWindow()

    def _sync_ui_mode_controls(self) -> None:
        mode = "pro" if self.settings_state.ui_mode == "pro" else "basic"
        if hasattr(self, "mode_basic_btn") and hasattr(self, "mode_pro_btn"):
            self.mode_basic_btn.setChecked(mode == "basic")
            self.mode_pro_btn.setChecked(mode == "pro")
        if hasattr(self, "s_ui_mode"):
            self.s_ui_mode.blockSignals(True)
            self.s_ui_mode.setCurrentText(mode)
            self.s_ui_mode.blockSignals(False)

    def set_ui_mode(self, mode: str) -> None:
        next_mode = "pro" if str(mode).strip().lower() == "pro" else "basic"
        if self.settings_state.ui_mode == next_mode:
            if next_mode == "basic":
                self.settings_state.right_panel_open = False
                self._set_concierge_collapsed(True, persist=False)
                QTimer.singleShot(0, lambda: self._set_concierge_collapsed(True, persist=False))
            self._sync_ui_mode_controls()
            return
        self.settings_state.ui_mode = next_mode
        if next_mode == "basic":
            self.settings_state.safe_only_mode = True
            self.settings_state.show_admin_tools = False
            self.settings_state.show_advanced_tools = False
            self.settings_state.right_panel_open = False
        else:
            self.settings_state.safe_only_mode = False
            self.settings_state.show_admin_tools = True
            self.settings_state.show_advanced_tools = True
            self.settings_state.right_panel_open = True
        self.layout_policy_state = layout_policy(self.settings_state)
        save_settings(self.settings_state)
        self.safety_policy = policy_from_settings(self.settings_state)
        self._sync_ui_mode_controls()
        self._sync_settings_ui()
        self._apply_mode_visibility()
        self._refresh_fixes()
        self._refresh_toolbox()
        self._refresh_runbooks()
        self._refresh_home_favorites()
        self._refresh_run_center()
        self._update_context_labels()
        self._update_concierge()
        self._set_concierge_collapsed(not self.settings_state.right_panel_open, persist=False)
        if next_mode == "basic":
            QTimer.singleShot(0, lambda: self._set_concierge_collapsed(True, persist=False))
        self.toasts.show_toast(f"Mode switched to {next_mode.title()}.")

    def _visible_capability_ids(self) -> set[str]:
        caps = get_visible_capabilities(
            self.settings_state.ui_mode,
            self.safety_policy,
            admin_enabled=self.settings_state.show_admin_tools,
        )
        return {cap.id for cap in caps}

    def _visible_ids_for_prefix(self, prefix: str) -> set[str]:
        out: set[str] = set()
        for cap_id in self._visible_capability_ids():
            if cap_id.startswith(prefix):
                pieces = cap_id.split(".", 1)
                if len(pieces) == 2 and pieces[1]:
                    out.add(pieces[1])
        return out

    def _playbooks_available(self) -> bool:
        return True

    def _apply_nav_mode_visibility(self) -> None:
        return

    def _apply_settings_mode_visibility(self) -> None:
        if not hasattr(self, "settings_nav"):
            return
        policy = self.layout_policy_state
        basic = policy.mode == "basic"
        for index in range(self.settings_nav.count()):
            item = self.settings_nav.item(index)
            label = str(item.data(Qt.UserRole) or item.text()).strip().lower()
            if label == "advanced":
                item.setHidden(not policy.show_settings_advanced)
        if hasattr(self, "run_card_widget"):
            self.run_card_widget.setVisible(policy.show_run_center)
        if hasattr(self, "s_admin"):
            self.s_admin.setText("Allow admin tools" if basic else "Enable Admin Tools")
            self.s_admin.setEnabled(True)
        if hasattr(self, "s_adv"):
            self.s_adv.blockSignals(True)
            self.s_adv.setChecked(False if basic else self.s_adv.isChecked())
            self.s_adv.setEnabled(not basic)
            self.s_adv.blockSignals(False)
        if hasattr(self, "s_admin_hint"):
            self.s_admin_hint.setText(
                "Enable admin tools in Basic only when a recommended action requires elevation."
                if basic
                else "Show admin-only fixes and runbooks."
            )
        if hasattr(self, "s_drawer_pin"):
            self.s_drawer_pin.setEnabled(True)
        if hasattr(self, "s_safe"):
            self.s_safe.setEnabled(not self.settings_state.show_admin_tools)
            if self.settings_state.show_admin_tools and self.s_safe.isChecked():
                self.s_safe.setChecked(False)
        if hasattr(self, "s_safe_hint"):
            self.s_safe_hint.setText("Safe-only turns off while admin tools are enabled.")
        if hasattr(self, "s_export_btn"):
            self.s_export_btn.setVisible(not basic)
            self.s_export_btn.setEnabled(not basic)
            self.s_export_btn.setToolTip("Switch to Pro mode to export settings JSON." if basic else "Export settings JSON.")
        if hasattr(self, "b_reset_onboarding"):
            self.b_reset_onboarding.setEnabled(not basic)

    def _apply_playbooks_mode_visibility(self) -> None:
        policy = self.layout_policy_state
        basic = policy.mode == "basic"
        if hasattr(self, "pb_advanced_toggle"):
            self.pb_advanced_toggle.setVisible(policy.show_script_tasks and self.pb_stack.currentIndex() == 0)
        if hasattr(self, "pb_basic_container"):
            self.pb_basic_container.setVisible(policy.show_playbooks_guided_basic)
        if hasattr(self, "pb_pro_console"):
            self.pb_pro_console.setVisible(policy.show_playbooks_pro_console)
        if hasattr(self, "task_card") and not policy.show_script_tasks:
            self.task_card.setVisible(False)
        if hasattr(self, "file_index_card"):
            self.file_index_card.setVisible(policy.show_playbooks_pro_console)
        if hasattr(self, "rb_audience"):
            self.rb_audience.blockSignals(True)
            self.rb_audience.clear()
            self.rb_audience.addItems(["All Audiences", "home"] if basic else ["All Audiences", "home", "it"])
            self.rb_audience.blockSignals(False)
        self._refresh_basic_playbooks_cards()

    def _apply_reports_mode_visibility(self) -> None:
        if not hasattr(self, "rep_preset"):
            return
        policy = self.layout_policy_state
        wanted = ["home_share"] if not policy.show_reports_full_presets else list(PRESETS)
        current = self.rep_preset.currentText().strip().lower() if self.rep_preset.count() else ""
        self.rep_preset.blockSignals(True)
        self.rep_preset.clear()
        self.rep_preset.addItems(wanted)
        target = current if current in wanted else wanted[0]
        self.rep_preset.setCurrentText(target)
        self.rep_preset.blockSignals(False)
        if hasattr(self, "rep_preset"):
            self.rep_preset.setEnabled(policy.show_reports_full_presets)
        if hasattr(self, "rep_preset_hint"):
            self.rep_preset_hint.setVisible(not policy.show_reports_full_presets)
        if hasattr(self, "rep_logs"):
            self.rep_logs.setVisible(policy.show_reports_advanced_options)
            self.rep_logs.setChecked(False if not policy.show_reports_advanced_options else self.rep_logs.isChecked())
        if hasattr(self, "rep_generate_override"):
            self.rep_generate_override.setVisible(policy.show_reports_advanced_options)
        self._update_context_labels()
        self._sync_reports_empty_state()

    def _apply_mode_visibility(self) -> None:
        self.layout_policy_state = layout_policy(self.settings_state)
        self._apply_nav_mode_visibility()
        self._apply_settings_mode_visibility()
        self._apply_playbooks_mode_visibility()
        self._apply_reports_mode_visibility()
        self._filter_settings_nav()

    def _status_elapsed_text(self) -> str:
        if self.active_worker is None or self.active_run_started <= 0:
            return "0s"
        return f"{int(max(0.0, time.monotonic() - self.active_run_started))}s"

    def _set_run_status(self, title: str, detail: str) -> None:
        self._run_status_detail_raw = str(detail or "").strip()
        if hasattr(self, "run_status_title"):
            self.run_status_title.setText(title)
        if hasattr(self, "run_status_detail"):
            clean = " ".join(self._run_status_detail_raw.splitlines()).strip()
            metrics = QFontMetrics(self.run_status_detail.font())
            max_width = max(120, self.run_status_detail.width() - 8)
            self.run_status_detail.setText(metrics.elidedText(clean, Qt.ElideRight, max_width))
        if hasattr(self, "run_status_chip"):
            status = str(title or "").strip().lower()
            kind = "info"
            if "fail" in status or "error" in status:
                kind = "crit"
            elif "warn" in status:
                kind = "warn"
            elif "run" in status:
                kind = "info"
            elif "success" in status or "ready" in status or "done" in status:
                kind = "ok"
            self.run_status_chip.setText(title if len(title) <= 16 else title[:15] + ".")
            self.run_status_chip.setProperty("kind", kind)
            self.run_status_chip.style().unpolish(self.run_status_chip)
            self.run_status_chip.style().polish(self.run_status_chip)

    def _set_quick_check_busy(self, busy: bool, spinner: str = "") -> None:
        if not hasattr(self, "btn_quick_check"):
            return
        self.btn_quick_check.setEnabled(not busy)
        self.btn_quick_check.setProperty("busy", bool(busy))
        if busy:
            spin = f"{spinner} " if spinner else ""
            self.btn_quick_check.setText(f"{spin}Running Quick Check")
        else:
            self.btn_quick_check.setText(self._quick_check_default_label)
        self.btn_quick_check.style().unpolish(self.btn_quick_check)
        self.btn_quick_check.style().polish(self.btn_quick_check)

    def _update_run_status_indicator(self) -> None:
        if self.active_worker is None:
            self._set_quick_check_busy(False)
            detail = self._last_run_line[:120].strip()
            if detail:
                self._set_run_status("Ready", f"{self._last_run_status}: {detail}")
            else:
                self._set_run_status("Ready", self._last_run_status)
            return
        self._status_spinner_index = (self._status_spinner_index + 1) % len(self._status_spinner_frames)
        spinner = self._status_spinner_frames[self._status_spinner_index]
        elapsed = self._status_elapsed_text()
        title = f"Running: {self.active_run_name or 'Task'}"
        line = self._last_run_log_line[:120].strip() or self._last_run_line[:120].strip()
        if not line and self.active_run_id:
            try:
                run_row = get_run(self.active_run_id)
                if isinstance(run_row, dict):
                    line = str(run_row.get("last_log_line", "")).strip()[:120]
            except Exception:
                line = ""
        if not line:
            line = "Running... waiting for live output."
        detail = f"{spinner} {line} | {elapsed}"
        self._set_run_status(title, detail)
        if "quick check" in str(self.active_run_name or "").strip().lower():
            self._set_quick_check_busy(True, spinner=spinner)

    def _subscribe_run_status_events(self, run_id: str) -> None:
        self.active_run_id = str(run_id or "").strip()

    def _unsubscribe_run_status_events(self) -> None:
        self.active_run_id = ""

    def _handle_run_bus_event(self, event: Any) -> None:
        event_run_id = str(getattr(event, "run_id", "") or "").strip()
        if self.active_run_id and event_run_id and event_run_id != self.active_run_id:
            return
        kind = str(getattr(event, "event_type", "")).strip().upper()
        message = str(getattr(event, "message", "") or "").strip()
        if kind == RunEventType.START:
            self._last_run_status = "Running"
            self._last_run_log_line = ""
            if message:
                self._last_run_line = message
            return
        if kind in {RunEventType.PROGRESS, RunEventType.STATUS}:
            if message:
                self._last_run_line = message
            return
        if kind in {RunEventType.STDOUT, RunEventType.STDERR, RunEventType.WARNING}:
            if message:
                self._last_run_log_line = message
                self._last_run_line = message
            return
        if kind == RunEventType.ERROR:
            self._last_run_status = "Failed"
            if message:
                self._last_run_log_line = message
                self._last_run_line = message
            return
        if kind != RunEventType.END:
            return
        data = getattr(event, "data", {}) or {}
        if isinstance(data, dict):
            code = int(data.get("code", 0))
        else:
            code = 0
        if code == 0:
            self._last_run_status = "Success"
        elif code == 130:
            self._last_run_status = "Cancelled"
        else:
            self._last_run_status = "Failed"
        if message:
            self._last_run_line = message

    def _show_page_help(self, title: str, text: str) -> None:
        dialog = HelpCenterDialog(f"{title} Help", text, self)
        dialog.exec()

    def _setting_hint(self, text: str) -> QLabel:
        lbl = QLabel(text)
        lbl.setObjectName("SubTitle")
        lbl.setWordWrap(True)
        return lbl

    def _setting_details(self, text: str) -> DrawerCard:
        drawer = DrawerCard("Details")
        drawer.set_text(text)
        return drawer

    def _settings_nav_item_widget(self, label: str, icon_name: str = "settings") -> QWidget:
        row = QWidget()
        row.setAttribute(Qt.WA_TransparentForMouseEvents, True)
        lay = QHBoxLayout(row)
        lay.setContentsMargins(8, 0, 8, 0)
        lay.setSpacing(8)
        icon = QLabel()
        icon.setObjectName("SettingsNavIcon")
        icon.setAttribute(Qt.WA_TransparentForMouseEvents, True)
        icon.setPixmap(get_icon(icon_name, row, size=16).pixmap(16, 16))
        icon.setFixedSize(18, 18)
        text = QLabel(label)
        text.setAttribute(Qt.WA_TransparentForMouseEvents, True)
        text.setWordWrap(False)
        lay.addWidget(icon, 0)
        lay.addWidget(text, 1)
        return row

    def _rebuild_settings_nav_items(self) -> None:
        if not hasattr(self, "settings_nav"):
            return
        current = self.settings_nav.currentRow()
        row_height = resolve_density_tokens(self.settings_state.density).nav_item_height
        for index in range(self.settings_nav.count()):
            item = self.settings_nav.item(index)
            label = str(item.data(Qt.UserRole) or item.text())
            icon_name = str(item.data(Qt.UserRole + 1) or "settings")
            item.setSizeHint(QSize(0, row_height))
            self.settings_nav.setItemWidget(item, self._settings_nav_item_widget(label, icon_name))
        if current >= 0:
            self.settings_nav.setCurrentRow(current)

    def _filter_settings_nav(self) -> None:
        if not hasattr(self, "settings_nav"):
            return
        q = self.settings_search.text().strip().lower() if hasattr(self, "settings_search") else ""
        basic = self.settings_state.ui_mode == "basic"
        desc = {
            "safety": "risk admin advanced rollback",
            "privacy": "sessions evidence logs local-only masking share-safe not collected storage path",
            "appearance": "theme density palette right panel scale ui drawer pin",
            "advanced": "logs diagnostics data folder evidence",
            "feedback": "bug ui feature message",
        }
        first_visible = -1
        for index in range(self.settings_nav.count()):
            item = self.settings_nav.item(index)
            label = str(item.data(Qt.UserRole) or item.text()).lower()
            blob = f"{label} {desc.get(label, '')}"
            hide = bool(q and q not in blob)
            if basic and label == "advanced":
                hide = True
            item.setHidden(hide)
            if (not hide) and first_visible < 0:
                first_visible = index
        if first_visible >= 0 and self.settings_nav.currentRow() != first_visible:
            self.settings_nav.setCurrentRow(first_visible)

    def _reset_settings_defaults(self) -> None:
        self.settings_state = AppSettings().normalized()
        self.layout_policy_state = layout_policy(self.settings_state)
        save_settings(self.settings_state)
        self._sync_settings_ui()
        self.safety_policy = policy_from_settings(self.settings_state)
        self._apply_theme()
        self._apply_mode_visibility()
        self._refresh_fixes()
        self._refresh_toolbox()
        self._refresh_runbooks()
        self._refresh_home_favorites()
        self._update_weekly_status()
        self._update_context_labels()
        self._update_concierge()
        self.toasts.show_toast("Settings reset to defaults.")

    def reset_onboarding_flow(self) -> None:
        if self.settings_state.ui_mode != "pro":
            self.toasts.show_toast("Reset onboarding is available in Pro mode.")
            if hasattr(self, "b_reset_onboarding"):
                self.b_reset_onboarding.setEnabled(False)
            return
        self.settings_state.onboarding_completed = False
        save_settings(self.settings_state)
        if hasattr(self, "b_reset_onboarding"):
            self.b_reset_onboarding.setEnabled(True)
        self.toasts.show_toast("Onboarding reset. It will appear on next launch.")

    def _export_settings_json(self) -> None:
        if self.settings_state.ui_mode != "pro":
            self.toasts.show_toast("Export Settings JSON is available in Pro mode.")
            return
        path, _ = QFileDialog.getSaveFileName(self, "Export Settings JSON", "fixfox_settings.json", "JSON (*.json)")
        if not path:
            return
        payload = {
            "ui_mode": self.settings_state.ui_mode,
            "share_safe_default": self.settings_state.share_safe_default,
            "mask_ip_default": self.settings_state.mask_ip_default,
            "right_panel_open": self.settings_state.right_panel_open,
            "safe_only_mode": self.settings_state.safe_only_mode,
            "show_admin_tools": self.settings_state.show_admin_tools,
            "show_advanced_tools": self.settings_state.show_advanced_tools,
            "diagnostic_mode": self.settings_state.diagnostic_mode,
            "theme_palette": self.settings_state.theme_palette,
            "theme_mode": self.settings_state.theme_mode,
            "density": self.settings_state.density,
            "ui_scale_pct": self.settings_state.ui_scale_pct,
            "favorites_fixes": self.settings_state.favorites_fixes or [],
            "favorites_tools": self.settings_state.favorites_tools or [],
            "favorites_runbooks": self.settings_state.favorites_runbooks or [],
        }
        Path(path).write_text(json.dumps(payload, indent=2), encoding="utf-8")
        self.toasts.show_toast("Settings exported.")

    def _build_pages(self) -> None:
        self.home_page = HomePage(self)
        self.playbooks_page = PlaybooksPage(self)
        self.diagnose_page = DiagnosePage(self)
        self.fixes_page = FixesPage(self)
        self.reports_page = ReportsPage(self)
        self.history_page = HistoryPage(self)
        self.settings_page = SettingsPage(self)
        self.pages.addWidget(self.home_page)
        self.pages.addWidget(self.playbooks_page)
        self.pages.addWidget(self.diagnose_page)
        self.pages.addWidget(self.fixes_page)
        self.pages.addWidget(self.reports_page)
        self.pages.addWidget(self.history_page)
        self.pages.addWidget(self.settings_page)

    def _refresh_db_info_label(self) -> None:
        if not hasattr(self, "db_info_label"):
            return
        try:
            stats = db_stats()
            size_mb = float(stats.size_bytes) / (1024 * 1024)
            self.db_info_label.setText(
                f"DB: {stats.path}\nSize: {size_mb:.2f} MB | sessions={stats.sessions} runs={stats.runs} findings={stats.findings} artifacts={stats.artifacts} file_index={stats.file_index_rows}"
            )
        except Exception as exc:
            self.db_info_label.setText(f"Database info unavailable: {exc}")

    def _rebuild_database_index(self) -> None:
        def task(progress_cb: Any, partial_cb: Any, log_cb: Any, cancel_event: Any, timeout_s: int) -> dict[str, Any]:
            del partial_cb, cancel_event, timeout_s
            progress_cb(10, "Rebuilding SQLite index")
            log_cb("Rebuilding from session JSON files...")
            result = rebuild_from_sessions_folder()
            progress_cb(100, "Done")
            log_cb(
                f"Rebuild done: sessions={result.get('sessions', 0)} runs={result.get('runs', 0)} findings={result.get('findings', 0)} artifacts={result.get('artifacts', 0)}"
            )
            return result

        self._start_task(
            "Rebuild Database Index",
            task,
            self._on_rebuild_database_index,
            timeout_s=300,
            risk="Safe",
            plain_summary="Rebuilds SQLite query/index tables from persisted session JSON.",
            details_text="Reads sessions/*.json and sessions/index.json, then repopulates sessions/runs/findings/artifacts tables.",
            next_steps="Refresh History and Run Center to validate rebuilt results.",
            rerun_cb=self._rebuild_database_index,
            run_metadata={"kind": "maintenance", "capability_id": "db_rebuild"},
        )

    def _on_rebuild_database_index(self, payload: dict[str, Any]) -> None:
        self._refresh_db_info_label()
        self._refresh_home_history()
        self._refresh_history()
        self._refresh_run_center()
        self.toasts.show_toast(
            f"DB rebuild complete: sessions={payload.get('sessions', 0)} runs={payload.get('runs', 0)} findings={payload.get('findings', 0)}."
        )

    def _vacuum_database_now(self) -> None:
        try:
            vacuum_database()
            self._refresh_db_info_label()
            self.toasts.show_toast("Database vacuum complete.")
        except Exception as exc:
            self.toasts.show_toast(f"Database vacuum failed: {exc}")

    def _clear_file_index_now(self) -> None:
        try:
            removed = clear_file_index_db()
            self._fast_file_results = []
            if hasattr(self, "file_index_results"):
                self.file_index_results.setPlainText("")
            if hasattr(self, "file_index_status"):
                self.file_index_status.setText(f"File index cleared ({removed} rows removed).")
            self._refresh_db_info_label()
            self.toasts.show_toast(f"Cleared file index rows: {removed}")
        except Exception as exc:
            self.toasts.show_toast(f"Clear file index failed: {exc}")

    def _apply_theme(self, *, refresh_data: bool = True) -> None:
        started = time.perf_counter()
        palette = normalize_palette(self.settings_state.theme_palette)
        mode = normalize_mode(self.settings_state.theme_mode)
        density = normalize_density(self.settings_state.density)
        scale_pct = clamp_ui_scale(getattr(self.settings_state, "ui_scale_pct", 100))
        self.settings_state.theme_palette = palette
        self.settings_state.theme_mode = mode
        self.settings_state.density = density
        self.settings_state.ui_scale_pct = scale_pct
        set_ui_scale_percent(scale_pct)
        self.setMinimumSize(scaled_min_window_size(scale_pct))
        app = QApplication.instance()
        if app is not None:
            app.setStyleSheet(build_qss(resolve_theme_tokens(palette, mode), mode, density))
        if refresh_data:
            self._refresh_theme_icons()
        else:
            QTimer.singleShot(0, self._refresh_theme_icons)
        if refresh_data:
            self._apply_density(refresh_data=True)
        else:
            QTimer.singleShot(0, lambda: self._apply_density(refresh_data=False))
        self._sync_panel_toggle_icon()
        LOGGER.debug("theme_apply_ms=%.2f palette=%s mode=%s density=%s scale=%s", (time.perf_counter() - started) * 1000.0, palette, mode, density, scale_pct)

    def _refresh_theme_icons(self) -> None:
        clear_icon_cache()
        if hasattr(self.app_shell, "toolbar") and hasattr(self.app_shell.toolbar, "refresh_icons"):
            self.app_shell.toolbar.refresh_icons()
        if hasattr(self, "side_sheet") and hasattr(self.side_sheet, "refresh_icons"):
            self.side_sheet.refresh_icons()
        if hasattr(self, "nav_shell") and hasattr(self.nav_shell, "refresh_icons"):
            self.nav_shell.refresh_icons()

    def _on_ui_scale_value_changed(self, value: int) -> None:
        pct = clamp_ui_scale(value)
        self._pending_ui_scale_pct = pct
        if hasattr(self, "s_ui_scale_value"):
            self.s_ui_scale_value.setText(f"{pct}%")
        if self._syncing_settings:
            return
        self._scale_apply_timer.start()

    def _apply_pending_ui_scale(self) -> None:
        pct = clamp_ui_scale(getattr(self, "_pending_ui_scale_pct", 100))
        if pct == self.settings_state.ui_scale_pct:
            return
        self.settings_state.ui_scale_pct = pct
        self._apply_theme()

    def _persist_ui_scale_setting(self) -> None:
        if self._syncing_settings:
            return
        self.settings_state.ui_scale_pct = clamp_ui_scale(getattr(self, "_pending_ui_scale_pct", self.settings_state.ui_scale_pct))
        save_settings(self.settings_state)
        self.toasts.show_toast(f"UI scale set to {self.settings_state.ui_scale_pct}%.")

    def _apply_density(self, *, refresh_data: bool = True) -> None:
        started = time.perf_counter()
        self._mark_startup_phase("mainwindow:apply_density_start")
        density = self.settings_state.density
        current_nav = self.nav.currentRow()
        for widget in self.findChildren(QWidget):
            setter = getattr(widget, "set_density", None)
            if callable(setter):
                try:
                    setter(density)
                except Exception as exc:
                    LOGGER.debug("Density update skipped for %s: %s", widget.__class__.__name__, exc)
        self._build_nav()
        self._rebuild_settings_nav_items()
        apply_button_guardrails(self, self.settings_state.density)
        if self.layout_overlay is not None and self.layout_overlay.isVisible():
            self.layout_overlay.sync_geometry()
        if current_nav >= 0:
            self.nav.setCurrentRow(min(current_nav, len(self.NAV_ITEMS) - 1))
        self._apply_mode_visibility()
        if refresh_data:
            self._refresh_home_history(); self._refresh_history(); self._refresh_run_center(); self._refresh_fixes(); self._refresh_toolbox(); self._refresh_runbooks(); self._refresh_home_favorites(); self._rebuild_diagnose_sections(self.current_session.get("findings", []))
        self._mark_startup_phase("mainwindow:apply_density_done")
        LOGGER.debug("density_apply_ms=%.2f density=%s", (time.perf_counter() - started) * 1000.0, density)

    def _open_help_menu(self) -> None:
        menu = QMenu(self)
        menu.addAction("Start Here", lambda: self._show_page_help("Start Here", "Use local guidance for setup, runs, and exports."))
        menu.addAction("Privacy", lambda: self._open_settings_section("Privacy"))
        menu.addAction("Safety", lambda: self._open_settings_section("Safety"))
        sender = self.sender()
        if isinstance(sender, QWidget):
            menu.exec(sender.mapToGlobal(sender.rect().bottomLeft()))

    def _open_docs(self) -> None:
        docs_file = Path.cwd() / "docs" / "ui-polish-checklist.md"
        if docs_file.exists() and os.name == "nt":
            os.startfile(str(docs_file))
            return
        self._show_page_help("Docs", "See docs/ui-polish-checklist.md for manual QA steps.")

    def _show_about_fixfox_dialog(self) -> None:
        local_paths = ensure_dirs()
        commit = "unknown"
        try:
            commit = (
                subprocess.check_output(["git", "rev-parse", "--short", "HEAD"], text=True, stderr=subprocess.DEVNULL).strip()
                or "unknown"
            )
        except Exception:
            commit = os.environ.get("FIXFOX_COMMIT", "unknown").strip() or "unknown"
        build_date = datetime.utcnow().strftime("%Y-%m-%d")
        text = (
            f"{APP_DISPLAY_NAME} {APP_VERSION}\n"
            f"Build date: {build_date}\n"
            f"Commit: {commit}\n\n"
            "Local-only by design. No telemetry and no automatic cloud transfer.\n\n"
            f"Logs path: {local_paths['logs']}\n"
            f"Exports path: {local_paths['exports']}"
        )
        msg = QMessageBox(self)
        msg.setWindowTitle("About Fix Fox")
        msg.setText(text)
        mark = QPixmap(resource_path("assets/brand/fixfox_mark.png")).scaled(48, 48, Qt.KeepAspectRatio, Qt.SmoothTransformation)
        if not mark.isNull():
            msg.setIconPixmap(mark)
        else:
            msg.setIcon(QMessageBox.Information)
        msg.exec()

    def _open_header_overflow_menu(self) -> None:
        menu = QMenu(self)
        menu.setObjectName("HeaderOverflowMenu")

        menu.addSection("Session")
        menu.addAction("New Session", self._start_new_session)
        menu.addAction("Open Session", self._open_session_file)
        menu.addAction("Export", lambda: self.nav.setCurrentRow(self.NAV_ITEMS.index("Reports")))
        menu.addAction("Open Exports Folder", self.open_last_export_folder)

        menu.addSection("View")
        details_toggle = QAction("Toggle Details Panel", self)
        details_toggle.setCheckable(True)
        details_toggle.setChecked(not self.concierge.collapsed)
        details_toggle.triggered.connect(lambda checked: self._set_concierge_collapsed(not checked, persist=True))
        menu.addAction(details_toggle)
        menu.addSeparator()

        density_menu = menu.addMenu("Density")
        d_compact = QAction("Compact", self)
        d_compact.setCheckable(True)
        d_compact.setChecked(self.settings_state.density == "compact")
        d_compact.triggered.connect(lambda: self._set_density_mode("compact"))
        d_comfort = QAction("Comfortable", self)
        d_comfort.setCheckable(True)
        d_comfort.setChecked(self.settings_state.density != "compact")
        d_comfort.triggered.connect(lambda: self._set_density_mode("comfortable"))
        density_menu.addAction(d_compact)
        density_menu.addAction(d_comfort)

        theme_menu = menu.addMenu("Theme")
        t_light = QAction("Light", self)
        t_light.setCheckable(True)
        t_light.setChecked(self.settings_state.theme_mode == "light")
        t_light.triggered.connect(lambda: self._set_theme_mode("light"))
        t_dark = QAction("Dark", self)
        t_dark.setCheckable(True)
        t_dark.setChecked(self.settings_state.theme_mode == "dark")
        t_dark.triggered.connect(lambda: self._set_theme_mode("dark"))
        theme_menu.addAction(t_light)
        theme_menu.addAction(t_dark)

        palette_menu = menu.addMenu("Palette")
        for key in ("fixfox", "graphite", "high_contrast"):
            label = palette_label(key)
            action = QAction(label, self)
            action.setCheckable(True)
            action.setChecked(normalize_palette(self.settings_state.theme_palette) == key)
            action.triggered.connect(lambda _checked=False, value=key: self._set_theme_palette(value))
            palette_menu.addAction(action)

        mode_menu = menu.addMenu("Mode")
        m_basic = QAction("Basic", self)
        m_basic.setCheckable(True)
        m_basic.setChecked(self.settings_state.ui_mode == "basic")
        m_basic.triggered.connect(lambda: self.set_ui_mode("basic"))
        m_pro = QAction("Pro", self)
        m_pro.setCheckable(True)
        m_pro.setChecked(self.settings_state.ui_mode == "pro")
        m_pro.triggered.connect(lambda: self.set_ui_mode("pro"))
        mode_menu.addAction(m_basic)
        mode_menu.addAction(m_pro)

        menu.addSection("Help")
        menu.addAction("Docs", self._open_docs)
        menu.addAction("About Fix Fox", self._show_about_fixfox_dialog)
        if os.environ.get("FIXFOX_DEV_MODE", "1").strip() == "1":
            menu.addAction("Dump UI Tree", self._dump_ui_tree)

        sender = self.sender()
        if isinstance(sender, QWidget):
            menu.exec(sender.mapToGlobal(sender.rect().bottomLeft()))

    def _set_theme_mode(self, mode: str) -> None:
        self.settings_state.theme_mode = normalize_mode(mode)
        self._apply_theme()
        save_settings(self.settings_state)

    def _set_density_mode(self, density: str) -> None:
        self.settings_state.density = normalize_density(density)
        self._apply_theme()
        save_settings(self.settings_state)

    def _set_theme_palette(self, palette: str) -> None:
        self.settings_state.theme_palette = normalize_palette(palette)
        self._apply_theme()
        save_settings(self.settings_state)

    def _start_new_session(self) -> None:
        self.current_session = {}
        self.selected_finding = {}
        self._sync_context_bar_visibility()
        self._update_context_labels()
        self._update_concierge()
        self.toasts.show_toast("Started a new session context.")

    def _open_session_file(self) -> None:
        path, _ = QFileDialog.getOpenFileName(
            self,
            "Open Session JSON",
            str(ensure_dirs()["sessions"]),
            "JSON (*.json)",
        )
        if not path:
            return
        sid = Path(path).stem
        self._load_session(sid)
        self.toasts.show_toast(f"Opened session {sid}.")

    def _log_ui_audit(self, message: str) -> None:
        line = str(message or "").strip()
        if not line:
            return
        print(line)
        LOGGER.info(line)
        if hasattr(self, "concierge") and hasattr(self.concierge, "append_log"):
            try:
                self.concierge.append_log(line)
            except Exception:
                pass

    def _dump_ui_tree(self) -> None:
        lines: list[str] = ["[FixFox UI Tree]"]
        roots = (
            getattr(self, "nav_shell", None),
            getattr(self.app_shell, "toolbar", None),
            getattr(self, "details_drawer", None),
        )
        labels = ("nav", "header", "side_sheet")
        for label, root in zip(labels, roots):
            if root is None:
                continue
            lines.append(f"{label}: {root.objectName()} ({root.__class__.__name__})")
            for child in root.findChildren(QWidget):
                name = child.objectName() or "-"
                lines.append(f"  - {name} ({child.__class__.__name__})")
        for line in lines:
            self._log_ui_audit(line)

    def _run_ui_structure_audit_once(self) -> None:
        nav_rails = [w for w in self.findChildren(QWidget) if w.objectName() == "NavRail"]
        legacy_names = {"Nav", "NavList", "DrawerNav", "LegacyNav", "MainNav"}
        legacy_widgets = [w for w in self.findChildren(QWidget) if w.objectName() in legacy_names]
        list_nav_candidates = [
            w
            for w in self.findChildren(QWidget)
            if (w.__class__.__name__ in {"QListWidget", "QTreeView"}) and (w.objectName() in legacy_names)
        ]
        sheet_visible = bool(hasattr(self, "concierge") and self.concierge.isVisible())
        self._log_ui_audit(f"[FixFox UI AUDIT] nav_widgets={len(nav_rails)} expected=1 (NavRail)")
        self._log_ui_audit(f"[FixFox UI AUDIT] legacy_nav_widgets={len(legacy_widgets)} list_candidates={len(list_nav_candidates)}")
        self._log_ui_audit(f"[FixFox UI AUDIT] side_sheet_visible_default={sheet_visible}")
        dev_mode = os.environ.get("FIXFOX_DEV_MODE", "1").strip() == "1"
        if dev_mode and nav_rails and (legacy_widgets or list_nav_candidates):
            raise RuntimeError(
                "Legacy main navigation widgets detected alongside NavRail. "
                "Delete legacy nav list construction instead of hiding it."
            )

    def _open_settings_section(self, label: str) -> None:
        target = str(label or "").strip().lower()
        self.nav.setCurrentRow(self.NAV_ITEMS.index("Settings"))
        if not hasattr(self, "settings_nav"):
            return
        for idx in range(self.settings_nav.count()):
            item = self.settings_nav.item(idx)
            name = str(item.data(Qt.UserRole) or item.text()).strip().lower()
            if name == target or name.startswith(target) or target in name:
                item.setHidden(False)
                self.settings_nav.setCurrentRow(idx)
                return

    def _open_profile_menu(self) -> None:
        m = QMenu(self)
        a1 = QAction("Focus Search (Ctrl+K)", self); a1.triggered.connect(self._focus_top_search)
        a2 = QAction("Settings", self); a2.triggered.connect(lambda: self.nav.setCurrentRow(self.NAV_ITEMS.index("Settings")))
        m.addAction(a1); m.addAction(a2)
        sender = self.sender()
        if isinstance(sender, QWidget):
            m.exec(sender.mapToGlobal(sender.rect().bottomLeft()))

    def _show_onboarding_if_needed(self) -> None:
        if os.environ.get("FIXFOX_SKIP_ONBOARDING", "").strip() == "1":
            return
        force = os.environ.get("FIXFOX_FORCE_ONBOARDING", "").strip() == "1"
        if (not force) and os.environ.get("QT_QPA_PLATFORM", "").strip().lower() in {"offscreen", "minimal"}:
            return
        if self.settings_state.onboarding_completed:
            return
        d = OnboardingFlow(
            self,
            theme_mode=self.settings_state.theme_mode,
            density=self.settings_state.density,
            ui_mode=self.settings_state.ui_mode,
            apply_preferences=self._apply_onboarding_preferences,
            can_resume_session=True,
        )
        completed = d.exec() == QDialog.Accepted and d.completed
        self.settings_state.onboarding_completed = bool(completed)
        save_settings(self.settings_state)
        if not completed:
            return
        if d.result_action == "quick_check":
            self.run_quick_check("Quick Check")
        elif d.result_action == "settings":
            self._open_settings_section("Appearance")
        elif d.result_action == "resume":
            self.nav.setCurrentRow(self.NAV_ITEMS.index("History"))
        else:
            self.nav.setCurrentRow(self.NAV_ITEMS.index("Home"))
        self.toasts.show_toast("You're set up.", timeout_ms=3200)

    def _apply_onboarding_preferences(self, theme_mode: str, density: str, ui_mode: str) -> None:
        self.settings_state.theme_mode = normalize_mode(theme_mode)
        self.settings_state.density = normalize_density(density)
        self.settings_state.ui_mode = "pro" if str(ui_mode).strip().lower() == "pro" else "basic"
        self._apply_theme()
        self._sync_ui_mode_controls()
        self._apply_mode_visibility()

    def _on_nav(self, idx: int) -> None:
        if idx < 0 or idx >= len(self.NAV_ITEMS):
            return
        self.pages.setCurrentIndex(idx)
        page_name = self.NAV_ITEMS[idx]
        self.settings_state.last_page = page_name
        if page_name in {"History", "Reports"}:
            refresh_dynamic_index_async(force=True)
        self._sync_context_bar_visibility()
        self._sync_shell_status_bar()
        self._update_concierge()

    def _on_nav_collapsed_changed(self, collapsed: bool) -> None:
        self.settings_state.nav_collapsed = True
        self._sync_shell_status_bar()

    def _on_settings_section_changed(self, idx: int) -> None:
        if not hasattr(self, "settings_nav"):
            return
        if idx < 0 or idx >= self.settings_nav.count():
            return
        item = self.settings_nav.item(idx)
        self.settings_state.last_settings_section = str(item.data(Qt.UserRole) or item.text() or "Safety")

    def _restore_layout_state(self) -> None:
        s = self.settings_state.normalized()
        self.resize(s.window_width, s.window_height)
        if s.window_x >= 0 and s.window_y >= 0:
            self.move(s.window_x, s.window_y)
        sizes = list(s.splitter_sizes or [])
        if len(sizes) >= 3:
            self.app_shell.set_details_width(max(MIN_RIGHT_PANEL_WIDTH, sizes[2]))
        self.settings_state.nav_collapsed = True
        page = s.last_page if s.last_page in self.NAV_ITEMS else "Home"
        self.nav.setCurrentRow(self.NAV_ITEMS.index(page))
        if hasattr(self, "settings_nav"):
            target = str(s.last_settings_section or "Safety").strip().lower()
            for idx in range(self.settings_nav.count()):
                item = self.settings_nav.item(idx)
                label = str(item.data(Qt.UserRole) or item.text()).strip().lower()
                if label == target:
                    self.settings_nav.setCurrentRow(idx)
                    break

    def _persist_layout_state(self) -> None:
        geo = self.geometry()
        self.settings_state.window_x = int(geo.x())
        self.settings_state.window_y = int(geo.y())
        self.settings_state.window_width = int(max(1080, geo.width()))
        self.settings_state.window_height = int(max(720, geo.height()))
        details_width = int(getattr(self.app_shell, "details_width", lambda: MIN_RIGHT_PANEL_WIDTH)())
        self.settings_state.splitter_sizes = [72, int(max(640, geo.width() - details_width - 72)), details_width]
        self.settings_state.nav_collapsed = True
        nav_idx = self.nav.currentRow()
        if 0 <= nav_idx < len(self.NAV_ITEMS):
            self.settings_state.last_page = self.NAV_ITEMS[nav_idx]
        if hasattr(self, "settings_nav"):
            settings_idx = self.settings_nav.currentRow()
            if 0 <= settings_idx < self.settings_nav.count():
                item = self.settings_nav.item(settings_idx)
                self.settings_state.last_settings_section = str(item.data(Qt.UserRole) or item.text() or "Safety")
        save_settings(self.settings_state)

    def _toggle_concierge(self) -> None:
        self._set_concierge_collapsed(not self.concierge.collapsed, persist=True)

    def _set_concierge_collapsed(self, collapsed: bool, persist: bool) -> None:
        self._persist_concierge_events = persist
        self.app_shell.set_details_open(not collapsed)
        self._persist_concierge_events = True
        self._sync_panel_toggle_icon()

    def _sync_shell_status_bar(self) -> None:
        if not hasattr(self, "shell_session_label"):
            return
        sid = str(self.current_session.get("session_id", "none")) if isinstance(self.current_session, dict) else "none"
        mode = "Pro" if self.settings_state.ui_mode == "pro" else "Basic"
        safety = "Safe-only ON" if self.settings_state.safe_only_mode else "Safe-only OFF"
        self.shell_session_label.setText(f"Session: {sid}")
        self.shell_mode_label.setText(f"Mode: {mode}")
        self.shell_safety_label.setText(f"{safety} | Rail navigation")

    def _page_callout_attr(self, page_name: str) -> str:
        mapping = {
            "Home": "home_callout",
            "Diagnose": "diag_callout",
            "Fixes": "fix_callout",
            "Reports": "rep_callout",
            "History": "hist_callout",
            "Playbooks": "pb_callout",
        }
        return mapping.get(page_name, "")

    def _show_page_callout(self, page_name: str, title: str, message: str, level: str = "warn") -> None:
        attr = self._page_callout_attr(page_name)
        if not attr:
            return
        widget = getattr(self, attr, None)
        if widget is None or not hasattr(widget, "set_message"):
            return
        widget.set_message(title, message, level=level)

    def _clear_page_callouts(self) -> None:
        for page_name in ("Home", "Diagnose", "Fixes", "Reports", "History", "Playbooks"):
            attr = self._page_callout_attr(page_name)
            widget = getattr(self, attr, None)
            if widget is None or not hasattr(widget, "set_message"):
                continue
            widget.set_message("", "", level="info")

    def _apply_responsive_concierge(self) -> None:
        pinned = bool(getattr(self.concierge, "pinned", getattr(self.settings_state, "details_drawer_pinned", True)))
        narrow = should_auto_collapse_right_panel(self.width()) and (not pinned)
        if narrow and not self._auto_concierge_collapse:
            self._auto_concierge_collapse = True
            self._set_concierge_collapsed(True, persist=False)
        elif not narrow and self._auto_concierge_collapse:
            self._auto_concierge_collapse = False
            self._set_concierge_collapsed(not self.settings_state.right_panel_open, persist=False)

    def _apply_responsive_header(self) -> None:
        if not hasattr(self, "top_search_stack"):
            return
        narrow = self.width() < 1120
        very_narrow = self.width() < 980
        self.app_shell.toolbar.set_search_collapsed(narrow, animate=False)
        if hasattr(self, "run_status_detail"):
            self.run_status_detail.setVisible(not very_narrow)

    def _toggle_layout_debug_overlay(self) -> None:
        if self.layout_overlay is None:
            self.layout_overlay = LayoutDebugOverlay(self)
        if self.layout_overlay.isVisible():
            self.layout_overlay.hide()
            self.toasts.show_toast("Layout debug overlay off.")
            return
        self.layout_overlay.sync_geometry()
        self.layout_overlay.show()
        self.toasts.show_toast("Layout debug overlay on (Ctrl+Alt+L).")

    def _run_ui_self_check(self) -> None:
        report_path = logs_dir() / f"ui_self_check_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log"
        lines: list[str] = [f"Fix Fox UI self-check @ {_now_local()}"]
        failures = 0
        checks = 0
        current_index = self.nav.currentRow()

        def record(ok: bool, name: str, detail: str = "") -> None:
            nonlocal failures, checks
            checks += 1
            status = "PASS" if ok else "FAIL"
            if not ok:
                failures += 1
            suffix = f" | {detail}" if detail else ""
            lines.append(f"{status} | {name}{suffix}")

        try:
            page_checks: dict[str, tuple[str, ...]] = {
                "Home": ("home_recent", "home_favorites"),
                "Playbooks": ("pb_stack", "tb_all", "rb_feed"),
                "Diagnose": ("diag_search", "diag_feed"),
                "Fixes": ("fix_list", "fix_detail"),
                "Reports": ("rep_steps", "rep_tree", "rep_evidence"),
                "History": ("hist_list", "run_center"),
                "Settings": ("settings_nav", "settings_stack"),
            }
            for idx, page in enumerate(self.NAV_ITEMS):
                self.nav.setCurrentRow(idx)
                QApplication.processEvents()
                record(self.pages.currentIndex() == idx, f"page_switch:{page}", f"index={self.pages.currentIndex()}")
                for attr in page_checks.get(page, ()):
                    record(hasattr(self, attr) and getattr(self, attr) is not None, f"widget:{page}:{attr}")

            tasks = list_script_tasks()
            task_meta = script_task_map()
            record(len(search_tools("")) >= 0, "tool_directory_query")
            record(len(tasks) > 0, "script_tasks_loaded", f"count={len(tasks)}")
            record(len(RUNBOOKS) > 0, "runbooks_loaded", f"count={len(RUNBOOKS)}")
            record(len(task_meta) > 0, "script_task_map_loaded", f"count={len(task_meta)}")
        except Exception as exc:
            failures += 1
            lines.append(f"FAIL | unexpected_exception | {exc}")
            lines.append(traceback.format_exc().strip())
        finally:
            if current_index >= 0:
                self.nav.setCurrentRow(current_index)
                QApplication.processEvents()

        lines.append(f"Summary: {checks - failures}/{checks} passed")
        report_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        LOGGER.info("UI self-check wrote report: %s", report_path)
        self.toasts.show_toast(
            f"UI self-check {'passed' if failures == 0 else 'found issues'} ({checks - failures}/{checks})."
        )

    def _sync_panel_toggle_icon(self) -> None:
        if hasattr(self, "btn_panel_toggle"):
            self.app_shell.toolbar.set_details_open(not self.concierge.collapsed)

    def _on_concierge(self, collapsed: bool) -> None:
        if self._persist_concierge_events:
            self.settings_state.right_panel_open = not collapsed
            save_settings(self.settings_state)
        self._sync_panel_toggle_icon()

    def _on_side_sheet_pin_changed(self, pinned: bool) -> None:
        self.settings_state.details_drawer_pinned = bool(pinned)
        save_settings(self.settings_state)

    def _update_concierge(self) -> None:
        self.concierge.clear_widgets()
        page = self.NAV_ITEMS[self.nav.currentRow()] if self.nav.currentRow() >= 0 else "Home"
        detail_title, detail_text = self._detail_for_page(page)
        action_text, action_cb = self._detail_action_for_page(page)
        action_btn = PrimaryButton(action_text)
        action_btn.clicked.connect(action_cb)
        detail_card = Card(detail_title, detail_text, right_widget=action_btn)
        self._prepare_concierge_widget(detail_card)
        self.concierge.add_widget(detail_card)
        if page == "Diagnose" and self.selected_finding:
            collect_btn = SoftButton("Collect Evidence Now")
            collect_btn.clicked.connect(self._collect_core_evidence)
            evidence = Card("Evidence Impact", self._evidence_hint_for_finding(self.selected_finding), right_widget=collect_btn)
            self._prepare_concierge_widget(evidence)
            self.concierge.add_widget(evidence)
        tips = Card("Recent changes / tips", self._changes_text())
        self._prepare_concierge_widget(tips)
        self.concierge.add_widget(tips)

    def _prepare_concierge_widget(self, widget: QWidget) -> None:
        widget.setSizePolicy(QSizePolicy.Expanding, QSizePolicy.Preferred)
        if hasattr(self, "concierge"):
            max_width = max(180, self.concierge.width() - 40)
        else:
            max_width = max(180, self.details_drawer.width() - 18) if hasattr(self, "details_drawer") else 420
        widget.setMaximumWidth(max_width)

    def _clamp_concierge_card_widths(self) -> None:
        if not hasattr(self, "concierge"):
            return
        max_width = max(180, self.concierge.width() - 40)
        for widget in self.concierge.findChildren(QWidget):
            if widget.objectName() in {"Card", "Drawer", "EmptyState"}:
                widget.setMaximumWidth(max_width)

    def _detail_for_page(self, page: str) -> tuple[str, str]:
        if page == "Diagnose":
            if not self.selected_finding:
                return ("Finding Detail", "Select a finding to view meaning and technical context.")
            plain = str(self.selected_finding.get("plain", self.selected_finding.get("detail", ""))).strip()
            technical = str(self.selected_finding.get("technical", "")).strip()
            text = plain or "No plain-language detail available."
            if technical:
                text += f"\n\nTechnical:\n{technical[:600]}"
            return ("Finding Detail", text)
        if page == "Fixes":
            selected = getattr(self, "selected_fix_key", "")
            fx = next((row for row in FIX_CATALOG if row.key == selected), None)
            if fx is None:
                return ("Fix Detail", "Select a fix to review risk, rollback, and commands.")
            return (
                fx.title,
                f"{fx.plain}\n\nRisk: {fx.risk}\nRollback: {fx.rollback}\n\nWhat changes:\n{fx.description}",
            )
        if page == "Playbooks":
            if self.layout_policy_state.show_playbooks_guided_basic and not any([getattr(self, "selected_tool_id", ""), getattr(self, "selected_task_id", ""), self.rb_selected_id]):
                return ("Guided Goals", "Choose a basic goal card to run the safest guided workflow.")
            selected_tool = getattr(self, "selected_tool_id", "")
            tool = next((row for row in TOOL_DIRECTORY if row.id == selected_tool), None)
            if tool is not None:
                return (
                    tool.title,
                    f"{tool.plain}\n\nWhen to use:\n{tool.desc}\n\nCollects/changes:\nLaunches: {tool.command}",
                )
            selected_task = script_task_map().get(getattr(self, "selected_task_id", ""))
            if selected_task is not None:
                return (
                    selected_task.title,
                    f"{selected_task.desc}\n\nCategory: {selected_task.category}\nRisk: {selected_task.risk}\nAdmin required: {selected_task.admin_required}",
                )
            rb = runbook_map().get(self.rb_selected_id) if self.rb_selected_id else None
            if rb is not None:
                return (
                    rb.title,
                    f"{rb.desc}\n\nAudience: {rb.audience}\nSteps: {len(rb.steps)}",
                )
            return ("Playbook Detail", "Select a tool or runbook to see action details.")
        if page == "Reports":
            return ("Export Context", "Build a validated pack, verify masking, and copy ticket summaries.")
        if page == "History":
            return ("Case Context", "Reopen, compare, and re-export prior sessions.")
        if page == "Settings":
            return ("Settings Context", "Safety policy affects Fixes, Tools, and runbooks immediately.")
        return ("Home Context", "Start from a goal card to create session-backed diagnostics and actions.")

    def _detail_action_for_page(self, page: str) -> tuple[str, Callable[[], None]]:
        if page == "Diagnose":
            return self._diagnose_action()
        if page == "Fixes":
            selected = getattr(self, "selected_fix_key", "")
            if selected:
                return ("Run Selected Fix", lambda: self.run_fix_action(selected))
            return ("Open Fixes", lambda: self.nav.setCurrentRow(self.NAV_ITEMS.index("Fixes")))
        if page == "Playbooks":
            if self.layout_policy_state.show_playbooks_guided_basic and not any([getattr(self, "selected_tool_id", ""), getattr(self, "selected_task_id", ""), self.rb_selected_id]):
                return ("Run Quick Check", lambda: self.run_quick_check("Quick Check"))
            selected_tool = getattr(self, "selected_tool_id", "")
            if selected_tool:
                return ("Run Selected Tool", lambda: self._launch_tool_payload(selected_tool))
            selected_task = getattr(self, "selected_task_id", "")
            if selected_task:
                return ("Run Selected Script Task", lambda: self._run_script_task(selected_task, dry_run=False))
            if self.rb_selected_id:
                return ("Run Selected Runbook", lambda: self.run_selected_runbook(True))
            return ("Open Tool Directory", lambda: self.nav.setCurrentRow(self.NAV_ITEMS.index("Playbooks")))
        if page == "Reports":
            return ("Generate Export", self.export_current_session)
        if page == "History":
            return ("Reopen Selected Session", self.reopen_selected_session)
        if page == "Settings":
            return ("Open Help Center", lambda: self._show_page_help("Settings", "Policy, privacy, and support guidance."))
        return ("Start Quick Check", lambda: self.run_quick_check("Quick Check"))

    def _diagnose_action(self) -> tuple[str, Callable[[], None]]:
        finding = self.selected_finding or {}
        title = str(finding.get("title", "")).lower()
        if "disk" in title:
            return ("Open Storage Settings Fix", lambda: self.run_fix_action("open_storage"))
        if "memory" in title or "cpu" in title:
            return ("Run Speed Runbook (Dry)", lambda: (self._select_runbook("home_speed_up_pc_safe"), self.run_selected_runbook(True)))
        if "proxy" in title or "network" in title or "wifi" in title:
            return ("Run Wi-Fi Runbook (Dry)", lambda: (self._select_runbook("home_fix_wifi_safe"), self.run_selected_runbook(True)))
        if "reboot" in title or "update" in title:
            return ("Open Windows Update Tool", lambda: self._launch_tool_payload("tool_windows_update"))
        if self.current_session:
            return ("Open Fixes", lambda: self.nav.setCurrentRow(self.NAV_ITEMS.index("Fixes")))
        return ("Run Quick Check", lambda: self.run_quick_check("Quick Check"))

    def _diagnose_text(self) -> str:
        rows = self.current_session.get("findings", [])
        return "Run scan to populate findings." if not rows else f"{len(rows)} findings available."

    def _changes_text(self) -> str:
        rows = load_index()
        return "No prior session for comparison." if len(rows) < 2 else f"Compare with {rows[1].session_id} in History."

    def _evidence_hint_for_finding(self, finding: dict[str, Any]) -> str:
        title = str(finding.get("title", "")).lower()
        if "network" in title or "proxy" in title or "wifi" in title:
            return "Helpful evidence: network bundle, DNS/proxy outputs, adapter state."
        if "disk" in title or "storage" in title:
            return "Helpful evidence: storage snapshot, large-file radar, cleanup preview."
        if "printer" in title:
            return "Helpful evidence: spooler status, queue snapshot, PrintService events."
        if "update" in title or "reboot" in title:
            return "Helpful evidence: update service status and pending reboot flags."
        return "Helpful evidence: system snapshot + event logs."

    def _has_active_session(self) -> bool:
        return bool(self.current_session and str(self.current_session.get("session_id", "")).strip())

    def _sync_context_bar_visibility(self) -> None:
        active = self._has_active_session()
        if hasattr(self, "ctx_full"):
            self.ctx_full.setVisible(active)
        if hasattr(self, "ctx_hint"):
            self.ctx_hint.setVisible(not active)
        self.context.setVisible(True)

    def _top_findings_text(self, rows: list[dict[str, Any]]) -> str:
        top = rows[:3]
        if not top:
            return "No findings"
        return " | ".join([str(x.get("title", "")) for x in top])

    def _mask(self, text: str) -> str:
        return mask_text(
            text,
            MaskingOptions(
                enabled=self.rep_safe.isChecked() if hasattr(self, "rep_safe") else self.settings_state.share_safe_default,
                mask_ip=self.rep_ip.isChecked() if hasattr(self, "rep_ip") else self.settings_state.mask_ip_default,
                extra_tokens=(
                    str(self.current_session.get("sysinfo", {}).get("hostname", "")),
                    str(self.current_session.get("sysinfo", {}).get("user", "")),
                    str(self.current_session.get("network", {}).get("ssid", "")),
                ),
            ),
        )

    def _copy_text(self, text: str) -> None:
        self.clipboard().setText(self._mask(text))
        self.toasts.show_toast("Copied (share-safe applied).")

    def _start_task(
        self,
        name: str,
        fn: Any,
        on_result: Any,
        timeout_s: int = 120,
        *,
        risk: str = "Safe",
        plain_summary: str = "",
        details_text: str = "",
        next_steps: str = "",
        rerun_cb: Callable[[], None] | None = None,
        evidence_root: str = "",
        run_id: str = "",
        run_metadata: dict[str, Any] | None = None,
    ) -> str:
        if self.active_worker is not None:
            self.toasts.show_toast("Another task is running.")
            return ""
        resolved_run_id = str(run_id or "").strip()
        if not resolved_run_id:
            resolved_run_id = self.run_event_bus.create_run(
                name=name,
                risk=risk,
                session_id=str(self.current_session.get("session_id", "")) if self.current_session else "",
                metadata=run_metadata or {},
            )
        self.active_run_id = resolved_run_id
        self.active_run_name = name
        self.active_run_started = time.monotonic()
        self._clear_page_callouts()
        self._last_run_status = "Running"
        self._last_run_line = "Starting..."
        self._last_run_log_line = ""
        self._subscribe_run_status_events(resolved_run_id)
        self.run_event_bus.publish(resolved_run_id, RunEventType.STATUS, message=f"Running: {name}")
        self._set_run_status(f"Running: {name}", "Starting... | 0s")
        if "quick check" in str(name or "").strip().lower():
            self._set_quick_check_busy(True)
        self.tool_runner = ToolRunnerWindow(
            name,
            risk=risk,
            session_id=str(self.current_session.get("session_id", "")) if self.current_session else "",
            run_id=resolved_run_id,
            event_bus=self.run_event_bus,
            plain_summary=plain_summary or f"Plain English: {name} is running.",
            details_text=details_text,
            next_steps=next_steps,
            mask_fn=self._mask,
            evidence_root=evidence_root,
            parent=self,
        )
        self.tool_runner.cancel_requested.connect(self._cancel_task)
        self.tool_runner.export_requested.connect(lambda: self.nav.setCurrentRow(self.NAV_ITEMS.index("Reports")))
        if rerun_cb is not None:
            self.tool_runner.rerun_requested.connect(rerun_cb)
        else:
            self.tool_runner.rerun_requested.connect(lambda: self.toasts.show_toast("Use the page action to re-run with same options."))
        self.tool_runner.output_saved.connect(self._on_tool_runner_output_saved)
        self.tool_runner.show()
        self.toasts.show_toast(f"{name}: running...")
        self.btn_cancel_task.setEnabled(True)
        worker = TaskWorker(fn, config=WorkerConfig(timeout_s=timeout_s))
        self.active_worker = worker
        worker.signals.progress.connect(lambda p, t: self._on_run_progress(resolved_run_id, name, p, t))
        worker.signals.partial.connect(lambda payload: self._on_run_partial(resolved_run_id, payload))
        worker.signals.log_line.connect(lambda line: self._on_run_log_line(resolved_run_id, line))
        worker.signals.result.connect(on_result)
        worker.signals.result.connect(lambda payload: self._on_run_result_event(resolved_run_id, payload))
        worker.signals.result.connect(self.tool_runner.on_result)
        worker.signals.error.connect(lambda msg: self._on_task_error(name, str(msg), run_id=resolved_run_id))
        worker.signals.cancelled.connect(lambda: self._on_run_cancelled(resolved_run_id, name))
        worker.signals.cancelled.connect(self.tool_runner.on_cancelled)
        worker.signals.finished.connect(self._finish_task)
        start_worker(worker)
        return resolved_run_id

    def _on_run_progress(self, run_id: str, name: str, pct: int, text: str) -> None:
        if text:
            self._last_run_line = str(text).strip()
        self.run_event_bus.publish(
            run_id,
            RunEventType.PROGRESS,
            message=str(text or "").strip(),
            progress=max(0, min(100, int(pct))),
        )
        if text:
            self.run_event_bus.publish(run_id, RunEventType.STATUS, message=str(text).strip())

    def _on_run_partial(self, run_id: str, payload: Any) -> None:
        if self.tool_runner is not None:
            self.tool_runner.on_partial(payload)
        data = payload if isinstance(payload, dict) else {"payload": str(payload)}
        self._last_run_line = "Received partial update."
        self.run_event_bus.publish(run_id, RunEventType.STATUS, message="Received partial update.")
        self.run_event_bus.publish(run_id, RunEventType.WARNING, message="Partial update received.", data=data)

    def _on_run_log_line(self, run_id: str, line: str) -> None:
        text = str(line or "").rstrip()
        if not text:
            return
        self._last_run_log_line = text
        self._last_run_line = text
        if text.lower().startswith("[stderr]"):
            self.run_event_bus.publish(run_id, RunEventType.STDERR, message=text)
            return
        self.run_event_bus.publish(run_id, RunEventType.STDOUT, message=text)

    def _on_run_result_event(self, run_id: str, payload: Any) -> None:
        data = payload if isinstance(payload, dict) else {"payload": str(payload)}
        if isinstance(data, dict) and "code" in data:
            code = int(data.get("code", 0))
        elif isinstance(data, dict) and data.get("cancelled"):
            code = 130
        else:
            code = 0
        if isinstance(data, dict):
            for key in ("output_files", "evidence_files"):
                rows = data.get(key, [])
                if isinstance(rows, list):
                    for path in rows:
                        p = str(path or "").strip()
                        if p:
                            self.run_event_bus.publish(run_id, RunEventType.ARTIFACT, message=f"Artifact: {p}", data={"path": p})
        if code != 0:
            message = str(data.get("user_message", "")).strip() or f"Run finished with code {code}."
            self.run_event_bus.publish(run_id, RunEventType.ERROR, message=message, data={"code": code})
            self.run_event_bus.publish(run_id, RunEventType.STATUS, message=f"Failed: {message}")
            self._last_run_status = "Failed"
            self._last_run_line = message
        else:
            self.run_event_bus.publish(run_id, RunEventType.STATUS, message="Completed successfully.")
            self._last_run_status = "Success"
            self._last_run_line = "Run completed successfully."
        self.run_event_bus.publish(run_id, RunEventType.END, message=f"Run finished with code {code}.", data={"code": code})

    def _on_run_cancelled(self, run_id: str, name: str) -> None:
        self.toasts.show_toast(f"{name} cancelled.")
        self.run_event_bus.publish(run_id, RunEventType.STATUS, message="Cancelled by user.")
        self.run_event_bus.publish(run_id, RunEventType.WARNING, message="Cancellation requested by user.")
        self.run_event_bus.publish(run_id, RunEventType.END, message="Run cancelled.", data={"code": 130})
        self._last_run_status = "Cancelled"
        self._last_run_line = "Cancelled by user."

    def _on_task_error(self, name: str, message: str, *, run_id: str = "") -> None:
        text = (message or "").strip()
        reason = ""
        next_steps: list[str] = []
        for raw_line in text.splitlines():
            line = raw_line.strip()
            if not line:
                continue
            lower = line.lower()
            if lower.startswith("reason:") and not reason:
                reason = line.split(":", 1)[1].strip()
                continue
            if lower.startswith("try next:"):
                step = line.split(":", 1)[1].strip()
                if step:
                    next_steps.append(step)
        if not reason:
            reason = text.splitlines()[0].strip() if text else "The operation failed."
        if not next_steps:
            next_steps = [
                "Re-run the same action.",
                "Run a related safe diagnostic first.",
                "Export a partial support pack.",
            ]
        plain = f"{name} failed: {reason}"
        rendered = "\n".join(
            [
                f"What failed: {name} did not complete.",
                f"Why: {reason}",
                *[f"Try next: {step}" for step in next_steps[:3]],
            ]
        )
        self.toasts.show_toast(plain)
        page = self.NAV_ITEMS[self.nav.currentRow()] if self.nav.currentRow() >= 0 else "Home"
        self._show_page_callout(page, f"{name} failed", reason, level="error")
        self._last_run_status = "Failed"
        self._last_run_line = reason
        rid = str(run_id or self.active_run_id or "").strip()
        if rid:
            self.run_event_bus.publish(rid, RunEventType.STATUS, message=f"Failed: {reason}")
            self.run_event_bus.publish(rid, RunEventType.ERROR, message=reason)
            for step in next_steps[:3]:
                self.run_event_bus.publish(rid, RunEventType.WARNING, message=f"Try next: {step}")
            self.run_event_bus.publish(rid, RunEventType.END, message="Run failed.", data={"code": 1})
        if self.tool_runner is not None:
            self.tool_runner.on_error(rendered)

    def _cancel_task(self) -> None:
        if self.active_worker:
            rid = str(self.active_run_id or "").strip()
            if rid:
                self.run_event_bus.publish(rid, RunEventType.STATUS, message="Cancellation requested by user.")
            self.active_worker.cancel()
            self.toasts.show_toast("Cancellation requested.")

    def _finish_task(self) -> None:
        self.active_worker = None
        self.active_run_id = ""
        self.active_run_name = ""
        self.active_run_started = 0.0
        self.btn_cancel_task.setEnabled(False)
        self._set_quick_check_busy(False)
        self._unsubscribe_run_status_events()
        self._update_run_status_indicator()

    def closeEvent(self, event: Any) -> None:  # type: ignore[override]
        self._unsubscribe_run_status_events()
        if self._run_status_subscription_id > 0:
            self.run_event_bus.unsubscribe(self._run_status_subscription_id)
            self._run_status_subscription_id = 0
        app = QApplication.instance()
        if app is not None:
            app.removeEventFilter(self)
        self._persist_layout_state()
        self._cancel_active_search_worker()
        self._search_popup.hide_popup()
        if self.tool_runner is not None:
            self.tool_runner.close()
            self.tool_runner = None
        super().closeEvent(event)

    def _on_tool_runner_output_saved(self, path: str) -> None:
        self._merge_files_into_session_evidence([path], "runner", "tool_runner_output")
        self._refresh_evidence_items()
        self.toasts.show_toast("Tool output saved and attached to evidence.")

    def run_quick_check(self, symptom: str) -> None:
        self.diag_loading.show()
        self._rebuild_diagnose_sections([])

        def task(progress_cb: Any, partial_cb: Any, log_cb: Any, cancel_event: Any, timeout_s: int) -> dict[str, Any]:
            del partial_cb, timeout_s
            if cancel_event.is_set():
                return {}
            progress_cb(20, "Collecting diagnostics")
            log_cb("Collecting baseline diagnostics.")
            data = diagnostics.quick_check()
            if cancel_event.is_set():
                return {}
            data["session_id"] = new_session_id(); data["symptom"] = symptom; data["created_local"] = _now_local(); data["actions"] = []
            progress_cb(100, "Done")
            log_cb(f"Collected {len(data.get('findings', []))} findings.")
            return data

        self._start_task(
            "Quick Check",
            task,
            self._on_quick_check,
            timeout_s=90,
            plain_summary="Runs diagnostics and builds a new session with findings.",
            details_text="Includes CPU, memory, disk, update and network heuristics.",
            next_steps="Review findings in Diagnose, then run a safe fix or runbook.",
            rerun_cb=lambda: self.run_quick_check(symptom),
            run_metadata={"kind": "diagnostic", "capability_id": "quick_check", "diagnostic": "quick_check"},
        )

    def _on_quick_check(self, data: dict[str, Any]) -> None:
        self.diag_loading.hide()
        if not data:
            return
        if self.settings_state.weekly_reminder_enabled:
            self.settings_state.last_weekly_check_utc = datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ")
            save_settings(self.settings_state)
        self.current_session = data
        persist_new_session(data, summary=f"{len(data.get('findings', []))} findings")
        self.diag_summary.title.setText(f"Results for: {data.get('symptom', 'Quick Check')}")
        self.diag_summary.sub.setText("Scan complete. Review grouped findings and run a recommended fix.")
        rows = data.get("findings", [])
        self.diag_top3.sub.setText(self._top_findings_text(rows))
        counts = {"CRIT": 0, "WARN": 0, "OK": 0, "INFO": 0}
        for row in rows:
            status = str(row.get("status", "INFO")).upper()
            if status in counts:
                counts[status] += 1
        self.diag_counts.sub.setText(f"CRIT {counts['CRIT']} | WARN {counts['WARN']} | OK {counts['OK']} | INFO {counts['INFO']}")
        self._rebuild_diagnose_sections(rows)
        self._update_status_from_session(data)
        self._rebuild_report_tree(); self._update_redaction_preview(); self._refresh_evidence_items(); self._refresh_home_history(); self._refresh_history(); self._refresh_run_center(); self._update_context_labels(); self._update_weekly_status(); self._update_diagnose_context({}); self._update_concierge()
        self._sync_shell_status_bar()
        self.nav.setCurrentRow(self.NAV_ITEMS.index("Diagnose"))
        total = len(rows)
        self._show_page_callout("Diagnose", "Scan complete", f"{total} findings ready for review.", level="success")
        self.toasts.show_toast(
            f"Quick Check complete: {total} findings (CRIT {counts['CRIT']} / WARN {counts['WARN']} / OK {counts['OK']} / INFO {counts['INFO']}).",
            timeout_ms=3600,
        )

    def _clear_layout(self, layout: QVBoxLayout) -> None:
        while layout.count():
            item = layout.takeAt(0)
            w = item.widget()
            if w is not None:
                w.deleteLater()

    def _rebuild_diagnose_sections(self, rows: list[dict[str, Any]]) -> None:
        self._diag_rows_all = list(rows)
        self._apply_diagnose_filters()

    def _apply_diagnose_filters(self) -> None:
        rows = list(getattr(self, "_diag_rows_all", []))
        q = self.diag_search.text().strip().lower() if hasattr(self, "diag_search") else ""
        severity = self.diag_severity.currentText().strip().upper() if hasattr(self, "diag_severity") else "ANY SEVERITY"
        recommended_only = self.diag_recommended.isChecked() if hasattr(self, "diag_recommended") else False
        sort_mode = self.diag_sort.currentText().strip().lower() if hasattr(self, "diag_sort") else "sort: severity"
        filtered: list[dict[str, Any]] = []
        for row in rows:
            status = str(row.get("status", "INFO")).upper()
            if severity != "ANY SEVERITY" and status != severity:
                continue
            if recommended_only and status not in {"CRIT", "WARN"}:
                continue
            blob = f"{row.get('title', '')} {row.get('plain', row.get('detail', ''))} {row.get('detail', '')}".lower()
            if q and q not in blob:
                continue
            filtered.append(row)
        if "title" in sort_mode:
            filtered.sort(key=lambda row: str(row.get("title", "")).lower())
        else:
            severity_rank = {"CRIT": 0, "WARN": 1, "OK": 2, "INFO": 3}
            filtered.sort(key=lambda row: (severity_rank.get(str(row.get("status", "INFO")).upper(), 9), str(row.get("title", "")).lower()))
        self._clear_layout(self.diag_feed_layout)
        if not filtered:
            run_cta = PrimaryButton("Run Quick Check")
            run_cta.clicked.connect(lambda: self.run_quick_check("Quick Check"))
            self.diag_feed_layout.addWidget(EmptyState("No Findings", "Run a scan to populate this view.", cta=run_cta, icon="!"))
            return
        grouped: dict[str, list[dict[str, Any]]] = {}
        for row in filtered:
            grouped.setdefault(str(row.get("category", "General")), []).append(row)
        for cat, items in grouped.items():
            severity = {"CRIT": 0, "WARN": 0, "OK": 0, "INFO": 0}
            for row in items:
                s = str(row.get("status", "INFO")).upper()
                if s in severity:
                    severity[s] += 1
            section = AccordionSection(cat, len(items), severity, density=self.settings_state.density)
            section.finding_context_requested.connect(lambda lw, pos: self._finding_menu(lw, pos))
            section.finding_selected.connect(self._update_diagnose_context)
            lw = section.list_widget
            lw.currentItemChanged.connect(lambda _a, _b, listw=lw: self._sync_row_selection_states(listw))
            for row in items:
                item = QListWidgetItem()
                item.setData(Qt.UserRole, row)
                item.setSizeHint(QSize(0, row_height_for_density(self.settings_state.density)))
                lw.addItem(item)
                widget = FindingRow(
                    title=str(row.get("title", "")),
                    subtitle=str(row.get("plain", row.get("detail", ""))),
                    status_badge=str(row.get("status", "INFO")),
                    payload=row,
                    density=self.settings_state.density,
                )
                widget.bind_to_list(lw, item)
                widget.activated.connect(self._update_diagnose_context)
                widget.context_requested.connect(lambda _payload, gpos, listw=lw: self._finding_menu(listw, listw.mapFromGlobal(gpos)))
                lw.setItemWidget(item, widget)
            if lw.count():
                lw.setCurrentRow(0)
            self.diag_feed_layout.addWidget(section)
        self.diag_feed_layout.addStretch(1)

    def _sync_row_selection_states(self, lw: QListWidget) -> None:
        current = lw.currentItem()
        for i in range(lw.count()):
            item = lw.item(i)
            row = lw.itemWidget(item)
            if isinstance(row, BaseRow):
                row.set_selected(item is current)

    def _update_diagnose_context(self, finding: dict[str, Any]) -> None:
        if not isinstance(finding, dict):
            finding = {}
        self.selected_finding = finding
        action_label, _action_cb = self._diagnose_action()
        if hasattr(self, "diag_next"):
            if finding:
                self.diag_next.title.setText("Next Best Action")
                self.diag_next.sub.setText(self._deterministic_next_action(finding))
            else:
                self.diag_next.title.setText("Next Best Action")
                self.diag_next.sub.setText("Run Quick Check to generate findings.")
        if hasattr(self, "diag_next_btn"):
            self.diag_next_btn.setText(action_label)
        if not finding:
            self._update_concierge()
            return
        self._set_concierge_collapsed(False, persist=True)
        self._update_concierge()

    def _run_next_best_action(self) -> None:
        _label, callback = self._diagnose_action()
        callback()

    def _deterministic_next_action(self, finding: dict[str, Any]) -> str:
        title = str(finding.get("title", "")).lower()
        if "disk" in title:
            return "Open Storage Settings, then run Export to capture post-cleanup state."
        if "memory" in title or "cpu" in title:
            return "Review startup apps, then run Home: Speed Up PC (Safe) in dry-run."
        if "proxy" in title or "network" in title:
            return "Open Network Status and compare before/after with session history."
        if finding.get("status") == "CRIT":
            return "Address this finding before lower-severity items."
        return "Review detail, run the safest matching fix, then export a summary."

    def _related_kb(self, finding: dict[str, Any]) -> str:
        blob = f"{finding.get('title', '')} {finding.get('detail', '')}".lower()
        picks: list[str] = []
        for card in KB_CARDS:
            title = card.title.lower()
            if "disk" in blob and "disk" in title:
                picks.append(card.title)
            elif ("proxy" in blob or "hosts" in blob) and ("proxy" in title or "hosts" in title):
                picks.append(card.title)
            elif "memory" in blob and "memory" in title:
                picks.append(card.title)
        if not picks:
            picks = [card.title for card in KB_CARDS[:2]]
        return " | ".join(picks[:2])

    def _update_status_strip(self) -> None:
        self._update_status_from_session(diagnostics.quick_check(include_capabilities=False))

    def _update_status_from_session(self, data: dict[str, Any]) -> None:
        m = data.get("metrics", {})
        rows = data.get("findings", [])
        reb = next((r for r in rows if "reboot" in str(r.get("title", "")).lower()), {})
        self.p_disk.setText(f"Disk: {m.get('disk_free_gb', 0):.1f} GB free")
        self.p_cpu.setText(f"CPU: {m.get('cpu_percent', 0):.0f}%")
        self.p_mem.setText(f"Memory: {m.get('mem_percent', 0):.0f}%")
        self.p_update.setText("Update: review")
        self.p_reboot.setText(f"Reboot: {reb.get('status', 'n/a')}")
        self.home_last.setText(f"Last checked: {_now_local()}")
        self._refresh_home_changes()

    def _refresh_home_changes(self) -> None:
        if not hasattr(self, "home_changes"):
            return
        pending_reboot = "unknown"
        updates = "n/a"
        reliability_critical = "n/a"
        findings = self.current_session.get("findings", []) if self.current_session else []
        reboot_row = next((row for row in findings if "reboot" in str(row.get("title", "")).lower()), None)
        if isinstance(reboot_row, dict):
            pending_reboot = "yes" if str(reboot_row.get("status", "")).upper() == "INFO" else "no"
        capability = self.current_session.get("capability_results", {}) if self.current_session else {}
        rel = capability.get("reliability_snapshot", {}) if isinstance(capability, dict) else {}
        if isinstance(rel, dict):
            raw = str(rel.get("output", "")).strip()
            if raw:
                try:
                    payload = json.loads(raw)
                    rows = payload if isinstance(payload, list) else [payload]
                    reliability_critical = str(
                        len(
                            [
                                row
                                for row in rows
                                if isinstance(row, dict)
                                and str(row.get("LevelDisplayName", "")).lower() in {"critical", "error"}
                            ]
                        )
                    )
                except Exception:
                    reliability_critical = "n/a"
        if self.current_session:
            updates = "captured in current session" if any("update" in str(row.get("title", "")).lower() for row in findings) else "not observed"
        self.home_changes.sub.setText(
            f"Pending reboot: {pending_reboot} | Recent updates: {updates} | Reliability critical events: {reliability_critical}"
        )

    def _make_fix_row(self, item: FeedItemAdapter, density: str) -> QWidget:
        key = str(item.payload)
        fx = next((f for f in FIX_CATALOG if f.key == key), None)
        if fx is None:
            return QLabel(item.title)
        row = FixRow(fx.title, fx.plain, fx.risk, payload=fx.key, density=density)
        row.preview_clicked.connect(lambda payload: self.toasts.show_toast(next((f.description for f in FIX_CATALOG if f.key == str(payload)), "")))
        row.run_clicked.connect(lambda payload: self.run_fix_action(str(payload)))
        return row

    def _set_fix_selection(self, key: str) -> None:
        if not key:
            return
        self.selected_fix_key = key
        fx = next((row for row in FIX_CATALOG if row.key == key), None)
        if fx is None:
            return
        if hasattr(self, "fix_detail"):
            self.fix_detail.title.setText(fx.title)
        if hasattr(self, "fix_detail_text"):
            self.fix_detail_text.setText(
                f"{fx.plain}\n\nRisk: {fx.risk}\nWhat changes: {fx.description}\nRollback: {fx.rollback}"
            )
        if hasattr(self, "fix_commands"):
            commands = "\n".join(fx.commands).strip()
            if not commands:
                commands = "This action opens Windows Settings only."
            self.fix_commands.set_text(commands)
        self._update_concierge()

    def _preview_selected_fix(self) -> None:
        fx = next((row for row in FIX_CATALOG if row.key == self.selected_fix_key), None)
        if fx is not None:
            self.toasts.show_toast(fx.description)

    def _run_selected_fix(self) -> None:
        if self.selected_fix_key:
            self.run_fix_action(self.selected_fix_key)

    def _active_fix_risks(self) -> set[str]:
        risks: set[str] = set()
        if hasattr(self, "fix_chip_safe") and self.fix_chip_safe.isChecked():
            risks.add("Safe")
        if hasattr(self, "fix_chip_admin") and self.fix_chip_admin.isChecked():
            risks.add("Admin")
        if hasattr(self, "fix_chip_adv") and self.fix_chip_adv.isChecked():
            risks.add("Advanced")
        return risks

    def _make_rollback_row(self, item: FeedItemAdapter, density: str) -> QWidget:
        row = ToolRow(item.title, item.category or "rollback", item.subtitle, payload=item.payload, density=density)
        row.open_clicked.connect(lambda payload: self._run_rollback_payload(payload))
        return row

    def _refresh_rollback_list(self) -> None:
        if not hasattr(self, "rollback_feed"):
            return
        adapters: list[FeedItemAdapter] = []
        actions = self.current_session.get("actions", []) if self.current_session else []
        if not isinstance(actions, list):
            actions = []
        reverse_map = {"startup_enable": "startup_disable", "startup_disable": "startup_enable"}
        for index, action in enumerate(reversed(actions[-30:]), start=1):
            if not isinstance(action, dict):
                continue
            key = str(action.get("key", "")).strip()
            if key not in reverse_map:
                continue
            undo_key = reverse_map[key]
            adapters.append(
                FeedItemAdapter(
                    key=f"rollback:{index}:{undo_key}",
                    title=f"Undo {action.get('title', key)}",
                    subtitle=f"Will run {undo_key} via ToolRunner",
                    payload={"undo_key": undo_key},
                    category="rollback",
                )
            )
        self.rollback_feed.set_items(adapters)

    def _run_selected_rollback(self) -> None:
        if not hasattr(self, "rollback_feed"):
            return
        item = self.rollback_feed.list_widget.currentItem()
        payload = item.data(Qt.UserRole) if item is not None else {}
        self._run_rollback_payload(payload)

    def _run_rollback_payload(self, payload: Any) -> None:
        if not isinstance(payload, dict):
            return
        undo_key = str(payload.get("undo_key", "")).strip()
        if undo_key:
            self.run_fix_action(undo_key)

    def _refresh_fixes(self) -> None:
        self.safety_policy = policy_from_settings(self.settings_state)
        policy = self.layout_policy_state
        scope = self.fix_scope.currentText() if hasattr(self, "fix_scope") else "Recommended"
        query = self.fix_search.text().strip().lower() if hasattr(self, "fix_search") else ""
        if hasattr(self, "fix_scope"):
            self.fix_scope.setVisible(policy.show_fixes_scope_controls)
            self.fix_scope.setEnabled(policy.show_fixes_scope_controls)
        if policy.force_fixes_recommended_only:
            scope = "Recommended"
            if hasattr(self, "fix_scope"):
                self.fix_scope.blockSignals(True)
                self.fix_scope.setCurrentText("Recommended")
                self.fix_scope.blockSignals(False)
        if policy.mode == "basic":
            allowed_risks = {"Safe", "Admin"} if policy.allow_admin_tools_in_basic else {"Safe"}
        else:
            allowed_risks = self._active_fix_risks()
        if hasattr(self, "fix_chip_safe"):
            self.fix_chip_safe.setVisible(policy.show_fixes_risk_filters)
        if hasattr(self, "fix_chip_admin"):
            self.fix_chip_admin.setVisible(policy.show_fixes_risk_filters)
        if hasattr(self, "fix_chip_adv"):
            self.fix_chip_adv.setVisible(policy.show_fixes_risk_filters)
        if hasattr(self, "fix_rollback"):
            self.fix_rollback.setVisible(policy.show_run_center)
        visible_fix_ids = self._visible_ids_for_prefix("fix_action.")
        adapters: list[FeedItemAdapter] = []
        for fx in list_fixes(self.safety_policy):
            if fx.key not in visible_fix_ids:
                continue
            if allowed_risks and fx.risk not in allowed_risks:
                continue
            if scope == "Recommended" and fx.risk != "Safe":
                continue
            if query:
                blob = f"{fx.title} {fx.description} {fx.plain} {fx.key}".lower()
                if query not in blob:
                    continue
            adapters.append(FeedItemAdapter(key=fx.key, title=fx.title, subtitle=fx.plain, payload=fx.key, category=fx.risk, status=fx.risk))
        self.fix_list.set_items(adapters)
        if not adapters:
            self.fix_list.show_empty("wrench", "No safe fixes match current filters. Switch to Pro to see more.")
            self.selected_fix_key = ""
            if hasattr(self, "fix_detail"):
                self.fix_detail.title.setText("Fix Detail")
            if hasattr(self, "fix_detail_text"):
                self.fix_detail_text.setText("No fix selected.")
            if hasattr(self, "fix_commands"):
                self.fix_commands.set_text("")
        if adapters:
            self._set_fix_selection(str(adapters[0].key))
        self.fix_info.sub.setText("Diagnostic mode is ON." if self.settings_state.diagnostic_mode else "Diagnostic mode is OFF.")
        if hasattr(self, "fix_policy_text"):
            self.fix_policy_text.setText("Safe-only mode ON" if self.settings_state.safe_only_mode else "Safe-only mode OFF")
        self._refresh_rollback_list()

    def run_fix_action(self, key: str) -> None:
        fx = next((f for f in FIX_CATALOG if f.key == key), None)
        if fx is None:
            return
        d = FixConfirmDialog(fx, self)
        if d.exec() != QDialog.Accepted:
            return

        def task(progress_cb: Any, partial_cb: Any, log_cb: Any, cancel_event: Any, timeout_s: int) -> dict[str, Any]:
            del partial_cb
            progress_cb(20, "Executing fix")
            log_cb(f"Executing fix: {fx.title}")
            code, out = run_fix(
                fx.key,
                self.safety_policy,
                require_admin_confirmation=fx.admin_required,
                reboot_warning_ack=d.reboot_ack or not fx.admin_required,
                log_cb=log_cb,
                cancel_event=cancel_event,
            )
            progress_cb(100, "Done")
            log_cb(str(out)[:2000])
            payload = {"code": code, "output": out, "key": fx.key, "title": fx.title, "risk": fx.risk}
            err = classify_exit(int(code), str(out))
            if err is not None:
                payload.update(err.as_payload())
            return payload

        self._start_task(
            fx.title,
            task,
            self._on_fix,
            risk=fx.risk,
            plain_summary=fx.plain,
            details_text="\n".join(fx.commands),
            next_steps=f"Review result then {'reboot if prompted' if fx.admin_required else 'continue with next safe action'}.",
            rerun_cb=lambda: self.run_fix_action(fx.key),
            run_metadata={"kind": "fix", "capability_id": fx.key, "fix_id": fx.key},
        )

    def _on_fix(self, payload: dict[str, Any]) -> None:
        self._append_action({"key": payload.get("key", ""), "title": payload.get("title", ""), "risk": payload.get("risk", "Safe"), "code": payload.get("code", 1), "result": str(payload.get("output", ""))[:8000]})
        self._refresh_rollback_list()
        self.toasts.show_toast(f"{payload.get('title', 'Fix')} {'completed' if payload.get('code') == 0 else 'failed'}.")

    def _append_action(self, action: dict[str, Any]) -> None:
        if not self.current_session:
            return
        self.current_session.setdefault("actions", []).append(action)
        save_session(self.current_session)
        self._refresh_run_center()

    def export_current_session(self) -> None:
        self._start_export_task(allow_validator_override=False)

    def export_current_session_allow_warnings(self) -> None:
        if not self.current_session:
            self.toasts.show_toast("No active session to export.")
            return
        ok = QMessageBox.question(
            self,
            "Allow validation warnings?",
            "This may export with share-safe validation warnings. Continue?",
            QMessageBox.Yes | QMessageBox.No,
            QMessageBox.No,
        )
        if ok != QMessageBox.Yes:
            return
        self._start_export_task(allow_validator_override=True)

    def _start_export_task(self, *, allow_validator_override: bool) -> None:
        if not self.current_session:
            self.toasts.show_toast("No active session to export.")
            return

        def task(progress_cb: Any, partial_cb: Any, log_cb: Any, cancel_event: Any, timeout_s: int) -> dict[str, Any]:
            del partial_cb, timeout_s
            if cancel_event.is_set():
                return {"cancelled": True, "code": 130, "user_message": "Export cancelled before start."}
            progress_cb(20, "Preparing export")
            log_cb(f"Generating preset: {self.rep_preset.currentText()}")
            res = export_session(
                self.current_session,
                preset=self.rep_preset.currentText(),
                share_safe=self.rep_safe.isChecked(),
                mask_ip=self.rep_ip.isChecked(),
                include_logs=self.rep_logs.isChecked(),
                allow_validator_override=allow_validator_override,
            )
            if cancel_event.is_set():
                return {"cancelled": True, "code": 130, "user_message": "Export cancelled."}
            progress_cb(100, "Done")
            log_cb(f"Export complete: {res.zip_path}")
            return {"zip": str(res.zip_path), "folder": str(res.folder_path), "short": res.ticket_summary_short, "detail": res.ticket_summary_detailed, "ok": res.validation_passed, "warn": res.validation_warnings}

        self._start_task(
            "Export",
            task,
            self._on_export,
            timeout_s=80,
            plain_summary="Builds a validated support pack from the active session.",
            details_text=f"Preset={self.rep_preset.currentText()} ShareSafe={self.rep_safe.isChecked()} MaskIP={self.rep_ip.isChecked()} AllowWarnings={allow_validator_override}",
            next_steps="Copy ticket summary or open report folder when complete. If validation fails, export stays blocked unless you explicitly allow warnings.",
            rerun_cb=(self.export_current_session_allow_warnings if allow_validator_override else self.export_current_session),
            run_metadata={"kind": "export", "capability_id": f"export_preset.{self.rep_preset.currentText()}", "export_preset": self.rep_preset.currentText()},
        )

    def _on_export(self, payload: dict[str, Any]) -> None:
        if payload.get("cancelled"):
            self.toasts.show_toast("Export cancelled.")
            return
        self.last_export = payload
        if self.current_session:
            self.current_session["last_export_preset"] = self.rep_preset.currentText()
            self.current_session["last_export_path"] = payload.get("zip", "")
            save_session(self.current_session)
            update_meta_export_path(self.current_session.get("session_id", ""), payload.get("zip", ""))
        self.rep_status.sub.setText(f"Validation {'passed' if payload.get('ok') else 'warnings'}: {payload.get('zip', '')}")
        if hasattr(self, "rep_steps"):
            self.rep_steps.setCurrentIndex(2)
        self._refresh_history(); self._refresh_home_history(); self._refresh_run_center(); self._refresh_evidence_items(); self._update_concierge(); self.toasts.show_toast("Export complete.")

    def _sync_reports_empty_state(self) -> None:
        has_session = bool(self.current_session and self.current_session.get("session_id"))
        if hasattr(self, "rep_empty_state"):
            self.rep_empty_state.setVisible(not has_session)
        if hasattr(self, "rep_steps"):
            self.rep_steps.setVisible(has_session)
        if hasattr(self, "rep_generate"):
            self.rep_generate.setEnabled(has_session)
        if hasattr(self, "rep_generate_override"):
            self.rep_generate_override.setEnabled(has_session)

    def _rebuild_report_tree(self) -> None:
        self.rep_tree.clear()
        if not self.current_session:
            self.rep_tree.addTopLevelItem(QTreeWidgetItem(["No session", "Run Quick Check"]))
            self._sync_reports_empty_state()
            return
        r = QTreeWidgetItem(["Session", self.current_session.get("session_id", "")])
        r.addChild(QTreeWidgetItem(["Symptom", self.current_session.get("symptom", "")]))
        r.addChild(QTreeWidgetItem(["Findings", str(len(self.current_session.get("findings", [])))]))
        r.addChild(QTreeWidgetItem(["Actions", str(len(self.current_session.get("actions", [])))]))
        r.addChild(QTreeWidgetItem(["Preset", self.rep_preset.currentText()]))
        self.rep_tree.addTopLevelItem(r); self.rep_tree.expandAll()
        self._sync_reports_empty_state()

    def _on_report_tree_item_selected(self, item: QTreeWidgetItem | None) -> None:
        if item is None:
            return
        if self.nav.currentRow() != self.NAV_ITEMS.index("Reports"):
            return
        if hasattr(self, "rep_tree") and (not self.rep_tree.hasFocus()):
            return
        self._set_concierge_collapsed(False, persist=True)
        self._update_concierge()

    def _refresh_evidence_items(self) -> None:
        if not hasattr(self, "rep_evidence"):
            return
        rows = self.current_session.get("evidence", {}).get("files", []) if self.current_session else []
        adapters: list[FeedItemAdapter] = []
        counts: dict[str, int] = {}
        for index, row in enumerate(rows):
            if not isinstance(row, dict):
                continue
            path = str(row.get("path", ""))
            if not path:
                continue
            category = str(row.get("category", "evidence"))
            counts[category] = counts.get(category, 0) + 1
            p = Path(path)
            adapters.append(
                FeedItemAdapter(
                    key=f"ev_{index}",
                    title=p.name,
                    subtitle=str(row.get("task_id", "")).strip() or p.parent.name,
                    payload={"path": path, "category": str(row.get("category", "evidence")), "task_id": str(row.get("task_id", ""))},
                    category=category,
                )
            )
        self.rep_evidence.set_items(adapters)
        if hasattr(self, "rep_evidence_checklist"):
            if not counts:
                self.rep_evidence_checklist.setText("Included: none")
            else:
                order = ["system", "network", "eventlogs", "updates", "crash", "printer", "evidence"]
                parts = [f"{name}:{counts[name]}" for name in order if name in counts]
                extra = [f"{name}:{value}" for name, value in counts.items() if name not in order]
                self.rep_evidence_checklist.setText("Included: " + ", ".join(parts + extra))
        if hasattr(self, "rep_evidence_status"):
            self.rep_evidence_status.clear()
            expected = [
                ("System snapshot", "system"),
                ("Network bundle", "network"),
                ("Event logs", "eventlogs"),
                ("Update evidence", "updates"),
                ("Crash evidence", "crash"),
                ("Printer evidence", "printer"),
            ]
            for label, category in expected:
                count = counts.get(category, 0)
                status = "Collected [OK]" if count > 0 else ("Optional [I]" if category in {"printer", "crash"} else "Missing [!]")
                note = f"{count} file(s)" if count > 0 else "Use Collect Now if needed."
                self.rep_evidence_status.addTopLevelItem(QTreeWidgetItem([label, status, note]))
            self.rep_evidence_status.expandAll()

    def _open_evidence_path(self, payload: Any) -> None:
        if not isinstance(payload, dict):
            return
        path = str(payload.get("path", ""))
        if not path:
            return
        target = Path(path)
        if not target.exists():
            self.toasts.show_toast("Evidence path not found.")
            return
        if os.name == "nt":
            os.startfile(str(target))

    def _evidence_menu(self, lw: QListWidget, pos: Any) -> None:
        it = lw.itemAt(pos)
        if it is None:
            return
        payload = it.data(Qt.UserRole) or {}
        if not isinstance(payload, dict):
            return
        path = str(payload.get("path", ""))
        if not path:
            return
        p = Path(path)
        actions = [
            ContextAction("Open file", lambda: self._open_evidence_path(payload)),
            ContextAction("Open folder", lambda: os.startfile(str(p.parent)) if os.name == "nt" else None),
            ContextAction("Copy path", lambda: self._copy_text(path)),
            ContextAction("Copy masked summary", lambda: self._copy_text(f"{payload.get('task_id', '')} | {payload.get('category', '')} | {p.name}")),
        ]
        show_context_menu(self, lw, pos, actions)

    def _update_redaction_preview(self) -> None:
        sample = "Host: DESKTOP-ABCD1234\nPath: C:\\Users\\John\\Downloads\nSSID: HomeWiFi\nIP: 10.0.0.8"
        if self.current_session:
            sample = f"Host: {self.current_session.get('sysinfo', {}).get('hostname', '')}\nUser: {self.current_session.get('sysinfo', {}).get('user', '')}\nSSID: {self.current_session.get('network', {}).get('ssid', '')}\nPath: C:\\Users\\Example\\Downloads\nIP: 192.168.1.7"
        p = redaction_preview(sample, MaskingOptions(enabled=self.rep_safe.isChecked(), mask_ip=self.rep_ip.isChecked(), extra_tokens=()))
        self.rep_preview.setPlainText(f"Before:\n{p['before']}\n\nAfter:\n{p['after']}")
        if hasattr(self, "rep_token_map"):
            self.rep_token_map.setText("Token map example: PC_1 / USER_1 / SSID_1")

    def _session_meta_payload(self, row: Any) -> dict[str, Any]:
        if isinstance(row, dict):
            return {
                "session_id": str(row.get("session_id", "")),
                "symptom": str(row.get("goal", row.get("symptom", "Quick Check"))),
                "summary": str(row.get("summary_plain", row.get("summary", ""))),
                "created_utc": str(row.get("created_at", row.get("updated_at", ""))),
                "last_export_path": str(row.get("last_export_path", "")),
                "pinned": bool(row.get("pinned", False)),
            }
        return {
            "session_id": row.session_id,
            "symptom": row.symptom,
            "summary": row.summary,
            "created_utc": row.created_utc,
            "last_export_path": row.last_export_path,
            "pinned": row.pinned,
        }

    def _make_session_row(self, item: FeedItemAdapter, density: str) -> QWidget:
        payload = item.payload if isinstance(item.payload, dict) else {}
        return SessionRow(
            session_id=str(payload.get("session_id", item.key)),
            symptom=str(payload.get("symptom", item.title)),
            summary=str(payload.get("summary", item.subtitle)),
            export_status="Exported" if payload.get("last_export_path") else "New",
            timestamp=str(payload.get("created_utc", "")),
            payload=payload,
            density=density,
        )

    def _refresh_home_history(self) -> None:
        rows: list[Any] = []
        try:
            rows = list_sessions_db(limit=8)
        except Exception:
            rows = []
        if not rows:
            rows = load_index()[:8]
        adapters = [
            FeedItemAdapter(
                key=str(self._session_meta_payload(r).get("session_id", "")),
                title=str(self._session_meta_payload(r).get("symptom", "")),
                subtitle=str(self._session_meta_payload(r).get("summary", "")),
                payload=self._session_meta_payload(r),
                timestamp=str(self._session_meta_payload(r).get("created_utc", "")),
                export_status="Exported" if self._session_meta_payload(r).get("last_export_path") else "New",
            )
            for r in rows
        ]
        self.home_recent.set_items(adapters)

    def _refresh_history(self) -> None:
        q = self.hist_search.text().strip().lower() if hasattr(self, "hist_search") else ""
        scope = self.hist_scope.currentText().strip().lower() if hasattr(self, "hist_scope") else "all sessions"
        adapters: list[FeedItemAdapter] = []
        rows: list[Any] = []
        try:
            rows = list_sessions_db(limit=300, query=q)
        except Exception:
            rows = []
        if not rows:
            rows = load_index()
        for r in rows:
            payload = self._session_meta_payload(r)
            blob = f"{payload.get('session_id', '')} {payload.get('symptom', '')} {payload.get('summary', '')}".lower()
            if q and q not in blob:
                continue
            exported = bool(payload.get("last_export_path"))
            if scope == "with exports" and not exported:
                continue
            if scope == "failures":
                summary_blob = str(payload.get("summary", "")).lower()
                if not any(token in summary_blob for token in ("fail", "error", "warn", "crit")):
                    continue
            adapters.append(
                FeedItemAdapter(
                    key=str(payload.get("session_id", "")),
                    title=str(payload.get("symptom", "")),
                    subtitle=str(payload.get("summary", "")),
                    payload=payload,
                    timestamp=str(payload.get("created_utc", "")),
                    export_status="Exported" if exported else "New",
                )
            )
        self.hist_list.set_items(adapters)

    def _selected_sid(self) -> str:
        item = self.hist_list.list_widget.currentItem()
        payload = item.data(Qt.UserRole) if item is not None else {}
        if isinstance(payload, dict):
            return str(payload.get("session_id", ""))
        return ""

    def _update_history_detail(self) -> None:
        sid = self._selected_sid()
        if not sid:
            self.hist_detail.sub.setText("Select a session.")
            if hasattr(self, "hist_compare"):
                self.hist_compare.set_text("")
            return
        try:
            s = load_session(sid)
        except Exception:
            self.hist_detail.sub.setText("Could not load session file.")
            return
        goals = s.get("symptom", "Quick Check")
        findings = s.get("findings", [])
        actions = s.get("actions", [])
        evidence = s.get("evidence", {}).get("files", []) if isinstance(s.get("evidence", {}), dict) else []
        top_rows = [str(row.get("title", "")).strip() for row in findings[:3] if str(row.get("title", "")).strip()]
        exports = "yes" if any(meta.session_id == sid and meta.last_export_path for meta in load_index()) else "no"
        lines = [
            f"Session ID: {sid}",
            f"Goal: {goals}",
            f"Actions taken: {len(actions)}",
            f"Evidence collected: {len(evidence)}",
            f"Export generated: {exports}",
            "Top findings:",
        ]
        if top_rows:
            lines.extend([f"- {row}" for row in top_rows])
        else:
            lines.append("- none")
        self.hist_detail.sub.setText("\n".join(lines))
        if hasattr(self, "hist_compare"):
            self.hist_compare.set_text("\n".join(lines))
        self._update_concierge()

    def reopen_selected_session(self) -> None:
        sid = self._selected_sid()
        if not sid:
            self.toasts.show_toast("Select a session first.")
            return
        self._load_session(sid); self.nav.setCurrentRow(self.NAV_ITEMS.index("Diagnose")); self.toasts.show_toast(f"Session {sid} reopened.")

    def reexport_selected_session(self) -> None:
        sid = self._selected_sid()
        if not sid:
            self.toasts.show_toast("Select a session first.")
            return
        self._load_session(sid); self.nav.setCurrentRow(self.NAV_ITEMS.index("Reports")); self.export_current_session()

    def compare_with_active_session(self) -> None:
        sid = self._selected_sid()
        if not sid or not self.current_session:
            self.toasts.show_toast("Need selected session and active session.")
            return
        other = load_session(sid)
        current_findings = self.current_session.get("findings", [])
        other_findings = other.get("findings", [])
        finding_delta = len(current_findings) - len(other_findings)
        current_disk = float(self.current_session.get("metrics", {}).get("disk_free_gb", 0) or 0)
        other_disk = float(other.get("metrics", {}).get("disk_free_gb", 0) or 0)
        current_reboot = any("reboot" in str(row.get("title", "")).lower() and str(row.get("status", "")).upper() == "INFO" for row in current_findings)
        other_reboot = any("reboot" in str(row.get("title", "")).lower() and str(row.get("status", "")).upper() == "INFO" for row in other_findings)
        lines = [
            f"Finding count delta: {finding_delta:+d}",
            f"Disk free delta (GB): {current_disk - other_disk:+.1f}",
            f"Pending reboot delta: {'changed' if current_reboot != other_reboot else 'no change'}",
            f"Action count delta: {len(self.current_session.get('actions', [])) - len(other.get('actions', [])):+d}",
        ]
        self.hist_detail.sub.setText("Compare with active session:\n" + "\n".join([f"- {line}" for line in lines]))
        if hasattr(self, "hist_compare"):
            self.hist_compare.set_text("\n".join([f"- {line}" for line in lines]))

    def _load_session(self, sid: str) -> None:
        try:
            s = load_session(sid)
        except Exception:
            self.toasts.show_toast(f"Session not found: {sid}")
            return
        self.current_session = s
        self.diag_summary.title.setText(f"Results for: {s.get('symptom', 'Quick Check')}")
        self.diag_summary.sub.setText("Session loaded. Review grouped findings and run a recommended fix.")
        top = s.get("findings", [])[:3]
        self.diag_top3.sub.setText(self._top_findings_text(s.get("findings", [])))
        self._rebuild_diagnose_sections(s.get("findings", []))
        self._update_diagnose_context({})
        self._update_status_from_session(s)
        self._rebuild_report_tree(); self._update_redaction_preview(); self._refresh_evidence_items(); self._refresh_run_center(); self._update_context_labels(); self._update_concierge()

    def _make_tool_row(self, item: FeedItemAdapter, density: str) -> QWidget:
        row = ToolRow(item.title, item.category or "tool", item.subtitle, payload=item.payload, density=density)
        row.open_clicked.connect(lambda payload: self._launch_tool_payload(str(payload)))
        return row

    def _make_task_row(self, item: FeedItemAdapter, density: str) -> QWidget:
        row = ToolRow(item.title, item.category or "task", item.subtitle, payload=item.payload, density=density)
        row.open_clicked.connect(lambda payload: self._run_script_task(str(payload), dry_run=False))
        return row

    def _make_evidence_row(self, item: FeedItemAdapter, density: str) -> QWidget:
        row = ToolRow(item.title, item.category or "evidence", item.subtitle, payload=item.payload, density=density)
        row.open_clicked.connect(self._open_evidence_path)
        return row

    def _make_runbook_row(self, item: FeedItemAdapter, density: str) -> QWidget:
        row = ToolRow(item.title, item.category or "runbook", item.subtitle, payload=item.payload, density=density)
        row.open_clicked.connect(lambda payload: self._set_runbook_selection(str(payload), open_details=True))
        return row

    def _make_run_center_row(self, item: FeedItemAdapter, density: str) -> QWidget:
        row = ToolRow(item.title, item.category or "run", item.subtitle, payload=item.payload, density=density)
        row.open_clicked.connect(self._run_center_activate)
        return row

    def _refresh_run_center(self) -> None:
        if not hasattr(self, "run_center"):
            return
        adapters: list[FeedItemAdapter] = []
        try:
            sid = str(self.current_session.get("session_id", "") if self.current_session else "").strip()
            runs = list_recent_runs(limit=20, session_id=sid if sid else "")
            for idx, run in enumerate(runs, start=1):
                capability = str(run.get("capability_id", "")).strip()
                run_kind = str(run.get("kind", "run")).strip() or "run"
                run_id = str(run.get("run_id", "")).strip()
                code = int(run.get("exit_code", 0) or 0)
                status = str(run.get("status", "unknown")).strip() or "unknown"
                title = capability or run_id or f"Run {idx}"
                last_line = str(run.get("last_log_line", "")).strip()
                sid_piece = f" | session={run.get('session_id', '')}" if run.get("session_id") else ""
                subtitle = f"{status} | code={code} | {run_kind}{sid_piece}"
                if last_line:
                    subtitle += f" | {last_line[:90]}"
                payload = {
                    "key": capability,
                    "title": title,
                    "code": code,
                    "result": last_line,
                    "_kind": run_kind,
                    "_run_id": run_id,
                }
                adapters.append(
                    FeedItemAdapter(
                        key=f"run:{idx}:{capability or run_id}",
                        title=title,
                        subtitle=subtitle,
                        payload=payload,
                        category=run_kind,
                        status=status,
                    )
                )
        except Exception:
            adapters = []

        if not adapters:
            actions: list[dict[str, Any]] = []
            current_actions = self.current_session.get("actions", []) if self.current_session else []
            if isinstance(current_actions, list):
                for row in current_actions:
                    if isinstance(row, dict):
                        actions.append(dict(row))
            for idx, action in enumerate(reversed(actions[-20:]), start=1):
                key = str(action.get("key", "")).strip()
                title = str(action.get("title", "")).strip() or key or f"Run {idx}"
                code = int(action.get("code", 0 if action.get("dry_run") else 1))
                status = "Completed" if code == 0 else "Failed"
                subtitle = f"{status} | code={code} | risk={action.get('risk', 'Safe')}"
                adapters.append(
                    FeedItemAdapter(
                        key=f"run:{idx}:{key}",
                        title=title,
                        subtitle=subtitle,
                        payload=action,
                        category=str(action.get("type", "run")).strip() or "run",
                        status=str(action.get("risk", "Safe")),
                    )
                )
        self.run_center.set_items(adapters)

    def _run_center_activate(self, payload: Any) -> None:
        if not isinstance(payload, dict):
            return
        key = str(payload.get("key", "")).strip()
        run_kind = str(payload.get("_kind", "")).strip().lower()
        if run_kind in {"tool", "script", "runbook", "fix"} and not key:
            key = str(payload.get("title", "")).strip()
        if not key:
            return
        if run_kind == "tool":
            self._launch_tool_payload(key)
            return
        if run_kind == "script":
            self.nav.setCurrentRow(self.NAV_ITEMS.index("Playbooks"))
            self._select_script_task(key)
            self._run_script_task(key, dry_run=False)
            return
        if run_kind == "runbook":
            self.nav.setCurrentRow(self.NAV_ITEMS.index("Playbooks"))
            self._select_runbook(key)
            self.run_selected_runbook(False)
            return
        if run_kind == "fix":
            self.nav.setCurrentRow(self.NAV_ITEMS.index("Fixes"))
            self.run_fix_action(key)
            return
        if key in script_task_map():
            self.nav.setCurrentRow(self.NAV_ITEMS.index("Playbooks"))
            self._select_script_task(key)
            self._run_script_task(key, dry_run=False)
            return
        if key in runbook_map():
            self.nav.setCurrentRow(self.NAV_ITEMS.index("Playbooks"))
            self._select_runbook(key)
            self.run_selected_runbook(False)
            return
        if any(fix.key == key for fix in FIX_CATALOG):
            self.nav.setCurrentRow(self.NAV_ITEMS.index("Fixes"))
            self.run_fix_action(key)
            return
        self.nav.setCurrentRow(self.NAV_ITEMS.index("Reports"))

    def _run_center_menu(self, lw: QListWidget, pos: Any) -> None:
        it = lw.itemAt(pos)
        if it is None:
            return
        payload = it.data(Qt.UserRole) or {}
        if not isinstance(payload, dict):
            return
        key = str(payload.get("key", "")).strip()
        summary = str(payload.get("result", "")).strip() or f"{payload.get('title', key)}"
        actions = [
            ContextAction("Re-run", lambda: self._run_center_activate(payload)),
            ContextAction("Copy simple summary", lambda: self._copy_text(summary[:4000])),
            ContextAction("Open Reports", lambda: self.nav.setCurrentRow(self.NAV_ITEMS.index("Reports"))),
            ContextAction("Open Logs Folder", lambda: os.startfile(str(logs_dir())) if os.name == "nt" else None),
            ContextAction("Export current session", self.export_current_session),
        ]
        if key:
            actions.append(ContextAction("Pin to Home quick actions", lambda: self._pin_action(key)))
        show_context_menu(self, lw, pos, actions)

    def _refresh_toolbox(self) -> None:
        guided_basic = self.layout_policy_state.show_playbooks_guided_basic
        q = self.tb_search.text().strip() if hasattr(self, "tb_search") else ""
        selected = self.tb_filter.currentText().strip().lower() if hasattr(self, "tb_filter") else "all categories"
        category_filter = "" if selected == "all categories" else self._tool_category_key(selected)
        allow_safe = not hasattr(self, "pb_chip_safe") or self.pb_chip_safe.isChecked()
        allow_admin = not hasattr(self, "pb_chip_admin") or self.pb_chip_admin.isChecked()
        allow_advanced = not hasattr(self, "pb_chip_advanced") or self.pb_chip_advanced.isChecked()

        def _risk_bucket(tool_id: str) -> str:
            key = str(tool_id or "").strip().lower()
            if "cmd_admin" in key:
                return "admin"
            if any(token in key for token in ("feedback", "get_help")):
                return "advanced"
            return "safe"

        def _is_allowed(tool_id: str) -> bool:
            bucket = _risk_bucket(tool_id)
            if bucket == "safe":
                return allow_safe
            if bucket == "admin":
                return allow_admin
            return allow_advanced

        visible_tool_ids = self._visible_ids_for_prefix("tool.")
        top_adapters = [
            FeedItemAdapter(
                key=t.id,
                title=t.title,
                subtitle=t.plain,
                payload=t.id,
                category=self._tool_category_label(t.category),
            )
            for t in TOP_TOOLS
            if t.id in visible_tool_ids and _is_allowed(t.id)
        ]
        rows = [tool for tool in search_tools(q) if tool.id in visible_tool_ids and _is_allowed(tool.id)]
        if category_filter:
            rows = [t for t in rows if category_filter in self._tool_category_key(t.category)]
        all_adapters = [
            FeedItemAdapter(
                key=t.id,
                title=t.title,
                subtitle=t.desc,
                payload=t.id,
                category=self._tool_category_label(t.category),
            )
            for t in rows
        ]
        self.tb_top.set_items(top_adapters)
        if not top_adapters:
            self.tb_top.show_empty("tool", "No pinned safe tools. Switch to Pro for full tool catalog.")
        self.tb_all.set_items(all_adapters)
        if not all_adapters:
            self.tb_all.show_empty("tool", "No tools match current filters. Switch to Pro to see more.")
        favorite_tool_ids = set(self.settings_state.favorites_tools or [])
        fav_adapters = [
            FeedItemAdapter(
                key=t.id,
                title=t.title,
                subtitle=t.desc,
                payload=t.id,
                category=self._tool_category_label(t.category),
            )
            for t in TOOL_DIRECTORY
            if t.id in favorite_tool_ids and t.id in visible_tool_ids
        ]
        if hasattr(self, "tb_favorites"):
            self.tb_favorites.set_items(fav_adapters)
            if not fav_adapters:
                self.tb_favorites.show_empty("star", "No favorite tools available in this mode.")
        if all_adapters and not guided_basic:
            valid_ids = {row.key for row in all_adapters}
            selected_id = self.selected_tool_id if self.selected_tool_id in valid_ids else all_adapters[0].key
            self._set_selected_tool(selected_id)
        elif (not guided_basic) and hasattr(self, "pb_tool_detail") and hasattr(self, "pb_tool_detail_text"):
            self.selected_tool_id = ""
            self.pb_tool_detail.title.setText("Tool Detail")
            self.pb_tool_detail_text.setText("No tools available for this mode and filter set. Switch to Pro for full directory.")
        if guided_basic:
            self.selected_tool_id = ""
            self.selected_task_id = ""
        self._refresh_script_tasks()
        self._refresh_runbooks()

    def _switch_playbooks_segment(self) -> None:
        segment = self.pb_segment.currentText().strip().lower() if hasattr(self, "pb_segment") else "tools"
        if self.layout_policy_state.show_playbooks_pro_console:
            self.pb_stack.setCurrentIndex(1 if segment == "runbooks" else 0)
        else:
            self.pb_stack.setCurrentIndex(1)
        if hasattr(self, "pb_advanced_toggle"):
            tools_mode = self.pb_stack.currentIndex() == 0
            self.pb_advanced_toggle.setVisible(self.layout_policy_state.show_script_tasks and tools_mode)
        self._apply_playbooks_mode_visibility()
        self._refresh_toolbox()
        self._update_concierge()

    def _toggle_advanced_script_tasks(self) -> None:
        if not self.layout_policy_state.show_script_tasks:
            self.toasts.show_toast("Switch to Pro mode to view advanced script tasks.")
            return
        if not hasattr(self, "task_card"):
            return
        visible = not self.task_card.isVisible()
        self.task_card.setVisible(visible)
        if hasattr(self, "pb_advanced_toggle"):
            self.pb_advanced_toggle.setText("Hide advanced script tasks" if visible else "Show advanced script tasks")

    def _current_file_index_roots(self) -> list[str]:
        raw = self.file_index_roots.text().strip() if hasattr(self, "file_index_roots") else ""
        if not raw:
            return []
        rows = [part.strip() for part in raw.replace("\n", ";").split(";")]
        return [row for row in rows if row]

    def _pick_file_index_root(self) -> None:
        start = str(Path.home())
        path = QFileDialog.getExistingDirectory(self, "Select File Index Root", start)
        if not path:
            return
        roots = self._current_file_index_roots()
        if path not in roots:
            roots.append(path)
        self.file_index_roots.setText(";".join(roots))

    def _build_file_index(self) -> None:
        roots = self._current_file_index_roots()
        if not roots:
            self.toasts.show_toast("Add at least one root path to index.")
            return
        budget_s = int(self.file_index_budget.value()) if hasattr(self, "file_index_budget") else 90

        def task(progress_cb: Any, partial_cb: Any, log_cb: Any, cancel_event: Any, timeout_s: int) -> dict[str, Any]:
            del partial_cb, timeout_s
            log_cb(f"[file-index] roots={';'.join(roots)}")
            result = index_roots(
                roots,
                budget_seconds=budget_s,
                cancel_event=cancel_event,
                progress_cb=progress_cb,
                log_cb=log_cb,
            )
            return result

        self._start_task(
            "Build File Index",
            task,
            self._on_file_index_built,
            timeout_s=max(30, budget_s + 30),
            risk="Safe",
            plain_summary="Builds an SQLite-backed file index for fast local file search.",
            details_text=f"roots={';'.join(roots)}\nbudget_s={budget_s}",
            next_steps="Search the index and export CSV results if needed.",
            rerun_cb=self._build_file_index,
            run_metadata={"kind": "index", "capability_id": "fast_file_index_build"},
        )

    def _on_file_index_built(self, payload: dict[str, Any]) -> None:
        roots = payload.get("roots", [])
        scanned = int(payload.get("scanned", 0))
        changed = int(payload.get("changed", 0))
        deleted = int(payload.get("deleted", 0))
        cancelled = bool(payload.get("cancelled"))
        status = (
            f"Cancelled. Scanned={scanned}, changed={changed}, removed={deleted}."
            if cancelled
            else f"Index ready. Scanned={scanned}, changed={changed}, removed={deleted}."
        )
        self.file_index_status.setText(status)
        if isinstance(roots, list) and roots:
            self.file_index_roots.setText(";".join([str(row) for row in roots]))
            self.settings_state.file_index_roots = [str(row) for row in roots]
            save_settings(self.settings_state)
        self.toasts.show_toast("File index build complete." if not cancelled else "File index build cancelled.")

    def _search_file_index(self) -> None:
        query = self.file_index_query.text().strip() if hasattr(self, "file_index_query") else ""
        if not query:
            self.file_index_results.setPlainText("Enter a query to search indexed files.")
            return
        rows = search_files(query, limit=300)
        self._fast_file_results = rows
        lines: list[str] = []
        for row in rows[:200]:
            size_mb = float(row.get("size_bytes", 0) or 0) / (1024 * 1024)
            lines.append(f"{row.get('name', '')} | {size_mb:.2f} MB | {row.get('path', '')}")
        self.file_index_results.setPlainText("\n".join(lines) if lines else "No indexed files matched this query.")
        self.file_index_status.setText(f"Query '{query}' returned {len(rows)} rows.")

    def _export_file_index_results(self) -> None:
        if not self._fast_file_results:
            self.toasts.show_toast("No file index results to export.")
            return
        path, _ = QFileDialog.getSaveFileName(self, "Export File Index Results", "file_index_results.csv", "CSV (*.csv)")
        if not path:
            return
        target = export_results_csv(self._fast_file_results, Path(path))
        self._merge_files_into_session_evidence([str(target)], "storage", "file_index_search")
        self._refresh_evidence_items()
        self.toasts.show_toast("File index results exported.")

    def _refresh_basic_playbooks_cards(self) -> None:
        if not hasattr(self, "pb_basic_goal_grid"):
            return
        while self.pb_basic_goal_grid.count():
            item = self.pb_basic_goal_grid.takeAt(0)
            widget = item.widget()
            if widget is not None:
                widget.deleteLater()
        goals: list[tuple[str, str, str, list[str]]] = [
            ("wifi", "Fix Wi-Fi", "Collect network evidence and run guided recovery checks.", ["Wi-Fi report + DNS/proxy checks", "Network evidence pack", "Ping verification"]),
            ("space", "Free Up Space", "Find the largest storage offenders before cleanup.", ["Storage radar", "Large file radar", "Downloads cleanup preview"]),
            ("speed", "Speed Up PC", "Check pressure, startup load, and reboot signals.", ["Performance sample window", "Startup/autostart pack", "Pending reboot sources"]),
            ("printer", "Printer Issues", "Capture printer health and spooler status.", ["Printer status snapshot", "Queue + service checks", "PrintService evidence"]),
            ("browser", "Browser Problems", "Run browser + network checks together.", ["Browser rescue helper", "DNS timing test", "Hosts/proxy checks"]),
            ("crashes", "App Crashes", "Collect crash-focused evidence for triage.", ["Crash helper", "Reliability snapshot", "Application/System event context"]),
        ]
        for idx, (goal_id, title, desc, runs) in enumerate(goals):
            run_btn = PrimaryButton("Start")
            run_btn.clicked.connect(lambda _checked=False, gid=goal_id: self._launch_basic_goal(gid))
            card = Card(title, desc, right_widget=run_btn)
            card.body_layout().addWidget(QLabel("Runs:"))
            for line in runs:
                card.body_layout().addWidget(QLabel(f"- {line}"))
            row = idx // 2
            col = idx % 2
            self.pb_basic_goal_grid.addWidget(card, row, col)

    def _launch_basic_goal(self, goal_id: str) -> None:
        mapping_runbook = {
            "wifi": "home_fix_wifi_safe",
            "space": "home_free_up_space_safe",
            "speed": "home_speed_up_pc_safe",
            "printer": "home_printer_rescue",
            "browser": "home_browser_problems",
        }
        if goal_id in mapping_runbook:
            self._select_runbook(mapping_runbook[goal_id])
            self.run_selected_runbook(True)
            return
        if goal_id == "crashes":
            self.run_quick_check("Crashes")

    def _set_selected_tool(self, tid: str) -> None:
        if not tid:
            return
        self.selected_tool_id = tid
        self.selected_task_id = ""
        tool = next((x for x in TOOL_DIRECTORY if x.id == tid), None)
        if tool is None:
            return
        if hasattr(self, "pb_tool_detail_text"):
            category_label = self._tool_category_label(tool.category)
            self.pb_tool_detail.title.setText(tool.title)
            self.pb_tool_detail_text.setText(
                f"{tool.plain}\n\nWhen to use:\n{tool.desc}\n\nCategory:\n{category_label}\n\nCollects/changes:\nLaunches {tool.command}\nSafety: Safe"
            )
        if hasattr(self, "pb_detail_steps"):
            self.pb_detail_steps.set_text(tool.command)
        if hasattr(self, "pb_detail_dry"):
            self.pb_detail_dry.setEnabled(False)
            self.pb_detail_dry.setProperty("selected_task_id", "")
        if hasattr(self, "pb_detail_run"):
            self.pb_detail_run.setProperty("selected_task_id", "")
        self._update_concierge()

    def _set_selected_script_task(self, task_id: str) -> None:
        if not task_id:
            return
        task = script_task_map().get(task_id)
        if task is None:
            return
        self.selected_tool_id = ""
        self.selected_task_id = task.id
        if hasattr(self, "pb_tool_detail_text"):
            self.pb_tool_detail.title.setText(task.title)
            self.pb_tool_detail_text.setText(
                f"{task.desc}\n\nWhen to use:\nCategory: {task.category}\n\nCollects/changes:\nTask execution with ToolRunner artifacts.\nSafety: {task.risk}"
            )
        if hasattr(self, "pb_detail_steps"):
            self.pb_detail_steps.set_text(f"Task: {task.id}\nCommand: {' '.join(task.command or ('<custom-runner>',))}")
        if hasattr(self, "pb_detail_dry"):
            self.pb_detail_dry.setEnabled(True)
            self.pb_detail_dry.setProperty("selected_task_id", task.id)
        if hasattr(self, "pb_detail_run"):
            self.pb_detail_run.setProperty("selected_task_id", task.id)
        self._update_concierge()

    def _run_selected_tool(self) -> None:
        task_id = str(self.pb_detail_run.property("selected_task_id") or "").strip() if hasattr(self, "pb_detail_run") else ""
        if task_id:
            self._run_script_task(task_id, dry_run=False)
            return
        if self.selected_tool_id:
            self._launch_tool_payload(self.selected_tool_id)

    def _dry_run_selected_tool(self) -> None:
        task_id = str(self.pb_detail_dry.property("selected_task_id") or "").strip() if hasattr(self, "pb_detail_dry") else ""
        if task_id:
            self._run_script_task(task_id, dry_run=True)

    def _refresh_script_tasks(self) -> None:
        if not hasattr(self, "task_feed"):
            return
        visible_task_ids = self._visible_ids_for_prefix("script_task.")
        q = self.tb_search.text().strip().lower() if hasattr(self, "tb_search") else ""
        category = self.task_filter.currentText().strip().lower() if hasattr(self, "task_filter") else "all task categories"
        adapters: list[FeedItemAdapter] = []
        for task in list_script_tasks():
            if task.id not in visible_task_ids:
                continue
            if category != "all task categories" and task.category.lower() != category:
                continue
            blob = f"{task.id} {task.title} {task.desc} {task.category} {task.risk}".lower()
            if q and q not in blob:
                continue
            adapters.append(
                FeedItemAdapter(
                    key=task.id,
                    title=task.title,
                    subtitle=task.desc,
                    payload=task.id,
                    category=task.category,
                    status=task.risk,
                )
            )
        self.task_feed.set_items(adapters)
        if not adapters:
            self.task_feed.show_empty("task", "No script tasks available. Switch to Pro mode to view advanced tasks.")
            self.selected_task_id = ""
        if adapters and self.selected_task_id not in {row.key for row in adapters}:
            self.selected_task_id = adapters[0].key

    @staticmethod
    def _tool_category_key(value: str) -> str:
        return str(value or "").strip().lower().replace(" ", "_")

    @classmethod
    def _tool_category_label(cls, value: str) -> str:
        key = cls._tool_category_key(value)
        if key == "windows_links":
            return "Windows Links"
        return key.replace("_", " ").title() if key else "General"

    def _refresh_runbooks(self) -> None:
        guided_basic = self.layout_policy_state.show_playbooks_guided_basic
        visible_runbook_ids = self._visible_ids_for_prefix("runbook.")
        q = self.tb_search.text().strip().lower() if hasattr(self, "tb_search") else ""
        audience = self.rb_audience.currentText().strip().lower() if hasattr(self, "rb_audience") else "all audiences"
        adapters: list[FeedItemAdapter] = []
        for r in RUNBOOKS:
            if r.id not in visible_runbook_ids:
                continue
            if audience != "all audiences" and r.audience.lower() != audience:
                continue
            blob = f"{r.id} {r.title} {r.desc} {r.audience}".lower()
            if q and q not in blob:
                continue
            adapters.append(FeedItemAdapter(key=r.id, title=r.title, subtitle=r.desc, payload=r.id, category=r.audience))
        self.rb_feed.set_items(adapters)
        if not adapters:
            self.rb_feed.show_empty("book", "No runbooks available for this mode. Switch to Pro for IT runbooks.")
            self.rb_selected_id = ""
            if hasattr(self, "rb_detail"):
                self.rb_detail.title.setText("Runbook Detail")
                self.rb_detail.sub.setText("No runbook selected.")
            if hasattr(self, "rb_steps"):
                self.rb_steps.set_text("Switch to Pro mode to access IT/MSP runbooks.")
        curated: list[FeedItemAdapter] = []
        curated_home = [r for r in RUNBOOKS if r.audience == "home" and r.id in visible_runbook_ids][:3]
        curated_it = [r for r in RUNBOOKS if r.audience == "it" and r.id in visible_runbook_ids][:3]
        for row in curated_home + curated_it:
            if audience != "all audiences" and row.audience.lower() != audience:
                continue
            if q and q not in f"{row.id} {row.title} {row.desc}".lower():
                continue
            curated.append(FeedItemAdapter(key=row.id, title=row.title, subtitle=row.desc, payload=row.id, category=row.audience))
        if hasattr(self, "rb_curated"):
            self.rb_curated.set_items(curated)
            if not curated:
                self.rb_curated.show_empty("book", "No curated runbooks in this mode.")
        valid_ids = {row.key for row in adapters}
        if self.rb_selected_id and self.rb_selected_id not in valid_ids:
            self.rb_selected_id = adapters[0].key if adapters else ""
        if (not self.rb_selected_id) and adapters and not guided_basic:
            self.rb_selected_id = adapters[0].key
        if self.rb_selected_id and not guided_basic:
            self._set_runbook_selection(self.rb_selected_id, open_details=not self._startup_warmup_active)
        if guided_basic and adapters:
            self.rb_selected_id = ""

    def _set_runbook_selection(self, rid: str, *, open_details: bool = False) -> None:
        if not rid:
            return
        self.rb_selected_id = rid
        selected = next((r for r in RUNBOOKS if r.id == rid), None)
        if selected is not None:
            if hasattr(self, "rb_detail"):
                self.rb_detail.title.setText(selected.title)
                self.rb_detail.sub.setText(
                    f"{selected.desc}\nAudience: {selected.audience}\nSteps: {len(selected.steps)}"
                )
            if hasattr(self, "rb_steps"):
                self.rb_steps.set_text("\n".join([f"{step.title} ({step.task_id})" for step in selected.steps]))
        if open_details:
            self._set_concierge_collapsed(False, persist=True)
        self._update_concierge()

    def _launch_tool_payload(self, tid: str) -> None:
        if tid not in self._visible_ids_for_prefix("tool."):
            self.toasts.show_toast("This tool is available in Pro mode.")
            return
        t = next((x for x in TOOL_DIRECTORY if x.id == tid), None)
        if t is None:
            return
        sid = self._ensure_active_session(f"Tool: {t.title}")
        run_id = self.run_event_bus.create_run(
            name=f"Tool: {t.title}",
            risk="Safe",
            session_id=sid,
            metadata={"kind": "tool", "capability_id": t.id, "tool_id": t.id},
        )

        def task(progress_cb: Any, partial_cb: Any, log_cb: Any, cancel_event: Any, timeout_s: int) -> dict[str, Any]:
            del partial_cb
            if cancel_event.is_set():
                return {"code": 130, "cancelled": True, "user_message": "Launch cancelled."}
            progress_cb(15, "Launching tool")
            log_cb(f"[tool] {t.id} -> {t.command}")
            code, out = launch_tool(t.id, dry_run=False)
            if cancel_event.is_set():
                return {"code": 130, "cancelled": True, "user_message": "Launch cancelled."}
            out_dir = ensure_dirs()["state"] / "tool_runs" / sid
            out_dir.mkdir(parents=True, exist_ok=True)
            artifact = out_dir / f"{t.id}_result.txt"
            artifact.write_text(
                self._mask(
                    "\n".join(
                        [
                            f"Tool ID: {t.id}",
                            f"Title: {t.title}",
                            f"Category: {self._tool_category_label(t.category)}",
                            f"Command: {t.command}",
                            f"Exit code: {code}",
                            "",
                            str(out),
                        ]
                    )
                ),
                encoding="utf-8",
            )
            progress_cb(100, "Done")
            payload = {
                "key": t.id,
                "title": t.title,
                "risk": "Safe",
                "code": int(code),
                "result": str(out)[:8000],
                "task_id": t.id,
                "category": t.category,
                "summary_text": f"{t.title}\n\nWhat we checked:\n- Launched tool command.\n\nWhat we found:\n- Exit code: {code}\n\nWhat we changed:\n- Opened built-in Windows tool or URI.\n\nNext steps:\n- Review output and run related helper tasks.\n\nTechnical appendix:\n- command={t.command}\n",
                "next_steps_list": [
                    "Review tool output and rerun if needed.",
                    "Run a related script task for artifact capture.",
                    "Export a support pack if you need escalation.",
                ],
                "evidence_root": str(ensure_dirs()["state"] / "tool_runs" / sid),
                "output_files": [str(artifact)],
            }
            err = classify_exit(int(code), str(out))
            if err is not None:
                payload.update(err.as_payload())
                payload["next_steps_list"] = ensure_next_steps(payload.get("suggested_next_steps"))
            return payload

        self._start_task(
            f"Tool: {t.title}",
            task,
            self._on_tool_launch_result,
            timeout_s=40,
            risk="Safe",
            plain_summary=t.plain,
            details_text=f"Tool ID: {t.id}\nCommand: {t.command}\nCategory: {self._tool_category_label(t.category)}",
            next_steps="Use script tasks for deeper evidence capture and export.",
            rerun_cb=lambda: self._launch_tool_payload(t.id),
            evidence_root=str(ensure_dirs()["state"] / "tool_runs" / sid),
            run_id=run_id,
        )

    def _on_tool_launch_result(self, payload: dict[str, Any]) -> None:
        if payload.get("cancelled"):
            self.toasts.show_toast("Tool launch cancelled.")
            return
        code = int(payload.get("code", 1))
        self._append_action(
            {
                "key": str(payload.get("key", "")),
                "title": str(payload.get("title", "Tool")),
                "risk": str(payload.get("risk", "Safe")),
                "code": code,
                "result": str(payload.get("result", ""))[:8000],
            }
        )
        self._merge_files_into_session_evidence(
            payload.get("output_files", []),
            str(payload.get("category", "tool")),
            str(payload.get("task_id", payload.get("key", ""))),
        )
        self._refresh_evidence_items()
        self.toasts.show_toast(
            f"{payload.get('title', 'Tool')} {'completed' if code == 0 else 'failed'}."
        )

    def _selected_task_id(self) -> str:
        if not hasattr(self, "task_feed"):
            return ""
        item = self.task_feed.list_widget.currentItem()
        return str(item.data(Qt.UserRole) or "") if item is not None else ""

    def _run_selected_script_task(self) -> None:
        tid = self._selected_task_id()
        if not tid:
            self.toasts.show_toast("Select a script task first.")
            return
        self._run_script_task(tid, dry_run=False)

    def _run_script_task(self, task_id: str, dry_run: bool) -> None:
        if task_id not in self._visible_ids_for_prefix("script_task."):
            self.toasts.show_toast("This script task is available in Pro mode.")
            return
        task_meta = script_task_map().get(task_id)
        if task_meta is None:
            self.toasts.show_toast("Script task not found.")
            return
        if self.active_worker is not None:
            self.toasts.show_toast("Another task is running.")
            return
        sid = self._ensure_active_session(f"Script Task: {task_meta.title}")
        mask_options = MaskingOptions(
            enabled=self.rep_safe.isChecked() if hasattr(self, "rep_safe") else self.settings_state.share_safe_default,
            mask_ip=self.rep_ip.isChecked() if hasattr(self, "rep_ip") else self.settings_state.mask_ip_default,
            extra_tokens=(),
        )
        run_id = self.run_event_bus.create_run(
            name=f"Script Task: {task_meta.title}",
            risk=task_meta.risk,
            session_id=sid,
            metadata={"kind": "script", "capability_id": task_meta.id, "task_id": task_meta.id},
        )

        def task(progress_cb: Any, partial_cb: Any, log_cb: Any, cancel_event: Any, timeout_s: int) -> dict[str, Any]:
            del partial_cb
            progress_cb(5, "Starting")
            log_cb(f"[task] {task_meta.id} ({task_meta.category})")
            out_dir = ensure_dirs()["state"] / "task_runs" / sid / task_meta.category
            result = run_script_task(
                task_id,
                dry_run=dry_run,
                output_dir=out_dir,
                mask_options=mask_options,
                cancel_event=cancel_event,
                timeout_override_s=timeout_s,
                log_cb=log_cb,
                run_event_bus=self.run_event_bus,
                run_id=run_id,
            )
            code = int(result.get("code", 0 if result.get("dry_run") else 1))
            stdout = str(result.get("stdout", "")).strip()
            stderr = str(result.get("stderr", "")).strip()
            if stdout:
                log_cb(f"[stdout] {stdout[:2200]}")
            if stderr:
                log_cb(f"[stderr] {stderr[:2200]}")
            progress_cb(100, "Completed")
            payload = {
                **result,
                "task_id": task_id,
                "evidence_root": str(out_dir),
                "summary": f"{task_meta.title} ({'dry-run' if dry_run else 'run'}) exited with code {code}.",
            }
            err = classify_exit(code, stderr or stdout)
            if err is not None:
                payload.update(err.as_payload())
                payload["next_steps_list"] = ensure_next_steps(payload.get("suggested_next_steps"))
            return payload

        self._start_task(
            f"Script Task: {task_meta.title}",
            task,
            lambda payload: self._on_script_task(task_meta, payload),
            timeout_s=max(30, task_meta.timeout_s),
            risk=task_meta.risk,
            plain_summary=f"{task_meta.desc} This {'does not change system state' if dry_run else 'may change system state if designed to remediate'}.",
            details_text=f"Task ID: {task_meta.id}\nCategory: {task_meta.category}\nAdmin required: {task_meta.admin_required}\nTimeout: {task_meta.timeout_s}s",
            next_steps=self._task_next_steps(task_meta),
            rerun_cb=lambda: self._run_script_task(task_id, dry_run),
            evidence_root=str(ensure_dirs()["state"] / "task_runs" / sid),
            run_id=run_id,
        )

    def _on_script_task(self, task_meta: Any, payload: dict[str, Any]) -> None:
        code = int(payload.get("code", 0 if payload.get("dry_run") else 1))
        status = "completed" if code == 0 else "failed"
        self._append_action(
            {
                "key": str(payload.get("task_id", task_meta.id)),
                "title": task_meta.title,
                "risk": task_meta.risk,
                "code": code,
                "result": str(payload.get("stderr", "") or payload.get("stdout", ""))[:8000],
            }
        )
        self._merge_files_into_session_evidence(payload.get("output_files", []), task_meta.category, str(payload.get("task_id", task_meta.id)))
        self._refresh_evidence_items()
        self.toasts.show_toast(f"{task_meta.title} {status}.")

    def _collect_core_evidence(self) -> None:
        session_id = self._ensure_active_session("Evidence Collection")
        mask_options = MaskingOptions(
            enabled=self.rep_safe.isChecked() if hasattr(self, "rep_safe") else self.settings_state.share_safe_default,
            mask_ip=self.rep_ip.isChecked() if hasattr(self, "rep_ip") else self.settings_state.mask_ip_default,
            extra_tokens=(),
        )

        def task(progress_cb: Any, partial_cb: Any, log_cb: Any, cancel_event: Any, timeout_s: int) -> dict[str, Any]:
            del partial_cb
            log_cb("[evidence] Collecting core evidence bundle.")
            bundle_plan: list[tuple[str, Any, int, int, int]] = [
                ("System", collect_system_snapshot, 0, 22, 180),
                ("Network", collect_network_bundle, 22, 44, 180),
                ("Updates", collect_update_bundle, 44, 66, 220),
                ("Crash", collect_crash_bundle, 66, 84, 220),
                ("Event Logs", collect_event_logs, 84, 100, 240),
            ]
            merged_files: list[Any] = []
            summary_lines: list[str] = []
            evidence_root = str(ensure_dirs()["state"] / "evidence")
            for title, collector, start_pct, end_pct, min_timeout in bundle_plan:
                if cancel_event.is_set():
                    break
                span = max(1, end_pct - start_pct)
                result = collector(
                    session_id,
                    mask_options=mask_options,
                    progress_cb=lambda p, t, start=start_pct, span=span: progress_cb(start + int((max(0, min(100, p)) / 100.0) * span), t),
                    cancel_event=cancel_event,
                    timeout_s=max(min_timeout, int(timeout_s * 0.2)),
                    log_cb=log_cb,
                )
                evidence_root = result.root_dir
                merged_files.extend(result.files)
                summary_lines.append(f"{title} bundle:")
                summary_lines.append(result.summary_text.strip())
                if result.warnings:
                    summary_lines.extend([f"warning: {row}" for row in result.warnings])
                if cancel_event.is_set() or result.cancelled:
                    break
            for line in "\n".join(summary_lines).splitlines():
                if line.strip():
                    log_cb(line.strip())
            return {
                "session_id": session_id,
                "cancelled": bool(cancel_event.is_set()),
                "summary": "\n".join(summary_lines),
                "evidence_root": evidence_root,
                "evidence_files": [row.path for row in merged_files],
                "files": [{"path": row.path, "category": row.category, "task_id": row.task_id} for row in merged_files],
            }

        self._start_task(
            "Evidence Collection",
            task,
            self._on_evidence_collection,
            timeout_s=520,
            risk="Safe",
            plain_summary="Collects a core evidence bundle for support without changing system settings.",
            details_text="Bundles: system snapshot + network + updates + crash + event logs (.evtx best effort).",
            next_steps="Review evidence in Reports, then export a support pack for sharing.",
            rerun_cb=self._collect_core_evidence,
            evidence_root=str(ensure_dirs()["state"] / "evidence"),
            run_metadata={"kind": "evidence", "capability_id": "core_evidence_collection"},
        )

    def _on_evidence_collection(self, payload: dict[str, Any]) -> None:
        if not self.current_session:
            return
        files = payload.get("files", [])
        for row in files:
            if not isinstance(row, dict):
                continue
            self._merge_files_into_session_evidence([str(row.get("path", ""))], str(row.get("category", "evidence")), str(row.get("task_id", "")))
        self._append_action(
            {
                "key": "evidence_collection",
                "title": "Evidence Collection",
                "risk": "Safe",
                "code": 130 if payload.get("cancelled") else 0,
                "result": str(payload.get("summary", ""))[:8000],
            }
        )
        self._refresh_evidence_items()
        self.toasts.show_toast("Evidence collection complete." if not payload.get("cancelled") else "Evidence collection cancelled.")

    def _merge_files_into_session_evidence(self, files: Any, category: str, task_id: str) -> None:
        if not self.current_session:
            return
        evidence = self.current_session.setdefault("evidence", {})
        rows = evidence.setdefault("files", [])
        seen = {str(row.get("path", "")) for row in rows if isinstance(row, dict)}
        changed = False
        for path in files or []:
            p = str(path or "").strip()
            if not p or p in seen:
                continue
            rows.append({"path": p, "category": category or "evidence", "task_id": task_id})
            seen.add(p)
            changed = True
        if changed:
            save_session(self.current_session)

    def _task_next_steps(self, task: Any) -> str:
        category = str(getattr(task, "category", "")).lower()
        if category in {"network", "browser", "cloud"}:
            return "Compare adapter/IP output with previous sessions and run a network runbook if instability persists."
        if category in {"updates", "services", "security", "wmi"}:
            return "Check update service state and reboot status, then rerun update checks."
        if category in {"printer", "audio", "privacy", "devices", "office"}:
            return "Review spooler/queue output and verify printer connectivity."
        if category in {"integrity", "system"}:
            return "Review integrity findings and schedule longer scans off-hours if needed."
        if category in {"evidence", "eventlogs", "crash"}:
            return "Review collected evidence files and export a ticket pack for escalation."
        return "Review output summary, then run a related fix or runbook if needed."

    def run_selected_runbook(self, dry_run: bool) -> None:
        rid = self.rb_selected_id
        if not rid:
            self.toasts.show_toast("Select a runbook first.")
            return
        if rid not in self._visible_ids_for_prefix("runbook."):
            self.toasts.show_toast("This runbook is available in Pro mode.")
            return
        if self.active_worker is not None:
            self.toasts.show_toast("Another task is running.")
            return
        selected = next((r for r in RUNBOOKS if r.id == rid), None)
        create_restore_point = False
        if (not dry_run) and selected and selected.audience == "it":
            ok, create_restore_point = self._confirm_admin_batch(selected)
            if not ok:
                return
        run_id = self.run_event_bus.create_run(
            name=selected.title if selected is not None else "Runbook",
            risk="Admin" if selected and selected.audience == "it" else "Safe",
            session_id=str(self.current_session.get("session_id", "")) if self.current_session else "",
            metadata={"kind": "runbook", "capability_id": rid, "runbook_id": rid, "dry_run": dry_run},
        )

        def task(progress_cb: Any, partial_cb: Any, log_cb: Any, cancel_event: Any, timeout_s: int) -> dict[str, Any]:
            sid = self._ensure_active_session(f"Runbook: {rid}")
            del sid
            return execute_runbook(
                rid,
                dry_run=dry_run,
                progress_cb=progress_cb,
                partial_cb=partial_cb,
                log_cb=log_cb,
                cancel_event=cancel_event,
                timeout_s=timeout_s,
                mask_options=MaskingOptions(
                    enabled=self.rep_safe.isChecked() if hasattr(self, "rep_safe") else self.settings_state.share_safe_default,
                    mask_ip=self.rep_ip.isChecked() if hasattr(self, "rep_ip") else self.settings_state.mask_ip_default,
                    extra_tokens=(),
                ),
                create_restore_point_before_admin=create_restore_point,
                run_event_bus=self.run_event_bus,
                run_id=run_id,
            )

        self._start_task(
            selected.title if selected is not None else "Runbook",
            task,
            lambda payload: self._on_runbook(rid, payload),
            timeout_s=140,
            risk="Admin" if selected and selected.audience == "it" else "Safe",
            plain_summary=(selected.desc if selected is not None else "Runs a sequence of diagnostics and fixes."),
            details_text="\n".join([f"{s.title}: {s.task_id}" for s in selected.steps]) if selected is not None else "",
            next_steps="Review collected evidence, then export a ticket pack if escalation is needed.",
            rerun_cb=lambda: self.run_selected_runbook(dry_run),
            run_id=run_id,
        )

    def _confirm_admin_batch(self, runbook: Any) -> tuple[bool, bool]:
        dialog = QDialog(self)
        dialog.setWindowTitle(f"Confirm IT Runbook: {runbook.title}")
        dialog.resize(620, 360)
        layout = QVBoxLayout(dialog)
        layout.addWidget(QLabel("This runbook may execute multiple diagnostic commands."))
        layout.addWidget(QLabel("Commands are listed below; review before continuing."))
        drawer = DrawerCard("Batch Commands")
        drawer.set_text("\n".join([step.task_id for step in runbook.steps]))
        layout.addWidget(drawer)
        reboot_check = QCheckBox("I understand this may require a reboot for follow-up fixes.")
        restore_check = QCheckBox("Create a restore point before admin steps (best effort).")
        restore_check.setChecked(True)
        layout.addWidget(reboot_check)
        layout.addWidget(restore_check)
        buttons = QDialogButtonBox(QDialogButtonBox.Ok | QDialogButtonBox.Cancel)
        layout.addWidget(buttons)
        buttons.accepted.connect(dialog.accept)
        buttons.rejected.connect(dialog.reject)
        if dialog.exec() != QDialog.Accepted:
            return False, False
        if not reboot_check.isChecked():
            self.toasts.show_toast("Reboot acknowledgement required for IT runbook.")
            return False, False
        return True, restore_check.isChecked()

    def _on_runbook(self, rid: str, payload: dict[str, Any]) -> None:
        if hasattr(self, "rb_detail"):
            self.rb_detail.sub.setText(str(payload.get("summary_text", payload.get("title", rid)))[:1200])
        self._merge_runbook_evidence(payload)
        step_codes = [
            int((step.get("result", {}) or {}).get("code", 0 if payload.get("dry_run") else 1))
            for step in payload.get("steps", [])
            if isinstance(step, dict)
        ]
        failed = any(code not in (0,) for code in step_codes)
        status_code = 130 if payload.get("cancelled") else (1 if failed else 0)
        risk = "Admin" if payload.get("requires_admin") else "Safe"
        self._append_action(
            {
                "key": rid,
                "title": payload.get("title", rid),
                "risk": risk,
                "code": status_code,
                "result": str(payload.get("summary_text", ""))[:8000],
            }
        )
        self._refresh_evidence_items()

    def _merge_runbook_evidence(self, payload: dict[str, Any]) -> None:
        if not self.current_session:
            return
        evidence = self.current_session.setdefault("evidence", {})
        files = evidence.setdefault("files", [])
        actions = self.current_session.setdefault("actions", [])
        existing = {str(row.get("path", "")) for row in files if isinstance(row, dict)}
        task_meta = script_task_map()
        for path in payload.get("evidence_files", []):
            if not path or path in existing:
                continue
            category = "evidence"
            task_id = ""
            for step in payload.get("steps", []):
                result = step.get("result", {})
                output_files = result.get("output_files", [])
                if path in output_files:
                    task_id = str(step.get("task_id", ""))
                    category = task_meta.get(task_id).category if task_id in task_meta else str(result.get("category", "evidence"))
                    break
            files.append({"path": path, "category": category, "task_id": task_id})
            existing.add(path)
        for step in payload.get("steps", []):
            if not isinstance(step, dict):
                continue
            result = step.get("result", {})
            if not isinstance(result, dict):
                continue
            actions.append(
                {
                    "key": str(step.get("task_id", "")),
                    "title": str(step.get("title", "")),
                    "risk": "Admin" if bool(result.get("admin_required")) else "Safe",
                    "code": int(result.get("code", 0 if result.get("dry_run") else 1)),
                    "result": str(result.get("stderr", "") or result.get("stdout", ""))[:8000],
                }
            )
        save_session(self.current_session)

    def _ensure_active_session(self, symptom: str) -> str:
        if self.current_session and self.current_session.get("session_id"):
            return str(self.current_session.get("session_id"))
        sid = new_session_id()
        self.current_session = {
            "session_id": sid,
            "symptom": symptom,
            "created_local": _now_local(),
            "actions": [],
            "findings": [],
            "sysinfo": {"timestamp_utc": datetime.utcnow().isoformat(), "hostname": "", "user": ""},
            "network": {"ssid": ""},
        }
        persist_new_session(self.current_session, summary="Runbook evidence session")
        self._refresh_home_history()
        self._refresh_history()
        self._update_context_labels()
        return sid

    def open_command_palette(self) -> None:
        self._search_popup.hide_popup()
        d = CommandPaletteDialog(self, allowed_capability_ids=self._visible_capability_ids()); d.search.setText(self.top_search.text())
        if d.exec() != QDialog.Accepted:
            return
        self._dispatch_search_selection(d.selected_kind, d.selected_key)

    def _dispatch_search_selection(self, kind: str, key: str) -> None:
        k, v = str(kind or "").strip(), str(key or "").strip()
        if not k or not v:
            return
        if k == "route":
            route_map = {
                "home": "Home",
                "playbooks": "Playbooks",
                "diagnose": "Diagnose",
                "fixes": "Fixes",
                "reports": "Reports",
                "history": "History",
                "settings": "Settings",
            }
            route_name = route_map.get(v.lower(), "")
            if route_name and route_name in self.NAV_ITEMS:
                self.nav.setCurrentRow(self.NAV_ITEMS.index(route_name))
        elif k == "play":
            if v.startswith("runbook."):
                self._dispatch_search_selection("runbook", v.split(".", 1)[1])
            elif v.startswith("task."):
                self._dispatch_search_selection("task", v.split(".", 1)[1])
        elif k == "session":
            self._load_session(v); self.nav.setCurrentRow(self.NAV_ITEMS.index("History"))
        elif k == "fix":
            self.nav.setCurrentRow(self.NAV_ITEMS.index("Fixes")); self._select_fix(v)
        elif k == "runbook":
            if self._playbooks_available():
                self.nav.setCurrentRow(self.NAV_ITEMS.index("Playbooks"))
                if hasattr(self, "pb_segment"):
                    self.pb_segment.setCurrentText("Runbooks")
                self._select_runbook(v)
            else:
                self.rb_selected_id = v
                self.run_selected_runbook(True)
        elif k == "task":
            if self._playbooks_available():
                self.nav.setCurrentRow(self.NAV_ITEMS.index("Playbooks"))
                if hasattr(self, "pb_segment"):
                    self.pb_segment.setCurrentText("Tools")
                task = script_task_map().get(v)
                self.tb_search.setText(task.title if task is not None else v)
                self._select_script_task(v)
            else:
                self.toasts.show_toast("Script tasks are available in Pro mode.")
        elif k == "capability":
            if v.startswith("fix_action."):
                self.nav.setCurrentRow(self.NAV_ITEMS.index("Fixes"))
                self._select_fix(v.split(".", 1)[1])
            elif v.startswith("runbook."):
                runbook_id = v.split(".", 1)[1]
                if self._playbooks_available():
                    self.nav.setCurrentRow(self.NAV_ITEMS.index("Playbooks"))
                    if hasattr(self, "pb_segment"):
                        self.pb_segment.setCurrentText("Runbooks")
                    self._select_runbook(runbook_id)
                else:
                    self.rb_selected_id = runbook_id
                    self.run_selected_runbook(True)
            elif v.startswith("tool."):
                self._launch_tool_payload(v.split(".", 1)[1])
            elif v.startswith("script_task."):
                task_id = v.split(".", 1)[1]
                if self._playbooks_available():
                    self.nav.setCurrentRow(self.NAV_ITEMS.index("Playbooks"))
                    if hasattr(self, "pb_segment"):
                        self.pb_segment.setCurrentText("Tools")
                    self._select_script_task(task_id)
                else:
                    self.toasts.show_toast("Script tasks are available in Pro mode.")
            else:
                self.nav.setCurrentRow(self.NAV_ITEMS.index("Home"))
        elif k == "tool":
            self._launch_tool_payload(v)
        elif k == "export":
            self._copy_text(v + ".zip")
        elif k == "kb":
            self.nav.setCurrentRow(self.NAV_ITEMS.index("Diagnose"))
        elif k == "run":
            self.nav.setCurrentRow(self.NAV_ITEMS.index("History"))
            self.toasts.show_toast(f"Run indexed: {v}")
        elif k == "finding":
            self.nav.setCurrentRow(self.NAV_ITEMS.index("Diagnose"))
            if hasattr(self, "diag_search"):
                self.diag_search.setText(v)
        elif k == "artifact":
            target = Path(v)
            if target.exists():
                if os.name == "nt":
                    os.startfile(str(target))
            else:
                self.toasts.show_toast("Artifact path not found.")
        elif k == "file":
            target = Path(v)
            if target.exists():
                if os.name == "nt":
                    os.startfile(str(target))
            else:
                self.toasts.show_toast("Indexed file path not found.")
        else:
            self.nav.setCurrentRow(0)

    def _select_fix(self, key: str) -> None:
        lw = self.fix_list.list_widget
        for i in range(lw.count()):
            it = lw.item(i)
            if str(it.data(Qt.UserRole)) == key:
                lw.setCurrentItem(it); lw.scrollToItem(it); break

    def _select_runbook(self, rid: str) -> None:
        if hasattr(self, "pb_segment"):
            self.pb_segment.setCurrentText("Runbooks")
        lw = self.rb_feed.list_widget
        for i in range(lw.count()):
            it = lw.item(i)
            if str(it.data(Qt.UserRole)) == rid:
                lw.setCurrentItem(it); lw.scrollToItem(it); self._set_runbook_selection(rid, open_details=True); break

    def _select_script_task(self, task_id: str) -> None:
        if hasattr(self, "pb_segment"):
            self.pb_segment.setCurrentText("Tools")
        if not hasattr(self, "task_feed"):
            return
        lw = self.task_feed.list_widget
        for i in range(lw.count()):
            it = lw.item(i)
            if str(it.data(Qt.UserRole)) == task_id:
                lw.setCurrentItem(it)
                lw.scrollToItem(it)
                break

    def _finding_menu(self, lw: QListWidget, pos: Any) -> None:
        it = lw.itemAt(pos)
        if it is None:
            return
        row = it.data(Qt.UserRole) or {}
        actions = [
            ContextAction("Copy summary", lambda: self._copy_text(str(row.get("plain", row.get("detail", ""))))),
            ContextAction("Copy technical", lambda: self._copy_text(str(row.get("technical", row.get("detail", ""))))),
            ContextAction("Copy next steps", lambda: self._copy_text(self._deterministic_next_action(row))),
            ContextAction("Preview", lambda: self.toasts.show_toast(str(row.get("detail", "")))),
            ContextAction("Export selection", lambda: self.nav.setCurrentRow(self.NAV_ITEMS.index("Reports"))),
            ContextAction("Tag/Pin", lambda: self._pin_action(str(row.get("title", "finding")))),
            ContextAction("Add note", lambda: self._add_note(str(row.get("title", "finding")))),
        ]
        show_context_menu(self, lw, pos, actions)

    def _fix_menu(self, lw: QListWidget, pos: Any) -> None:
        it = lw.itemAt(pos)
        if it is None:
            return
        key = str(it.data(Qt.UserRole) or "")
        fx = next((f for f in FIX_CATALOG if f.key == key), None)
        if fx is None:
            return
        actions = [
            ContextAction("Copy summary", lambda: self._copy_text(fx.plain)),
            ContextAction("Copy technical", lambda: self._copy_text("\n".join(fx.commands))),
            ContextAction("Preview", lambda: self.toasts.show_toast(fx.description)),
            ContextAction("Run", lambda: self.run_fix_action(key)),
            ContextAction("Run with Tool Runner", lambda: self.run_fix_action(key)),
        ]
        if self._is_favorite("fixes", key):
            actions.append(ContextAction("Remove from favorites", lambda: self._toggle_favorite("fixes", key, False)))
        else:
            actions.append(ContextAction("Add to favorites", lambda: self._toggle_favorite("fixes", key, True)))
        actions.extend(
            [
                ContextAction("Pin to Home quick actions", lambda: self._pin_action(key)),
                ContextAction("Add note", lambda: self._add_note(fx.title)),
            ]
        )
        show_context_menu(self, lw, pos, actions)

    def _tool_menu(self, lw: QListWidget, pos: Any) -> None:
        it = lw.itemAt(pos)
        if it is None:
            return
        tid = str(it.data(Qt.UserRole) or "")
        tool = next((x for x in TOOL_DIRECTORY if x.id == tid), None)
        if tool is None:
            return
        actions = [
            ContextAction("Copy summary", lambda: self._copy_text(tool.plain)),
            ContextAction("Copy technical", lambda: self._copy_text(tool.command)),
            ContextAction("Preview", lambda: self.toasts.show_toast(tool.desc)),
            ContextAction("Launch", lambda: self._launch_tool_payload(tid)),
        ]
        if self._is_favorite("tools", tid):
            actions.append(ContextAction("Remove from favorites", lambda: self._toggle_favorite("tools", tid, False)))
        else:
            actions.append(ContextAction("Add to favorites", lambda: self._toggle_favorite("tools", tid, True)))
        actions.extend(
            [
                ContextAction("Pin to Home quick actions", lambda: self._pin_action(tool.id)),
                ContextAction("Add note", lambda: self._add_note(tool.title)),
            ]
        )
        show_context_menu(self, lw, pos, actions)

    def _task_menu(self, lw: QListWidget, pos: Any) -> None:
        it = lw.itemAt(pos)
        if it is None:
            return
        tid = str(it.data(Qt.UserRole) or "")
        task = script_task_map().get(tid)
        if task is None:
            return
        actions = [
            ContextAction("Copy summary", lambda: self._copy_text(task.desc)),
            ContextAction("Copy technical", lambda: self._copy_text(" ".join(task.command or ("<custom-runner>",)))),
            ContextAction("Preview", lambda: self.toasts.show_toast(f"{task.title} [{task.category}] {task.risk}")),
            ContextAction("Run dry-run", lambda: self._run_script_task(task.id, dry_run=True)),
            ContextAction("Run", lambda: self._run_script_task(task.id, dry_run=False)),
            ContextAction("Pin to Home quick actions", lambda: self._pin_action(task.id)),
        ]
        show_context_menu(self, lw, pos, actions)

    def _runbook_menu(self, lw: QListWidget, pos: Any) -> None:
        it = lw.itemAt(pos)
        if it is None:
            return
        rid = str(it.data(Qt.UserRole) or "")
        rb = next((x for x in RUNBOOKS if x.id == rid), None)
        if rb is None:
            return
        actions = [
            ContextAction("Preview", lambda: self._set_runbook_selection(rid, open_details=True)),
            ContextAction("Run dry-run", lambda: (self._set_runbook_selection(rid, open_details=True), self.run_selected_runbook(True))),
            ContextAction("Run", lambda: (self._set_runbook_selection(rid, open_details=True), self.run_selected_runbook(False))),
            ContextAction("Run with Tool Runner", lambda: (self._set_runbook_selection(rid, open_details=True), self.run_selected_runbook(False))),
            ContextAction("Export-only", lambda: self._runbook_export_only(rid)),
            ContextAction("Copy what it does", lambda: self._copy_runbook_summary(rid)),
        ]
        if self._is_favorite("runbooks", rid):
            actions.append(ContextAction("Remove from favorites", lambda: self._toggle_favorite("runbooks", rid, False)))
        else:
            actions.append(ContextAction("Add to favorites", lambda: self._toggle_favorite("runbooks", rid, True)))
        actions.append(ContextAction("Pin to Home quick actions", lambda: self._pin_action(rid)))
        show_context_menu(self, lw, pos, actions)

    def _runbook_export_only(self, rid: str) -> None:
        self._set_runbook_selection(rid, open_details=True)
        if not self.current_session:
            self.toasts.show_toast("No active session to export.")
            return
        self.nav.setCurrentRow(self.NAV_ITEMS.index("Reports"))
        self.export_current_session()

    def _copy_runbook_summary(self, rid: str) -> None:
        rb = runbook_map().get(rid)
        if rb is None:
            return
        lines = [f"{rb.title}", rb.desc, "", "Steps:"]
        for step in rb.steps:
            lines.append(f"- {step.title} ({step.task_id})")
        self._copy_text("\n".join(lines))

    def _session_menu(self, lw: QListWidget, pos: Any) -> None:
        it = lw.itemAt(pos)
        if it is None:
            return
        payload = it.data(Qt.UserRole) or {}
        sid = str(payload.get("session_id", ""))
        if not sid:
            return
        summary = f"{payload.get('session_id', '')} | {payload.get('symptom', '')} | {payload.get('summary', '')}"
        actions = [
            ContextAction("Copy summary", lambda: self._copy_text(summary)),
            ContextAction("Copy technical", lambda: self._copy_text(f"session_id={sid}")),
            ContextAction("Preview", lambda: self._load_session(sid)),
            ContextAction("Export selection", lambda: (self._load_session(sid), self.nav.setCurrentRow(self.NAV_ITEMS.index("Reports")))),
            ContextAction("Tag/Pin", lambda: self._pin_session(sid)),
            ContextAction("Add note", lambda: self._add_note(sid)),
        ]
        show_context_menu(self, lw, pos, actions)

    def _home_favorite_menu(self, lw: QListWidget, pos: Any) -> None:
        it = lw.itemAt(pos)
        if it is None:
            return
        payload = it.data(Qt.UserRole) or {}
        if not isinstance(payload, dict):
            return
        kind = str(payload.get("kind", ""))
        key = str(payload.get("key", ""))
        actions = [
            ContextAction("Run/Open", lambda: self._launch_home_favorite(payload)),
            ContextAction("Remove favorite", lambda: self._toggle_favorite(kind, key, False)),
            ContextAction("Pin to Home quick actions", lambda: self._pin_action(key)),
        ]
        show_context_menu(self, lw, pos, actions)

    def _add_note(self, text: str) -> None:
        if not self.current_session:
            return
        self.current_session.setdefault("notes", []).append({"time": _now_local(), "text": text})
        save_session(self.current_session)
        self.toasts.show_toast("Note added.")

    def _pin_action(self, key: str) -> None:
        pins = self.settings_state.pinned_actions or []
        if key not in pins:
            pins.append(key)
        self.settings_state.pinned_actions = pins
        save_settings(self.settings_state)
        self.toasts.show_toast("Pinned.")

    def _pin_session(self, sid: str) -> None:
        rows = load_index()
        for row in rows:
            if row.session_id == sid:
                row.pinned = True
                add_or_update_meta(row)
                break
        self.toasts.show_toast("Session pinned.")

    def _is_favorite(self, kind: str, key: str) -> bool:
        if kind == "fixes":
            return key in (self.settings_state.favorites_fixes or [])
        if kind == "tools":
            return key in (self.settings_state.favorites_tools or [])
        if kind == "runbooks":
            return key in (self.settings_state.favorites_runbooks or [])
        return False

    def _toggle_favorite(self, kind: str, key: str, enabled: bool) -> None:
        if kind == "fixes":
            rows = self.settings_state.favorites_fixes or []
        elif kind == "tools":
            rows = self.settings_state.favorites_tools or []
        elif kind == "runbooks":
            rows = self.settings_state.favorites_runbooks or []
        else:
            return
        if enabled and key not in rows:
            rows.append(key)
        if (not enabled) and key in rows:
            rows.remove(key)
        if kind == "fixes":
            self.settings_state.favorites_fixes = rows
        elif kind == "tools":
            self.settings_state.favorites_tools = rows
        else:
            self.settings_state.favorites_runbooks = rows
        save_settings(self.settings_state)
        self._refresh_home_favorites()
        self.toasts.show_toast("Favorites updated.")

    def _make_home_favorite_row(self, item: FeedItemAdapter, density: str) -> QWidget:
        payload = item.payload if isinstance(item.payload, dict) else {}
        row = ToolRow(item.title, item.category or "fav", item.subtitle, payload=payload, density=density)
        row.open_clicked.connect(self._launch_home_favorite)
        return row

    def _refresh_home_favorites(self) -> None:
        visible = self._visible_capability_ids()
        entries: list[FeedItemAdapter] = []
        for key in self.settings_state.favorites_fixes or []:
            fx = next((f for f in FIX_CATALOG if f.key == key), None)
            if fx is not None and f"fix_action.{fx.key}" in visible:
                entries.append(FeedItemAdapter(key=f"fix:{fx.key}", title=fx.title, subtitle=fx.plain, payload={"kind": "fixes", "key": fx.key}, category="Fix"))
        for key in self.settings_state.favorites_tools or []:
            tool = next((t for t in TOOL_DIRECTORY if t.id == key), None)
            if tool is not None and f"tool.{tool.id}" in visible:
                entries.append(FeedItemAdapter(key=f"tool:{tool.id}", title=tool.title, subtitle=tool.desc, payload={"kind": "tools", "key": tool.id}, category="Tool"))
        for key in self.settings_state.favorites_runbooks or []:
            rb = next((r for r in RUNBOOKS if r.id == key), None)
            if rb is not None and f"runbook.{rb.id}" in visible:
                entries.append(FeedItemAdapter(key=f"runbook:{rb.id}", title=rb.title, subtitle=rb.desc, payload={"kind": "runbooks", "key": rb.id}, category="Runbook"))
        self.home_favorites.set_items(entries[:6] if len(entries) > 6 else entries)
        if not entries:
            self.home_favorites.show_empty("*", "No quick actions in this mode. Switch to Pro to see more.")

    def _launch_home_favorite(self, payload: Any) -> None:
        if not isinstance(payload, dict):
            return
        kind = str(payload.get("kind", ""))
        key = str(payload.get("key", ""))
        visible = self._visible_capability_ids()
        if kind == "fixes":
            if f"fix_action.{key}" not in visible:
                self.toasts.show_toast("This fix is available in Pro mode.")
                return
            self.nav.setCurrentRow(self.NAV_ITEMS.index("Fixes"))
            self.run_fix_action(key)
        elif kind == "tools":
            if f"tool.{key}" not in visible:
                self.toasts.show_toast("This tool is available in Pro mode.")
                return
            self._launch_tool_payload(key)
        elif kind == "runbooks":
            if f"runbook.{key}" not in visible:
                self.toasts.show_toast("This runbook is available in Pro mode.")
                return
            self._set_runbook_selection(key, open_details=True)
            self.run_selected_runbook(True)

    def _update_weekly_status(self) -> None:
        if not hasattr(self, "weekly_card"):
            return
        if not self.settings_state.weekly_reminder_enabled:
            self.weekly_card.sub.setText("Reminder is off.")
            return
        last_raw = (self.settings_state.last_weekly_check_utc or "").strip()
        due = True
        if last_raw:
            try:
                last_dt = datetime.strptime(last_raw, "%Y-%m-%dT%H:%M:%SZ")
                due = (datetime.utcnow() - last_dt).days >= 7
            except Exception:
                due = True
        if not last_raw:
            self.weekly_card.sub.setText("Weekly check is enabled. No completed check recorded yet.")
        elif due:
            self.weekly_card.sub.setText(f"Weekly check is due. Last completed check: {last_raw}.")
        else:
            self.weekly_card.sub.setText(f"Weekly check is up to date. Last completed check: {last_raw}.")

    def _update_context_labels(self) -> None:
        self.ctx_session.setText(f"Session: {self.current_session.get('session_id', 'none')}")
        self.ctx_symptom.setText(f"Symptom: {self.current_session.get('symptom', 'n/a')}")
        self.ctx_share.setText(f"Share-safe: {'on' if self.rep_safe.isChecked() else 'off'}" if hasattr(self, "rep_safe") else "Share-safe: on")
        self.ctx_preset.setText(f"Preset: {self.rep_preset.currentText()}" if hasattr(self, "rep_preset") else "Preset: home_share")
        self.ctx_last.setText(f"Last run: {self.current_session.get('created_local', 'n/a')}")
        self._sync_context_bar_visibility()

    def copy_session_summary(self) -> None:
        if not self.current_session:
            self.toasts.show_toast("No active session.")
            return
        lines = [f"Session {self.current_session.get('session_id', '')}", f"Symptom: {self.current_session.get('symptom', '')}", f"Findings: {len(self.current_session.get('findings', []))}"]
        for finding in self.current_session.get("findings", [])[:5]:
            lines.append(f"- {finding.get('status', 'INFO')}: {finding.get('title', '')} - {finding.get('detail', '')}")
        self._copy_text("\n".join(lines))

    def end_session(self) -> None:
        self.current_session = {}
        self.selected_finding = {}
        self.diag_summary.title.setText("No active session")
        self.diag_summary.sub.setText("Run Quick Check from Home.")
        self.diag_top3.sub.setText("No findings yet.")
        self.diag_counts.sub.setText("CRIT 0 | WARN 0 | OK 0 | INFO 0")
        self._rebuild_diagnose_sections([])
        self._update_diagnose_context({})
        self._refresh_evidence_items()
        self._rebuild_report_tree()
        self._refresh_run_center()
        self._refresh_home_changes()
        self._update_context_labels()
        self.toasts.show_toast("Session ended.")

    def export_last_session(self) -> None:
        rows = load_index()
        if not rows:
            self.toasts.show_toast("No sessions yet.")
            return
        self._load_session(rows[0].session_id)
        self.nav.setCurrentRow(self.NAV_ITEMS.index("Reports"))
        self.export_current_session()

    def open_last_export_folder(self) -> None:
        p = self.last_export.get("folder", "")
        if not p:
            self.toasts.show_toast("No export folder yet.")
            return
        if os.name == "nt":
            os.startfile(p)

    def copy_last_export_path(self) -> None:
        p = self.last_export.get("zip", "")
        if not p:
            self.toasts.show_toast("No export path yet.")
            return
        self._copy_text(p)

    def copy_ticket_summary(self, short: bool) -> None:
        k = "short" if short else "detail"
        txt = self.last_export.get(k, "")
        if not txt:
            self.toasts.show_toast("No ticket summary yet.")
            return
        self._copy_text(txt)

    def save_feedback_form(self) -> None:
        p = save_feedback(self.fb_name.text().strip(), self.fb_mail.text().strip(), self.fb_cat.currentText(), self.fb_msg.toPlainText().strip())
        self.fb_msg.clear()
        self.toasts.show_toast(f"Feedback saved: {p.name}")

    def create_desktop_logo(self, *, force: bool) -> None:
        try:
            path, created = ensure_logo_on_desktop(overwrite=force)
            status = "created" if created else "already exists"
            self.toasts.show_toast(f"Desktop logo {status}: {path.name}")
            LOGGER.info(
                "action type=tool_open/utility name=create_desktop_logo force=%s created=%s path=%s",
                force,
                created,
                path,
            )
            if self.current_session:
                self._append_action(
                    {
                        "type": "tool_open/utility",
                        "key": "create_desktop_logo",
                        "title": "Create Desktop Logo",
                        "risk": "Safe",
                        "code": 0,
                        "result": f"{status}: {path}",
                    }
                )
            self._refresh_history()
        except Exception as exc:
            LOGGER.warning("Desktop logo action failed: %s", exc)
            self.toasts.show_toast("Could not create Desktop logo. Check logs.")

    def _sync_settings_ui(self) -> None:
        self._syncing_settings = True
        s = self.settings_state
        self.s_safe.setChecked(s.safe_only_mode); self.s_admin.setChecked(s.show_admin_tools); self.s_adv.setChecked(s.show_advanced_tools)
        self.s_diag.setChecked(s.diagnostic_mode); self.s_share.setChecked(s.share_safe_default); self.s_ip.setChecked(s.mask_ip_default)
        self.s_weekly.setChecked(s.weekly_reminder_enabled)
        if hasattr(self, "s_drawer_pin"):
            self.s_drawer_pin.setChecked(bool(getattr(s, "details_drawer_pinned", True)))
        if hasattr(self, "side_sheet"):
            self.side_sheet.set_pinned(bool(getattr(s, "details_drawer_pinned", True)))
        self.s_palette.setCurrentText(palette_label(s.theme_palette)); self.s_mode.setCurrentText(normalize_mode(s.theme_mode)); self.s_density.setCurrentText(normalize_density(s.density))
        if hasattr(self, "s_ui_scale"):
            self._pending_ui_scale_pct = clamp_ui_scale(getattr(s, "ui_scale_pct", 100))
            self.s_ui_scale.blockSignals(True)
            self.s_ui_scale.setValue(self._pending_ui_scale_pct)
            self.s_ui_scale.blockSignals(False)
        if hasattr(self, "s_ui_scale_value"):
            self.s_ui_scale_value.setText(f"{self._pending_ui_scale_pct}%")
        if hasattr(self, "s_ui_mode"):
            self.s_ui_mode.blockSignals(True)
            self.s_ui_mode.setCurrentText("pro" if s.ui_mode == "pro" else "basic")
            self.s_ui_mode.blockSignals(False)
        self._syncing_settings = False
        self._sync_ui_mode_controls()

    def save_settings_from_ui(self) -> None:
        if self._syncing_settings:
            return
        self.settings_state.safe_only_mode = self.s_safe.isChecked(); self.settings_state.show_admin_tools = self.s_admin.isChecked(); self.settings_state.show_advanced_tools = self.s_adv.isChecked(); self.settings_state.diagnostic_mode = self.s_diag.isChecked()
        self.settings_state.share_safe_default = self.s_share.isChecked(); self.settings_state.mask_ip_default = self.s_ip.isChecked(); self.settings_state.weekly_reminder_enabled = self.s_weekly.isChecked()
        self.settings_state.nav_collapsed = True
        if hasattr(self, "s_drawer_pin"):
            self.settings_state.details_drawer_pinned = self.s_drawer_pin.isChecked()
        if hasattr(self, "side_sheet"):
            self.side_sheet.set_pinned(bool(self.settings_state.details_drawer_pinned))
        self.settings_state.theme_palette = palette_key_from_label(self.s_palette.currentText()); self.settings_state.theme_mode = self.s_mode.currentText(); self.settings_state.density = self.s_density.currentText()
        if hasattr(self, "s_ui_scale"):
            self.settings_state.ui_scale_pct = clamp_ui_scale(self.s_ui_scale.value())
        if hasattr(self, "s_ui_mode"):
            self.settings_state.ui_mode = "pro" if self.s_ui_mode.currentText().strip().lower() == "pro" else "basic"
        self.layout_policy_state = layout_policy(self.settings_state)
        if self.settings_state.ui_mode == "basic":
            if self.settings_state.show_admin_tools and self.settings_state.safe_only_mode:
                self.settings_state.safe_only_mode = False
                self.s_safe.blockSignals(True)
                self.s_safe.setChecked(False)
                self.s_safe.blockSignals(False)
            if (not self.settings_state.show_admin_tools) and (not self.settings_state.safe_only_mode):
                self.settings_state.safe_only_mode = True
                self.s_safe.blockSignals(True)
                self.s_safe.setChecked(True)
                self.s_safe.blockSignals(False)
            self.settings_state.show_advanced_tools = False
            self.s_adv.blockSignals(True)
            self.s_adv.setChecked(False)
            self.s_adv.blockSignals(False)
        save_settings(self.settings_state)
        self.safety_policy = policy_from_settings(self.settings_state)
        self._refresh_fixes()
        self._refresh_toolbox()
        self._refresh_runbooks()
        self._refresh_home_favorites()
        self._apply_mode_visibility()
        self._apply_theme()
        self._refresh_db_info_label()
        desired_open = self.settings_state.right_panel_open and self.layout_policy_state.right_panel_default_open
        self._set_concierge_collapsed(not desired_open, persist=False)
        self._update_weekly_status()
        self._update_context_labels()
        self._sync_shell_status_bar()
        self.toasts.show_toast("Settings saved.")




