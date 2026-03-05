from __future__ import annotations

import json
import os
import unittest
from unittest.mock import patch
from pathlib import Path

from src.core.diagnostics import quick_check
from src.core.errors import classify_exit
from src.core.exporter import export_session, validate_export_folder
from src.core.masking import MaskingOptions, mask_text
from src.core.onboarding import OnboardingState
from src.core.registry import CAPABILITIES
from src.core.runbooks import execute_runbook
from src.core.script_tasks import run_script_task
from src.core.settings import AppSettings
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


class OnboardingTests(unittest.TestCase):
    def test_skip_and_dont_show_again(self) -> None:
        state = OnboardingState()
        self.assertTrue(state.should_show())
        state.mark_skipped(dont_show_again=True)
        self.assertTrue(state.completed)
        self.assertFalse(state.should_show())


class SettingsTests(unittest.TestCase):
    def test_ui_scale_is_clamped_on_normalize(self) -> None:
        low = AppSettings(ui_scale_pct=10).normalized()
        high = AppSettings(ui_scale_pct=300).normalized()
        ok = AppSettings(ui_scale_pct=110).normalized()
        self.assertEqual(low.ui_scale_pct, 90)
        self.assertEqual(high.ui_scale_pct, 125)
        self.assertEqual(ok.ui_scale_pct, 110)


class OnboardingLaunchTests(unittest.TestCase):
    def setUp(self) -> None:
        os.environ["QT_QPA_PLATFORM"] = "offscreen"
        os.environ["FIXFOX_FORCE_ONBOARDING"] = "1"
        os.environ.pop("FIXFOX_SKIP_ONBOARDING", None)

    def tearDown(self) -> None:
        os.environ.pop("FIXFOX_FORCE_ONBOARDING", None)

    def test_onboarding_shown_when_incomplete(self) -> None:
        from PySide6.QtWidgets import QApplication, QDialog
        from src.ui.main_window import MainWindow

        calls: dict[str, int] = {"shown": 0}

        class DummyOnboarding:
            def __init__(self, *args, **kwargs) -> None:
                del args, kwargs
                calls["shown"] += 1
                self.completed = True
                self.result_action = "none"

            def exec(self) -> int:
                return QDialog.Accepted

        with (
            patch("src.ui.main_window_impl.load_settings", return_value=AppSettings(onboarding_completed=False)),
            patch("src.ui.main_window_impl.save_settings"),
            patch("src.ui.main_window_impl.OnboardingFlow", DummyOnboarding),
        ):
            app = QApplication.instance() or QApplication([])
            window = MainWindow()
            app.processEvents()
            window.close()
            app.processEvents()
        self.assertEqual(calls["shown"], 1)

    def test_onboarding_not_shown_when_completed(self) -> None:
        from PySide6.QtWidgets import QApplication
        from src.ui.main_window import MainWindow

        calls: dict[str, int] = {"shown": 0}

        class DummyOnboarding:
            def __init__(self, *args, **kwargs) -> None:
                del args, kwargs
                calls["shown"] += 1

        with (
            patch("src.ui.main_window_impl.load_settings", return_value=AppSettings(onboarding_completed=True)),
            patch("src.ui.main_window_impl.save_settings"),
            patch("src.ui.main_window_impl.OnboardingFlow", DummyOnboarding),
        ):
            app = QApplication.instance() or QApplication([])
            window = MainWindow()
            app.processEvents()
            window.close()
            app.processEvents()
        self.assertEqual(calls["shown"], 0)


if __name__ == "__main__":
    unittest.main()
