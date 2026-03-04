from __future__ import annotations

from .tokens import (
    BASE_FONT_FAMILY,
    DENSITY_TOKENS,
    RADIUS_SCALE,
    SPACING_SCALE,
    TYPOGRAPHY_SCALE,
    DensityTokens,
    ThemeTokens,
)

PALETTE_ORDER = ("fixfox_m3",)

PALETTE_LABELS: dict[str, str] = {
    "fixfox_m3": "Fix Fox M3",
}

PALETTE_ALIASES: dict[str, str] = {
    "graphite_blue": "fixfox_m3",
    "neutral_slate": "fixfox_m3",
    "indigo": "fixfox_m3",
    "monochrome": "fixfox_m3",
    "fixfox_graphite": "fixfox_m3",
    "fixfox_slate": "fixfox_m3",
    "fixfox_indigo": "fixfox_m3",
    "fixfox_mono": "fixfox_m3",
    "Fix Fox M3": "fixfox_m3",
    "Fix Fox Graphite": "fixfox_m3",
    "Fix Fox Slate": "fixfox_m3",
    "Fix Fox Indigo": "fixfox_m3",
    "Fix Fox Mono": "fixfox_m3",
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

THEME_PALETTES: dict[str, ThemeTokens] = dict(PALETTE_TOKENS_LIGHT)


def available_palettes() -> tuple[str, ...]:
    return PALETTE_ORDER


def available_palette_labels() -> tuple[str, ...]:
    return tuple(PALETTE_LABELS[key] for key in PALETTE_ORDER)


def normalize_palette(palette: str) -> str:
    if palette in PALETTE_TOKENS_LIGHT:
        return palette
    mapped = PALETTE_ALIASES.get(palette, "")
    if mapped in PALETTE_TOKENS_LIGHT:
        return mapped
    return "fixfox_m3"


def palette_label(palette: str) -> str:
    return PALETTE_LABELS.get(normalize_palette(palette), PALETTE_LABELS["fixfox_m3"])


def palette_key_from_label(label: str) -> str:
    if label in PALETTE_LABELS.values():
        for key, value in PALETTE_LABELS.items():
            if value == label:
                return key
    return normalize_palette(label)


def normalize_mode(mode: str) -> str:
    return mode if mode in {"dark", "light"} else "light"


def normalize_density(density: str) -> str:
    return density if density in DENSITY_TOKENS else "comfortable"


def resolve_theme_tokens(palette: str, mode: str) -> ThemeTokens:
    palette_key = normalize_palette(palette)
    mode_key = normalize_mode(mode)
    source = PALETTE_TOKENS_DARK if mode_key == "dark" else PALETTE_TOKENS_LIGHT
    return source[palette_key]


def resolve_density_tokens(density: str) -> DensityTokens:
    return DENSITY_TOKENS[normalize_density(density)]


__all__ = [
    "BASE_FONT_FAMILY",
    "TYPOGRAPHY_SCALE",
    "SPACING_SCALE",
    "RADIUS_SCALE",
    "ThemeTokens",
    "DensityTokens",
    "PALETTE_ORDER",
    "PALETTE_LABELS",
    "PALETTE_ALIASES",
    "PALETTE_TOKENS_LIGHT",
    "PALETTE_TOKENS_DARK",
    "THEME_PALETTES",
    "available_palettes",
    "available_palette_labels",
    "normalize_palette",
    "palette_label",
    "palette_key_from_label",
    "normalize_mode",
    "normalize_density",
    "resolve_theme_tokens",
    "resolve_density_tokens",
]
