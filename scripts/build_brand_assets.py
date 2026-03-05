from __future__ import annotations

from collections import Counter, deque
from pathlib import Path

try:
    from PIL import Image
except ImportError as exc:  # pragma: no cover - helper script
    raise SystemExit("Pillow is required. Install with: python -m pip install pillow") from exc


REPO_ROOT = Path(__file__).resolve().parent.parent
BRAND_DIR = REPO_ROOT / "src" / "assets" / "brand"
SOURCE = BRAND_DIR / "fixfox_logo_source.png"
MARK = BRAND_DIR / "fixfox_mark.png"
MARK_2X = BRAND_DIR / "fixfox_mark@2x.png"
ICON = BRAND_DIR / "fixfox_icon.ico"


def _resample() -> int:
    return getattr(Image, "Resampling", Image).LANCZOS


def _quantize(color: tuple[int, int, int], step: int = 16) -> tuple[int, int, int]:
    return tuple((max(0, min(255, int(c))) // step) * step for c in color)


def _color_distance(a: tuple[int, int, int], b: tuple[int, int, int]) -> int:
    return abs(a[0] - b[0]) + abs(a[1] - b[1]) + abs(a[2] - b[2])


def _dominant_border_color(image: Image.Image) -> tuple[int, int, int]:
    px = image.load()
    w, h = image.size
    colors: list[tuple[int, int, int]] = []
    for x in range(w):
        colors.append(tuple(px[x, 0][:3]))
        colors.append(tuple(px[x, h - 1][:3]))
    for y in range(h):
        colors.append(tuple(px[0, y][:3]))
        colors.append(tuple(px[w - 1, y][:3]))
    counter = Counter(_quantize(c) for c in colors)
    return max(counter.items(), key=lambda item: item[1])[0]


def _strip_edge_background(image: Image.Image, tolerance: int = 42) -> tuple[Image.Image, int]:
    rgba = image.convert("RGBA")
    px = rgba.load()
    w, h = rgba.size
    bg = _dominant_border_color(rgba)
    q: deque[tuple[int, int]] = deque()
    seen: set[tuple[int, int]] = set()
    removed = 0

    def enqueue(x: int, y: int) -> None:
        if (x, y) in seen:
            return
        seen.add((x, y))
        q.append((x, y))

    for x in range(w):
        enqueue(x, 0)
        enqueue(x, h - 1)
    for y in range(h):
        enqueue(0, y)
        enqueue(w - 1, y)

    while q:
        x, y = q.popleft()
        r, g, b, a = px[x, y]
        if a == 0:
            continue
        if _color_distance((r, g, b), bg) > tolerance:
            continue
        px[x, y] = (r, g, b, 0)
        removed += 1
        if x > 0:
            enqueue(x - 1, y)
        if x + 1 < w:
            enqueue(x + 1, y)
        if y > 0:
            enqueue(x, y - 1)
        if y + 1 < h:
            enqueue(x, y + 1)
    return rgba, removed


def _fit_square(image: Image.Image, side: int) -> Image.Image:
    side = max(64, int(side))
    rgba = image.convert("RGBA")
    bbox = rgba.getbbox()
    if bbox is None:
        raise ValueError("Source image is empty after transparency processing.")
    cropped = rgba.crop(bbox)
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    target = int(side * 0.84)
    ratio = min(target / max(1, cropped.width), target / max(1, cropped.height))
    size = (
        max(1, int(round(cropped.width * ratio))),
        max(1, int(round(cropped.height * ratio))),
    )
    resized = cropped.resize(size, _resample())
    offset = ((side - resized.width) // 2, (side - resized.height) // 2)
    canvas.alpha_composite(resized, offset)
    return canvas


def _print_size(path: Path) -> None:
    size = path.stat().st_size
    print(f"{path.relative_to(REPO_ROOT)}: {size} bytes")


def build() -> None:
    BRAND_DIR.mkdir(parents=True, exist_ok=True)
    if not SOURCE.exists():
        raise FileNotFoundError(f"Required source logo missing: {SOURCE}")
    if SOURCE.name.endswith(".png.png"):
        raise RuntimeError("Source logo still has double extension.")

    with Image.open(SOURCE) as raw:
        source_rgba, removed = _strip_edge_background(raw)
        mark = _fit_square(source_rgba, 256)
        mark2x = _fit_square(source_rgba, 512)
        mark.save(MARK, format="PNG")
        mark2x.save(MARK_2X, format="PNG")
        mark2x.save(
            ICON,
            format="ICO",
            sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
        )

    print(f"Brand asset generation success (removed_bg_pixels={removed})")
    _print_size(SOURCE)
    _print_size(MARK)
    _print_size(MARK_2X)
    _print_size(ICON)


if __name__ == "__main__":  # pragma: no cover - helper script
    build()
