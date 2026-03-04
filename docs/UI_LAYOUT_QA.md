# UI Layout QA (Fix Fox)

Date: 2026-03-03

## Observed Layout Risks (Pre-Guardrail)
- Top bar action row could compress icon/buttons at narrow widths.
- Context bar could clip action buttons (`Export`, `Copy Summary`, `End Session`) on smaller windows.
- Playbooks/Tools page had dense control clusters (search + segment + filters + run buttons) with clipping risk.
- Right concierge panel could remain expanded at narrow widths and reduce usable center content.
- Fixed nav width and mixed fixed control sizing increased overlap/cutoff risk when resizing.

## Controls Most At Risk
- Tool page: `Run Selected`, `Collect Core Evidence`, script-task filters.
- Reports page: export controls and quick actions on narrow windows.
- Top command bar: cancel/export/help/panel/menu buttons.
- Context strip action buttons.

## Hard-Coded Sizing Risks Identified
- Fixed nav width and ad-hoc button sizing.
- Per-page action rows without global minimum width/height guardrails.
- Right panel width not enforced with a minimum when expanded.

## Scrollability Notes
- Primary pages are already wrapped in scroll areas.
- Risk remains for control rows inside cards where button minimum sizes were previously not enforced.

## Guardrail Rules Implemented
- Added centralized guardrails in `src/ui/layout_guardrails.py`:
  - `MIN_WINDOW_SIZE = 1100x700`
  - `MIN_RIGHT_PANEL_WIDTH = 260`
  - `MIN_NAV_WIDTH = 210`
  - density-aware minimum button and row sizing tokens
  - responsive collapse threshold logic for right panel
- Main window now:
  - enforces minimum window size
  - applies button minimum size guardrails globally
  - auto-collapses right panel below threshold
  - keeps right panel minimum width when expanded
- Added dev hotkey: `Ctrl+Alt+L`
  - toggles `LayoutDebugOverlay`
  - overlays widget bounds
  - highlights likely clipped widgets in red (best effort)

## Acceptance Check Notes
- At minimum window size (`1100x700`), primary action buttons remain reachable.
- ToolRunner remains resizable and accessible.
- Reports export controls remain reachable via page scroll and guarded button sizing.
