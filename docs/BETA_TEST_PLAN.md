# BETA Test Plan

## Scope
- Diagnose -> Fix -> Export -> History -> Re-export flow
- Share-safe masking in UI copy actions and exports
- Worker behavior (progress/cancel/no freeze)
- Safety policy and diagnostic mode gating

## Scenarios
1. Fresh launch onboarding
- Confirm 3-screen onboarding appears once
- Skip with "do not show again" and relaunch

2. Quick check and findings
- Run Quick Check from Home
- Verify Diagnose groups findings and context menu copy works

3. Fix execution
- Run a Safe fix (Flush DNS)
- Verify confirmation drawer, action log, and session action entry
- Toggle diagnostic mode ON and verify fixes are blocked

4. Export validation
- Generate Home Share pack and Ticket pack
- Verify `manifest.json`, `hashes.txt`, `summary.md`, `session.json` exist
- Verify validation status is pass

5. History reopen and re-export
- Open History
- Reopen old session
- Re-export without rerun

6. Runbooks
- Run one home runbook dry-run
- Run one IT runbook and verify batch confirmation + reboot checkbox

7. Safety and policy
- Enable safe-only and hide admin tools
- Confirm admin fixes are not visible/executable

8. Crash/logging path checks
- Use Settings -> Open Logs Folder
- Use Settings -> Copy Log Path

## Exit Criteria
- No crashes in core flow
- No UI freeze during scan/export/runbook
- All smoke/unit tests pass
