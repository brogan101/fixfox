from __future__ import annotations

import json
import os
import sys
import time
from datetime import datetime
from pathlib import Path
from typing import Any


def _repo_root() -> Path:
    return Path(__file__).resolve().parent.parent


def _ensure_repo_on_path() -> None:
    root = str(_repo_root())
    if root not in sys.path:
        sys.path.insert(0, root)


def _pump(app, seconds: float) -> None:
    deadline = time.time() + seconds
    while time.time() < deadline:
        app.processEvents()
        time.sleep(0.02)


def _visible_widget_clipping_issues(window) -> list[str]:
    from PySide6.QtGui import QFontMetrics
    from PySide6.QtWidgets import QAbstractButton, QAbstractScrollArea, QLabel, QWidget

    ignore_text_names = {
        "BadgeINFO",
        "BadgeWARN",
        "BadgeERROR",
        "BadgeSUCCESS",
        "BadgeRiskGuided",
        "BadgeRiskSafe",
        "BadgeRiskAdmin",
        "Pill",
        "RowTitle",
        "RowSubtitle",
        "BottomStatusText",
        "RunnerStatusChip",
    }

    def _has_scroll_ancestor(widget: QWidget) -> bool:
        parent = widget.parentWidget()
        while parent is not None:
            if isinstance(parent, QAbstractScrollArea):
                return True
            parent = parent.parentWidget()
        return False

    issues: list[str] = []
    for widget in window.findChildren(QWidget):
        if not widget.isVisible():
            continue
        name = widget.objectName() or widget.metaObject().className()
        parent = widget.parentWidget()
        if parent is not None and not _has_scroll_ancestor(widget) and name not in {"QWidget", "RowBase"}:
            if widget.geometry().right() > parent.contentsRect().right() + 4 or widget.geometry().bottom() > parent.contentsRect().bottom() + 4:
                issues.append(f"layout_overflow:{name}:{widget.geometry().width()}x{widget.geometry().height()}")
        if isinstance(widget, QLabel):
            text = " ".join((widget.text() or "").split()).strip()
            if text and not widget.wordWrap() and name not in ignore_text_names:
                metrics = QFontMetrics(widget.font())
                text_width = metrics.horizontalAdvance(text)
                if text_width > max(0, widget.contentsRect().width() - 6) and widget.sizeHint().width() - widget.width() > 120:
                    issues.append(f"text_clip:{name}:{text[:80]}")
            pix = widget.pixmap()
            if pix is not None and not pix.isNull() and (
                pix.width() > widget.contentsRect().width() + 2 or pix.height() > widget.contentsRect().height() + 2
            ):
                issues.append(f"icon_clip:{name}:{pix.width()}x{pix.height()}>{widget.width()}x{widget.height()}")
        elif isinstance(widget, QAbstractButton):
            text = " ".join((widget.text() or "").split()).strip()
            if text:
                metrics = QFontMetrics(widget.font())
                text_width = metrics.horizontalAdvance(text)
                if text_width > max(0, widget.contentsRect().width() - 10) and widget.sizeHint().width() - widget.width() > 120:
                    issues.append(f"button_clip:{name}:{text[:80]}")
            icon_size = widget.iconSize()
            if not icon_size.isEmpty():
                if icon_size.width() > widget.contentsRect().width() + 2 or icon_size.height() > widget.contentsRect().height() + 2:
                    issues.append(
                        f"button_icon_clip:{name}:{icon_size.width()}x{icon_size.height()}>{widget.width()}x{widget.height()}"
                    )
    return sorted(set(issues))


