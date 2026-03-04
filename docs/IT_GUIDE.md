# Fix Fox IT Guide

## Goal
Provide repeatable, local-first ticket triage with validated support-pack exports.

## Recommended IT Workflow
1. Run `IT: Ticket Triage Pack` (dry run first if needed).
2. Review step-by-step output in ToolRunner.
3. Validate findings/actions in Diagnose/History.
4. Export `ticket` preset from Reports.
5. Attach ZIP + ticket summary to your service desk ticket.

## Required IT Runbooks
- IT: Ticket Triage Pack
- IT: Update Repair (Admin)
- IT: Network Stack Repair (Admin)
- IT: Integrity Check (Admin)

## Evidence Bundles in Ticket Pack
- `evidence/system`
- `evidence/network`
- `evidence/updates`
- `evidence/crash`
- `evidence/eventlogs`
- `evidence/printer` (when available)

Each bundle includes summary artifacts where applicable.

## Export Validation
Validator checks:
- required file presence
- manifest file coverage
- hash coverage and match
- share-safe leakage patterns (hostname/user path/SSID tokens)

Default behavior blocks invalid export.
An explicit override path exists with user confirmation.

## Dry Run and Admin Safety
- Runbooks support dry-run and checkpoints.
- Admin runbooks show batch confirmation with:
  - restore point option
  - reboot warning acknowledgement

## Operational Notes
- Long operations are worker-based (no UI freeze).
- Cancel/timeouts are enforced in ToolRunner.
- Partial evidence remains exportable for escalation workflows.
