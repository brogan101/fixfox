# UI QA Results (2026-03-04)

## Automated gates
- `.\\.venv\\Scripts\\python.exe -m src.tests.smoke`
  - Result: **pass**.
- `.\\.venv\\Scripts\\python.exe scripts/capability_audit.py`
  - Result: **pass** (`140 ok / 0 failing`).
- Offscreen UI launch check (`AppShell` + timed quit)
  - Result: **pass** (`UI_STARTUP_OK`).

## Required checks

### Min window size
- Baseline min size (`MIN_WINDOW_SIZE`) respected.
- No blocked controls found in smoke navigation traversal.
- Right panel responsive collapse still works.

### 1080p window
- Top bar run status card remains readable with larger icon + two-line status.
- Home/Playbooks/Diagnose/Fixes/Reports/History/Settings are navigable without control overlap.

### Basic/Pro layout behavior
- Basic:
  - Playbooks guided container visible.
  - Pro console hidden.
  - concierge default collapsed.
- Pro:
  - Playbooks pro console visible.
  - script tasks visible.

### ToolRunner and run status
- Starting safe tool creates ToolRunner.
- Run events include at least `START`, `STATUS`, `END`.
- Top bar status detail updates during runs.
- Clicking top status card opens/focuses ToolRunner.

### Scrollbar/theme consistency
- Scrollbars themed via global QSS (no default Qt bars observed in tested flows).
- Splitter handle styling active with hover/pressed states.
- Tree/list/tab/header styles consistent with token-driven theme.

### QFont warning check
- `QFont::setPointSize: Point size <= 0 (-1)` warnings observed during launch/tests: **0**.

## Known residual risks
- `src/ui/main_window.py` remains large; extraction is in-progress via `src/ui/pages/*` wrappers and `AppShell` entrypoint.
- Full per-page method migration to dedicated page modules is still technical debt, not a release blocker for current behavior.

## Release gate verdict
- **Pass** for current polish scope (UI consistency, run-status/log reliability, capability wiring integrity, smoke coverage).
