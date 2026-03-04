from __future__ import annotations

from dataclasses import dataclass
from typing import Callable

from PySide6.QtCore import QPoint
from PySide6.QtWidgets import QMenu, QWidget


@dataclass(frozen=True)
class ContextAction:
    label: str
    callback: Callable[[], None] | None = None
    enabled: bool = True
    separator: bool = False


def show_context_menu(parent: QWidget, anchor: QWidget, pos: QPoint, actions: list[ContextAction]) -> None:
    menu = QMenu(parent)
    for action in actions:
        if action.separator:
            menu.addSeparator()
            continue
        q_action = menu.addAction(action.label)
        q_action.setEnabled(action.enabled)
        if action.callback is not None:
            q_action.triggered.connect(action.callback)
    menu.exec(anchor.mapToGlobal(pos))
