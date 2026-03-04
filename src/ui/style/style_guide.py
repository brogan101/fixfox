from __future__ import annotations

from ..theme import SPACING_SCALE, resolve_density_tokens


def spacing(key: str) -> int:
    return int(SPACING_SCALE.get(key, SPACING_SCALE["sm"]))


def tight_spacing(density: str) -> int:
    return spacing("sm") if str(density).strip().lower() == "comfortable" else spacing("xs")


def control_height(density: str) -> int:
    return resolve_density_tokens(density).button_height


def icon_size(density: str) -> int:
    return resolve_density_tokens(density).icon_size
