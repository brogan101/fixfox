from __future__ import annotations

from .tokens import (
    BASE_FONT_FAMILY,
    DENSITY_TOKENS,
    ELEVATION_SCALE,
    RADIUS_SCALE,
    SPACING_SCALE,
    TYPOGRAPHY_SCALE,
    DensityTokens,
    ThemeTokens,
)

PALETTE_ORDER = ("fixfox", "graphite", "high_contrast")
_UI_SCALE_PERCENT = 100

PALETTE_LABELS: dict[str, str] = {
    "fixfox": "Fix Fox",
    "graphite": "Graphite",
    "high_contrast": "High Contrast",
}

PALETTE_ALIASES: dict[str, str] = {
    "fixfox_m3": "fixfox",
    "graphite_blue": "graphite",
    "neutral_slate": "graphite",
    "monochrome": "graphite",
    "fixfox_graphite": "graphite",
    "Fix Fox M3": "fixfox",
    "Fix Fox": "fixfox",
    "Fix Fox Graphite": "graphite",
    "Graphite": "graphite",
    "High Contrast": "high_contrast",
    "high-contrast": "high_contrast",
    "high_contrast": "high_contrast",
}

PALETTE_TOKENS_LIGHT: dict[str, ThemeTokens] = {
    "fixfox": ThemeTokens(
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
        shadow2="#EEF1F5",
    ),
    "graphite": ThemeTokens(
        bg0="#F2F4F7",
        bg1="#ECEFF3",
        panel="#FFFFFF",
        panel2="#E8ECF1",
        border="#C3CAD4",
        text="#1B2430",
        text_muted="#556272",
        accent="#2E77D0",
        accent_hover="#4288DC",
        accent_pressed="#235EA8",
        ok="#0F8A4A",
        warn="#A86412",
        crit="#BA2B2B",
        info="#2E77D0",
        shadow1="#CAD1DB",
        shadow2="#E3E7EE",
    ),
    "high_contrast": ThemeTokens(
        bg0="#FFFFFF",
        bg1="#FAFAFA",
        panel="#FFFFFF",
        panel2="#F2F2F2",
        border="#101010",
        text="#000000",
        text_muted="#1E1E1E",
        accent="#0057D8",
        accent_hover="#0B63EA",
        accent_pressed="#003EA0",
        ok="#007A33",
        warn="#8A4B00",
        crit="#B00020",
        info="#0057D8",
        shadow1="#DCDCDC",
        shadow2="#F0F0F0",
    ),
}

PALETTE_TOKENS_DARK: dict[str, ThemeTokens] = {
    "fixfox": ThemeTokens(
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
        shadow2="#1A2336",
    ),
    "graphite": ThemeTokens(
        bg0="#101317",
        bg1="#141920",
        panel="#161C25",
        panel2="#1D2631",
        border="#394657",
        text="#EAF0F7",
        text_muted="#BAC4D1",
        accent="#5A9DFF",
        accent_hover="#74AEFF",
        accent_pressed="#3D82E8",
        ok="#2FBE73",
        warn="#D89B49",
        crit="#F06A6A",
        info="#5A9DFF",
        shadow1="#0A0D12",
        shadow2="#232D39",
    ),
    "high_contrast": ThemeTokens(
        bg0="#000000",
        bg1="#050505",
        panel="#000000",
        panel2="#0A0A0A",
        border="#FFFFFF",
        text="#FFFFFF",
        text_muted="#F0F0F0",
        accent="#FFD400",
        accent_hover="#FFE062",
        accent_pressed="#E0B900",
        ok="#36FF8F",
        warn="#FFC340",
        crit="#FF6B6B",
        info="#7DC2FF",
        shadow1="#000000",
        shadow2="#121212",
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
    return "fixfox"


def palette_label(palette: str) -> str:
    return PALETTE_LABELS.get(normalize_palette(palette), PALETTE_LABELS["fixfox"])


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


def clamp_ui_scale(scale_percent: int) -> int:
    return max(90, min(125, int(scale_percent)))


def set_ui_scale_percent(scale_percent: int) -> int:
    global _UI_SCALE_PERCENT
    _UI_SCALE_PERCENT = clamp_ui_scale(scale_percent)
    return _UI_SCALE_PERCENT


def ui_scale_percent() -> int:
    return _UI_SCALE_PERCENT


def spacing_multiplier(scale_percent: int | None = None) -> float:
    pct = clamp_ui_scale(scale_percent if scale_percent is not None else _UI_SCALE_PERCENT)
    # Keep spacing adjustments subtle across the 90%-125% UI scale range.
    return 1.0 + ((pct - 100) / 400.0)


def resolve_theme_tokens(palette: str, mode: str) -> ThemeTokens:
    palette_key = normalize_palette(palette)
    mode_key = normalize_mode(mode)
    source = PALETTE_TOKENS_DARK if mode_key == "dark" else PALETTE_TOKENS_LIGHT
    return source[palette_key]


def resolve_density_tokens(density: str, scale_percent: int | None = None) -> DensityTokens:
    key = normalize_density(density)
    base = DENSITY_TOKENS[key]
    pct = clamp_ui_scale(scale_percent if scale_percent is not None else _UI_SCALE_PERCENT)
    if pct == 100:
        return base

    factor = pct / 100.0

    def _scaled(value: int, minimum: int = 1) -> int:
        return max(minimum, int(round(float(value) * factor)))

    return DensityTokens(
        font_size=_scaled(base.font_size, 8),
        nav_item_height=_scaled(base.nav_item_height, 28),
        list_row_height=_scaled(base.list_row_height, 40),
        button_height=_scaled(base.button_height, 28),
        input_height=_scaled(base.input_height, 26),
        card_padding_v=_scaled(base.card_padding_v, 8),
        card_padding_h=_scaled(base.card_padding_h, 8),
        corner_radius=_scaled(base.corner_radius, 8),
        icon_size=_scaled(base.icon_size, 14),
    )


__all__ = [
    "BASE_FONT_FAMILY",
    "TYPOGRAPHY_SCALE",
    "SPACING_SCALE",
    "RADIUS_SCALE",
    "ELEVATION_SCALE",
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
    "clamp_ui_scale",
    "set_ui_scale_percent",
    "ui_scale_percent",
    "spacing_multiplier",
    "resolve_theme_tokens",
    "resolve_density_tokens",
]
