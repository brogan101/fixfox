from __future__ import annotations

import os
import sys
from pathlib import Path
from typing import Callable

from PySide6.QtCore import QtMsgType, qInstallMessageHandler


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


def install_qt_message_handler(log_path: str, *, echo_to_console: bool | None = None) -> Callable[[], None]:
    path = Path(log_path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.touch(exist_ok=True)
    echo = bool(echo_to_console)
    if echo_to_console is None:
        echo = os.environ.get("FIXFOX_QT_WARNINGS_ECHO", "").strip().lower() in {"1", "true", "yes", "on"}

    def _handler(msg_type: QtMsgType, context: object, message: str) -> None:
        del context
        line = f"[{_msg_type_name(msg_type)}] {message}".rstrip()
        with path.open("a", encoding="utf-8", errors="ignore") as handle:
            handle.write(line + "\n")
        if echo:
            print(line, file=sys.stderr)

    previous = qInstallMessageHandler(_handler)

    def _cleanup() -> None:
        qInstallMessageHandler(previous)

    return _cleanup


def read_qt_warnings(log_path: str | Path) -> list[str]:
    path = Path(log_path)
    if not path.exists():
        return []
    return [line.rstrip() for line in path.read_text(encoding="utf-8", errors="ignore").splitlines() if line.strip()]
