from __future__ import annotations

import json
import os
import sys
import time
from datetime import datetime
from pathlib import Path


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


def main() -> int:
    os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
    _ensure_repo_on_path()

    from PySide6.QtWidgets import QApplication

    from src.ui.main_window import MainWindow
    from src.ui.runtime_bootstrap import apply_runtime_ui_bootstrap

    app = QApplication.instance() or QApplication([])
    bootstrap = apply_runtime_ui_bootstrap(app)

    docs_dir = _repo_root() / "docs"
    shots_dir = docs_dir / "screenshots" / datetime.now().strftime("%Y%m%d_%H%M%S")
    shots_dir.mkdir(parents=True, exist_ok=True)

    window = MainWindow()
    window.resize(1600, 980)
    window.showMaximized()
    _pump(app, 1.0)

    manifest: dict[str, object] = {
        "generated_at": datetime.now().isoformat(),
        "font_family": bootstrap.font_family,
        "stylesheet_length": bootstrap.stylesheet_length,
        "screenshots": [],
    }

    for index, page_name in enumerate(window.NAV_ITEMS, start=1):
        window.nav.setCurrentRow(index - 1)
        _pump(app, 0.6)
        path = shots_dir / f"maximized_{index}_{page_name.lower()}.png"
        window.grab().save(str(path))
        manifest["screenshots"].append(str(path))

    if hasattr(window, "top_search"):
        window.top_search.setText("windows update")
        payload = window._compute_global_search_payload(
            query="windows update",
            visible=window._visible_capability_ids(),
            basic_mode=window.layout_policy_state.mode == "basic",
        )
        window._search_request_id += 1
        window._apply_global_search_payload(window._search_request_id, payload)
        _pump(app, 0.3)
        search_path = shots_dir / "search_windows_update.png"
        window.grab().save(str(search_path))
        manifest["search_query"] = "windows update"
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

    manifest_path = shots_dir / "MANIFEST.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"ui_walkthrough={shots_dir}")
    print(f"manifest={manifest_path}")
    window.close()
    _pump(app, 0.1)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
