from __future__ import annotations

from typing import Any


def build_playbooks_page(window: Any):
    page = window._build_toolbox()
    page.setObjectName("PagePlaybooks")
    page.setProperty("page_id", "playbooks")
    return page
