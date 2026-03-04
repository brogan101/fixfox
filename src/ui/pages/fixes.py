from __future__ import annotations

from typing import Any


def build_fixes_page(window: Any):
    page = window._build_fixes()
    page.setObjectName("PageFixes")
    page.setProperty("page_id", "fixes")
    return page
