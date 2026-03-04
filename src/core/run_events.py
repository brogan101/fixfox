from __future__ import annotations

from collections import deque
from dataclasses import dataclass
from threading import RLock
from time import time
from typing import Any, Callable
from uuid import uuid4


class RunEventType:
    START = "START"
    PROGRESS = "PROGRESS"
    STATUS = "STATUS"
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
        self._subscribers: dict[str, dict[int, Callable[[RunEvent], None]]] = {}
        self._subscription_index: dict[int, str] = {}
        self._next_subscription_id = 0
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
        callbacks: list[Callable[[RunEvent], None]] = []
        with self._lock:
            buffer = self._buffers.get(rid)
            if buffer is None:
                buffer = _RunBuffer(self._max_events_per_run)
                self._buffers[rid] = buffer
            event = buffer.append(event_type, message, progress, data, rid)
            callbacks.extend(self._subscribers.get(rid, {}).values())
            callbacks.extend(self._subscribers.get("*", {}).values())
        try:
            from .db import record_run_event

            record_run_event(
                rid,
                event_type,
                event.timestamp_utc,
                message=event.message,
                data=event.data if isinstance(event.data, dict) else None,
            )
        except Exception:
            # Index failures must never block runtime run-event delivery.
            pass
        for callback in callbacks:
            self._notify_subscriber(callback, event)
        return event

    def subscribe(
        self,
        run_id: str,
        callback: Callable[[RunEvent], None],
        *,
        replay_since: int = 0,
    ) -> int:
        rid = str(run_id or "").strip() or "*"
        replay_rows: list[RunEvent] = []
        with self._lock:
            self._next_subscription_id += 1
            sid = self._next_subscription_id
            bucket = self._subscribers.setdefault(rid, {})
            bucket[sid] = callback
            self._subscription_index[sid] = rid
            if rid != "*":
                buffer = self._buffers.get(rid)
                if buffer is not None:
                    replay_rows = buffer.since(max(0, int(replay_since)))
        for event in replay_rows:
            self._notify_subscriber(callback, event)
        return sid

    def subscribe_global(self, callback: Callable[[RunEvent], None], *, replay_buffered: bool = False) -> int:
        replay_rows: list[RunEvent] = []
        with self._lock:
            self._next_subscription_id += 1
            sid = self._next_subscription_id
            bucket = self._subscribers.setdefault("*", {})
            bucket[sid] = callback
            self._subscription_index[sid] = "*"
            if replay_buffered:
                for buffer in self._buffers.values():
                    replay_rows.extend(list(buffer.events))
        replay_rows.sort(key=lambda row: row.timestamp_utc)
        for event in replay_rows:
            self._notify_subscriber(callback, event)
        return sid

    def unsubscribe(self, subscription_id: int) -> None:
        sid = int(subscription_id)
        with self._lock:
            rid = self._subscription_index.pop(sid, "")
            if not rid:
                return
            bucket = self._subscribers.get(rid)
            if bucket is None:
                return
            bucket.pop(sid, None)
            if not bucket:
                self._subscribers.pop(rid, None)

    def _notify_subscriber(self, callback: Callable[[RunEvent], None], event: RunEvent) -> None:
        try:
            callback(event)
        except Exception:
            return

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
            bucket = self._subscribers.pop(rid, None)
            if bucket:
                for sid in bucket:
                    self._subscription_index.pop(sid, None)


_EVENT_BUS: RunEventBus | None = None


def get_run_event_bus() -> RunEventBus:
    global _EVENT_BUS
    if _EVENT_BUS is None:
        _EVENT_BUS = RunEventBus()
    return _EVENT_BUS
