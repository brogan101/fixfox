from __future__ import annotations

from typing import Any


def build_home_page(window: Any):
    page = window._build_home()
    page.setObjectName("PageHome")
    page.setProperty("page_id", "home")
    return page
