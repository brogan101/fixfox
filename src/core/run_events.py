from __future__ import annotations

from collections import deque
from dataclasses import dataclass
from threading import RLock
from time import time
from typing import Any
from uuid import uuid4


class RunEventType:
    START = "START"
    PROGRESS = "PROGRESS"
    STDOUT = "STDOUT"
    STDERR = "STDERR"
    ARTIFACT = "ARTIFACT"
    WARNING = "WARNING"
    ERROR = "ERROR"
    END = "END"


@dataclass(frozen=True)
class RunEvent:
    run_id: str
    seq: int
    event_type: str
    timestamp_utc: float
    message: str = ""
    progress: int | None = None
    data: dict[str, Any] | None = None


class _RunBuffer:
    def __init__(self, max_events: int) -> None:
        self.events: deque[RunEvent] = deque(maxlen=max_events)
        self.next_seq = 0

    def append(self, event_type: str, message: str, progress: int | None, data: dict[str, Any] | None, run_id: str) -> RunEvent:
        self.next_seq += 1
        event = RunEvent(
            run_id=run_id,
            seq=self.next_seq,
            event_type=event_type,
            timestamp_utc=time(),
            message=message,
            progress=progress,
            data=data,
        )
        self.events.append(event)
        return event

    def since(self, cursor: int) -> list[RunEvent]:
        if not self.events:
            return []
        return [event for event in self.events if event.seq > cursor]


class RunEventBus:
    def __init__(self, max_events_per_run: int = 5000) -> None:
        self._max_events_per_run = max(200, int(max_events_per_run))
        self._buffers: dict[str, _RunBuffer] = {}
        self._lock = RLock()

    def create_run(self, *, name: str, risk: str, session_id: str = "", metadata: dict[str, Any] | None = None) -> str:
        run_id = f"run_{uuid4().hex[:12]}"
        payload = {"name": name, "risk": risk, "session_id": session_id}
        if metadata:
            payload.update(metadata)
        self.publish(run_id, RunEventType.START, message=f"{name} started.", data=payload)
        return run_id

    def publish(
        self,
        run_id: str,
        event_type: str,
        *,
        message: str = "",
        progress: int | None = None,
        data: dict[str, Any] | None = None,
    ) -> RunEvent:
        rid = str(run_id or "").strip()
        if not rid:
            rid = f"run_{uuid4().hex[:12]}"
        with self._lock:
            buffer = self._buffers.get(rid)
            if buffer is None:
                buffer = _RunBuffer(self._max_events_per_run)
                self._buffers[rid] = buffer
            return buffer.append(event_type, message, progress, data, rid)

    def events_since(self, run_id: str, cursor: int = 0) -> tuple[list[RunEvent], int]:
        rid = str(run_id or "").strip()
        if not rid:
            return [], cursor
        with self._lock:
            buffer = self._buffers.get(rid)
            if buffer is None:
                return [], cursor
            rows = buffer.since(max(0, int(cursor)))
            next_cursor = rows[-1].seq if rows else max(0, int(cursor))
            return rows, next_cursor

    def latest_cursor(self, run_id: str) -> int:
        rid = str(run_id or "").strip()
        if not rid:
            return 0
        with self._lock:
            buffer = self._buffers.get(rid)
            if buffer is None:
                return 0
            return buffer.events[-1].seq if buffer.events else 0

    def clear_run(self, run_id: str) -> None:
        rid = str(run_id or "").strip()
        if not rid:
            return
        with self._lock:
            self._buffers.pop(rid, None)


_EVENT_BUS: RunEventBus | None = None


def get_run_event_bus() -> RunEventBus:
    global _EVENT_BUS
    if _EVENT_BUS is None:
        _EVENT_BUS = RunEventBus()
    return _EVENT_BUS

