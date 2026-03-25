# FixFox Troubleshooting and FAQ

## FixFox says a repair failed

Read the receipt or support summary first. FixFox should tell you:

- what step failed
- what was not changed
- whether verification passed
- whether rollback is available
- what to try next

If the issue points to policy, sign-in, permissions, certificates, or destructive recovery, create a support package and escalate.

## FixFox starts but looks empty

Run a Quick Scan from Home. If the app still shows no useful state:

- open Settings and review the profile and landing page
- open Device Health to refresh the system snapshot
- open Activity to confirm whether anything ran recently

## FixFox was closed during a workflow

Reopen the app and review the recovery notice. Fresh guided work can be restored. Stale or unsafe interrupted work is kept for inspection instead of resuming automatically.

## Update checks are unavailable

FixFox can keep working without the update feed. Open Settings to review the current update status and release notes path.

## Support package creation failed

Check that:

- `%APPDATA%\FixFox` is writable
- the disk is not full
- antivirus or policy is not blocking the output folder

If needed, open the data folder from Settings and review the app log.

## A Windows tool would not open

That usually means the machine is managed, the tool is blocked by policy, or Windows does not expose that page on the current build. Use the related support center, then create a support package if the block matters to the repair.

## When to stop using FixFox

Stop and escalate when:

- Windows startup or recovery is the next step
- the device is managed and policy keeps reversing changes
- sign-in, MFA, certificates, or permissions are the actual blocker
- the issue points to reinstall or hardware failure
