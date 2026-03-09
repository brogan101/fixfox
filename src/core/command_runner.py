from __future__ import annotations

import os
import signal
import subprocess
import threading
import time
from dataclasses import dataclass
from typing import Any, Callable, Mapping, Sequence

from .run_events import RunEventBus, RunEventType


MAX_CAPTURE_CHARS = 12_000


@dataclass
class CommandResult:
    code: int
    stdout: str
    stderr: str
    timed_out: bool
    duration_s: float

    @property
    def combined(self) -> str:
        text = (self.stdout or "").strip()
        err = (self.stderr or "").strip()
        if text and err:
            return f"{text}\n{err}"
        return text or err


def _truncate(text: str, limit: int = MAX_CAPTURE_CHARS) -> str:
    if len(text) <= limit:
        return text
    return text[:limit] + "\n...[truncated]..."


def _emit(callback: Callable[[str], None] | None, line: str) -> None:
    if callback is None or not line:
        return
    try:
        callback(line)
    except Exception:
        # Callback failures should not break command execution.
        return


def _pump_stream(
    stream: Any,
    sink: list[str],
    callback: Callable[[str], None] | None,
    done: threading.Event,
    *,
    event_bus: RunEventBus | None = None,
    run_id: str = "",
    stream_type: str = RunEventType.STDOUT,
) -> None:
    try:
        if stream is None:
            return
        while True:
            try:
                line = stream.readline()
            except ValueError:
                # The parent can close the pipe during shutdown if the worker is
                # still winding down. Treat that as normal end-of-stream.
                break
            if line == "":
                break
            line = line.rstrip("\r\n")
            sink.append(line)
            _emit(callback, line)
            if event_bus is not None and run_id:
                event_bus.publish(run_id, stream_type, message=line)
    finally:
        done.set()


def run_command(
    cmd: Sequence[str],
    timeout_s: int = 30,
    cancel_event: threading.Event | None = None,
    on_stdout_line: Callable[[str], None] | None = None,
    on_stderr_line: Callable[[str], None] | None = None,
    cwd: str | None = None,
    env: Mapping[str, str] | None = None,
    event_bus: RunEventBus | None = None,
    run_id: str = "",
) -> CommandResult:
    start = time.monotonic()
    stdout_lines: list[str] = []
    stderr_lines: list[str] = []
    timed_out = False
    cancelled = False
    popen_kwargs: dict[str, Any] = {}
    if subprocess.os.name == "nt":
        popen_kwargs["creationflags"] = subprocess.CREATE_NEW_PROCESS_GROUP
    else:
        popen_kwargs["start_new_session"] = True
    try:
        if event_bus is not None and run_id:
            event_bus.publish(run_id, RunEventType.START, message="Process started.", data={"cmd": list(cmd)})
            event_bus.publish(run_id, RunEventType.STATUS, message="Process launched.")
        process = subprocess.Popen(
            list(cmd),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
            shell=False,
            cwd=cwd,
            env=dict(env) if env is not None else None,
            bufsize=1,
            **popen_kwargs,
        )
    except Exception as exc:
        if event_bus is not None and run_id:
            event_bus.publish(run_id, RunEventType.ERROR, message=f"Process failed to start: {exc}")
            event_bus.publish(run_id, RunEventType.END, message="Process ended with startup error.", data={"code": 1})
        return CommandResult(
            code=1,
            stdout="",
            stderr=f"ERROR: {exc}",
            timed_out=False,
            duration_s=time.monotonic() - start,
        )

    done_out = threading.Event()
    done_err = threading.Event()
    out_thread = threading.Thread(
        target=_pump_stream,
        args=(process.stdout, stdout_lines, on_stdout_line, done_out),
        kwargs={"event_bus": event_bus, "run_id": run_id, "stream_type": RunEventType.STDOUT},
        daemon=True,
    )
    err_thread = threading.Thread(
        target=_pump_stream,
        args=(process.stderr, stderr_lines, on_stderr_line, done_err),
        kwargs={"event_bus": event_bus, "run_id": run_id, "stream_type": RunEventType.STDERR},
        daemon=True,
    )
    out_thread.start()
    err_thread.start()

    try:
        last_heartbeat = start
        while process.poll() is None:
            if cancel_event and cancel_event.is_set():
                cancelled = True
                _terminate_process_tree(process)
                break
            if time.monotonic() - start > timeout_s:
                timed_out = True
                _terminate_process_tree(process)
                break
            now = time.monotonic()
            if event_bus is not None and run_id and now - last_heartbeat >= 1.0:
                elapsed = int(max(0.0, now - start))
                event_bus.publish(
                    run_id,
                    RunEventType.PROGRESS,
                    message=f"Running... {elapsed}s elapsed.",
                    data={"elapsed_s": elapsed},
                )
                event_bus.publish(run_id, RunEventType.STATUS, message=f"Running... {elapsed}s elapsed.")
                last_heartbeat = now
            time.sleep(0.05)
    finally:
        try:
            process.wait(timeout=1.5)
        except Exception:
            ...
        done_out.wait(timeout=1.0)
        done_err.wait(timeout=1.0)
        out_thread.join(timeout=1.0)
        err_thread.join(timeout=1.0)
        try:
            if process.stdout is not None:
                process.stdout.close()
        except Exception:
            ...
        try:
            if process.stderr is not None:
                process.stderr.close()
        except Exception:
            ...

    code = process.returncode if process.returncode is not None else 1
    if timed_out:
        stderr_lines.append("Timed out.")
        _emit(on_stderr_line, "Timed out.")
        if event_bus is not None and run_id:
            event_bus.publish(run_id, RunEventType.WARNING, message="Command timed out.", data={"code": 124})
            event_bus.publish(run_id, RunEventType.STATUS, message="Timed out.")
        code = 124
    elif cancelled or (cancel_event and cancel_event.is_set()):
        stderr_lines.append("Cancelled.")
        _emit(on_stderr_line, "Cancelled.")
        if event_bus is not None and run_id:
            event_bus.publish(run_id, RunEventType.WARNING, message="Command cancelled.", data={"code": 130})
            event_bus.publish(run_id, RunEventType.STATUS, message="Cancelled.")
        code = 130
    if event_bus is not None and run_id:
        level = RunEventType.END if code == 0 else RunEventType.ERROR
        message = "Process completed." if code == 0 else f"Process ended with code {code}."
        event_bus.publish(run_id, level, message=message, data={"code": code, "timed_out": timed_out})
        event_bus.publish(run_id, RunEventType.STATUS, message=("Completed." if code == 0 else f"Completed with code {code}."))
        event_bus.publish(run_id, RunEventType.END, message="Command run finished.", data={"code": code})

    return CommandResult(
        code=code,
        stdout=_truncate("\n".join(stdout_lines)),
        stderr=_truncate("\n".join(stderr_lines)),
        timed_out=timed_out,
        duration_s=time.monotonic() - start,
    )


def _terminate_process_tree(process: subprocess.Popen[str]) -> None:
    if process.poll() is not None:
        return
    if subprocess.os.name == "nt":
        try:
            subprocess.run(
                ["taskkill", "/PID", str(process.pid), "/T", "/F"],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                check=False,
                timeout=3,
            )
            return
        except Exception:
            ...
    else:
        try:
            pgid = os.getpgid(process.pid)
            os.killpg(pgid, signal.SIGTERM)
            process.wait(timeout=1.5)
            return
        except Exception:
            ...
    try:
        process.terminate()
        process.wait(timeout=1.5)
    except Exception:
        try:
            process.kill()
        except Exception:
            ...
