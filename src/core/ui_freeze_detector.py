from __future__ import annotations

import logging
import sys
import threading
import time
import traceback
from pathlib import Path

from PySide6.QtCore import QObject, QTimer


LOGGER = logging.getLogger("fixfox.watchdog")


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


class UIFreezeDetector(QObject):
    def __init__(
        self,
        *,
        heartbeat_ms: int = 100,
        freeze_threshold_ms: int = 500,
        watchdog_log_path: Path | None = None,
    ) -> None:
        super().__init__()
        self._heartbeat_ms = int(max(50, heartbeat_ms))
        self._freeze_threshold_s = float(max(0.2, freeze_threshold_ms / 1000.0))
        self._watchdog_log_path = Path(watchdog_log_path) if watchdog_log_path else (Path.cwd() / "logs" / "fixfox_watchdog.log")
        self._watchdog_log_path.parent.mkdir(parents=True, exist_ok=True)
        self._timer = QTimer(self)
        self._timer.setInterval(self._heartbeat_ms)
        self._timer.timeout.connect(self._heartbeat)
        self._last_heartbeat = time.perf_counter()
        self._monitor_thread: threading.Thread | None = None
        self._monitor_stop = threading.Event()
        self._freeze_active = False
        self._freeze_count = 0

    @property
    def freeze_count(self) -> int:
        return int(self._freeze_count)

    def _append(self, line: str) -> None:
        text = str(line).rstrip()
        try:
            with self._watchdog_log_path.open("a", encoding="utf-8") as handle:
                handle.write(text + "\n")
        except Exception:
            pass

    def _heartbeat(self) -> None:
        self._last_heartbeat = time.perf_counter()
        if self._freeze_active:
            self._freeze_active = False
            LOGGER.info("UI FREEZE RECOVERED")
            self._append("UI FREEZE RECOVERED")

    def _monitor(self) -> None:
        sleep_s = max(0.05, self._heartbeat_ms / 1000.0 / 2.0)
        while not self._monitor_stop.wait(sleep_s):
            delta = time.perf_counter() - self._last_heartbeat
            if delta < self._freeze_threshold_s:
                continue
            if self._freeze_active:
                continue
            self._freeze_active = True
            self._freeze_count += 1
            stalled_ms = delta * 1000.0
            marker = f"UI FREEZE DETECTED stalled_ms={stalled_ms:.1f}"
            LOGGER.error(marker)
            self._append(marker)
            try:
                dump = _dump_thread_stacks()
                self._append("ui_freeze_thread_dump_begin")
                self._append(dump)
                self._append("ui_freeze_thread_dump_end")
            except Exception as exc:
                self._append(f"ui_freeze_thread_dump_failed error={exc}")

    def start(self) -> None:
        if self._timer.isActive():
            return
        self._last_heartbeat = time.perf_counter()
        self._monitor_stop.clear()
        self._timer.start()
        if self._monitor_thread is None or not self._monitor_thread.is_alive():
            self._monitor_thread = threading.Thread(target=self._monitor, name="fixfox-ui-freeze-monitor", daemon=True)
            self._monitor_thread.start()
        self._append(
            f"ui_freeze_detector_started heartbeat_ms={self._heartbeat_ms} threshold_ms={int(self._freeze_threshold_s * 1000)}"
        )

    def stop(self) -> None:
        if self._timer.isActive():
            self._timer.stop()
        self._monitor_stop.set()
        self._append(f"ui_freeze_detector_stopped freeze_count={self._freeze_count}")
