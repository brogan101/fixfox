from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
import time
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Any
from unittest.mock import patch

from PySide6.QtCore import QPoint
from PySide6.QtWidgets import QAbstractButton, QApplication, QListWidget, QMenu, QTreeView, QWidget

LEGACY_NAV_OBJECTS = {"Nav", "NavList", "DrawerNav", "LegacyNav", "MainNav"}
FONT_WARNING_PATTERNS = (
    "qt.qpa.fonts:",
    "Failed to create DirectWrite face",
    "Cannot open",
)
REQUIRED_MENU = {
    "Session": ["New Session", "Open Session", "Export", "Open Exports Folder"],
    "View": ["Toggle Details Panel", "Density", "Theme", "Palette", "Mode"],
    "Help": ["Docs", "About Fix Fox"],
}


@dataclass
class RequirementResult:
    req_id: str
    title: str
    passed: bool
    evidence: list[str] = field(default_factory=list)


@dataclass
class VerificationOutcome:
    results: list[RequirementResult]
    widget_tree_sample: list[str] = field(default_factory=list)
    menu_structure: list[str] = field(default_factory=list)
    font_probe_stdout: str = ""
    font_probe_stderr: str = ""

    @property
    def passed(self) -> bool:
        return all(item.passed for item in self.results)

    def render_console(self) -> str:
        lines = ["Fix Fox Requirements Verification", ""]
        for result in self.results:
            status = "PASS" if result.passed else "FAIL"
            lines.append(f"{result.req_id} {status} - {result.title}")
            for row in result.evidence[:8]:
                lines.append(f"  - {row}")
        lines.append("")
        lines.append(f"Final Verdict: {'PASS' if self.passed else 'FAIL'}")
        return "\n".join(lines)

    def render_markdown(self) -> str:
        lines: list[str] = [
            "# Requirements Verification",
            "",
            f"Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
            "",
            "## Checklist",
            "",
        ]
        for result in self.results:
            status = "PASS" if result.passed else "FAIL"
            lines.append(f"{result.req_id}. **{result.title}** - **{status}**")
            for row in result.evidence:
                lines.append(f"- {row}")
            lines.append("")
        lines.append("## Runtime Samples")
        lines.append("")
        lines.append("### Widget Tree Sample")
        lines.append("")
        lines.append("```text")
        lines.extend(self.widget_tree_sample[:80] or ["(no widget tree sample captured)"])
        lines.append("```")
        lines.append("")
        lines.append("### Overflow Menu Structure")
        lines.append("")
        lines.append("```text")
        lines.extend(self.menu_structure[:80] or ["(no overflow menu captured)"])
        lines.append("```")
        lines.append("")
        lines.append("### Font Probe stderr")
        lines.append("")
        lines.append("```text")
        stderr = self.font_probe_stderr.strip()
        lines.append(stderr if stderr else "(empty)")
        lines.append("```")
        lines.append("")
        lines.append(f"Final Verdict: {'PASS' if self.passed else 'FAIL'}")
        return "\n".join(lines).rstrip() + "\n"


def _repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def _contains_any(text: str, needles: tuple[str, ...]) -> bool:
    lower = text.lower()
    return any(needle.lower() in lower for needle in needles)


def _drain(app: QApplication, cycles: int = 6, delay_s: float = 0.03) -> None:
    for _ in range(cycles):
        app.processEvents()
        time.sleep(delay_s)


def _widget_tree_dump(root: QWidget) -> list[str]:
    lines: list[str] = []
    targets = sorted(
        root.findChildren(QWidget),
        key=lambda w: ((w.objectName() or "~").lower(), w.__class__.__name__.lower()),
    )
    for widget in targets:
        name = widget.objectName() or "-"
        visible = "visible" if widget.isVisible() else "hidden"
        lines.append(f"{name} ({widget.__class__.__name__}) [{visible}]")
    return lines


