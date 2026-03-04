from __future__ import annotations

import json
from dataclasses import dataclass, asdict
from pathlib import Path
from typing import Any

from .paths import settings_path


@dataclass
class AppSettings:
    share_safe_default: bool = True
    mask_ip_default: bool = False
    right_panel_open: bool = True
    onboarding_completed: bool = False
    onboarding_goal: str = "speed"
    diagnostic_mode: bool = False
    safe_only_mode: bool = True
    show_admin_tools: bool = False
    show_advanced_tools: bool = False
    weekly_reminder_enabled: bool = False
    weekly_reminder_day: int = 0
    last_weekly_check_utc: str = ""
    pinned_actions: list[str] | None = None
    theme_palette: str = "fixfox_m3"
    theme_mode: str = "light"
    density: str = "comfortable"
    ui_mode: str = "basic"
    favorites_fixes: list[str] | None = None
    favorites_tools: list[str] | None = None
    favorites_runbooks: list[str] | None = None
    file_index_roots: list[str] | None = None

    def normalized(self) -> "AppSettings":
        self.ui_mode = "pro" if str(self.ui_mode).strip().lower() == "pro" else "basic"
        if self.pinned_actions is None:
            self.pinned_actions = []
        if self.favorites_fixes is None:
            self.favorites_fixes = []
        if self.favorites_tools is None:
            self.favorites_tools = []
        if self.favorites_runbooks is None:
            self.favorites_runbooks = []
        if self.file_index_roots is None:
            self.file_index_roots = []
        return self


def _coerce_settings(data: dict[str, Any]) -> AppSettings:
    base = asdict(AppSettings())
    base.update({k: v for k, v in data.items() if k in base})
    return AppSettings(**base).normalized()


def load_settings() -> AppSettings:
    path = settings_path()
    if not path.exists():
        return AppSettings().normalized()
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return AppSettings().normalized()
    if not isinstance(payload, dict):
        return AppSettings().normalized()
    return _coerce_settings(payload)


def save_settings(settings: AppSettings) -> Path:
    path = settings_path()
    normalized = settings.normalized()
    path.write_text(json.dumps(asdict(normalized), indent=2), encoding="utf-8")
    try:
        from .db import replace_all_favorites, set_file_index_roots

        replace_all_favorites(
            fixes=normalized.favorites_fixes or [],
            tools=normalized.favorites_tools or [],
            runbooks=normalized.favorites_runbooks or [],
        )
        set_file_index_roots(normalized.file_index_roots or [])
    except Exception:
        pass
    return path
