from __future__ import annotations

from dataclasses import dataclass

from .theme import ThemeTokens


SPACING_SCALE_V2: dict[str, int] = {
    "xxs": 4,
    "xs": 8,
    "sm": 12,
    "md": 16,
    "lg": 24,
    "xl": 32,
}

RADII_V2: dict[str, int] = {
    "sm": 10,
    "md": 14,
    "lg": 18,
}

TYPOGRAPHY_PT_V2: dict[str, int] = {
    "xs": 12,
    "sm": 13,
    "body": 15,
    "h3": 18,
    "h2": 22,
}

FONT_WEIGHTS_QT: dict[str, int] = {
    "normal": 50,
    "medium": 57,
    "demibold": 63,
    "bold": 75,
    "extrabold": 81,
    "black": 87,
}

ELEVATION_V2: dict[str, tuple[int, int]] = {
    "0": (0, 0),
    "1": (6, 18),
    "2": (10, 28),
}


@dataclass(frozen=True)
class SemanticColorsV2:
    bg: str
    surface: str
    surface2: str
    text: str
    text2: str
    border: str
    accent: str
    accent2: str
    ok: str
    warn: str
    crit: str


def semantic_colors_from_theme(tokens: ThemeTokens) -> SemanticColorsV2:
    return SemanticColorsV2(
        bg=tokens.bg0,
        surface=tokens.panel,
        surface2=tokens.panel2,
        text=tokens.text,
        text2=tokens.text_muted,
        border=tokens.border,
        accent=tokens.accent,
        accent2=tokens.info,
        ok=tokens.ok,
        warn=tokens.warn,
        crit=tokens.crit,
    )


def token_spacing(key: str, default: str = "sm") -> int:
    return int(SPACING_SCALE_V2.get(str(key).strip().lower(), SPACING_SCALE_V2[default]))


def token_radius(key: str, default: str = "md") -> int:
    return int(RADII_V2.get(str(key).strip().lower(), RADII_V2[default]))


def token_typography(key: str, default: str = "body") -> int:
    return int(TYPOGRAPHY_PT_V2.get(str(key).strip().lower(), TYPOGRAPHY_PT_V2[default]))


def token_weight(key: str, default: str = "normal") -> int:
    return int(FONT_WEIGHTS_QT.get(str(key).strip().lower(), FONT_WEIGHTS_QT[default]))
