# FixFox User Guide

## 1. Start
```powershell
.venv\Scripts\python.exe -m src.app
```

- Launch behavior:
  - FixFox opens directly into the main shell.
  - A branded splash screen appears immediately while the shell and saved settings load.
  - There is no separate onboarding flow.

## 2. Home-First Workflow
1. Open Home and choose a goal card:
   - Fix Wi-Fi
   - Free Up Space
   - Speed Up PC
   - Printer Rescue
2. Review findings in Diagnose.
3. Run safe fixes or runbooks from Playbooks/Fixes.
4. Create a support bundle from Reports (`home_share` for normal sharing, `ticket` for escalation).

## 3. Quick Actions and Search
- Pin commonly used actions to Home Quick Actions.
- Use top search or `Ctrl+K` command palette for tools, runbooks, fixes, sessions, and KB.

## 4. ToolRunner Basics
All long operations run in ToolRunner with:
- progress, cancel, timeout handling
- live log stream
- plain-English summary + next steps
- export shortcut
- artifact save/copy actions

## 5. Reports and Support Bundles
Reports is a 3-step flow:
1. Configure (preset + masking + logs)
2. Preview (redaction + evidence checklist)
3. Create bundle (validation + post-bundle actions)

Presets:
- `home_share`: light, share-safe default
- `ticket`: MSP/IT handoff with full evidence bundles
- `full`: broadest package

## 6. Safety and Privacy Defaults
- Safe-only mode is on by default.
- Admin operations require explicit confirmation.
- Share-safe masking is applied to copy/export surfaces and generated text artifacts.

## 7. Screenshot Capture Plan
- Capture checklist is maintained in:
  - `packaging/listing/screenshots_plan.md`

## 8. Logs and Support
- Logs:
  - `%LOCALAPPDATA%\FixFox\logs\fixfox.log`
  - `%LOCALAPPDATA%\FixFox\logs\crash.log`
- Settings:
  - `%LOCALAPPDATA%\FixFox\state\settings.json`
- Support bundles:
  - `%LOCALAPPDATA%\FixFox\exports`

## 9. Desktop Logo Utility
- Settings -> Advanced includes:
  - `Create Desktop Logo` (no overwrite)
  - `Recreate Desktop Logo` (overwrite)
- Output file:
  - `Desktop\FixFoxLogo.png`
