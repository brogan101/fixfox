from __future__ import annotations

from ..theme import SPACING_SCALE, resolve_density_tokens, spacing_multiplier


def spacing(key: str) -> int:
    base = int(SPACING_SCALE.get(key, SPACING_SCALE["sm"]))
    return max(2, int(round(base * spacing_multiplier())))


def tight_spacing(density: str) -> int:
    return spacing("sm") if str(density).strip().lower() == "comfortable" else spacing("xs")


def control_height(density: str) -> int:
    return resolve_density_tokens(density).button_height


def icon_size(density: str) -> int:
    return resolve_density_tokens(density).icon_size
