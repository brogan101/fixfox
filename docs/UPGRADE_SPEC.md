# PC Concierge — Codex Upgrade Spec (v1 → Sell Quality)

## North-star
- Looks/feels like a Windows 11 Fluent app (calm, consistent spacing, real typography)
- Every action is explainable + logged
- Safe-by-default, reversible (undo/restore point where applicable)
- Generates clean support packs (HTML + JSON + logs + optional event logs)

## MUST DO (v1.1)
1) Save full session JSON: `%LOCALAPPDATA%\PCConcierge\sessions\<session_id>.json`
2) History can reopen a session and export without rerun
3) Export options + share-safe masking (computer name, username, SSID)
4) Fix confirmation drawer: “what will change / risk / rollback”
5) Deeper diagnostics:
   - Storage: top folders (sizes) for Downloads + AppData caches
   - Performance: top CPU/RAM processes
   - Printer: spooler status + queue count
   - Updates: pending reboot + update service state
6) UI polish:
   - consistent spacing grid, typography scale, proper empty/error states

## SELL QUALITY (v2)
- Installer (MSI or MSIX) + code signing
- Playbooks (preset bundles)
- Command palette (Ctrl+K), search, keyboard nav
- Rich support pack: optional event logs, update history, drivers, actions taken

## Codex prompt (copy/paste)
You are upgrading an existing Python/PySide6 desktop app called PC Concierge.
Rules:
- Keep the app runnable at all times.
- Avoid risky "registry cleaning" or fake optimization.
- Every fix must be logged and reversible when possible.
- Prioritize UI polish: spacing, typography, consistent component styling.
Tasks:
1) Implement full session persistence (save session JSON files, history can reopen sessions and export).
2) Add export options and share-safe mode masking.
3) Add a confirmation drawer for fixes with risk labels and rollback notes.
4) Expand diagnostics depth for Storage/Performance/Update/Printer.
5) Add type hints and minimal tests for report rendering and masking.
