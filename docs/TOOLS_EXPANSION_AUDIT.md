# Tools Expansion Audit (Fix Fox)

Date: 2026-03-03

## Baseline Inventory (Before This Pass)

### Existing Tool/Task Surface
- Script-task execution already routed through ToolRunner from Playbooks.
- Tool directory launch actions already routed through ToolRunner.
- Evidence collector bundles already present and export-integrated.
- Global search + Ctrl+K already indexed tools/tasks/runbooks/sessions/KB/exports.

### Existing Runbook Surface
- Home runbooks: fix wifi, free space, speed up, printer rescue, browser, audio, onedrive.
- IT runbooks: ticket triage, update repair, network repair, integrity check, crash triage, usb/bt triage, office triage.

## Unfinished or Unwired Areas Found

- Missing explicit Home USB/Bluetooth runbook entry.
- Artifact naming inconsistency for key tools:
  - large file radar
  - downloads cleanup buckets
  - performance sampler
  - duplicate hash scan summary
- Capability metadata schema did not include rich fields (`plain_1liner`, `technical_detail`, `safety_note`, `next_steps`, explicit kind/category).
- No dedicated run-center style view for recent run actions.
- Smoke test coverage mismatch with requested gate (`home_fix_wifi_safe` + H03 storage radar artifact assertions).

## UI Jank / Reliability Findings

- Dense horizontal button rows at narrow widths can become crowded (top bar, context strip, task action row).
- Right panel/content competition required stricter refresh checks at resize.
- Filter/category parity needed for new task groups (`storage`, `repair`, `hardware`).

## Missing ToolRunner Integration Points

- Core integrations existed, but new task families needed verification for:
  - deterministic summaries
  - consistent next-step payloads
  - artifact output naming consistency

## Punchlist (Prioritized)

### P0
- Add missing runbook and finish new tool artifacts.
- Align smoke/unit gates to requested flows.
- Expand capability schema + regenerate catalog.

### P1
- Add Run Center view in existing nav (History/Reports scope).
- Expand task category filters for new groups.

### P2
- Additional visual polish and optional mini-toolbar refinements.

## Closure Status (This Implementation)
- P0 closed.
- P1 implemented (Run Center in History + category filter expansion).
- P2 deferred (optional, non-blocking).
