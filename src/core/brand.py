from __future__ import annotations

from pathlib import Path


APP_NAME = "FixFox"
APP_DISPLAY_NAME = "FixFox"
APP_TAGLINE = "Fast fixes. Clean support packs."
APP_ID = "com.fixfox.utility"

EXPORT_PREFIX = "FixFox_SupportBundle"
REPORT_TITLE = "FixFox Support Bundle"

BRAND_PRIMARY_COLOR = "#FEA643"
BRAND_ASSET_DIR = "assets/brand"
ICON_SOURCE_PNG = "assets/brand/fixfox_logo_source.png"
ICON_PNG = "assets/brand/fixfox_mark.png"
ICON_PNG_2X = "assets/brand/fixfox_mark@2x.png"
ICON_ICO = "assets/brand/fixfox_icon.ico"

DESKTOP_LOGO_FILENAME = "FixFoxLogo.png"


def src_root() -> Path:
    return Path(__file__).resolve().parent.parent


def asset_path(rel_path: str) -> Path:
    return src_root() / rel_path
