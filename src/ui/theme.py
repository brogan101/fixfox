from __future__ import annotations

from dataclasses import dataclass

BASE_FONT_FAMILY = '"Segoe UI", "Segoe UI Variable", Arial, sans-serif'

TYPOGRAPHY_SCALE: dict[str, int] = {
    "title": 22,
    "section": 14,
    "body": 13,
    "caption": 12,
}

SPACING_SCALE: dict[str, int] = {
    "xs": 6,
    "sm": 10,
    "md": 14,
    "lg": 18,
    "xl": 22,
}

RADIUS_SCALE: dict[str, int] = {
    "sm": 12,
    "md": 16,
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
        font_size=13,
        nav_item_height=40,
        list_row_height=66,
        button_height=36,
        input_height=34,
        card_padding_v=12,
        card_padding_h=14,
        corner_radius=12,
        icon_size=18,
    ),
    "compact": DensityTokens(
        font_size=12,
        nav_item_height=34,
        list_row_height=56,
        button_height=32,
        input_height=30,
        card_padding_v=10,
        card_padding_h=12,
        corner_radius=10,
        icon_size=16,
    ),
}


PALETTE_ORDER = ("fixfox_graphite", "fixfox_slate", "fixfox_indigo", "fixfox_mono")

PALETTE_LABELS: dict[str, str] = {
    "fixfox_graphite": "Fix Fox Graphite",
    "fixfox_slate": "Fix Fox Slate",
    "fixfox_indigo": "Fix Fox Indigo",
    "fixfox_mono": "Fix Fox Mono",
}

PALETTE_ALIASES: dict[str, str] = {
    "graphite_blue": "fixfox_graphite",
    "neutral_slate": "fixfox_slate",
    "indigo": "fixfox_indigo",
    "monochrome": "fixfox_mono",
    "Fix Fox Graphite": "fixfox_graphite",
    "Fix Fox Slate": "fixfox_slate",
    "Fix Fox Indigo": "fixfox_indigo",
    "Fix Fox Mono": "fixfox_mono",
}

PALETTE_TOKENS_DARK: dict[str, ThemeTokens] = {
    "fixfox_graphite": ThemeTokens(
        bg0="#0E111A",
        bg1="#111625",
        panel="#141A26",
        panel2="#1A2233",
        border="#2A344A",
        text="#E9EDF5",
        text_muted="#AAB3C5",
        accent="#FEA643",
        accent_hover="#FFB868",
        accent_pressed="#E58E2D",
        ok="#2ECC71",
        warn="#F5C542",
        crit="#FF5A5F",
        info="#4DA3FF",
        shadow1="#070A12",
        shadow2="#04060A",
    ),
    "fixfox_slate": ThemeTokens(
        bg0="#0F141C",
        bg1="#121A23",
        panel="#161F2A",
        panel2="#1C2836",
        border="#2B3A4E",
        text="#E8EEF7",
        text_muted="#A6B2C4",
        accent="#FEA643",
        accent_hover="#FFB868",
        accent_pressed="#E58E2D",
        ok="#3BD17F",
        warn="#F2C94C",
        crit="#FF6363",
        info="#5AA9FF",
        shadow1="#090E14",
        shadow2="#05080C",
    ),
    "fixfox_indigo": ThemeTokens(
        bg0="#0B1020",
        bg1="#0E1530",
        panel="#121C3D",
        panel2="#172552",
        border="#2A3A6A",
        text="#EEF2FF",
        text_muted="#B6C0E0",
        accent="#FEA643",
        accent_hover="#FFB868",
        accent_pressed="#E58E2D",
        ok="#34D399",
        warn="#FBBF24",
        crit="#FB7185",
        info="#60A5FA",
        shadow1="#060B16",
        shadow2="#03060F",
    ),
    "fixfox_mono": ThemeTokens(
        bg0="#0B0D12",
        bg1="#0F121A",
        panel="#141826",
        panel2="#1A2031",
        border="#2A3144",
        text="#F2F4F8",
        text_muted="#B2B8C7",
        accent="#FEA643",
        accent_hover="#FFB868",
        accent_pressed="#E58E2D",
        ok="#31C48D",
        warn="#FDBA74",
        crit="#F87171",
        info="#93C5FD",
        shadow1="#07090E",
        shadow2="#04060A",
    ),
}

