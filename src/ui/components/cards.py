from __future__ import annotations

from typing import Any

from ..widgets import Card, DrawerCard, EmptyState


def section_card(title: str, subtitle: str = "", **kwargs: Any) -> Card:
    return Card(title, subtitle, **kwargs)


def details_card(title: str = "Details") -> DrawerCard:
    return DrawerCard(title)


def empty_card(title: str, subtitle: str, **kwargs: Any) -> EmptyState:
    return EmptyState(title, subtitle, **kwargs)

