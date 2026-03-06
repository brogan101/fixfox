from __future__ import annotations

import os
import sys
from pathlib import Path

from PySide6.QtCore import QtMsgType, qInstallMessageHandler
from PySide6.QtWidgets import QApplication, QComboBox, QWidget

REPO_ROOT = Path(__file__).resolve().parent.parent
REPORT_PATH = REPO_ROOT / "docs" / "qss_sanity_report.txt"

if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))


def _msg_type_name(msg_type: QtMsgType) -> str:
    if msg_type == QtMsgType.QtDebugMsg:
        return "DEBUG"
    if msg_type == QtMsgType.QtInfoMsg:
        return "INFO"
    if msg_type == QtMsgType.QtWarningMsg:
        return "WARNING"
    if msg_type == QtMsgType.QtCriticalMsg:
        return "CRITICAL"
    if msg_type == QtMsgType.QtFatalMsg:
        return "FATAL"
    return "UNKNOWN"


def _is_whitelisted_unknown_property(message: str) -> bool:
    return "unknown property qproperty-" in message.strip().lower()


def run_qss_sanity(*, report_path: Path = REPORT_PATH, verbose: bool = True) -> tuple[bool, list[str], list[str]]:
    os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
    messages: list[str] = []
    failures: list[str] = []

    def _handler(msg_type: QtMsgType, context: object, message: str) -> None:
        del context
        line = f"[{_msg_type_name(msg_type)}] {message}"
        messages.append(line)
        lower = message.lower()
        if "could not parse application stylesheet" in lower:
            failures.append(line)
            return
        if "unknown property" in lower and not _is_whitelisted_unknown_property(message):
            failures.append(line)

    prev = qInstallMessageHandler(_handler)
    ok = False
    try:
        from src.core.qt_runtime import ensure_qt_runtime_env, is_fatal_qt_warning
        from src.core.settings import load_settings
        from src.ui.app_qss import build_qss
        from src.ui.theme import resolve_theme_tokens

        ensure_qt_runtime_env()
        app = QApplication.instance() or QApplication([])
        settings = load_settings().normalized()
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
        for line in messages:
            msg = line.split("] ", 1)[1] if "] " in line else line
            if _is_whitelisted_unknown_property(msg):
                continue
            if is_fatal_qt_warning(msg):
                failures.append(line)
        failures = list(dict.fromkeys(failures))
        ok = len(failures) == 0
    except Exception as exc:
        failures.append(f"Exception while checking QSS: {exc}")
        ok = False
    finally:
        qInstallMessageHandler(prev)

    report_path.parent.mkdir(parents=True, exist_ok=True)
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
    lines += ["", "Qt Messages:"]
    if messages:
        lines.extend(f"- {line}" for line in messages)
    else:
        lines.append("- none")
    if ok:
        lines += ["", "OK"]
    report_path.write_text("\n".join(lines) + "\n", encoding="utf-8")

    if verbose:
        print(f"qss_sanity_report={report_path}")
        print(f"qss_sanity_result={'PASS' if ok else 'FAIL'}")
        if failures:
            for line in failures:
                print(line)
    return ok, failures, messages


def main() -> int:
    ok, _failures, _messages = run_qss_sanity(verbose=True)
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
