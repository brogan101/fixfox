from __future__ import annotations

from dataclasses import dataclass

BASE_FONT_FAMILY = '"Segoe UI", "Segoe UI Variable", Arial, sans-serif'

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
    "xxl": 32,
}

RADIUS_SCALE: dict[str, int] = {
    "sm": 10,
    "md": 14,
    "lg": 18,
}


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


PALETTE_ORDER = ("fixfox_m3",)

PALETTE_LABELS: dict[str, str] = {
    "fixfox_m3": "Fix Fox M3",
}

PALETTE_ALIASES: dict[str, str] = {
    "graphite_blue": "fixfox_m3",
    "neutral_slate": "fixfox_m3",
    "indigo": "fixfox_m3",
    "monochrome": "fixfox_m3",
    "Fix Fox Graphite": "fixfox_m3",
    "Fix Fox Slate": "fixfox_m3",
    "Fix Fox Indigo": "fixfox_m3",
    "Fix Fox Mono": "fixfox_m3",
    "Fix Fox M3": "fixfox_m3",
    "fixfox_graphite": "fixfox_m3",
    "fixfox_slate": "fixfox_m3",
    "fixfox_indigo": "fixfox_m3",
    "fixfox_mono": "fixfox_m3",
}

PALETTE_TOKENS_DARK: dict[str, ThemeTokens] = {
    "fixfox_m3": ThemeTokens(
        bg0="#0B0D12",
        bg1="#0E1118",
        panel="#101521",
        panel2="#141B2B",
        border="#2A344A",
        text="#E9EDF5",
        text_muted="#AAB3C5",
        accent="#FEA643",
        accent_hover="#FFB868",
        accent_pressed="#E58E2D",
        ok="#16A34A",
        warn="#D97706",
        crit="#DC2626",
        info="#2563EB",
        shadow1="#07090E",
        shadow2="#04060A",
    ),
}

PALETTE_TOKENS_LIGHT: dict[str, ThemeTokens] = {
    "fixfox_m3": ThemeTokens(
        bg0="#FAFAFA",
        bg1="#F6F7F9",
        panel="#FFFFFF",
        panel2="#F6F7F9",
        border="#DDE3EA",
        text="#111827",
        text_muted="#4B5563",
        accent="#FEA643",
        accent_hover="#FFB868",
        accent_pressed="#E58E2D",
        ok="#16A34A",
        warn="#D97706",
        crit="#DC2626",
        info="#2563EB",
        shadow1="#CFD6DF",
        shadow2="#E6E9EE",
    ),
}


THEME_PALETTES: dict[str, ThemeTokens] = dict(PALETTE_TOKENS_DARK)


def available_palettes() -> tuple[str, ...]:
    return PALETTE_ORDER


def available_palette_labels() -> tuple[str, ...]:
    return tuple(PALETTE_LABELS[key] for key in PALETTE_ORDER)


def palette_label(palette: str) -> str:
    return PALETTE_LABELS.get(normalize_palette(palette), PALETTE_LABELS["fixfox_m3"])


def palette_key_from_label(label: str) -> str:
    if label in PALETTE_LABELS.values():
        for key, value in PALETTE_LABELS.items():
            if value == label:
                return key
    return normalize_palette(label)


def normalize_palette(palette: str) -> str:
    if palette in PALETTE_TOKENS_DARK:
        return palette
    mapped = PALETTE_ALIASES.get(palette, "")
    if mapped in PALETTE_TOKENS_DARK:
        return mapped
    return "fixfox_m3"


def normalize_mode(mode: str) -> str:
    return mode if mode in {"dark", "light"} else "light"


def normalize_density(density: str) -> str:
    return density if density in DENSITY_TOKENS else "comfortable"


def resolve_theme_tokens(palette: str, mode: str) -> ThemeTokens:
    palette_key = normalize_palette(palette)
    mode_key = normalize_mode(mode)
    source = PALETTE_TOKENS_LIGHT if mode_key == "light" else PALETTE_TOKENS_DARK
    return source[palette_key]


def resolve_density_tokens(density: str) -> DensityTokens:
    return DENSITY_TOKENS[normalize_density(density)]
