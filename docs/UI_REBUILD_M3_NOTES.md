# UI Rebuild M3 Notes

Date: 2026-03-04

## What changed

### 1) Material 3 design system and tokens
- Added [docs/DESIGN_SYSTEM_M3.md](./DESIGN_SYSTEM_M3.md) as the UI single source of truth.
- Reworked theme tokens in `src/ui/theme.py`:
  - Light-first neutral surfaces and brand-orange accent flow.
  - Optional dark mode retained.
  - Updated spacing/radius/density scales to M3-oriented values.

### 2) Light mode default
- Updated default settings in `src/core/settings.py`:
  - `theme_palette = "fixfox_m3"`
  - `theme_mode = "light"`

### 3) Global QSS rebuild (Material-ish)
- Replaced `src/ui/app_qss.py` with a token-driven M3 stylesheet:
  - white/near-white surfaces
  - tonal elevation layers via border + surface contrast
  - consistent focus rings
  - button variants (filled/tonal/text)
  - themed menus/tooltips/inputs/lists/tabs/check controls
  - thin themed scrollbars + styled splitters
  - disabled state consistency

### 4) Component library upgrades
- Updated `src/ui/widgets.py`:
  - Card elevation property support
  - new `Chip`, `Badge`, `SecondaryButton`, `TextButton`, `DrawerPanel`
  - existing `PrimaryButton`/`SoftButton` retained for compatibility

### 5) Row and execution UX integrity
- `src/ui/components/rows.py`:
  - consistent left icon slot for all row families
  - retains explicit activation model (single click select; double-click/Enter execute)
- `src/ui/components/feed_renderer.py` already prevents duplicate double-click activation emissions.

### 6) ToolRunner M3 polish
- Updated `src/ui/components/tool_runner.py`:
  - status chip with semantic states (info/ok/warn/crit)
  - cleaner button labeling and header rhythm
  - retains run-event streaming and cancellation behavior

### 7) Playbooks/tool UX polish
- `src/core/toolbox.py`:
  - introduced `windows_links` category for `ms-settings`/Windows-link tools
  - curated `TOP_TOOLS` order so Storage Settings is no longer first/default
- `src/ui/main_window.py`:
  - added "Windows Links" category filter
  - category labels normalized for display
  - tool detail/output text now uses readable category labels

### 8) Shell/page consistency hooks
- `src/ui/pages/*.py` assign page identity metadata for consistent shell-level styling.
- `src/ui/shell.py` remains the concrete shell entrypoint and exposes shell region map.

## No functionality loss guarantees
- Core flows preserved:
  - tools, script tasks, runbooks
  - ToolRunner execution and live events
  - exports/masking/validator/session indexing
  - Basic/Pro mode policy routing
- No changes to output structure/manifest contracts.

## How to test

### Automated
1. `python -m src.tests.smoke`
2. Optional: `python -m compileall src`

### Manual UX checks
1. Launch app (`python -m src.app`) and confirm light mode default on fresh settings.
2. Verify single-click on rows selects only.
3. Verify explicit Run/Open and double-click/Enter execute.
4. Start a tool and confirm:
   - ToolRunner opens
   - top run-status updates live
   - clicking run-status opens ToolRunner
5. Toggle Basic/Pro and confirm layout simplification in Basic and full console surfaces in Pro.
6. Validate scrollbar and splitter styling on pages with long content.

## Screenshot plan (for release notes)
1. Shell overview (Home + top command bar + nav + detail pane)
2. Playbooks Pro (tools list + detail pane + action buttons)
3. Diagnose page (chips + grouped feed + right detail)
4. Reports stepper (configure/preview/generate)
5. ToolRunner during active run with status chip and live output
6. Settings Appearance page showing M3 light/dark options
