from __future__ import annotations

from PySide6.QtGui import QFont


def clamp_point_size(ps: int | None, default_ps: int = 12) -> int:
    if ps is None or int(ps) <= 0:
        return int(default_ps)
    return int(ps)


def safe_copy_font(base: QFont, default_ps: int = 12) -> QFont:
    copied = QFont(base)
    copied.setPointSize(clamp_point_size(copied.pointSize(), default_ps))
    return copied
