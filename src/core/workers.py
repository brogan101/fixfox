from __future__ import annotations

import threading
import traceback
from dataclasses import dataclass
from typing import Any, Callable

from PySide6.QtCore import QObject, QRunnable, QThreadPool, Signal

from .errors import ToolError, ensure_next_steps


@dataclass
class WorkerConfig:
    timeout_s: int = 120


class WorkerSignals(QObject):
    progress = Signal(int, str)
    partial = Signal(object)
    log_line = Signal(str)
    result = Signal(object)
    error = Signal(str)
    finished = Signal()
    cancelled = Signal()


class TaskWorker(QRunnable):
    def __init__(
        self,
        fn: Callable[..., Any],
        *,
        kwargs: dict[str, Any] | None = None,
        config: WorkerConfig | None = None,
    ) -> None:
        super().__init__()
        self.fn = fn
        self.kwargs = kwargs or {}
        self.config = config or WorkerConfig()
        self.signals = WorkerSignals()
        self.cancel_event = threading.Event()

    def cancel(self) -> None:
        self.cancel_event.set()

    def run(self) -> None:
        try:
            kwargs = {
                "progress_cb": self.signals.progress.emit,
                "partial_cb": self.signals.partial.emit,
                "log_cb": self.signals.log_line.emit,
                "cancel_event": self.cancel_event,
                "timeout_s": self.config.timeout_s,
                **self.kwargs,
            }
            try:
                result = self.fn(**kwargs)
            except TypeError as exc:
                if "log_cb" not in str(exc):
                    raise
                kwargs.pop("log_cb", None)
                result = self.fn(**kwargs)
            if self.cancel_event.is_set():
                self.signals.cancelled.emit()
            else:
                self.signals.result.emit(result)
        except ToolError as exc:
            lines = [f"Reason: {exc.user_message}"]
            if exc.technical_message:
                lines.append(f"Technical: {exc.technical_message}")
            for step in ensure_next_steps(exc.suggested_next_steps):
                lines.append(f"Try next: {step}")
            self.signals.error.emit("\n".join(lines))
        except Exception:
            _ = traceback.format_exc()
            self.signals.error.emit(
                "Reason: The operation failed unexpectedly.\n"
                "Try next: Re-run the same action.\n"
                "Try next: Run a safe diagnostic tool first.\n"
                "Try next: Export a partial support pack."
            )
        finally:
            self.signals.finished.emit()


THREAD_POOL = QThreadPool.globalInstance()


def start_worker(worker: TaskWorker) -> TaskWorker:
    THREAD_POOL.start(worker)
    return worker