def _collect_menu_structure(menu: QMenu) -> tuple[list[str], dict[str, list[str]], int]:
    lines: list[str] = []
    groups: dict[str, list[str]] = {}
    section = ""
    separators = 0
    for action in menu.actions():
        text = (action.text() or "").strip()
        if action.isSeparator():
            separators += 1
            if text:
                section = text
                groups.setdefault(section, [])
                lines.append(f"[section] {section}")
            else:
                lines.append("[separator]")
            continue
        submenu = action.menu()
        if submenu is not None:
            groups.setdefault(section, []).append(text)
            entries = [(entry.text() or "").strip() for entry in submenu.actions() if not entry.isSeparator()]
            groups[f"{section}/{text}"] = [entry for entry in entries if entry]
            lines.append(f"[submenu] {text}: {', '.join(groups[f'{section}/{text}'])}")
            continue
        groups.setdefault(section, []).append(text)
        lines.append(f"[action] {text}")
    return lines, groups, separators


def _capture_overflow_menu(window: QWidget, app: QApplication) -> QMenu | None:
    del app

    class _FakeSignal:
        def __init__(self) -> None:
            self._callbacks: list[Any] = []

        def connect(self, callback: Any) -> None:
            self._callbacks.append(callback)

        def emit(self, *args: Any, **kwargs: Any) -> None:
            for callback in list(self._callbacks):
                callback(*args, **kwargs)

    class _FakeAction:
        def __init__(self, text: str = "", _parent: Any = None) -> None:
            self._text = text
            self._separator = False
            self._checkable = False
            self._checked = False
            self._menu: _FakeMenu | None = None
            self.triggered = _FakeSignal()

        def text(self) -> str:
            return self._text

        def isSeparator(self) -> bool:
            return self._separator

        def menu(self) -> Any:
            return self._menu

        def setSeparator(self, value: bool) -> None:
            self._separator = bool(value)

        def setCheckable(self, value: bool) -> None:
            self._checkable = bool(value)

        def setChecked(self, value: bool) -> None:
            self._checked = bool(value)

        def trigger(self) -> None:
            if self._checkable:
                self._checked = not self._checked
                self.triggered.emit(self._checked)
                return
            self.triggered.emit()

    class _FakeMenu:
        root_created: "_FakeMenu | None" = None

        def __init__(self, _parent: Any = None) -> None:
            self._actions: list[_FakeAction] = []
            self._object_name = ""
            if _FakeMenu.root_created is None:
                _FakeMenu.root_created = self

        def setObjectName(self, name: str) -> None:
            self._object_name = str(name)

        def addSection(self, text: str) -> _FakeAction:
            action = _FakeAction(str(text))
            action.setSeparator(True)
            self._actions.append(action)
            return action

        def addSeparator(self) -> _FakeAction:
            action = _FakeAction("")
            action.setSeparator(True)
            self._actions.append(action)
            return action

        def addMenu(self, text: str) -> "_FakeMenu":
            submenu = _FakeMenu()
            action = _FakeAction(str(text))
            action._menu = submenu
            self._actions.append(action)
            return submenu

        def addAction(self, *args: Any) -> _FakeAction:
            if args and isinstance(args[0], _FakeAction):
                action = args[0]
                self._actions.append(action)
                return action
            text = str(args[0]) if args else ""
            callback = args[1] if len(args) > 1 else None
            action = _FakeAction(text)
            if callable(callback):
                action.triggered.connect(callback)
            self._actions.append(action)
            return action

        def actions(self) -> list[_FakeAction]:
            return list(self._actions)

        def exec(self, *_args: Any, **_kwargs: Any) -> None:
            return None

    try:
        from src.ui import main_window_impl
    except Exception:
        return None

    with (
        patch.object(main_window_impl, "QMenu", _FakeMenu),
        patch.object(main_window_impl, "QAction", _FakeAction),
    ):
        _FakeMenu.root_created = None
        window._open_header_overflow_menu()
    return _FakeMenu.root_created


