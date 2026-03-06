from __future__ import annotations

import logging
import os
import sys
import threading
import traceback
from datetime import datetime
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from PySide6.QtCore import QEvent, QObject, QTimer, QtMsgType, qInstallMessageHandler
from PySide6.QtWidgets import QApplication, QWidget

from .qt_runtime import is_fatal_qt_warning, is_ignorable_qt_warning

LOGGER = logging.getLogger("fixfox.startup")
WATCHDOG_LOGGER = logging.getLogger("fixfox.watchdog")


def _qt_msg_name(msg_type: QtMsgType) -> str:
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


def _dump_thread_stacks() -> str:
    frames = sys._current_frames()
    rows: list[str] = []
    for thread in threading.enumerate():
        ident = thread.ident
        rows.append(f"\n--- thread={thread.name} ident={ident} daemon={thread.daemon} ---")
        if ident is None:
            rows.append("no frame")
            continue
        frame = frames.get(ident)
        if frame is None:
            rows.append("no frame")
            continue
        rows.extend(traceback.format_stack(frame))
    return "".join(rows)


def _default_watchdog_log_path() -> Path:
    ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    out = Path.cwd() / "logs" / f"launch_watchdog_{ts}.txt"
    out.parent.mkdir(parents=True, exist_ok=True)
    return out


def _default_qt_log_path() -> Path:
    out = Path.cwd() / "logs" / "fixfox_qt.log"
    out.parent.mkdir(parents=True, exist_ok=True)
    return out


@dataclass
class StartupMetrics:
    started_at: float
    first_paint_ms: float = -1.0
    stalled_logged: bool = False


