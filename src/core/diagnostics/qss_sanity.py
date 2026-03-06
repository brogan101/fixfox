from __future__ import annotations

import logging
import os
import tempfile
from dataclasses import dataclass
from pathlib import Path

from PySide6.QtWidgets import QApplication, QComboBox, QWidget

from ..qt_runtime import ensure_qt_runtime_env, is_fatal_qt_warning, is_font_warning, is_qss_warning
from ..settings import load_settings
from ...app import _load_bundled_font
from ...ui.app_qss import build_qss
from ...ui.font_utils import font_asset_candidates
from ...ui.theme import resolve_theme_tokens, set_ui_scale_percent
from .qt_warnings import install_qt_message_handler, read_qt_warnings


@dataclass
class QssSanityResult:
    ok: bool
    failures: list[str]
    qt_warnings: list[str]
    report_path: str = ""


def run_qss_sanity(
    *,
    report_path: str | Path | None = None,
    warning_log_path: str | Path | None = None,
    verbose: bool = True,
) -> QssSanityResult:
    os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
    warnings_path = Path(warning_log_path) if warning_log_path else Path(tempfile.gettempdir()) / f"fixfox_qss_sanity_{os.getpid()}.log"
    warnings_path.parent.mkdir(parents=True, exist_ok=True)
    warnings_path.write_text("", encoding="utf-8")
    cleanup = install_qt_message_handler(str(warnings_path))
    failures: list[str] = []
    try:
        ensure_qt_runtime_env()
        app = QApplication.instance() or QApplication([])
        settings = load_settings().normalized()
        set_ui_scale_percent(getattr(settings, "ui_scale_pct", 100))
        _load_bundled_font(logging.getLogger("fixfox.qss_sanity"), font_asset_candidates)
        tokens = resolve_theme_tokens(settings.theme_palette, settings.theme_mode)
        qss = build_qss(tokens, settings.theme_mode, settings.density)
        if not str(qss).strip():
            failures.append("QSS builder returned empty stylesheet.")
        app.setStyleSheet(qss)
        probe = QWidget()
        combo = QComboBox(probe)
        combo.addItems(["One", "Two"])
        probe.show()
        combo.show()
        app.processEvents()
        app.processEvents()
        probe.close()
        app.processEvents()
    except Exception as exc:
        failures.append(f"Exception while checking QSS: {exc}")
    finally:
        cleanup()

    qt_warnings = read_qt_warnings(warnings_path)
    for line in qt_warnings:
        message = line.split("] ", 1)[1] if "] " in line else line
        if is_qss_warning(message) or is_font_warning(message) or is_fatal_qt_warning(message):
            failures.append(line)
    failures = list(dict.fromkeys(failures))
    ok = len(failures) == 0

    resolved_report = Path(report_path) if report_path else None
    if resolved_report is not None:
        resolved_report.parent.mkdir(parents=True, exist_ok=True)
        lines = [
            "FixFox QSS Sanity Check",
            f"result={'OK' if ok else 'FAIL'}",
            "",
            "Failures:",
        ]
        if failures:
            lines.extend(f"- {line}" for line in failures)
        else:
            lines.append("- none")
        lines += ["", "Qt Warnings:"]
        if qt_warnings:
            lines.extend(f"- {line}" for line in qt_warnings)
        else:
            lines.append("- none")
        if ok:
            lines += ["", "OK"]
        resolved_report.write_text("\n".join(lines) + "\n", encoding="utf-8")

    result = QssSanityResult(
        ok=ok,
        failures=failures,
        qt_warnings=qt_warnings,
        report_path=str(resolved_report) if resolved_report else "",
    )
    if verbose:
        print(f"qss_sanity_result={'PASS' if result.ok else 'FAIL'}")
        if resolved_report is not None:
            print(f"qss_sanity_report={resolved_report}")
        for line in result.failures:
            print(line)
    return result