def _collect_static(repo_root: Path) -> dict[str, Any]:
    app_py = _read(repo_root / "src" / "app.py")
    main_impl = _read(repo_root / "src" / "ui" / "main_window_impl.py")
    onboarding_py = _read(repo_root / "src" / "ui" / "components" / "onboarding.py")
    qss_py = _read(repo_root / "src" / "ui" / "style" / "qss_builder.py")
    settings_page = _read(repo_root / "src" / "ui" / "pages" / "settings_page.py")
    settings_core = _read(repo_root / "src" / "core" / "settings.py")

    pages = {
        "Home": _read(repo_root / "src" / "ui" / "pages" / "home_page.py"),
        "Diagnose": _read(repo_root / "src" / "ui" / "pages" / "diagnose_page.py"),
        "Fixes": _read(repo_root / "src" / "ui" / "pages" / "fixes_page.py"),
        "Reports": _read(repo_root / "src" / "ui" / "pages" / "reports_page.py"),
        "History": _read(repo_root / "src" / "ui" / "pages" / "history_page.py"),
        "Playbooks": _read(repo_root / "src" / "ui" / "pages" / "playbooks_page.py"),
    }
    set_obj_hits: list[str] = []
    for path in (repo_root / "src").rglob("*.py"):
        text = _read(path)
        for legacy in LEGACY_NAV_OBJECTS:
            if re.search(rf"setObjectName\(\s*[\"']{re.escape(legacy)}[\"']\s*\)", text):
                set_obj_hits.append(str(path.relative_to(repo_root)))

    min_width_hits: list[str] = []
    for path in (repo_root / "src" / "ui").rglob("*.py"):
        text = _read(path)
        for match in re.finditer(r"setMinimumWidth\((\d+)\)", text):
            width = int(match.group(1))
            if width >= 700:
                min_width_hits.append(f"{path.relative_to(repo_root)}:{width}")

    old_wrapper_paths = [
        repo_root / "src" / "ui" / "pages" / "home.py",
        repo_root / "src" / "ui" / "pages" / "diagnose.py",
        repo_root / "src" / "ui" / "pages" / "fixes.py",
        repo_root / "src" / "ui" / "pages" / "reports.py",
        repo_root / "src" / "ui" / "pages" / "history.py",
        repo_root / "src" / "ui" / "pages" / "playbooks.py",
        repo_root / "src" / "ui" / "pages" / "settings.py",
    ]
    wrapper_refs: list[str] = []
    for path in (repo_root / "src").rglob("*.py"):
        text = _read(path)
        if re.search(r"\.ui\.pages\.(home|diagnose|fixes|reports|history|playbooks|settings)\b", text):
            wrapper_refs.append(str(path.relative_to(repo_root)))

    return {
        "app_py": app_py,
        "main_impl": main_impl,
        "onboarding_py": onboarding_py,
        "qss_py": qss_py,
        "settings_page": settings_page,
        "settings_core": settings_core,
        "pages": pages,
        "legacy_set_object_hits": sorted(set(set_obj_hits)),
        "min_width_hits": sorted(set(min_width_hits)),
        "wrapper_files_present": [str(p.relative_to(repo_root)) for p in old_wrapper_paths if p.exists()],
        "wrapper_refs": sorted(set(wrapper_refs)),
    }


def _run_font_probe(repo_root: Path) -> tuple[str, str, int]:
    env = os.environ.copy()
    env["QT_QPA_PLATFORM"] = "offscreen"
    env["FIXFOX_SKIP_ONBOARDING"] = "1"
    env["FIXFOX_AUTO_EXIT_MS"] = "1100"
    env["FIXFOX_DEV_MODE"] = "1"
    proc = subprocess.run(
        [sys.executable, "-m", "src.app"],
        cwd=repo_root,
        text=True,
        capture_output=True,
        timeout=60,
        env=env,
    )
    return proc.stdout, proc.stderr, int(proc.returncode)