def main() -> int:
    os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
    _ensure_repo_on_path()

    from PySide6.QtWidgets import QApplication

    from src.core.diagnostics.qt_warnings import install_qt_message_handler, read_qt_warnings
    from src.ui.main_window import MainWindow
    from src.ui.runtime_bootstrap import (
        apply_runtime_ui_bootstrap,
        validate_runtime_persistence,
    )

    app = QApplication.instance() or QApplication([])
    docs_dir = _repo_root() / "docs"
    shots_dir = docs_dir / "screenshots" / datetime.now().strftime("%Y%m%d_%H%M%S")
    shots_dir.mkdir(parents=True, exist_ok=True)

    qt_warnings_path = shots_dir / "qt_warnings.txt"
    qt_warnings_path.write_text("", encoding="utf-8")
    cleanup_qt_handler = install_qt_message_handler(str(qt_warnings_path))

    try:
        bootstrap = apply_runtime_ui_bootstrap(app)
        window = MainWindow()
        window.resize(1600, 980)
        window.showMaximized()
        _pump(app, 1.0)

        manifest: dict[str, Any] = {
            "generated_at": datetime.now().isoformat(),
            "font_family": bootstrap.font_family,
            "stylesheet_length": bootstrap.stylesheet_length,
            "screenshots": [],
            "artifacts": {
                "manifest": "",
                "qt_warnings": str(qt_warnings_path),
                "clipping_report": str(shots_dir / "clipping_report.txt"),
                "visible_text_sanity": str(shots_dir / "visible_text_sanity_report.txt"),
            },
            "pages": {},
        }

        clipping_lines: list[str] = []
        visible_lines: list[str] = []

        for index, page_name in enumerate(window.NAV_ITEMS, start=1):
            window.nav.setCurrentRow(index - 1)
            _pump(app, 0.35)
            persistence = validate_runtime_persistence(window, app, page_label=page_name)
            page_clipping = _visible_widget_clipping_issues(window)

            path = shots_dir / f"maximized_{index}_{page_name.lower()}.png"
            window.grab().save(str(path))
            manifest["screenshots"].append(str(path))
            manifest["pages"][page_name] = {
                "screenshot": str(path),
                "persistence_ok": persistence.ok,
                "visible_text_count": persistence.snapshots[0].visible_text_count if persistence.snapshots else 0,
                "blank_container_count": persistence.snapshots[0].blank_container_count if persistence.snapshots else 0,
                "failures": list(persistence.failures),
                "clipping_issues": page_clipping,
            }
            visible_lines.extend(persistence.to_lines())
            visible_lines.append("")
            clipping_lines.append(f"Page: {page_name}")
            if page_clipping:
                clipping_lines.extend([f"- {row}" for row in page_clipping])
            else:
                clipping_lines.append("- none")
            clipping_lines.append("")

        if hasattr(window, "top_search"):
            window.top_search.setText("windows update stuck")
            payload = window._compute_global_search_payload(
                query="windows update stuck",
                visible=window._visible_capability_ids(),
                basic_mode=window.layout_policy_state.mode == "basic",
            )
            window._search_request_id += 1
            window._apply_global_search_payload(window._search_request_id, payload)
            _pump(app, 0.4)
            search_path = shots_dir / "search_windows_update.png"
            window.grab().save(str(search_path))
            manifest["search_query"] = "windows update stuck"
            manifest["search_screenshot"] = str(search_path)
            window._search_popup.hide_popup()
            window.top_search.clear()

        if hasattr(window, "pb_issue_family"):
            window.nav.setCurrentRow(window.NAV_ITEMS.index("Playbooks"))
            _pump(app, 0.3)
            window.pb_issue_scope.setCurrentIndex(max(window.pb_issue_scope.findData("deep"), 0))
            _pump(app, 0.2)
            detail_path = shots_dir / "playbooks_deep_scope.png"
            window.grab().save(str(detail_path))
            manifest["deep_scope_screenshot"] = str(detail_path)
            window._select_support_issue("network_no_internet_access", open_target="playbooks")
            _pump(app, 0.3)
            playbook_detail_path = shots_dir / "playbook_detail_network.png"
            window.grab().save(str(playbook_detail_path))
            manifest["playbook_detail_screenshot"] = str(playbook_detail_path)

        if hasattr(window, "support_fix_list"):
            window._select_support_issue("network_no_internet_access", open_target="fixes")
            _pump(app, 0.4)
            fix_flow_path = shots_dir / "fix_flow_network.png"
            window.grab().save(str(fix_flow_path))
            manifest["fix_flow_screenshot"] = str(fix_flow_path)

        if hasattr(window, "settings_nav"):
            window.nav.setCurrentRow(window.NAV_ITEMS.index("Settings"))
            _pump(app, 0.2)
            settings_support_path = shots_dir / "settings_support_area.png"
            window.grab().save(str(settings_support_path))
            manifest["settings_support_screenshot"] = str(settings_support_path)

        clipping_path = shots_dir / "clipping_report.txt"
        visible_path = shots_dir / "visible_text_sanity_report.txt"
        clipping_path.write_text("\n".join(clipping_lines).strip() + "\n", encoding="utf-8")
        visible_path.write_text("\n".join(visible_lines).strip() + "\n", encoding="utf-8")

        qt_warnings = read_qt_warnings(qt_warnings_path)
        manifest["qt_warning_count"] = len(qt_warnings)
        manifest["clipping_issue_count"] = sum(len(page.get("clipping_issues", [])) for page in manifest["pages"].values())
        manifest["visible_text_failures"] = [
            {"page": name, "failures": page.get("failures", [])}
            for name, page in manifest["pages"].items()
            if page.get("failures")
        ]

        manifest_path = shots_dir / "MANIFEST.json"
        manifest["artifacts"]["manifest"] = str(manifest_path)
        manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        print(f"ui_walkthrough={shots_dir}")
        print(f"manifest={manifest_path}")
        print(f"qt_warnings={qt_warnings_path}")
        print(f"clipping_report={clipping_path}")
        print(f"visible_text_sanity_report={visible_path}")
        window.close()
        _pump(app, 0.1)
        return 0
    finally:
        cleanup_qt_handler()


if __name__ == "__main__":
    raise SystemExit(main())
