from __future__ import annotations

from collections.abc import Callable
from PySide6.QtWidgets import QFrame, QScrollArea, QWidget

from ..components.page_primitives import PageHeader


class PageScroll(QScrollArea):
    def __init__(self, *, page_id: str, object_name: str) -> None:
        super().__init__()
        self.setObjectName(object_name)
        self.setProperty("page_id", page_id)
        self.setWidgetResizable(True)
        self.setFrameShape(QFrame.NoFrame)
        self.viewport().setObjectName("PageViewport")
        self.content = QWidget()
        self.setWidget(self.content)


def build_page_header(
    title: str,
    subtitle: str,
    *,
    cta: QWidget | None = None,
    help_text: str = "",
    on_help: Callable[[str, str], None] | None = None,
) -> QWidget:
    return PageHeader(
        title=title,
        subtitle=subtitle,
        cta=cta,
        help_text=help_text,
        on_help=on_help,
    )
