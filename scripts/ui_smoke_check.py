from __future__ import annotations

import os
import sys
from pathlib import Path

from PySide6.QtWidgets import QApplication, QWidget


REQUIRED_ICONS = (
    "home",
    "playbooks",
    "diagnose",
    "fixes",
    "reports",
    "history",
    "settings",
    "help",
    "search",
    "run",
    "stop",
    "export",
    "panel",
    "overflow",
    "pin",
    "close",
    "info",
    "shield",
    "privacy",
)


def _repo_root() -> Path:
    return Path(__file__).resolve().parent.parent


REPO_ROOT = _repo_root()
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))


def _check_icons(root: Path) -> list[str]:
    failures: list[str] = []
    icon_dir = root / "src" / "assets" / "icons"
    for name in REQUIRED_ICONS:
        if not (icon_dir / f"{name}.svg").exists() and not (icon_dir / f"{name}.png").exists():
            failures.append(f"missing icon asset: {name}")
    return failures


def _check_forbidden_strings(root: Path) -> list[str]:
    failures: list[str] = []
    for path in (root / "src" / "ui").rglob("*.py"):
        text = path.read_text(encoding="utf-8")
        if "About Qt" in text:
            failures.append(f"forbidden user-facing string 'About Qt' found in {path}")
    return failures


def _check_shell_runtime() -> list[str]:
    failures: list[str] = []
    os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
    os.environ["FIXFOX_SKIP_ONBOARDING"] = "1"

    from src.ui.main_window import MainWindow

    app = QApplication.instance() or QApplication([])
    window = MainWindow()
    window.show()
    app.processEvents()
    try:
        nav_rails = [w for w in window.findChildren(QWidget) if w.objectName() == "NavRail"]
        if len(nav_rails) != 1:
            failures.append(f"expected exactly one NavRail, found {len(nav_rails)}")
        for button in (window.btn_export, window.btn_overflow, window.compact_search_btn):
            if button.icon().isNull():
                failures.append(f"toolbar icon missing for {button.objectName() or button.__class__.__name__}")
    finally:
        window.close()
        app.processEvents()
    return failures


def main() -> int:
    root = _repo_root()
    failures = []
    failures.extend(_check_icons(root))
    failures.extend(_check_forbidden_strings(root))
    failures.extend(_check_shell_runtime())
    if failures:
        print("UI smoke check: FAIL")
        for row in failures:
            print(f"- {row}")
        return 1
    print("UI smoke check: PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
