from __future__ import annotations

import sys
from pathlib import Path

from PySide6.QtGui import QFont

from ..core.utils import resource_path


def clamp_point_size(ps: int | None, default_ps: int = 12) -> int:
    if ps is None or int(ps) <= 0:
        return int(default_ps)
    return int(ps)


def safe_copy_font(base: QFont, default_ps: int = 12) -> QFont:
    copied = QFont(base)
    copied.setPointSize(clamp_point_size(copied.pointSize(), default_ps))
    return copied


def font_asset_candidates(font_filename: str = "NotoSans-Regular.ttf") -> list[Path]:
    candidates: list[Path] = []
    src_root = Path(__file__).resolve().parent.parent
    candidates.append(src_root / "assets" / "fonts" / font_filename)

    meipass = getattr(sys, "_MEIPASS", None)
    if meipass:
        candidates.append(Path(meipass) / "assets" / "fonts" / font_filename)

    try:
        candidates.append(Path(resource_path(f"assets/fonts/{font_filename}")))
    except Exception:
        pass

    deduped: list[Path] = []
    seen: set[str] = set()
    for path in candidates:
        key = str(path.resolve()) if path.exists() else str(path)
        if key in seen:
            continue
        seen.add(key)
        deduped.append(path)
    return deduped
