from __future__ import annotations

import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from src.core.masking import MaskingOptions
from src.core.support_playbooks import deep_support_playbook_map, deep_support_playbook_stats, execute_support_playbook

OUT_PATH = REPO_ROOT / "docs" / "support_playbook_audit.json"


def main() -> int:
    selected = [
        ("identity_credential_repair", "diagnose"),
        ("network_baseline_repair", "diagnose"),
        ("outlook_mailbox_repair", "diagnose"),
        ("teams_meeting_repair", "diagnose"),
        ("profile_shell_repair", "full"),
        ("support_bundle_export_playbook", "diagnose"),
        ("windows_update_repair", "diagnose"),
    ]
    mask = MaskingOptions(enabled=True, mask_ip=True, extra_tokens=())
    runs: list[dict[str, object]] = []
    for playbook_id, mode in selected:
        payload = execute_support_playbook(
            playbook_id,
            mode=mode,
            mask_options=mask,
            timeout_s=240,
            allow_admin_actions=False,
        )
        runs.append(
            {
                "playbook_id": playbook_id,
                "mode": mode,
                "title": payload.get("title", playbook_id),
                "code": int(payload.get("code", 0)),
                "cancelled": bool(payload.get("cancelled", False)),
                "finding_count": len(payload.get("findings", [])) if isinstance(payload.get("findings", []), list) else 0,
                "evidence_files": len(payload.get("evidence_files", [])) if isinstance(payload.get("evidence_files", []), list) else 0,
                "summary_excerpt": str(payload.get("summary_text", ""))[:600],
                "evidence_root": str(payload.get("evidence_root", "")),
            }
        )
    payload = {
        "stats": deep_support_playbook_stats().__dict__,
        "playbooks": sorted(deep_support_playbook_map().keys()),
        "executed_runs": runs,
    }
    OUT_PATH.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    print(f"support_playbook_audit={OUT_PATH}")
    print(f"deep_playbooks={payload['stats']['playbook_count']}")
    print(f"executed_runs={len(runs)}")
    for row in runs:
        print(f"{row['playbook_id']} mode={row['mode']} code={row['code']} findings={row['finding_count']} evidence_files={row['evidence_files']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
