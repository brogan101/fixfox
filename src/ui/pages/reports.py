from __future__ import annotations

from typing import Any


def build_reports_page(window: Any):
    page = window._build_reports()
    page.setObjectName("PageReports")
    page.setProperty("page_id", "reports")
    return page
