from __future__ import annotations

import json
import os
import time
import unittest
from pathlib import Path

from src.core.diagnostics import quick_check
from src.core.errors import classify_exit
from src.core.exporter import export_session, validate_export_folder
from src.core.logging_setup import write_crash_report
from src.core.masking import MaskingOptions, mask_text
from src.core.play_registry import CATEGORY_ICON_MAP, list_play_entries
from src.core.registry import CAPABILITIES
from src.core.runbooks import execute_runbook
from src.core.script_tasks import run_script_task
from src.core.settings import AppSettings, SETTINGS_VERSION, export_settings_snapshot
from src.ui.layout_guardrails import should_auto_collapse_right_panel


class MaskingTests(unittest.TestCase):
    def test_masks_windows_user_path(self) -> None:
        text = r"C:\Users\Alice\Downloads\report.txt"
        out = mask_text(text, MaskingOptions(enabled=True, mask_ip=False))
        self.assertIn(r"C:\Users\<user>", out)

    def test_masks_desktop_name_and_ssid_token(self) -> None:
        text = "Host DESKTOP-ABCD1234 connected to HomeWifi"
        out = mask_text(text, MaskingOptions(enabled=True, extra_tokens=("HomeWifi",)))
        self.assertIn("<pc-name>", out)
        self.assertIn("<redacted>", out)

    def test_masks_ip_when_enabled(self) -> None:
        text = "Client IP: 192.168.1.77"
        out = mask_text(text, MaskingOptions(enabled=True, mask_ip=True))
        self.assertIn("<ip>", out)


class ExportTests(unittest.TestCase):
    def test_manifest_contains_expected_fields_and_validator(self) -> None:
        session = quick_check()
        session["session_id"] = "S_UNIT"
        session["symptom"] = r"Unit C:\Users\Alice DESKTOP-ABCD1234"
        session["created_local"] = "2026-03-03 00:00:00"
        session["actions"] = []
        session["network"] = {"ssid": "HomeWifi"}
        session["evidence"] = {"files": []}
        result = export_session(session, preset="ticket", share_safe=True, mask_ip=True, include_logs=True)
        manifest = json.loads(result.manifest_path.read_text(encoding="utf-8"))
        self.assertEqual(manifest["session_id"], "S_UNIT")
        self.assertIn("generated_utc", manifest)
        self.assertGreaterEqual(len(manifest["files"]), 8)
        ok, warnings = validate_export_folder(
            result.folder_path,
            manifest,
            True,
            raw_tokens=["DESKTOP-ABCD1234", "Alice", "HomeWifi"],
        )
        self.assertTrue(ok, msg=f"validator warnings: {warnings}")


class RunbookTests(unittest.TestCase):
    def test_runbook_dry_run_checkpoints_and_summary(self) -> None:
        logs: list[str] = []
        result = execute_runbook(
            "home_fix_wifi_safe",
            dry_run=True,
            log_cb=lambda line: logs.append(line),
        )
        self.assertTrue(result["dry_run"])
        self.assertFalse(result["cancelled"])
        self.assertGreater(len(result["steps"]), 0)
        self.assertTrue(any(bool(step.get("checkpoint")) for step in result["steps"]))
        self.assertIn("summary_text", result)
        self.assertIn("recommended_export_preset", result)
        self.assertGreater(len(logs), 0)


class ErrorMappingTests(unittest.TestCase):
    def test_error_mapping_contains_user_message_and_next_steps(self) -> None:
        err = classify_exit(124, "Timed out.")
        self.assertIsNotNone(err)
        assert err is not None
        self.assertTrue(bool(err.user_message.strip()))
        self.assertGreaterEqual(len(err.suggested_next_steps), 1)


class CrashLoggingTests(unittest.TestCase):
    def test_crash_report_is_written(self) -> None:
        path = write_crash_report("unit crash marker")
        self.assertTrue(path.exists())
        self.assertIn("unit crash marker", path.read_text(encoding="utf-8", errors="ignore"))


