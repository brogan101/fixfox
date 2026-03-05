from __future__ import annotations

from collections.abc import Callable
from PySide6.QtCore import Qt
from PySide6.QtWidgets import QFrame, QHBoxLayout, QLabel, QScrollArea, QVBoxLayout, QWidget

from ..components.rows import IconButton
from ..style import spacing


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
    widget = QWidget()
    layout = QVBoxLayout(widget)
    layout.setContentsMargins(0, 0, 0, 0)
    layout.setSpacing(spacing("sm"))
    top = QHBoxLayout()
    labels = QVBoxLayout()
    labels.setContentsMargins(0, 0, 0, 0)
    labels.setSpacing(spacing("xs"))
    title_label = QLabel(title)
    title_label.setObjectName("Title")
    subtitle_label = QLabel(subtitle)
    subtitle_label.setObjectName("SubTitle")
    labels.addWidget(title_label)
    labels.addWidget(subtitle_label)
    top.addLayout(labels, 1)
    help_btn = IconButton("help", widget, f"{title} help")
    if on_help is not None:
        help_btn.clicked.connect(lambda: on_help(title, help_text or subtitle))
    top.addWidget(help_btn, 0, Qt.AlignTop)
    if cta is not None:
        top.addWidget(cta, 0, Qt.AlignRight | Qt.AlignTop)
    layout.addLayout(top)
    return widget
