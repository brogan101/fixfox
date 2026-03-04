from __future__ import annotations
import os
import shutil
from pathlib import Path

APP_NAME = "FixFox"
LEGACY_APP_NAME = "PCConcierge"


def _migrate_legacy_if_needed(branded: Path, legacy: Path) -> None:
    if branded.exists() or not legacy.exists() or not legacy.is_dir():
        return
    try:
        shutil.copytree(legacy, branded, dirs_exist_ok=True)
    except Exception:
        return


def local_app_dir() -> Path:
    # Windows: %LOCALAPPDATA%\FixFox
    # Linux:   ~/.local/share/FixFox
    # macOS:   ~/Library/Application Support/FixFox
    if os.name == "nt":
        base = os.environ.get("LOCALAPPDATA") or os.environ.get("APPDATA") or str(Path.home())
        branded = Path(base) / APP_NAME
        legacy = Path(base) / LEGACY_APP_NAME
        _migrate_legacy_if_needed(branded, legacy)
        return branded
    branded = Path.home() / ".local" / "share" / APP_NAME
    legacy = Path.home() / ".local" / "share" / LEGACY_APP_NAME
    _migrate_legacy_if_needed(branded, legacy)
    return branded

def ensure_dirs() -> dict[str, Path]:
    base = local_app_dir()
    sessions = base / "sessions"
    exports = base / "exports"
    logs = base / "logs"
    feedback = base / "feedback"
    state = base / "state"
    for p in (base, sessions, exports, logs, feedback, state):
        p.mkdir(parents=True, exist_ok=True)
    return {
        "base": base,
        "sessions": sessions,
        "exports": exports,
        "logs": logs,
        "feedback": feedback,
        "state": state,
    }


def settings_path() -> Path:
    return ensure_dirs()["state"] / "settings.json"
