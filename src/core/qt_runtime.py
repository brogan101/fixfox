from __future__ import annotations

import os
from pathlib import Path
from typing import Any


FATAL_QT_WARNING_PATTERNS: tuple[str, ...] = (
    "could not parse application stylesheet",
    "unknown property",
    "failed to create directwrite face",
    "cannot open file",
    "cannot find font directory",
)


def _repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def bundled_font_dir() -> Path:
    return _repo_root() / "src" / "assets" / "fonts"


def ensure_qt_runtime_env(logger: Any | None = None) -> dict[str, str]:
    updates: dict[str, str] = {}
    font_dir = bundled_font_dir()
    if font_dir.exists() and not os.environ.get("QT_QPA_FONTDIR", "").strip():
        os.environ["QT_QPA_FONTDIR"] = str(font_dir)
        updates["QT_QPA_FONTDIR"] = str(font_dir)
    if logger is not None and updates:
        try:
            logger.info("qt_runtime_env_updates=%s", updates)
        except Exception:
            pass
    return updates


def is_fatal_qt_warning(message: str) -> bool:
    text = str(message or "").strip().lower()
    if not text:
        return False
    if "unknown property qproperty-" in text:
        return False
    return any(token in text for token in FATAL_QT_WARNING_PATTERNS)
