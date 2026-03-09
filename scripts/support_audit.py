from __future__ import annotations

import json
import sys
from collections import Counter
from datetime import datetime
from pathlib import Path


def _repo_root() -> Path:
    return Path(__file__).resolve().parent.parent


def _ensure_repo_on_path() -> None:
    root = str(_repo_root())
    if root not in sys.path:
        sys.path.insert(0, root)


def main() -> int:
    _ensure_repo_on_path()

    from src.core.support_catalog import (
        catalog_stats,
        diagnostics_for_issue,
        family_issue_counts,
        fixes_for_issue,
        issue_map,
        list_families,
        playbook_issue_counts,
        playbooks_for_issue,
    )
    from src.core.support_playbooks import deep_support_playbook_map, deep_support_playbook_stats, execute_support_playbook
    from src.core.search import query_index

    docs_dir = _repo_root() / "docs"
    docs_dir.mkdir(parents=True, exist_ok=True)
    run_root = docs_dir / "support_audit_runs" / datetime.now().strftime("%Y%m%d_%H%M%S")
    run_root.mkdir(parents=True, exist_ok=True)

    stats = catalog_stats()
    deep = deep_support_playbook_stats()
    issues = list(issue_map().values())
    playbook_counts = playbook_issue_counts()
    missing = [
        issue.id
        for issue in issues
        if not issue.playbook_ids or not playbooks_for_issue(issue.id) or not diagnostics_for_issue(issue.id) or not fixes_for_issue(issue.id)
    ]

    family_rows = []
    for family in list_families():
        issue_count = family_issue_counts().get(family.id, 0)
        family_rows.append(
            {
                "id": family.id,
                "code": family.code,
                "title": family.title,
                "issue_count": issue_count,
            }
        )

    sample_execution_ids = [
        "identity_credential_repair",
        "network_baseline_repair",
        "outlook_mailbox_repair",
        "windows_update_repair",
    ]
    execution_rows = []
    for playbook_id in sample_execution_ids:
        payload = execute_support_playbook(
            playbook_id,
            mode="diagnose",
            allow_admin_actions=False,
            output_root=run_root / playbook_id,
            mask_options=None,
            timeout_s=180,
        )
        execution_rows.append(
            {
                "playbook_id": playbook_id,
                "title": payload.get("title", playbook_id),
                "status": "fail" if int(payload.get("code", 0)) else "pass",
                "finding_count": len(payload.get("findings", [])),
                "evidence_root": payload.get("evidence_root", ""),
                "support_bundle_integrated": bool(payload.get("support_bundle_integrated", False)),
            }
        )

    search_terms = [
        "internet not working",
        "wifi",
        "vpn",
        "outlook password",
        "teams camera",
        "printer offline",
        "slow pc",
        "full disk",
        "bitlocker",
        "windows update",
    ]
    search_rows = {
        term: [{"kind": item.kind, "key": item.key, "title": item.title} for item in query_index(term, limit=5)]
        for term in search_terms
    }

    issue_type_counter = Counter()
    for issue in issues:
        if issue.network_required:
            issue_type_counter["network_required"] += 1
        if issue.reboot_required:
            issue_type_counter["reboot_required"] += 1
        issue_type_counter[str(issue.permissions or "unknown")] += 1

    payload = {
        "generated_at": datetime.now().isoformat(),
        "catalog_stats": stats.__dict__,
        "deep_stats": deep.__dict__,
        "missing_issue_paths": missing,
        "family_rows": family_rows,
        "playbook_issue_counts": dict(sorted(playbook_counts.items())),
        "deep_playbook_ids": sorted(deep_support_playbook_map().keys()),
        "search_samples": search_rows,
        "execution_samples": execution_rows,
        "issue_type_counter": dict(issue_type_counter),
    }

    out_file = docs_dir / "support_audit.json"
    out_file.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    print(f"support_audit={out_file}")
    print(
        "coverage "
        f"issues={stats.issue_count} families={stats.family_count} playbooks={stats.playbook_count} "
        f"deep_playbooks={deep.playbook_count} diagnostics={stats.diagnostic_count} fixes={stats.fix_count} missing={len(missing)}"
    )
    for row in execution_rows:
        print(
            f"exec {row['playbook_id']} status={row['status']} findings={row['finding_count']} bundle={row['support_bundle_integrated']}"
        )
    return 0 if not missing else 1


if __name__ == "__main__":
    raise SystemExit(main())