class GuardrailTests(unittest.TestCase):
    def test_right_panel_auto_collapse_threshold(self) -> None:
        self.assertTrue(should_auto_collapse_right_panel(1000))
        self.assertFalse(should_auto_collapse_right_panel(1400))


class ArtifactTests(unittest.TestCase):
    def test_browser_rescue_creates_summary_artifact(self) -> None:
        out_dir = Path.cwd() / ".unit_tmp" / "browser_rescue"
        result = run_script_task(
            "task_browser_rescue",
            dry_run=False,
            output_dir=out_dir,
            mask_options=MaskingOptions(enabled=True, mask_ip=True),
        )
        self.assertIn(int(result.get("code", 1)), (0, 1))
        files = [Path(str(p)) for p in result.get("output_files", [])]
        self.assertGreaterEqual(len(files), 1)
        self.assertTrue(any(p.name.lower().endswith(".txt") for p in files))
        self.assertTrue(any(p.exists() for p in files))

    def test_duplicate_scan_creates_report_file_in_test_mode(self) -> None:
        root = Path.cwd() / ".unit_tmp" / "dup_scan_fixture"
        root.mkdir(parents=True, exist_ok=True)
        (root / "a.txt").write_text("duplicate fixture", encoding="utf-8")
        (root / "b.txt").write_text("duplicate fixture", encoding="utf-8")
        out_dir = Path.cwd() / ".unit_tmp" / "dup_scan_out"
        old = os.environ.get("FIXFOX_TEST_DUP_ROOT")
        os.environ["FIXFOX_TEST_DUP_ROOT"] = str(root)
        try:
            result = run_script_task(
                "task_duplicate_hash_scan",
                dry_run=False,
                output_dir=out_dir,
                mask_options=MaskingOptions(enabled=True, mask_ip=True),
            )
        finally:
            if old is None:
                os.environ.pop("FIXFOX_TEST_DUP_ROOT", None)
            else:
                os.environ["FIXFOX_TEST_DUP_ROOT"] = old
        self.assertIn(int(result.get("code", 1)), (0, 1))
        files = [Path(str(p)) for p in result.get("output_files", [])]
        self.assertTrue(any(p.name.lower() == "duplicates_report.csv" for p in files))
        self.assertTrue((out_dir / "duplicates_report.csv").exists())


class RegistryTests(unittest.TestCase):
    def test_registry_minimum_count(self) -> None:
        self.assertGreaterEqual(len(CAPABILITIES), 12)


class PlayRegistryContractTests(unittest.TestCase):
    def test_play_registry_metadata_and_dedupe_contract(self) -> None:
        rows = list_play_entries()
        self.assertGreaterEqual(len(rows), 20)
        seen_ids: set[str] = set()
        title_to_categories: dict[str, set[str]] = {}
        for row in rows:
            self.assertTrue(row.id.strip())
            self.assertTrue(row.title.strip())
            self.assertTrue(row.category.strip())
            self.assertTrue(row.risk_badge.strip())
            self.assertGreater(row.estimated_minutes, 0)
            self.assertIn(row.automation_level, {"auto", "guided", "evidence-only"})
            self.assertTrue(row.entrypoint.strip())
            self.assertNotIn(row.id, seen_ids, msg=f"duplicate play id: {row.id}")
            seen_ids.add(row.id)
            title_to_categories.setdefault(row.title.lower(), set()).add(row.category.lower())
        dup_titles = [title for title, cats in title_to_categories.items() if len(cats) > 1]
        self.assertFalse(dup_titles, msg=f"duplicate titles across categories: {dup_titles[:8]}")

    def test_play_category_icon_mapping_exists(self) -> None:
        icons_root = Path(__file__).resolve().parents[1] / "assets" / "icons"
        self.assertTrue(icons_root.exists())
        for category, icon_name in CATEGORY_ICON_MAP.items():
            self.assertTrue(category.strip())
            self.assertTrue(icon_name.strip())
            icon_svg = icons_root / f"{icon_name}.svg"
            icon_png = icons_root / f"{icon_name}.png"
            self.assertTrue(icon_svg.exists() or icon_png.exists(), msg=f"missing icon asset for category={category} icon={icon_name}")


