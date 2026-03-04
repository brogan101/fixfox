# UI Redesign Spec (2026-03-04)

## Architecture direction
- Execution shell entrypoint: `src/ui/shell.py` (`AppShell`).
- Main runtime host remains `src/ui/main_window.py` for behavior stability.
- Page modules exist under `src/ui/pages/*` and are the staging layer for continued extraction.
- Shared style rules centralized into:
  - `src/ui/theme.py` (tokens/density/spacing scale)
  - `src/ui/app_qss.py` (single QSS builder)
  - `src/ui/style/style_guide.py` (spacing/control/icon helpers)

## Layout model
- Global: left nav + center content + right concierge/detail panel.
- Top bar:
  - enlarged run status card (2-line model)
  - search
  - Basic/Pro mode toggle
  - high-frequency actions (cancel/export/help/panel/menu)
- Execution surface: ToolRunner is the sole run/log UI.

## Basic vs Pro as real layout modes
Policy source: `src/ui/ui_state.py` (`LayoutPolicy`).

### Basic
- Right panel defaults collapsed.
- Playbooks shows guided goals container; pro console hidden.
- Script tasks hidden.
- Reports presets simplified to `home_share`.
- Settings advanced section hidden.

### Pro
- Right panel defaults visible.
- Playbooks pro console visible with full directories and script tasks.
- Reports full presets visible.
- Settings advanced section visible.

## Theme rules
- Font family: `Segoe UI` (`BASE_FONT_FAMILY`).
- Font unit: `pt` (prevents pixel-font point-size warnings).
- No random per-widget `setStyleSheet` overrides.
- Colors come from `ThemeTokens` only.
- Accent color reserved for CTA/focus/high-signal states.

## Typography scale
- H1/title: `body + 9pt`
- Section/card title: `body + 1pt`
- Body: density base (`13pt` comfortable, `12pt` compact)
- Caption/subtitle: `body - 1pt`

## Spacing and sizing scale
- Spacing tokens: `6 / 10 / 14 / 18 / 22`
- Density controls (comfortable/compact):
  - row height `66 / 56`
  - nav row `40 / 34`
  - button `36 / 32`
  - input `34 / 30`
  - icon `18 / 16`

## Scrollbar and splitter spec
- Scrollbars themed globally for all `QAbstractScrollArea` derivatives.
- States: base, hover, pressed.
- Thickness by density:
  - comfortable `12px`
  - compact `10px`
- Splitter handles themed with hover/pressed states:
  - comfortable `8px`
  - compact `6px`

## Component consistency rules
- Cards/rows use density-derived padding.
- Tree/list/table headers share theme section style.
- Tabs share single tab visual language.
- Top bar run-status text is ellipsized from raw detail text to avoid clipping.

## Functionality integrity rules
- Long-running operations use worker pipeline with cancel/timeouts.
- Every run path emits run events and surfaces status/log state in ToolRunner + top bar.
- Capability registry entrypoints must map to real callables (validated by capability audit harness).
