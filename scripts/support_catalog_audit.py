from __future__ import annotations

import json
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from src.core.support_catalog import export_catalog_summary


OUT_PATH = REPO_ROOT / "docs" / "support_catalog_report.json"


def main() -> int:
    payload = export_catalog_summary()
    OUT_PATH.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    print(f"support_catalog_report={OUT_PATH}")
    stats = payload.get("stats", {})
    print(
        "support_catalog_stats="
        f"issues={stats.get('issue_count', 0)} "
        f"families={stats.get('family_count', 0)} "
        f"playbooks={stats.get('playbook_count', 0)} "
        f"diagnostics={stats.get('diagnostic_count', 0)} "
        f"fixes={stats.get('fix_count', 0)}"
    )
    errors = payload.get("validation_errors", [])
    if errors:
        print(f"validation_errors={len(errors)}")
        for row in errors[:20]:
            print(f"- {row}")
        return 1
    print("validation_errors=0")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