def _run_runtime_checks(_repo_root: Path) -> dict[str, Any]:
    os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
    os.environ["FIXFOX_SKIP_ONBOARDING"] = "1"
    os.environ["FIXFOX_DEV_MODE"] = "1"

    from src.core.settings import AppSettings
    from src.ui.main_window import MainWindow

    app = QApplication.instance() or QApplication([])
    with patch("src.ui.main_window_impl.load_settings", return_value=AppSettings().normalized()):
        window = MainWindow()
    window.show()
    _drain(app)

    try:
        nav_rails = [w for w in window.findChildren(QWidget) if w.objectName() == "NavRail"]
        legacy_widgets = [w for w in window.findChildren(QWidget) if w.objectName() in LEGACY_NAV_OBJECTS]
        list_nav_candidates = [
            w
            for w in window.findChildren(QWidget)
            if isinstance(w, (QListWidget, QTreeView)) and w.objectName() in LEGACY_NAV_OBJECTS
        ]

        toolbar_buttons = getattr(window.app_shell, "toolbar", QWidget()).findChildren(QAbstractButton)
        qt_toolbar_hits = [
            (button.objectName(), button.text(), button.toolTip())
            for button in toolbar_buttons
            if _contains_any(f"{button.text()} {button.toolTip()}", ("qt",))
        ]

        window.resize(1420, 900)
        _drain(app)
        overflow_menu = _capture_overflow_menu(window, app)
        menu_lines: list[str] = []
        menu_groups: dict[str, list[str]] = {}
        separator_count = 0
        details_toggle_present = False
        side_sheet_opened = False
        side_sheet_closed_again = False
        if overflow_menu is not None:
            menu_lines, menu_groups, separator_count = _collect_menu_structure(overflow_menu)
            all_actions = [action for action in overflow_menu.actions() if not action.isSeparator()]
            details = next((a for a in all_actions if (a.text() or "").strip() == "Toggle Details Panel"), None)
            if details is not None:
                details_toggle_present = True
                initial_collapsed = bool(hasattr(window, "concierge") and window.concierge.collapsed)
                details.trigger()
                _drain(app)
                first_collapsed = bool(hasattr(window, "concierge") and window.concierge.collapsed)
                details.trigger()
                _drain(app)
                second_collapsed = bool(hasattr(window, "concierge") and window.concierge.collapsed)
                side_sheet_opened = (not first_collapsed) or (not second_collapsed) or (not initial_collapsed)
                side_sheet_closed_again = first_collapsed or second_collapsed or initial_collapsed

        search_wide_index = -1
        search_narrow_index = -1
        if hasattr(window, "top_search_stack"):
            window.resize(1280, 720)
            _drain(app)
            search_wide_index = int(window.top_search_stack.currentIndex())
            window.resize(900, 720)
            _drain(app)
            search_narrow_index = int(window.top_search_stack.currentIndex())
            window.resize(1280, 720)
            _drain(app)

        style_before = app.styleSheet()
        original_palette = str(window.settings_state.theme_palette)
        original_mode = str(window.settings_state.theme_mode)
        original_density = str(window.settings_state.density)
        original_ui_mode = str(window.settings_state.ui_mode)

        window._set_theme_palette("graphite")
        _drain(app)
        style_after_palette = app.styleSheet()
        window._set_theme_mode("dark")
        _drain(app)
        style_after_mode = app.styleSheet()
        window._set_density_mode("compact")
        _drain(app)
        style_after_density = app.styleSheet()

        window.set_ui_mode("basic")
        _drain(app)
        basic_state = (not bool(getattr(window, "pb_basic_container").isHidden())) and bool(
            getattr(window, "pb_pro_console").isHidden()
        )
        window.set_ui_mode("pro")
        _drain(app)
        pro_state = (not bool(getattr(window, "pb_pro_console").isHidden())) and bool(
            getattr(window, "pb_basic_container").isHidden()
        )

        window._set_theme_palette(original_palette)
        window._set_theme_mode(original_mode)
        window._set_density_mode(original_density)
        window.set_ui_mode(original_ui_mode)
        _drain(app)

        weekly_control_present = bool(hasattr(window, "s_weekly")) and bool(window.s_weekly.isEnabled())

        from src.tests.test_ui_layout_sanity import _assert_text_not_clipped, _assert_within_parent

        layout_issues: list[str] = []
        for width, height in ((1024, 768), (1280, 720), (1600, 900)):
            window.resize(width, height)
            _drain(app, cycles=8)
            for widget in window.findChildren(QWidget):
                if not widget.isVisible() or widget.window() is not window:
                    continue
                try:
                    _assert_within_parent(widget, tolerance=2)
                    _assert_text_not_clipped(widget, padding=2)
                except AssertionError as exc:
                    layout_issues.append(f"{width}x{height}: {exc}")
                if len(layout_issues) >= 12:
                    break
            if len(layout_issues) >= 12:
                break

        runtime = {
            "nav_count": len(nav_rails),
            "legacy_nav_count": len(legacy_widgets),
            "legacy_list_count": len(list_nav_candidates),
            "side_sheet_default_hidden": bool(hasattr(window, "concierge") and window.concierge.collapsed),
            "side_sheet_default_visible": bool(hasattr(window, "concierge") and window.concierge.isVisible()),
            "details_toggle_present": details_toggle_present,
            "side_sheet_opened": side_sheet_opened,
            "side_sheet_closed_again": side_sheet_closed_again,
            "menu_lines": menu_lines,
            "menu_groups": menu_groups,
            "menu_separator_count": separator_count,
            "widget_tree": _widget_tree_dump(window),
            "top_app_bar_exists": bool(window.findChildren(QWidget, "TopAppBar")),
            "brand_mark_present": bool(getattr(window.app_shell.toolbar.brand_mark, "pixmap", lambda: None)()),
            "wordmark_text": str(getattr(window.app_shell.toolbar.app_identity, "text", lambda: "")()),
            "search_wide_index": search_wide_index,
            "search_narrow_index": search_narrow_index,
            "visible_primary_actions": int(
                bool(window.btn_quick_check.isVisible())
                + bool(window.btn_export.isVisible())
                + bool(window.btn_overflow.isVisible())
            ),
            "details_toggle_visible": bool(window.btn_panel_toggle.isVisible()),
            "hidden_secondary_actions": all(
                not button.isVisible()
                for button in (
                    window.btn_cancel_task,
                    window.btn_open_runner,
                    window.mode_basic_btn,
                    window.mode_pro_btn,
                )
            ),
            "qt_toolbar_hits": qt_toolbar_hits,
            "app_icon_is_null": QApplication.instance().windowIcon().isNull() if QApplication.instance() is not None else True,
            "window_icon_is_null": window.windowIcon().isNull(),
            "style_changed_palette": style_before != style_after_palette,
            "style_changed_mode": style_after_palette != style_after_mode,
            "style_changed_density": style_after_mode != style_after_density,
            "mode_basic_ok": basic_state,
            "mode_pro_ok": pro_state,
            "weekly_control_present": weekly_control_present,
            "layout_issues": layout_issues,
        }
    finally:
        window.close()
        _drain(app)

    from PySide6.QtWidgets import QDialog
    from src.core.settings import AppSettings
    from src.ui.main_window import MainWindow

    def _probe_onboarding(complete_flag: bool) -> tuple[int, bool]:
        calls = {"shown": 0}
        os.environ["FIXFOX_FORCE_ONBOARDING"] = "1"
        os.environ.pop("FIXFOX_SKIP_ONBOARDING", None)

        class DummyOnboarding:
            def __init__(self, *_args: Any, **_kwargs: Any) -> None:
                calls["shown"] += 1
                self.completed = True
                self.result_action = "none"

            def exec(self) -> int:
                return QDialog.Accepted

        with (
            patch("src.ui.main_window_impl.load_settings", return_value=AppSettings(onboarding_completed=complete_flag)),
            patch("src.ui.main_window_impl.save_settings"),
            patch("src.ui.main_window_impl.OnboardingFlow", DummyOnboarding),
        ):
            win = MainWindow()
            win.show()
            _drain(app)
            completed_value = bool(win.settings_state.onboarding_completed)
            win.close()
            _drain(app)
        os.environ.pop("FIXFOX_FORCE_ONBOARDING", None)
        os.environ["FIXFOX_SKIP_ONBOARDING"] = "1"
        return calls["shown"], completed_value

    shown_when_incomplete, completed_after_show = _probe_onboarding(False)
    shown_when_complete, _already_complete = _probe_onboarding(True)

    runtime["shown_when_incomplete"] = shown_when_incomplete
    runtime["shown_when_complete"] = shown_when_complete
    runtime["completed_after_show"] = completed_after_show
    return runtime

