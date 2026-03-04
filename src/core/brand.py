from __future__ import annotations

from pathlib import Path


APP_NAME = "Fix Fox"
APP_DISPLAY_NAME = "Fix Fox"
APP_TAGLINE = "Fast fixes. Clean support packs."
APP_ID = "com.fixfox.utility"

EXPORT_PREFIX = "FixFox_SupportPack"
REPORT_TITLE = "Fix Fox - Support Pack"

BRAND_PRIMARY_COLOR = "#FEA643"
BRAND_ASSET_DIR = "assets/brand"
ICON_PNG = "assets/brand/fixfox.png"
ICON_ICO = "assets/brand/fixfox.ico"

DESKTOP_LOGO_FILENAME = "FixFoxLogo.png"


def src_root() -> Path:
    return Path(__file__).resolve().parent.parent


def asset_path(rel_path: str) -> Path:
    return src_root() / rel_path
