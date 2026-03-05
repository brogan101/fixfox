from __future__ import annotations

import argparse
import shutil
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError as exc:  # pragma: no cover - helper script
    raise SystemExit("Pillow is required. Install with: python -m pip install pillow") from exc


PNG_SIZES = (16, 24, 32, 48, 64, 128, 256, 512)
ICO_SIZES = (16, 24, 32, 48, 64, 128, 256)


def _resample() -> int:
    return getattr(Image, "Resampling", Image).LANCZOS


def generate_icons(source: Path, assets_dir: Path) -> None:
    if not source.exists():
        raise FileNotFoundError(f"Missing source image: {source}")
    assets_dir.mkdir(parents=True, exist_ok=True)
    png_dir = assets_dir / "png"
    png_dir.mkdir(parents=True, exist_ok=True)

    with Image.open(source) as raw:
        image = raw.convert("RGBA")
        for size in PNG_SIZES:
            target = png_dir / f"fixfox_{size}.png"
            image.resize((size, size), _resample()).save(target, format="PNG")
        ico_target = assets_dir / "fixfox.ico"
        image.save(ico_target, format="ICO", sizes=[(size, size) for size in ICO_SIZES])
        shutil.copy2(ico_target, assets_dir / "fixfox_icon.ico")

    canonical_target = (assets_dir / "fixfox.png").resolve()
    if source.resolve() != canonical_target:
        shutil.copy2(source, canonical_target)


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate Fix Fox icon sizes and ICO layers.")
    parser.add_argument("--source", default="src/assets/brand/fixfox_logo_source.png", help="Path to source brand PNG.")
    parser.add_argument("--assets-dir", default="src/assets/brand", help="Target brand assets directory.")
    parser.add_argument(
        "--sync-src-assets",
        default="src/assets/brand",
        help="Optional src/assets brand directory to mirror generated outputs.",
    )
    args = parser.parse_args()

    source = Path(args.source).resolve()
    assets_dir = Path(args.assets_dir).resolve()
    generate_icons(source, assets_dir)

    sync_dir = Path(args.sync_src_assets).resolve()
    sync_dir.mkdir(parents=True, exist_ok=True)
    if sync_dir != assets_dir:
        shutil.copy2(assets_dir / "fixfox.png", sync_dir / "fixfox.png")
        shutil.copy2(assets_dir / "fixfox.ico", sync_dir / "fixfox.ico")
        shutil.copy2(assets_dir / "fixfox_icon.ico", sync_dir / "fixfox_icon.ico")

    print(f"Generated icons in: {assets_dir}")
    print(f"Mirrored runtime icons to: {sync_dir}")
    return 0


if __name__ == "__main__":  # pragma: no cover - helper script
    raise SystemExit(main())
