from __future__ import annotations

import json
import threading
import time
from datetime import datetime
from pathlib import Path
from statistics import fmean
from typing import Any


class PerfRecorder:
    def __init__(self) -> None:
        self._lock = threading.Lock()
        self._active: dict[str, float] = {}
        self._metrics: dict[str, list[float]] = {}
        self._metadata: dict[str, Any] = {}
        self._session_started = time.perf_counter()
        self._last_report: Path | None = None

    def reset(self) -> None:
        with self._lock:
            self._active.clear()
            self._metrics.clear()
            self._metadata.clear()
            self._session_started = time.perf_counter()

    def set_meta(self, key: str, value: Any) -> None:
        with self._lock:
            self._metadata[str(key)] = value

    def record(self, metric_name: str, value_ms: float) -> None:
        if value_ms < 0:
            return
        with self._lock:
            self._metrics.setdefault(str(metric_name), []).append(float(value_ms))

    def start_timer(self, key: str) -> None:
        with self._lock:
            self._active[str(key)] = time.perf_counter()

    def stop_timer(self, key: str, metric_name: str) -> float:
        with self._lock:
            started = self._active.pop(str(key), None)
        if started is None:
            return -1.0
        elapsed_ms = (time.perf_counter() - started) * 1000.0
        self.record(metric_name, elapsed_ms)
        return elapsed_ms

    def snapshot(self) -> dict[str, Any]:
        with self._lock:
            rows = {name: list(values) for name, values in self._metrics.items()}
            metadata = dict(self._metadata)
            started = self._session_started
        metrics: dict[str, Any] = {}
        for name, values in rows.items():
            if not values:
                continue
            metrics[name] = {
                "count": len(values),
                "last_ms": round(values[-1], 3),
                "avg_ms": round(float(fmean(values)), 3),
                "max_ms": round(max(values), 3),
            }
        return {
            "generated_utc": datetime.utcnow().isoformat() + "Z",
            "uptime_ms": round((time.perf_counter() - started) * 1000.0, 3),
            "metadata": metadata,
            "metrics": metrics,
        }

    def write_report(self, path: Path | None = None) -> Path:
        out = path or (Path.cwd() / "logs" / f"perf_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json")
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps(self.snapshot(), indent=2), encoding="utf-8")
        self._last_report = out
        return out

    @property
    def last_report(self) -> Path | None:
        return self._last_report


PERF_RECORDER = PerfRecorder()
