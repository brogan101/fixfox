from __future__ import annotations

from dataclasses import dataclass
from typing import Any

from ..core.settings import AppSettings


@dataclass(frozen=True)
class LayoutPolicy:
    mode: str
    right_panel_default_open: bool
    show_playbooks_guided_basic: bool
    show_playbooks_pro_console: bool
    show_playbooks_segment_controls: bool
    show_script_tasks: bool
    allow_admin_tools_in_basic: bool
    force_fixes_recommended_only: bool
    show_fixes_scope_controls: bool
    show_fixes_risk_filters: bool
    show_reports_full_presets: bool
    show_reports_advanced_options: bool
    show_settings_advanced: bool
    show_run_center: bool


def _mode_key(value: Any) -> str:
    return "pro" if str(value).strip().lower() == "pro" else "basic"


def is_basic(settings_or_mode: AppSettings | str) -> bool:
    if isinstance(settings_or_mode, AppSettings):
        return _mode_key(settings_or_mode.ui_mode) == "basic"
    return _mode_key(settings_or_mode) == "basic"


def is_pro(settings_or_mode: AppSettings | str) -> bool:
    return not is_basic(settings_or_mode)


def layout_policy(settings: AppSettings) -> LayoutPolicy:
    mode = _mode_key(settings.ui_mode)
    basic = mode == "basic"
    allow_admin_in_basic = basic and bool(settings.show_admin_tools)
    return LayoutPolicy(
        mode=mode,
        right_panel_default_open=(not basic),
        show_playbooks_guided_basic=basic,
        show_playbooks_pro_console=(not basic),
        show_playbooks_segment_controls=(not basic),
        show_script_tasks=(not basic),
        allow_admin_tools_in_basic=allow_admin_in_basic,
        force_fixes_recommended_only=basic,
        show_fixes_scope_controls=(not basic),
        show_fixes_risk_filters=(not basic),
        show_reports_full_presets=(not basic),
        show_reports_advanced_options=(not basic),
        show_settings_advanced=(not basic),
        show_run_center=(not basic),
    )
