from __future__ import annotations

import json
import os
import time
import zipfile
from pathlib import Path

from src.core.command_runner import run_command
from src.core import diagnostics, exporter, registry, runbooks
from src.core.db import (
    count_artifacts_for_session,
    count_findings_for_session,
    db_stats,
    get_run,
    get_session,
    initialize_db,
    rebuild_from_sessions_folder,
)
from src.core.evidence_collector import (
    collect_crash_bundle,
    collect_event_logs,
    collect_network_bundle,
    collect_system_snapshot,
    collect_update_bundle,
)
from src.core.exporter import validate_export_folder
from src.core.masking import MaskingOptions
from src.core.run_events import RunEventType, get_run_event_bus
from src.core.script_tasks import run_script_task
from src.core.sessions import save_session


def _assert(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def _read_zip_listing(path: Path) -> set[str]:
    with zipfile.ZipFile(path, "r") as zf:
        return set(zf.namelist())


def _merge_evidence(session: dict, files: list[str], category: str, task_id: str) -> None:
    evidence = session.setdefault("evidence", {})
    rows = evidence.setdefault("files", [])
    for file_path in files:
        rows.append({"path": file_path, "category": category, "task_id": task_id})


def _ui_module_smoke() -> None:
    os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
    os.environ["FIXFOX_SKIP_ONBOARDING"] = "1"
    from PySide6.QtCore import Qt
    from PySide6.QtTest import QTest
    from PySide6.QtWidgets import QApplication
    from src.ui.main_window import MainWindow
    from src.ui.components.rows import ToolRow

    app = QApplication.instance() or QApplication([])
    window = MainWindow()
    bus = get_run_event_bus()

    def wait_for_worker(timeout_s: int = 120) -> None:
        deadline = time.monotonic() + timeout_s
        while window.active_worker is not None and time.monotonic() < deadline:
            app.processEvents()
            time.sleep(0.05)
        _assert(window.active_worker is None, "UI worker did not finish in time")

    def assert_run_events(run_id: str, context: str) -> None:
        _assert(bool(run_id), f"{context}: ToolRunner run_id missing")
        events, _cursor = bus.events_since(run_id, 0)
        event_types = {event.event_type for event in events}
        _assert(RunEventType.START in event_types, f"{context}: missing START event")
        _assert(RunEventType.STATUS in event_types, f"{context}: missing STATUS event")
        _assert(RunEventType.END in event_types, f"{context}: missing END event")
        _assert(
            any(kind in event_types for kind in {RunEventType.PROGRESS, RunEventType.STDOUT, RunEventType.STDERR, RunEventType.WARNING, RunEventType.ERROR}),
            f"{context}: missing live run events",
        )
        run_row = get_run(run_id)
        _assert(run_row is not None, f"{context}: run row missing in sqlite index")
        _assert(str(run_row.get("status", "")).strip() != "", f"{context}: run status missing in sqlite index")

    def find_tool_row(tool_id: str) -> ToolRow:
        for feed_name in ("tb_top", "tb_all", "tb_favorites"):
            feed = getattr(window, feed_name, None)
            if feed is None:
                continue
            lw = getattr(feed, "list_widget", None)
            if lw is None:
                continue
            for i in range(lw.count()):
                item = lw.item(i)
                if str(item.data(Qt.UserRole) or "") != tool_id:
                    continue
                row = lw.itemWidget(item)
                if isinstance(row, ToolRow):
                    return row
        raise AssertionError(f"tool row not found in feeds: {tool_id}")

    try:
        for idx, page in enumerate(window.NAV_ITEMS):
            window.nav.setCurrentRow(idx)
            app.processEvents()
            _assert(window.pages.currentIndex() == idx, f"page switch failed for {page}")

        # Basic mode layout policy
        window.set_ui_mode("basic")
        app.processEvents()
        _assert(window.settings_state.ui_mode == "basic", "failed to switch to basic mode")
        playbooks_idx = window.NAV_ITEMS.index("Playbooks")
        playbooks_item = window.nav.item(playbooks_idx)
        _assert(playbooks_item is not None and not playbooks_item.isHidden(), "Playbooks should remain visible in basic mode")
        window.nav.setCurrentRow(playbooks_idx)
        app.processEvents()
        _assert(hasattr(window, "pb_basic_container") and not window.pb_basic_container.isHidden(), "Basic guided Playbooks layout should be visible")
        _assert(hasattr(window, "pb_pro_console") and window.pb_pro_console.isHidden(), "Pro Playbooks console should be hidden in basic mode")
        _assert(window.concierge.collapsed, "Concierge panel should default to collapsed in basic mode")
        _assert("script_task.task_wifi_report_fix_wizard" not in window._visible_capability_ids(), "pro script task leaked in basic mode")

        # Pro mode enables advanced capability directories immediately
        window.set_ui_mode("pro")
        app.processEvents()
        playbooks_item = window.nav.item(playbooks_idx)
        _assert(playbooks_item is not None and not playbooks_item.isHidden(), "Playbooks should be visible in pro mode")
        window.nav.setCurrentRow(playbooks_idx)
        app.processEvents()
        _assert(hasattr(window, "pb_basic_container") and window.pb_basic_container.isHidden(), "Basic guided layout should be hidden in pro mode")
        _assert(hasattr(window, "pb_pro_console") and not window.pb_pro_console.isHidden(), "Pro Playbooks console should be visible")
        _assert("script_task.task_wifi_report_fix_wizard" in window._visible_capability_ids(), "pro script task missing in pro mode")

        # Single-click must not launch any action.
        tool_row = find_tool_row("tool_storage")
        QTest.mouseClick(tool_row, Qt.LeftButton, Qt.NoModifier, tool_row.rect().center())
        app.processEvents()
        time.sleep(0.05)
        app.processEvents()
        _assert(window.active_worker is None, "single-click on tool row unexpectedly started a worker")
        _assert(window.tool_runner is None, "single-click on tool row unexpectedly opened ToolRunner")

        # Explicit action must launch and open ToolRunner.
        tool_row.open_btn.click()
        wait_for_worker()
        _assert(window.tool_runner is not None, "ToolRunner window missing after safe tool run")
        window.tool_runner.hide()
        app.processEvents()
        window.run_status_panel.clicked.emit()
        app.processEvents()
        _assert(window.tool_runner.isVisible(), "run status card click did not open/focus ToolRunner")
        assert_run_events(window.tool_runner.run_id, "safe_tool")
        _assert(bool(window.run_status_detail.text().strip()), "run status detail did not update")
        sid = str(window.current_session.get("session_id", "")) if isinstance(window.current_session, dict) else ""
        _assert(bool(sid), "safe_tool: active session id missing")
        _assert(get_session(sid) is not None, "safe_tool: session row missing in sqlite index")

        window._run_script_task("task_wifi_report_fix_wizard", dry_run=True)
        wait_for_worker()
        _assert(window.tool_runner is not None, "ToolRunner window was not created")
        assert_run_events(window.tool_runner.run_id, "script_task")

        window._select_runbook("home_fix_wifi_safe")
        window.run_selected_runbook(True)
        wait_for_worker()
        _assert(window.tool_runner is not None, "ToolRunner window missing after runbook dry-run")
        assert_run_events(window.tool_runner.run_id, "runbook_dry_run")
    finally:
        if window.tool_runner is not None:
            window.tool_runner.close()
        window.close()
        app.processEvents()


def main() -> int:
    db_file = initialize_db()
    _assert(db_file.exists(), "sqlite db file was not initialized")
    _assert(len(registry.CAPABILITIES) >= 20, "capability registry too small")
    _ui_module_smoke()
    bus = get_run_event_bus()
    cmd_run_id = bus.create_run(name="smoke_command_runner", risk="Safe", session_id="S_SMOKE")
    cmd = ["cmd", "/c", "echo eventbus_smoke"] if os.name == "nt" else ["sh", "-lc", "echo eventbus_smoke"]
    cmd_result = run_command(cmd, timeout_s=15, event_bus=bus, run_id=cmd_run_id)
    _assert(cmd_result.code == 0, "command_runner smoke command failed")
    cmd_events, _cmd_cursor = bus.events_since(cmd_run_id, 0)
    cmd_types = {event.event_type for event in cmd_events}
    _assert(RunEventType.STDOUT in cmd_types, "command_runner did not publish STDOUT event")
    _assert(RunEventType.END in cmd_types, "command_runner did not publish END event")

    session = diagnostics.quick_check()
    session["session_id"] = "S_SMOKE"
    session["symptom"] = r"Smoke path C:\Users\John\Desktop on DESKTOP-SMOKE"
    session["created_local"] = "2026-03-03 00:00:00"
    session["actions"] = []
    session["network"] = {"ssid": "HomeWifi"}
    session["evidence"] = {"files": []}
    save_session(session)
    _assert(get_session("S_SMOKE") is not None, "session row not indexed in sqlite")
    _assert(count_findings_for_session("S_SMOKE") > 0, "session findings were not indexed in sqlite")
    mask = MaskingOptions(enabled=True, mask_ip=True, extra_tokens=("DESKTOP-SMOKE", "HomeWifi", "John"))

    # Required runbook dry-run path
    rb = runbooks.execute_runbook("home_fix_wifi_safe", dry_run=True)
    _assert(rb["dry_run"] is True, "home fix wifi runbook dry-run failed")
    _assert(len(rb["steps"]) > 0, "home runbook produced no steps")
    wifi_dry = run_script_task("task_wifi_report_fix_wizard", dry_run=True, output_dir=Path.cwd() / ".smoke_tmp" / "wifi_dry", mask_options=mask)
    _assert(wifi_dry["dry_run"] is True, "Fix Wi-Fi dry-run tool path failed")

    # Required HOME tool execution (H03 Storage Radar)
    tool_root = Path.cwd() / ".smoke_tmp" / "fixfox_smoke_tools"
    storage_result = run_script_task("task_storage_radar", dry_run=False, output_dir=tool_root / "storage", mask_options=mask)
    _assert(int(storage_result.get("code", 1)) in (0, 1), "storage radar tool did not return expected code")
    storage_files = [Path(str(p)) for p in storage_result.get("output_files", [])]
    _assert(len(storage_files) >= 2, "storage radar did not create enough artifacts")
    _assert(any(p.name.lower() == "storage_radar.csv" for p in storage_files), "storage_radar.csv missing")
    _assert(any("storage_radar_summary" in p.name.lower() for p in storage_files), "storage radar summary missing")
    _assert(any(p.name.lower() == "downloads_plan.csv" for p in storage_files), "downloads_plan.csv missing")
    _merge_evidence(session, [str(p) for p in storage_result.get("output_files", [])], "storage", "task_storage_radar")

    sys_result = collect_system_snapshot(session["session_id"], mask_options=mask, timeout_s=240)
    net_result = collect_network_bundle(session["session_id"], mask_options=mask, timeout_s=240)
    upd_result = collect_update_bundle(session["session_id"], mask_options=mask, timeout_s=260)
    crash_result = collect_crash_bundle(session["session_id"], mask_options=mask, timeout_s=260)
    logs_result = collect_event_logs(session["session_id"], mask_options=mask, timeout_s=300)
    _assert(len(sys_result.files_created) > 0, "system evidence collector created no files")
    _assert(len(net_result.files_created) > 0, "network evidence collector created no files")
    _assert(len(upd_result.files_created) > 0, "updates evidence collector created no files")
    _assert(len(crash_result.files_created) > 0, "crash evidence collector created no files")
    _assert(len(logs_result.files_created) > 0, "eventlogs evidence collector created no files")
    _merge_evidence(session, sys_result.files_created, "system", "collector_system")
    _merge_evidence(session, net_result.files_created, "network", "collector_network")
    _merge_evidence(session, upd_result.files_created, "updates", "collector_updates")
    _merge_evidence(session, crash_result.files_created, "crash", "collector_crash")
    _merge_evidence(session, logs_result.files_created, "eventlogs", "collector_eventlogs")

    home_pack = exporter.export_session(
        session,
        preset="home_share",
        share_safe=True,
        mask_ip=True,
        include_logs=False,
        allow_validator_override=False,
    )
    _assert(home_pack.validation_passed, f"home export validation failed: {home_pack.validation_warnings}")

    ticket = exporter.export_session(
        session,
        preset="ticket",
        share_safe=True,
        mask_ip=True,
        include_logs=True,
        allow_validator_override=False,
    )
    _assert(ticket.validation_passed, f"ticket export validation failed: {ticket.validation_warnings}")
    _assert(count_artifacts_for_session("S_SMOKE") > 0, "artifacts were not indexed in sqlite after export")

    listing = _read_zip_listing(ticket.zip_path)
    required_files = {
        "report/report.html",
        "report/summary.md",
        "report/findings.csv",
        "data/session.json",
        "logs/actions.txt",
        "logs/diagnostics.txt",
        "manifest/manifest.json",
        "manifest/hashes.txt",
    }
    for required in required_files:
        _assert(required in listing, f"{required} missing from {ticket.zip_path.name}")

    manifest = json.loads(ticket.manifest_path.read_text(encoding="utf-8"))
    _assert(manifest["session_id"] == "S_SMOKE", "manifest session mismatch")
    _assert(len(manifest["files"]) >= 8, "manifest missing file entries")
    ok, warnings = validate_export_folder(
        ticket.folder_path,
        manifest,
        True,
        raw_tokens=["DESKTOP-SMOKE", "John", "HomeWifi"],
    )
    _assert(ok, f"export folder validator failed: {warnings}")

    for req in ("evidence/eventlogs", "evidence/network", "evidence/system", "evidence/updates", "evidence/crash"):
        _assert((ticket.folder_path / req).exists(), f"ticket evidence path missing: {req}")

    summary_md = (ticket.folder_path / "report" / "summary.md").read_text(encoding="utf-8")
    data_session = (ticket.folder_path / "data" / "session.json").read_text(encoding="utf-8")
    _assert("C:\\Users\\John" not in summary_md, "summary markdown contains unmasked user path")
    _assert("DESKTOP-SMOKE" not in summary_md, "summary markdown contains unmasked host token")
    _assert("HomeWifi" not in data_session, "session.json contains unmasked SSID token")

    rebuilt = rebuild_from_sessions_folder()
    _assert(int(rebuilt.get("sessions", 0)) >= 1, "db rebuild indexed zero sessions")
    stats = db_stats()
    _assert(stats.sessions >= 1, "db stats sessions count invalid after rebuild")

    print("Smoke test passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
