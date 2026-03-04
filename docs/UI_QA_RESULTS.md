# UI QA Results (2026-03-04)

## Automated gates
- `.\\.venv\\Scripts\\python.exe -m src.tests.smoke`
  - Result: **pass**.
- `.\\.venv\\Scripts\\python.exe scripts/capability_audit.py`
  - Result: **pass** (`140 ok / 0 failing`).
- Offscreen startup check (`AppShell` for ~0.9s)
  - Result: **pass** (`UI_STARTUP_OK`).

## Interaction-model regression checks

### Single-click safety (critical)
- Playbooks tool rows:
  - single-click selects row only.
  - no worker starts, no ToolRunner opens.
- Execution requires explicit action:
  - row `Open` button, Enter/Return, or double-click.

### Run status and ToolRunner handoff
- Safe tool run opens ToolRunner.
- Top-bar run-status card click opens/focuses ToolRunner.
- Run-status line 2 streams last log line + elapsed while running.

## Layout and mode checks

### Window sizing
- Minimum window size: no clipped critical controls in tested flows.
- 1080p windowed: shell regions remain crisp and aligned.

### Basic vs Pro layout behavior
- Basic:
  - Playbooks guided goals visible.
  - Pro console hidden.
  - right detail pane default collapsed.
- Pro:
  - Playbooks pro console visible.
  - script tasks exposed behind Advanced toggle.

## Theme and component checks
- Global theme tokens drive shell/page surfaces.
- Scrollbars are themed in both axes; no default Qt scrollbar look observed.
- Splitter handles follow theme with hover/pressed states.
- Typography remains point-based (`pt`), no `QFont::setPointSize(-1)` warning observed in startup/tests.

## Toolbox behavior checks
- Added `Windows Links` tool category and routing.
- `Storage Settings` no longer appears as first default Top Tool.
- Top Tools now prioritize non-annoying diagnostic/evidence-first ordering.

## Residual risk
- `src/ui/main_window.py` is still large; page modules are now shell/page entrypoints but full extraction remains future technical debt.

## Release verdict
- **Pass** for this UI remake pass: explicit execution model, mode differentiation, themed shell consistency, and regression coverage all validated.
