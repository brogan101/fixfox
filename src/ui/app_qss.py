from __future__ import annotations

# Compatibility shim: keep legacy import path while style modules are canonical.
from .style.qss_builder import build_qss

__all__ = ["build_qss"]

