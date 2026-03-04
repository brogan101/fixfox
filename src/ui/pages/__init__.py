from __future__ import annotations

from .diagnose import build_diagnose_page
from .fixes import build_fixes_page
from .history import build_history_page
from .home import build_home_page
from .playbooks import build_playbooks_page
from .reports import build_reports_page
from .settings import build_settings_page

__all__ = [
    "build_home_page",
    "build_playbooks_page",
    "build_diagnose_page",
    "build_fixes_page",
    "build_reports_page",
    "build_history_page",
    "build_settings_page",
]
