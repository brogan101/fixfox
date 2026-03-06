from __future__ import annotations

import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parent.parent
REPORT_PATH = REPO_ROOT / "docs" / "font_sanity_report.txt"

if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from src.core.diagnostics.font_sanity import run_font_sanity


def main() -> int:
    result = run_font_sanity(report_path=REPORT_PATH, verbose=True)
    return 0 if result.ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
