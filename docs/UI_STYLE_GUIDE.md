# UI Style Guide (Release Baseline)

## Single source of truth
- Theme tokens: `src/ui/theme.py`
- Global stylesheet builder: `src/ui/app_qss.py`
- Runtime theme application: `src/app.py` and `MainWindow._apply_theme(...)`
- Mode/layout policy: `src/ui/ui_state.py`

## Typography
- Font family: `Segoe UI` via `BASE_FONT_FAMILY`
- QSS typography unit: `pt` (not `px`)
- Scale (comfortable density base):
  - Body: `13pt`
  - Run status title emphasis: `+2pt`
  - Page title emphasis: `+9pt`
  - Subtitle/caption: `-1pt`
  - Section/card title: `+1pt`
- Monospace log surface: `Consolas` using `safe_copy_font(...)` in ToolRunner.

## Density and sizing
From `DensityTokens` in `theme.py`:
- Comfortable: row `66`, nav `40`, button `36`, input `34`, icon `18`
- Compact: row `56`, nav `34`, button `32`, input `30`, icon `16`

Shared constants:
- Spacing scale: `6 / 10 / 14 / 18 / 22`
- Border thickness: `1px` baseline
- Corner radius: density-token driven

## Runtime status surface
Top-bar run status card:
- 2-line content model:
  - Line 1: state (`Running: <tool>`, `Ready`, `Failed`, `Cancelled`)
  - Line 2: latest line + elapsed time (ellipsized)
- Min width: `660px`
- Icon size: `28px`

## Scrollbars and splitters
All defined in global QSS (`build_qss`), no widget-local overrides.

Scrollbars:
- Vertical + horizontal themed
- States: base, hover, pressed
- Thickness:
  - Comfortable: `12px`
  - Compact: `10px`

Splitter handles:
- Horizontal + vertical themed
- States: base, hover, pressed
- Thickness:
  - Comfortable: `8px`
  - Compact: `6px`

## Text input surfaces
Styled consistently in global QSS:
- `QLineEdit`, `QComboBox`, `QTextEdit`, `QPlainTextEdit`, `QTreeWidget`
- Shared background, border, radius, and focus ring behavior.

## Guardrails
- No page/widget ad-hoc `setStyleSheet(...)` overrides.
- No hardcoded runtime colors outside token/QSS builder flow.
- Accent color reserved for focus/CTA/high-signal state, not full-surface saturation.
