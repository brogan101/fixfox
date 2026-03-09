# Release Checklist

## Build

- `python scripts/build_release.py`
- confirm `dist/FixFox/FixFox.exe` exists
- confirm the EXE icon matches FixFox branding in Explorer and the taskbar

## Smoke

- launch `FixFox.exe`
- confirm splash, window icon, and About dialog show the current RC metadata
- switch across Home / Playbooks / Diagnose / Fixes / Reports / History / Settings
- confirm support search returns support issues and deep playbooks for:
  - `windows update`
  - `printer offline`
  - `slow pc`
- confirm Settings shows support/export/build information

## Export / Support

- confirm logs folder is created
- confirm support bundle export route opens from Reports / Settings
- confirm a deep playbook run creates evidence under the session/export path

## Sign-off

- source app launch verified
- packaged EXE launch verified
- screenshots updated
- `docs/support_audit.json` refreshed
