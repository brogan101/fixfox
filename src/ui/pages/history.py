from __future__ import annotations

from typing import Any


def build_history_page(window: Any):
    page = window._build_history()
    page.setObjectName("PageHistory")
    page.setProperty("page_id", "history")
    return page
