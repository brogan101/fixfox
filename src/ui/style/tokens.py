from __future__ import annotations

import sys
from dataclasses import dataclass


if sys.platform == "win32":
    BASE_FONT_FAMILY = '"Segoe UI Variable", "Segoe UI", "Noto Sans", Arial'
else:
    BASE_FONT_FAMILY = '"Noto Sans", "DejaVu Sans", "Liberation Sans", Arial'


@dataclass(frozen=True)
class ThemeTokens:
    bg0: str
    bg1: str
    panel: str
    panel2: str
    border: str
    text: str
    text_muted: str
    accent: str
    accent_hover: str
    accent_pressed: str
    ok: str
    warn: str
    crit: str
    info: str
    shadow1: str
    shadow2: str


@dataclass(frozen=True)
class DensityTokens:
    font_size: int
    nav_item_height: int
    list_row_height: int
    button_height: int
    input_height: int
    card_padding_v: int
    card_padding_h: int
    corner_radius: int
    icon_size: int


TYPOGRAPHY_SCALE: dict[str, int] = {
    "h1": 16,
    "h2": 13,
    "h3": 12,
    "body": 11,
    "caption": 9,
}


SPACING_SCALE: dict[str, int] = {
    "xs": 4,
    "sm": 8,
    "md": 12,
    "lg": 16,
    "xl": 24,
}


RADIUS_SCALE: dict[str, int] = {
    "sm": 10,
    "md": 14,
}


ELEVATION_SCALE: dict[str, int] = {
    "raised": 1,
    "overlay": 2,
}


DENSITY_TOKENS: dict[str, DensityTokens] = {
    "comfortable": DensityTokens(
        font_size=11,
        nav_item_height=44,
        list_row_height=66,
        button_height=40,
        input_height=36,
        card_padding_v=16,
        card_padding_h=16,
        corner_radius=14,
        icon_size=18,
    ),
    "compact": DensityTokens(
        font_size=10,
        nav_item_height=34,
        list_row_height=56,
        button_height=34,
        input_height=30,
        card_padding_v=12,
        card_padding_h=12,
        corner_radius=10,
        icon_size=16,
    ),
}