class StartupWatchdog(QObject):
    def __init__(
        self,
        *,
        stall_threshold_s: float = 5.0,
        tick_ms: int = 250,
        watchdog_log_path: Path | None = None,
        qt_log_path: Path | None = None,
    ) -> None:
        super().__init__()
        import time

        self._time = time
        self._metrics = StartupMetrics(started_at=time.perf_counter())
        self._stall_threshold_s = float(max(1.0, stall_threshold_s))
        self._phase = "bootstrap"
        self._last_tick_bucket = -1
        self._timer = QTimer(self)
        self._timer.setInterval(max(100, int(tick_ms)))
        self._timer.timeout.connect(self._on_tick)
        self._first_paint_seen = False
        self._qt_handler_prev: Any = None
        self._qt_warning_lines: list[str] = []
        self._qt_fatal_lines: list[str] = []
        self._watchdog_log_path = Path(watchdog_log_path) if watchdog_log_path else _default_watchdog_log_path()
        self._qt_log_path = Path(qt_log_path) if qt_log_path else _default_qt_log_path()
        self._watchdog_log_path.parent.mkdir(parents=True, exist_ok=True)
        self._qt_log_path.parent.mkdir(parents=True, exist_ok=True)
        self._append_watchdog(f"startup_watchdog_initialized stall_threshold_s={self._stall_threshold_s:.2f}")
        self._append_watchdog(f"watchdog_log_path={self._watchdog_log_path}")
        self._append_watchdog(f"qt_log_path={self._qt_log_path}")
        self._append_qt("qt_message_handler_initialized")

    @property
    def first_paint_ms(self) -> float:
        return self._metrics.first_paint_ms

    @property
    def watchdog_log_path(self) -> Path:
        return self._watchdog_log_path

    @property
    def qt_log_path(self) -> Path:
        return self._qt_log_path

    @property
    def phase(self) -> str:
        return self._phase

    @property
    def qt_warning_lines(self) -> list[str]:
        return list(self._qt_warning_lines)

    @property
    def qt_fatal_lines(self) -> list[str]:
        return list(self._qt_fatal_lines)

    def _append_watchdog(self, line: str) -> None:
        text = str(line).rstrip()
        try:
            with self._watchdog_log_path.open("a", encoding="utf-8") as handle:
                handle.write(text + "\n")
        except Exception:
            pass

    def _append_qt(self, line: str) -> None:
        text = str(line).rstrip()
        try:
            with self._qt_log_path.open("a", encoding="utf-8") as handle:
                handle.write(text + "\n")
        except Exception:
            pass

    def set_phase(self, phase: str) -> None:
        self._phase = str(phase or "unknown").strip() or "unknown"
        elapsed = (self._time.perf_counter() - self._metrics.started_at) * 1000.0
        LOGGER.info("startup_phase=%s elapsed_ms=%.1f", self._phase, elapsed)
        WATCHDOG_LOGGER.info("startup_phase=%s elapsed_ms=%.1f", self._phase, elapsed)
        self._append_watchdog(f"startup_phase={self._phase} elapsed_ms={elapsed:.1f}")

    def mark(self, phase: str) -> None:
        self.set_phase(phase)

    def start(self) -> None:
        self._timer.start()
        self.mark("watchdog_started")

    def stop(self) -> None:
        if self._timer.isActive():
            self._timer.stop()
        if self._qt_handler_prev is not None:
            qInstallMessageHandler(self._qt_handler_prev)
            self._qt_handler_prev = None
        elapsed_ms = (self._time.perf_counter() - self._metrics.started_at) * 1000.0
        self._append_watchdog(f"watchdog_stopped elapsed_ms={elapsed_ms:.1f} first_paint_ms={self._metrics.first_paint_ms:.1f}")

    def install_qt_message_handler(self) -> None:
        def _handler(msg_type: QtMsgType, context: Any, message: str) -> None:
            del context
            if is_ignorable_qt_warning(message):
                return
            text = f"qt[{_qt_msg_name(msg_type)}] {message}"
            msg_lower = str(message or "").lower()
            self._append_qt(text)
            if is_fatal_qt_warning(msg_lower):
                self._qt_fatal_lines.append(text)
                self._qt_warning_lines.append(text)
                LOGGER.error(text)
                WATCHDOG_LOGGER.error(text)
            elif msg_type in {QtMsgType.QtWarningMsg, QtMsgType.QtCriticalMsg, QtMsgType.QtFatalMsg}:
                self._qt_warning_lines.append(text)
                LOGGER.warning(text)
                WATCHDOG_LOGGER.warning(text)

        self._qt_handler_prev = qInstallMessageHandler(_handler)

    def attach_window(self, widget: QWidget) -> None:
        widget.installEventFilter(self)

    def eventFilter(self, watched: QObject, event: QEvent) -> bool:  # type: ignore[override]
        if (not self._first_paint_seen) and event.type() in {QEvent.Show, QEvent.Paint, QEvent.UpdateRequest, QEvent.Expose}:
            self._first_paint_seen = True
            elapsed_ms = (self._time.perf_counter() - self._metrics.started_at) * 1000.0
            self._metrics.first_paint_ms = elapsed_ms
            LOGGER.info("startup_first_paint_ms=%.1f phase=%s", elapsed_ms, self._phase)
            WATCHDOG_LOGGER.info("startup_first_paint_ms=%.1f phase=%s", elapsed_ms, self._phase)
            self._append_watchdog(f"startup_first_paint_ms={elapsed_ms:.1f} phase={self._phase}")
            if self._timer.isActive():
                self._timer.stop()
        return super().eventFilter(watched, event)

    def _on_tick(self) -> None:
        elapsed_s = self._time.perf_counter() - self._metrics.started_at
        if self._first_paint_seen:
            if self._timer.isActive():
                self._timer.stop()
            return
        bucket = int(elapsed_s)
        if bucket != self._last_tick_bucket:
            self._last_tick_bucket = bucket
            LOGGER.info("startup_watchdog_tick phase=%s elapsed_ms=%.1f", self._phase, elapsed_s * 1000.0)
            self._append_watchdog(f"startup_watchdog_tick phase={self._phase} elapsed_ms={elapsed_s * 1000.0:.1f}")
        if elapsed_s < self._stall_threshold_s:
            return
        if self._metrics.stalled_logged:
            return
        self._metrics.stalled_logged = True
        LOGGER.error("STARTUP STALLED phase=%s elapsed_ms=%.1f", self._phase, elapsed_s * 1000.0)
        WATCHDOG_LOGGER.error("STARTUP STALLED phase=%s elapsed_ms=%.1f", self._phase, elapsed_s * 1000.0)
        self._append_watchdog(f"STARTUP STALLED phase={self._phase} elapsed_ms={elapsed_s * 1000.0:.1f}")
        if os.environ.get("FIXFOX_DUMP_STARTUP_STACKS", "1").strip() == "1":
            try:
                dump = _dump_thread_stacks()
                LOGGER.error("startup_stall_thread_dump_begin%s", dump)
                LOGGER.error("startup_stall_thread_dump_end")
                self._append_watchdog("startup_stall_thread_dump_begin")
                self._append_watchdog(dump)
                self._append_watchdog("startup_stall_thread_dump_end")
            except Exception as exc:
                LOGGER.error("startup_stall_thread_dump_failed error=%s", exc)
                self._append_watchdog(f"startup_stall_thread_dump_failed error={exc}")


def install_startup_watchdog() -> StartupWatchdog:
    app = QApplication.instance()
    watchdog = StartupWatchdog()
    watchdog.install_qt_message_handler()
    if app is not None:
        watchdog.start()
    return watchdog
