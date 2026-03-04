from __future__ import annotations

from typing import Any


def build_diagnose_page(window: Any):
    page = window._build_diagnose()
    page.setObjectName("PageDiagnose")
    page.setProperty("page_id", "diagnose")
    return page
