# Play Capability Matrix

## Automation levels

| Level | Meaning |
|---|---|
| `auto` | Fully automated local action when prerequisites are met. |
| `guided` | Guided workflow with user confirmations and checkpoints. |
| `evidence-only` | Collects evidence only; no remediation applied. |

## Capability boundaries

| Scope | What FixFox can do locally | Notes |
|---|---|---|
| Local safe diagnostics | CPU/RAM/disk checks, network snapshots, startup/system profiling, event exports | Default Basic mode surface. |
| Local guided remediation | DNS flush, selected runbooks, bounded repair chains with confirmation | Requires explicit user action. |
| Admin-required remediation | Service resets, stack resets, integrity/update repair chains | Requires elevated privileges and warnings. |
| Org/IT-owned actions | Domain policies, MDM remediation, managed identity/SSO unlocks | Export evidence + guided handoff only. |

## Admin and organization constraints

- Admin rights required: tasks marked `admin_required=true` in play metadata.
- Organization tooling required: domain unlock, MDM policy changes, enterprise identity actions.
- When admin is unavailable: FixFox provides guided steps + evidence-pack output.

## Registry contract

- Source of truth: `src/core/play_registry.py`.
- Required per-play metadata:
  - `id`
  - `title`
  - `category`
  - `risk_badge`
  - `estimated_minutes`
  - `admin_required`
  - `automation_level`
  - `entrypoint`

