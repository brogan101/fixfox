# FixFox Privacy and Local Data

FixFox stores its working data locally on the current PC under `%APPDATA%\\FixFox`.

## What FixFox stores locally

- settings and behavior profile choices
- repair receipts and runbook history
- notifications for the current support session
- startup verification and app logs
- interrupted-repair state when a workflow needs safe recovery
- support packages you explicitly create

## What support packages include

Basic support packages include:

- issue summary
- device health snapshot
- recent repair receipts
- triage summary when available
- recent alerts if enabled

Technician-level packages can include:

- richer technical receipt history
- deeper health and runbook details
- more complete post-check context

## Redaction

- machine name and user name are redacted in support summaries
- IP address is redacted in Basic support packages
- you can preview the support summary before you open or share it

## What FixFox does not do by default

- it does not upload data automatically
- it does not require an account
- it does not collect telemetry in the background
- it does not share support packages unless you explicitly open or move them yourself