PALETTE_TOKENS_LIGHT: dict[str, ThemeTokens] = {
    "fixfox_graphite": ThemeTokens(
        bg0="#F5F7FB",
        bg1="#EAEFF7",
        panel="#FFFFFF",
        panel2="#F3F6FC",
        border="#C9D3E3",
        text="#152033",
        text_muted="#47556C",
        accent="#C67612",
        accent_hover="#D98A2B",
        accent_pressed="#A45E0D",
        ok="#1E8E5A",
        warn="#B17A0E",
        crit="#C0392B",
        info="#2C6AC9",
        shadow1="#BFCBE0",
        shadow2="#D8E1EE",
    ),
    "fixfox_slate": ThemeTokens(
        bg0="#F4F7FA",
        bg1="#E8EEF5",
        panel="#FFFFFF",
        panel2="#F2F6FB",
        border="#C6D2E2",
        text="#172233",
        text_muted="#4A586E",
        accent="#BD7314",
        accent_hover="#D0852A",
        accent_pressed="#A06210",
        ok="#1F8A60",
        warn="#AC7912",
        crit="#BF3A34",
        info="#2F70CC",
        shadow1="#C2CDDB",
        shadow2="#DDE4EF",
    ),
    "fixfox_indigo": ThemeTokens(
        bg0="#F1F4FC",
        bg1="#E6EBF8",
        panel="#FFFFFF",
        panel2="#EEF2FC",
        border="#BCC9E5",
        text="#1A2442",
        text_muted="#4D5D84",
        accent="#B66F17",
        accent_hover="#CB832E",
        accent_pressed="#995D12",
        ok="#1E8964",
        warn="#A97612",
        crit="#B73B50",
        info="#2B63C2",
        shadow1="#C1CCE6",
        shadow2="#D9E2F3",
    ),
    "fixfox_mono": ThemeTokens(
        bg0="#F6F7FA",
        bg1="#EBEEF3",
        panel="#FFFFFF",
        panel2="#F3F5F9",
        border="#CAD0DC",
        text="#1B2130",
        text_muted="#4D576E",
        accent="#B86E16",
        accent_hover="#CD8330",
        accent_pressed="#995A11",
        ok="#1F8865",
        warn="#A9771A",
        crit="#B44343",
        info="#3B68B7",
        shadow1="#C7CEDA",
        shadow2="#DEE3EC",
    ),
}


THEME_PALETTES: dict[str, ThemeTokens] = dict(PALETTE_TOKENS_DARK)


def available_palettes() -> tuple[str, ...]:
    return PALETTE_ORDER


def available_palette_labels() -> tuple[str, ...]:
    return tuple(PALETTE_LABELS[key] for key in PALETTE_ORDER)


def palette_label(palette: str) -> str:
    return PALETTE_LABELS.get(normalize_palette(palette), PALETTE_LABELS["fixfox_graphite"])


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
    return "fixfox_graphite"


def normalize_mode(mode: str) -> str:
    return mode if mode in {"dark", "light"} else "dark"


def normalize_density(density: str) -> str:
    return density if density in DENSITY_TOKENS else "comfortable"


def resolve_theme_tokens(palette: str, mode: str) -> ThemeTokens:
    palette_key = normalize_palette(palette)
    mode_key = normalize_mode(mode)
    source = PALETTE_TOKENS_LIGHT if mode_key == "light" else PALETTE_TOKENS_DARK
    return source[palette_key]


def resolve_density_tokens(density: str) -> DensityTokens:
    return DENSITY_TOKENS[normalize_density(density)]
