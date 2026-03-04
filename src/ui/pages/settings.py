from __future__ import annotations

from typing import Any


def build_settings_page(window: Any):
    page = window._build_settings()
    page.setObjectName("PageSettings")
    page.setProperty("page_id", "settings")
    return page
