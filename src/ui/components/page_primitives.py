from __future__ import annotations

from collections.abc import Callable

from PySide6.QtCore import Qt
from PySide6.QtGui import QFont
from PySide6.QtWidgets import QHBoxLayout, QLabel, QVBoxLayout, QWidget

from ..style import spacing, token_typography, token_weight
from .rows import IconButton


class SectionTitle(QLabel):
    def __init__(self, text: str) -> None:
        super().__init__(str(text or "").strip())
        self.setObjectName("SectionTitle")
        font = self.font()
        font.setPointSize(token_typography("sm"))
        font.setWeight(QFont.Weight(token_weight("demibold")))
        self.setFont(font)


class PageHeader(QWidget):
    def __init__(
        self,
        *,
        title: str,
        subtitle: str = "",
        cta: QWidget | None = None,
        help_text: str = "",
        on_help: Callable[[str, str], None] | None = None,
    ) -> None:
        super().__init__()
        root = QVBoxLayout(self)
        root.setContentsMargins(0, 0, 0, 0)
        root.setSpacing(spacing("xs"))

        row = QHBoxLayout()
        row.setContentsMargins(0, 0, 0, 0)
        row.setSpacing(spacing("sm"))

        labels = QVBoxLayout()
        labels.setContentsMargins(0, 0, 0, 0)
        labels.setSpacing(spacing("xxs"))

        self.title_label = QLabel(str(title or "").strip())
        self.title_label.setObjectName("Title")
        title_font = self.title_label.font()
        title_font.setPointSize(token_typography("h2"))
        title_font.setWeight(QFont.Weight(token_weight("demibold")))
        self.title_label.setFont(title_font)

        self.subtitle_label = QLabel(str(subtitle or "").strip())
        self.subtitle_label.setObjectName("SubTitle")
        self.subtitle_label.setWordWrap(True)
        sub_font = self.subtitle_label.font()
        sub_font.setPointSize(token_typography("sm"))
        sub_font.setWeight(QFont.Weight(token_weight("normal")))
        self.subtitle_label.setFont(sub_font)

        labels.addWidget(self.title_label)
        labels.addWidget(self.subtitle_label)

        row.addLayout(labels, 1)
        if cta is not None:
            row.addWidget(cta, 0, Qt.AlignRight | Qt.AlignTop)
        help_btn = IconButton("help", self, f"{title} help")
        if on_help is not None:
            help_btn.clicked.connect(lambda: on_help(str(title), str(help_text or subtitle)))
        row.addWidget(help_btn, 0, Qt.AlignTop)
        root.addLayout(row)
