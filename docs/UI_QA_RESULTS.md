# UI QA Results

Date: 2026-03-03

## Window Size Coverage
- Minimum window (`1100x700`): pass
  - No inaccessible controls in active viewport paths.
  - Overflow content remains reachable via page scroll areas.
- 1080p windowed (`1920x1080`): pass
- Wide desktop (`1600x900` and above): pass

## Right Panel Behavior
- Auto-collapse below threshold width: pass
- Manual toggle open/close: pass
- Essential page actions remain on page body while panel is collapsed: pass

## Page Consistency
- Home: goals + quick actions + what changed + recent sessions: pass
- Playbooks: directory + detail + advanced toggle behavior: pass
- Diagnose: grouped feed + toolbar filters + right-panel detail: pass
- Fixes: master->detail + risk chips + rollback center: pass
- Reports: 3-step flow + evidence checklist + redaction preview: pass
- History: case summary + compare drawer: pass
- Settings: hub nav styling + search + reset/export actions: pass

## ToolRunner Surface
- Run output remains in ToolRunner (no embedded page output widgets for runbook flow): pass
- Primary controls reduced, secondary actions under `More`: pass
- Deterministic overview sections present: pass

## Keyboard Navigation Sanity
- Tab traversal between top bar, nav rail, page actions, and ToolRunner controls: pass
- Enter/Space activation on row widgets and action buttons: pass
- Context-menu key support on list rows retained: pass
- Automated Tab-cycle focus sampling per page returned valid focus targets across:
  - `QLineEdit`, `QListWidget`, `QComboBox`, `QCheckBox`, `PrimaryButton`, `SoftButton`, `IconButton`, `QTabBar`.

## Manual Flow Checks
1. Home -> Fix Wi-Fi path -> ToolRunner -> export Home Share Pack: pass
2. IT Ticket Triage -> export Ticket Pack -> open `report.html`: pass
3. History reopen -> re-export without rerun: pass

## Notes
- Some lower-page controls are naturally below fold at minimum size by design; all are reachable through scroll policies.
