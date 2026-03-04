# UI M3 Rebuild Plan

Date: 2026-03-04

## Scope and guardrails
- Rebuild the UI to Material 3 Light default without replacing core application logic.
- Preserve all core outcomes: diagnostics, fixes, runbooks, exports, masking, validator, sessions.
- Keep worker-based execution and ToolRunner-only output surface.
- Keep explicit execution model: single click selects; run/open via button, double-click, or Enter.

## Current UI problems by page

### Home
- Good coverage of capabilities, but visual hierarchy between status, goals, and history is still weak.
- Cards and strips use mixed contrast levels that flatten page structure.

### Playbooks
- Pro surface is dense and can look visually crowded under long lists.
- Tool/task/runbook sections share controls but need cleaner sectioning and spacing rhythm.

### Diagnose
- Findings toolbar and feed are functionally complete but still compact in some density states.
- Detail emphasis and next-best-action visibility can be stronger.

### Fixes
- Works functionally, but list/detail relationship still feels heavy in dense states.
- Rollback area needs cleaner visual separation from primary fix actions.

### Reports
- Stepper flow is present but card rhythm and spacing can still feel utilitarian.
- Evidence/preview/generate sections need clearer tonal separation.

### History
- Summary and compare surfaces are functionally strong but not consistently card-elevated.

### Settings
- Functional coverage is broad; category/content structure exists but styling consistency can be tightened to M3 surfaces.

## Current inconsistencies (global)
- Theme source is split across legacy modules; needs single canonical style module path.
- Component set partially standardized but still mixed naming (`PrimaryButton`/`SoftButton`) vs M3 vocabulary.
- QSS currently themed, but needs stricter M3 light token discipline and explicit component state mapping.

## Accidental execution spots (audit)
- Prior root cause (`BaseRow.mousePressEvent` activation) was fixed.
- Current model must remain enforced by regression tests:
  - no launch on row single-click
  - launch only by button/double-click/Enter/context action.

## Proposed M3 Light token set (source of truth)
- `bg`: `#FAFAFA`
- `surface`: `#FFFFFF`
- `surface2`: `#F6F7F9`
- `surface3`: `#EEF1F5`
- `border`: `#DDE3EA`
- `text`: `#111827`
- `text_muted`: `#4B5563`
- `accent`: `#FEA643`
- `accent_hover`: `#FFB868`
- `accent_pressed`: `#E58E2D`
- semantic: `ok=#16A34A`, `warn=#D97706`, `crit=#DC2626`, `info=#2563EB`
- radius: `12/16/20`
- spacing: `4/8/12/16/24/32`
- typography: pt-only, Segoe UI family

## Proposed design-system module architecture
- `src/ui/style/tokens.py`: token dataclasses + scales
- `src/ui/style/theme.py`: mode/palette resolution, defaults, normalization
- `src/ui/style/qss_builder.py`: full M3 stylesheet construction from tokens
- `src/ui/theme.py` and `src/ui/app_qss.py` become compatibility wrappers to avoid broad runtime breakage.

## Proposed component library upgrades
- Keep existing components but add M3 vocabulary and state support:
  - SectionHeader
  - FilledButton / TonalButton / TextButton
  - Chip / FilterChip
  - Badge
  - Card with elevation levels
  - DrawerPanel
  - ToastHost (snackbar-like styling)
  - Unified row layout (icon/title-subtitle/actions)

## Proposed page layout direction
- Maintain current page behavior but tighten visual structure to a consistent 2-column max pattern.
- Home/Playbooks/Diagnose/Fixes/Reports/History/Settings will share:
  - section header rhythm
  - M3 card elevation hierarchy
  - consistent spacing and action placement
  - right detail pane integration.

## Basic vs Pro layout strategy
- Keep centralized policy (`ui_state`) as control plane.
- Basic:
  - simplified entry surfaces and reduced controls,
  - right pane collapsed by default.
- Pro:
  - full directories/toggles,
  - right pane open by default.

## Validation plan
1. `python -m src.tests.smoke`
2. Startup sanity (`AppShell` offscreen)
3. Manual checks:
   - no single-click execution
   - themed scrollbars/splitters across pages
   - Basic/Pro visibly different layout behavior
4. `git diff --stat` captured into rebuild report.
