# Safety

## Risk Levels
- Safe
  - Read-only diagnostics and low-risk helper actions
  - Default mode
- Admin
  - Elevated actions that may restart services or change system state
  - Explicit confirmation required
- Advanced
  - Expert-level workflows for experienced users

## Admin Guardrails
- Admin steps are policy-gated.
- Runbook admin flows include:
  - restore-point option (best effort)
  - reboot acknowledgement where applicable

## Rollback and Reversibility
- Fixes view includes rollback guidance in detail pane.
- Rollback center lists reversible session actions where mappings exist.

## Operational Safety Rules
- No fake optimization or scareware behavior.
- No silent deletion workflows.
- Preview-first behavior is used for cleanup-oriented tooling.
- Recycle Bin approach is used where applicable and supported.