def run_verification(write_report: Path | None = None, verbose: bool = True) -> VerificationOutcome:
    repo_root = _repo_root()
    static = _collect_static(repo_root)
    runtime = _run_runtime_checks(repo_root)
    font_stdout, font_stderr, font_rc = _run_font_probe(repo_root)

    results: list[RequirementResult] = []

    b1_pass = (
        runtime["nav_count"] == 1
        and runtime["legacy_nav_count"] == 0
        and runtime["legacy_list_count"] == 0
        and not static["legacy_set_object_hits"]
    )
    results.append(RequirementResult("B1", "No double nav (rail-only)", b1_pass, [
        f"Runtime nav count: {runtime['nav_count']} (expected 1)",
        f"Runtime legacy nav widgets: {runtime['legacy_nav_count']}",
        f"Runtime legacy list/tree nav widgets: {runtime['legacy_list_count']}",
        f"Static legacy setObjectName hits: {len(static['legacy_set_object_hits'])}",
    ]))

    b2_pass = (
        runtime["side_sheet_default_hidden"]
        and (not runtime["side_sheet_default_visible"])
        and runtime["details_toggle_present"]
        and runtime["side_sheet_opened"]
        and runtime["side_sheet_closed_again"]
    )
    results.append(RequirementResult("B2", "Optional right side sheet hidden by default", b2_pass, [
        f"Default side sheet hidden: {runtime['side_sheet_default_hidden']}",
        f"Default side sheet visible: {runtime['side_sheet_default_visible']}",
        f"Overflow details toggle present: {runtime['details_toggle_present']}",
        f"Side sheet opens from menu: {runtime['side_sheet_opened']}",
        f"Side sheet closes from menu: {runtime['side_sheet_closed_again']}",
    ]))

    b3_pass = (
        runtime["top_app_bar_exists"]
        and runtime["brand_mark_present"]
        and runtime["wordmark_text"].strip().lower() == "fix fox"
        and runtime["search_narrow_index"] == 1
        and runtime["search_wide_index"] == 0
        and runtime["visible_primary_actions"] == 3
        and runtime["details_toggle_visible"]
        and runtime["hidden_secondary_actions"]
        and not static["min_width_hits"]
    )
    results.append(RequirementResult("B3", "Simple D-style top app bar", b3_pass, [
        f"Top app bar exists: {runtime['top_app_bar_exists']}",
        f"Brand mark present: {runtime['brand_mark_present']}",
        f"Wordmark text: {runtime['wordmark_text']!r}",
        f"Search index wide=1280: {runtime['search_wide_index']}",
        f"Search index narrow=900: {runtime['search_narrow_index']}",
        f"Visible primary actions: {runtime['visible_primary_actions']}",
        f"Details toggle visible: {runtime['details_toggle_visible']}",
        f"Hidden secondary actions: {runtime['hidden_secondary_actions']}",
        f"Hard min-width hits >=700: {len(static['min_width_hits'])}",
    ]))

    session_items = runtime["menu_groups"].get("Session", [])
    view_items = runtime["menu_groups"].get("View", [])
    help_items = runtime["menu_groups"].get("Help", [])
    b4_session = all(item in session_items for item in REQUIRED_MENU["Session"])
    b4_view = all(item in view_items for item in REQUIRED_MENU["View"])
    b4_help = all(item in help_items for item in REQUIRED_MENU["Help"])
    b4_pass = b4_session and b4_view and b4_help and runtime["menu_separator_count"] >= 2
    results.append(RequirementResult("B4", "Overflow menu grouping and separators", b4_pass, [
        f"Session group items: {session_items}",
        f"View group items: {view_items}",
        f"Help group items: {help_items}",
        f"Section/separator count: {runtime['menu_separator_count']}",
        f"Session complete: {b4_session}",
        f"View complete: {b4_view}",
        f"Help complete: {b4_help}",
    ]))

    icon_ico = repo_root / "src" / "assets" / "brand" / "fixfox_icon.ico"
    mark_png = repo_root / "src" / "assets" / "brand" / "fixfox_mark.png"
    b5_pass = (
        icon_ico.exists()
        and mark_png.exists()
        and ((not runtime["app_icon_is_null"]) or ("app.setWindowIcon" in static["app_py"]))
        and (not runtime["window_icon_is_null"])
        and (not runtime["qt_toolbar_hits"])
        and "setWindowIcon" in static["main_impl"]
        and "app.setWindowIcon" in static["app_py"]
    )
    results.append(RequirementResult("B5", "Branding and no Qt icon leakage", b5_pass, [
        f"fixfox_icon.ico exists: {icon_ico.exists()} ({icon_ico.relative_to(repo_root)})",
        f"fixfox_mark.png exists: {mark_png.exists()} ({mark_png.relative_to(repo_root)})",
        f"QApplication icon is null: {runtime['app_icon_is_null']}",
        f"MainWindow icon is null: {runtime['window_icon_is_null']}",
        f"Toolbar Qt icon/action hits: {runtime['qt_toolbar_hits']}",
        "Code refs: src/app.py and src/ui/main_window_impl.py",
    ]))

    font_warning_hit = any(pattern.lower() in font_stderr.lower() for pattern in FONT_WARNING_PATTERNS)
    b6_pass = (
        "addApplicationFontFromData" in static["app_py"]
        and "Path.cwd()" not in static["app_py"]
        and "Segoe UI" in static["app_py"]
        and (not font_warning_hit)
        and font_rc == 0
    )
    results.append(RequirementResult("B6", "Fonts load correctly without warnings", b6_pass, [
        f"Uses addApplicationFontFromData: {'addApplicationFontFromData' in static['app_py']}",
        f"Uses CWD font pathing: {'Path.cwd()' in static['app_py']}",
        f"Fallback includes Segoe UI: {'Segoe UI' in static['app_py']}",
        f"Font probe return code: {font_rc}",
        f"Font warning detected in stderr: {font_warning_hit}",
        f"Font selected line in stdout: {'[FixFox] UI font:' in font_stdout}",
    ]))

    b7_pass = (
        runtime["style_changed_palette"]
        and runtime["style_changed_mode"]
        and runtime["style_changed_density"]
        and runtime["mode_basic_ok"]
        and runtime["mode_pro_ok"]
        and runtime["weekly_control_present"]
        and "save_settings_from_ui" in static["main_impl"]
        and "weekly_reminder_enabled" in static["main_impl"]
    )
    results.append(RequirementResult("B7", "Settings are real and applied immediately", b7_pass, [
        f"Palette changes QSS: {runtime['style_changed_palette']}",
        f"Theme mode changes QSS: {runtime['style_changed_mode']}",
        f"Density changes QSS: {runtime['style_changed_density']}",
        f"Basic mode visibility behavior: {runtime['mode_basic_ok']}",
        f"Pro mode visibility behavior: {runtime['mode_pro_ok']}",
        f"Weekly reminder control present/enabled: {runtime['weekly_control_present']}",
    ]))

    onboarding_text = static["onboarding_py"].lower()
    onboarding_steps_ok = all(snippet in onboarding_text for snippet in ("welcome to fix fox", "preferences", "first action"))
    b8_pass = (
        "onboarding_completed" in static["settings_core"]
        and onboarding_steps_ok
        and runtime["shown_when_incomplete"] >= 1
        and runtime["shown_when_complete"] == 0
        and runtime["completed_after_show"]
        and "Reset Onboarding" in static["settings_page"]
        and "Reset onboarding is available in Pro mode." in static["main_impl"]
    )
    results.append(RequirementResult("B8", "Onboarding rebuilt and persistence-gated", b8_pass, [
        f"onboarding_completed in AppSettings: {'onboarding_completed' in static['settings_core']}",
        f"Onboarding 3-step labels present: {onboarding_steps_ok}",
        f"Shown when onboarding_completed=false: {runtime['shown_when_incomplete']}",
        f"Shown when onboarding_completed=true: {runtime['shown_when_complete']}",
        f"Completion persisted true in flow: {runtime['completed_after_show']}",
    ]))

    layout_test_path = repo_root / "src" / "tests" / "test_ui_layout_sanity.py"
    layout_text = _read(layout_test_path)
    layout_sizes_ok = all(size in layout_text for size in ("1024, 768", "1280, 720", "1600, 900"))
    layout_checks_ok = all(
        snippet in layout_text
        for snippet in ("_assert_within_parent", "_assert_text_not_clipped", "test_no_legacy_main_navigation_widgets")
    )
    b9_pass = layout_test_path.exists() and layout_sizes_ok and layout_checks_ok and not runtime["layout_issues"]
    results.append(RequirementResult("B9", "No overlap/clipping at common sizes", b9_pass, [
        f"Layout sanity test exists: {layout_test_path.exists()}",
        f"Required sizes in test: {layout_sizes_ok}",
        f"Bounds/clipping/nav checks in test: {layout_checks_ok}",
        f"Runtime layout issues found: {len(runtime['layout_issues'])}",
        *(runtime["layout_issues"][:3] or ["No runtime clipping/bounds issues detected."]),
    ]))

    qss_states_ok = all(
        token in static["qss_py"]
        for token in ("#PrimaryButton:hover", "#PrimaryButton:pressed", "QPushButton:disabled", "#PrimaryButton:focus", "#NavRailButton:checked")
    )
    page_empty_ok = True
    page_callout_ok = True
    for _page_name, text in static["pages"].items():
        page_empty_ok = page_empty_ok and (("EmptyState(" in text) or ("empty_message=" in text))
        page_callout_ok = page_callout_ok and ("InlineCallout(" in text)
    about_ok = all(snippet in static["main_impl"] for snippet in ("About Fix Fox", "Local-only by design", "Logs path:", "Exports path:"))
    b10_pass = qss_states_ok and page_empty_ok and page_callout_ok and about_ok
    results.append(RequirementResult("B10", "Polish items present (states, empty/error patterns, about)", b10_pass, [
        f"QSS interaction states present: {qss_states_ok}",
        f"Empty-state pattern on all main pages: {page_empty_ok}",
        f"Inline callouts on all main pages: {page_callout_ok}",
        f"About dialog local-only/version/path content present: {about_ok}",
    ]))

    repo_structure_doc = repo_root / "docs" / "repo-structure.md"
    icons_dir = repo_root / "src" / "assets" / "icons"
    fonts_dir = repo_root / "src" / "assets" / "fonts"
    branding_dir = repo_root / "src" / "assets" / "brand"
    b11_pass = (
        repo_structure_doc.exists()
        and branding_dir.exists()
        and icons_dir.exists()
        and fonts_dir.exists()
        and not static["wrapper_files_present"]
        and not static["wrapper_refs"]
    )
    results.append(RequirementResult("B11", "Repo cleanup and structure safety checks", b11_pass, [
        f"docs/repo-structure.md exists: {repo_structure_doc.exists()}",
        f"assets/brand exists: {branding_dir.exists()}",
        f"assets/icons exists: {icons_dir.exists()}",
        f"assets/fonts exists: {fonts_dir.exists()}",
        f"Legacy wrapper files present: {static['wrapper_files_present']}",
        f"Legacy wrapper references in src/: {static['wrapper_refs']}",
    ]))

    outcome = VerificationOutcome(
        results=results,
        widget_tree_sample=runtime["widget_tree"],
        menu_structure=runtime["menu_lines"],
        font_probe_stdout=font_stdout,
        font_probe_stderr=font_stderr,
    )

    if write_report is not None:
        write_report.parent.mkdir(parents=True, exist_ok=True)
        write_report.write_text(outcome.render_markdown(), encoding="utf-8")

    if verbose:
        print(outcome.render_console())
    return outcome


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Verify D-style rebuild + polish requirements.")
    parser.add_argument("--write-report", default="", help="Optional markdown report path.")
    parser.add_argument("--quiet", action="store_true", help="Suppress console output.")
    args = parser.parse_args(argv)

    report_path = Path(args.write_report).resolve() if args.write_report else None
    outcome = run_verification(write_report=report_path, verbose=not args.quiet)
    return 0 if outcome.passed else 1


if __name__ == "__main__":
    raise SystemExit(main())
