from __future__ import annotations

import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from src.core.brand import APP_DISPLAY_NAME
from src.core.version import APP_VERSION


def _version_tuple() -> tuple[int, int, int, int]:
    parts = [int(piece) for piece in re.findall(r"\d+", APP_VERSION)[:4]]
    while len(parts) < 4:
        parts.append(0)
    return tuple(parts[:4])  # type: ignore[return-value]


def build_version_file(path: Path) -> Path:
    v0, v1, v2, v3 = _version_tuple()
    content = f"""# UTF-8
VSVersionInfo(
  ffi=FixedFileInfo(
    filevers=({v0}, {v1}, {v2}, {v3}),
    prodvers=({v0}, {v1}, {v2}, {v3}),
    mask=0x3f,
    flags=0x0,
    OS=0x40004,
    fileType=0x1,
    subtype=0x0,
    date=(0, 0)
  ),
  kids=[
    StringFileInfo([
      StringTable(
        '040904B0',
        [
          StringStruct('CompanyName', '{APP_DISPLAY_NAME}'),
          StringStruct('FileDescription', '{APP_DISPLAY_NAME}'),
          StringStruct('FileVersion', '{APP_VERSION}'),
          StringStruct('InternalName', 'FixFox'),
          StringStruct('OriginalFilename', 'FixFox.exe'),
          StringStruct('ProductName', '{APP_DISPLAY_NAME}'),
          StringStruct('ProductVersion', '{APP_VERSION}')
        ]
      )
    ]),
    VarFileInfo([VarStruct('Translation', [1033, 1200])])
  ]
)
"""
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")
    return path


def main() -> int:
    target = Path("packaging") / "windows_version.txt"
    build_version_file(target)
    print(f"Wrote version metadata: {target}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