class SettingsTests(unittest.TestCase):
    def test_ui_scale_is_clamped_on_normalize(self) -> None:
        low = AppSettings(ui_scale_pct=10).normalized()
        high = AppSettings(ui_scale_pct=300).normalized()
        ok = AppSettings(ui_scale_pct=110).normalized()
        self.assertEqual(low.ui_scale_pct, 90)
        self.assertEqual(high.ui_scale_pct, 125)
        self.assertEqual(ok.ui_scale_pct, 110)

    def test_settings_snapshot_is_safe_for_support_bundle(self) -> None:
        settings = AppSettings(
            window_x=20,
            window_y=40,
            window_width=1440,
            window_height=960,
            splitter_sizes=[72, 860, 340],
            ui_mode="pro",
        )
        payload = export_settings_snapshot(settings)
        self.assertEqual(payload["config_version"], SETTINGS_VERSION)
        self.assertEqual(payload["ui_mode"], "pro")
        self.assertNotIn("window_x", payload)
        self.assertNotIn("splitter_sizes", payload)


class SearchPerformanceTests(unittest.TestCase):
    def setUp(self) -> None:
        os.environ["QT_QPA_PLATFORM"] = "offscreen"
        os.environ["FIXFOX_SKIP_ONBOARDING"] = "1"

    def test_query_index_uses_cached_static_index(self) -> None:
        from src.core.search import get_search_cache_stats, query_index, reset_search_cache_for_tests

        reset_search_cache_for_tests()
        query_index("wifi", limit=10)
        stats_after_first = get_search_cache_stats()
        self.assertEqual(int(stats_after_first.get("static_builds", 0.0)), 1)
        for _ in range(6):
            query_index("wifi", limit=10)
        stats_after_many = get_search_cache_stats()
        self.assertEqual(int(stats_after_many.get("static_builds", 0.0)), 1)

    def test_global_search_typing_keeps_event_loop_responsive(self) -> None:
        from PySide6.QtCore import QTimer
        from PySide6.QtWidgets import QApplication

        from src.core.search import get_search_cache_stats, reset_search_cache_for_tests
        from src.ui.main_window import MainWindow

        app = QApplication.instance() or QApplication([])
        window = MainWindow()
        ticks = {"count": 0}
        timer = QTimer()
        timer.setInterval(15)
        timer.timeout.connect(lambda: ticks.__setitem__("count", int(ticks["count"]) + 1))
        try:
            warmup_deadline = time.monotonic() + 30.0
            while getattr(window, "_startup_warmup_active", False) and time.monotonic() < warmup_deadline:
                app.processEvents()
                time.sleep(0.05)
            self.assertFalse(getattr(window, "_startup_warmup_active", True), "startup warmup did not complete before search responsiveness probe")
            reset_search_cache_for_tests()
            before = get_search_cache_stats()
            timer.start()
            probe = "quickcheck"
            started = time.perf_counter()
            window.top_search.setFocus()
            for i in range(1, min(10, len(probe)) + 1):
                window.top_search.setText(probe[:i])
                window._schedule_global_search()
                app.processEvents()
                time.sleep(0.018)
            deadline = time.perf_counter() + 0.75
            while time.perf_counter() < deadline:
                app.processEvents()
                time.sleep(0.01)
            elapsed_ms = (time.perf_counter() - started) * 1000.0
            after = get_search_cache_stats()
            static_delta = int(after.get("static_builds", 0.0) - before.get("static_builds", 0.0))
            self.assertLessEqual(static_delta, 1, msg=f"search static index rebuilt too often: delta={static_delta}")
            self.assertGreaterEqual(int(ticks["count"]), 16, msg=f"event loop stalled during typing: ticks={ticks['count']}")
            self.assertLessEqual(elapsed_ms, 2400.0, msg=f"typing search exceeded blocking budget: {elapsed_ms:.1f}ms")
        finally:
            timer.stop()
            window.close()
            app.processEvents()


if __name__ == "__main__":
    unittest.main()
