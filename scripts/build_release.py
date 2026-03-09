from __future__ import annotations

import shutil
import sys
from pathlib import Path


def _repo_root() -> Path:
    return Path(__file__).resolve().parent.parent


def main() -> int:
    root = _repo_root()
    dist = root / "dist"
    build = root / "build"
    if dist.exists():
        shutil.rmtree(dist)
    if build.exists():
        shutil.rmtree(build)

    import PyInstaller.__main__

    PyInstaller.__main__.run(
        [
            "--noconfirm",
            "--clean",
            str(root / "FixFox.spec"),
        ]
    )
    exe_path = root / "dist" / "FixFox" / "FixFox.exe"
    print(f"release_exe={exe_path}")
    return 0 if exe_path.exists() else 1


if __name__ == "__main__":
    raise SystemExit(main())
