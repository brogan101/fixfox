# UI STYLE GUIDE

## Design system source
- Theme tokens: `src/ui/theme.py`
- Global styling: `src/ui/app_qss.py`
- Layout/density guardrails: `src/ui/layout_guardrails.py`

## Typography
- Font family: `Segoe UI` (with system fallbacks)
- Title/H1: `22px`
- Section/H2: `14px`
- Body: `13px` (comfortable), `12px` (compact)
- Caption/Subtext: `12px`
- Monospace only for live command/log output: `Consolas`

## Spacing scale
Use only this scale for margins/padding/gaps:
- `6`, `10`, `14`, `18`, `22`

## Corner radius + borders
- Small radius: `12` (comfortable), `10` (compact)
- Medium radius: `16`
- Border thickness: `1px` (default), `2px` only for explicit focus/error emphasis

## Density tokens
From `DensityTokens`:
- Comfortable:
  - `nav_item_height: 40`
  - `list_row_height: 66`
  - `button_height: 36`
  - `input_height: 34`
- Compact:
  - `nav_item_height: 34`
  - `list_row_height: 56`
  - `button_height: 32`
  - `input_height: 30`

## Color/palette rules
- Single source: `ThemeTokens` only.
- Supported palettes: Graphite, Slate, Indigo, Mono.
- Accent color is CTA-focused; avoid using accent as default background everywhere.
- No ad-hoc page-level hardcoded UI colors in app shell widgets.

## Scrollbar spec
Themed in `app_qss.py` for both axes.
- Thickness:
  - Comfortable: `12px`
  - Compact: `10px`
- States:
  - Track
  - Handle
  - Hover
  - Pressed
  - Add/sub line hidden
- Background and border always theme-token driven.

## Splitter spec
Themed in `app_qss.py`.
- Handle thickness:
  - Comfortable: `8px`
  - Compact: `6px`
- States:
  - Base
  - Hover
  - Pressed
- Subtle contrast, no bright separators.

## Consistency rules
- Shared row widgets (`ToolRow`, `FixRow`, `FindingRow`, `SessionRow`) define row behavior and alignment.
- Shared cards (`Card`, `DrawerCard`, `EmptyState`) define page section styling.
- Avoid page-specific inline QSS overrides.
