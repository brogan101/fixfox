from __future__ import annotations

import logging
import os
import shutil
from pathlib import Path

from .brand import DESKTOP_LOGO_FILENAME, ICON_PNG
from .utils import resource_path


_LOGGER = logging.getLogger("fixfox.brand")


def desktop_path() -> Path:
    profile = Path(os.environ.get("USERPROFILE", str(Path.home())))
    return profile / "Desktop"


def logo_source_path() -> Path:
    return Path(resource_path(ICON_PNG))


def ensure_logo_on_desktop(*, overwrite: bool = False) -> tuple[Path, bool]:
    source = logo_source_path()
    if not source.exists():
        raise FileNotFoundError(f"Brand icon not found: {source}")
    target = desktop_path() / DESKTOP_LOGO_FILENAME
    if target.exists() and not overwrite:
        _LOGGER.info("Desktop logo exists and overwrite disabled: %s", target)
        return target, False
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)
    _LOGGER.info("Desktop logo created: %s", target)
    return target, True
