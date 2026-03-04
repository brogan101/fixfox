from __future__ import annotations
import os, sys, subprocess, ctypes
from pathlib import Path
from threading import Event

from .command_runner import run_command

def is_windows() -> bool:
    return os.name == "nt"

def is_admin() -> bool:
    if not is_windows():
        return False
    try:
        return bool(ctypes.windll.shell32.IsUserAnAdmin())
    except Exception:
        return False

def run_cmd(cmd: list[str], timeout: int = 30, cancel_event: Event | None = None) -> tuple[int, str]:
    result = run_command(cmd, timeout_s=timeout, cancel_event=cancel_event)
    return result.code, result.combined.strip()

def open_uri(uri: str) -> tuple[int, str]:
    if not is_windows():
        return 1, "URI open is only implemented for Windows in v1."
    try:
        subprocess.Popen(f'start "" "{uri}"', shell=True)
        return 0, "Opened."
    except Exception as e:
        return 1, f"ERROR: {e}"

def resource_path(rel: str) -> str:
    base = getattr(sys, "_MEIPASS", None)
    if base:
        return str(Path(base) / rel)
    return str(Path(__file__).resolve().parent.parent / rel)
