# Fix Fox Material 3 Design System

Date: 2026-03-04

## 1) Seed and Palette

Seed color:
- `accent`: `#FEA643` (Fix Fox orange)

### Light mode tokens (default)
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

Semantic:
- `ok`: `#16A34A`
- `warn`: `#D97706`
- `crit`: `#DC2626`
- `info`: `#2563EB`

### Dark mode tokens (optional)
- `bg`: `#0B0D12`
- `surface`: `#101521`
- `surface2`: `#141B2B`
- `surface3`: `#1A2336`
- `border`: `#2A344A`
- `text`: `#E9EDF5`
- `text_muted`: `#AAB3C5`
- `accent`: same orange family with moderated saturation

## 2) Elevation Model (Qt-friendly)

Qt QSS has limited true shadows; elevation is represented by surface contrast + borders:
- `elev0`: `surface`
- `elev1`: `surface2` + subtle border
- `elev2`: `surface3` + stronger border

Use elevation consistently for cards, drawers, popups, and status shells.

## 3) Shape System

- `radius_sm`: `10`
- `radius_md`: `14`
- `radius_lg`: `18`

Usage:
- Inputs/chips: `radius_sm`
- Buttons/cards: `radius_md`
- Hero/status containers and large surfaces: `radius_lg`

## 4) Typography (pt only)

Font family:
- `"Segoe UI", "Segoe UI Variable", Arial, sans-serif`

Scale:
- `H1`: `16pt` semibold
- `H2`: `13pt` semibold
- `H3`: `11.5pt` semibold
- `Body`: `10.5pt` to `11pt`
- `Caption`: `9pt`

Rules:
- Use `pt` only for font-size in QSS.
- Simulate line-height via vertical padding and spacing tokens.
- Keep text density readable; avoid tight multiline blocks.

## 5) Spacing Scale

Allowed spacing values only:
- `4, 8, 12, 16, 24, 32`

Rules:
- No random margins/padding.
- Compose larger spacing using these units only.
- Page and component spacing derives from this scale and density profile.

## 6) Density Profiles

### Comfortable
- Row height: `64-66`
- Button height: `40`
- Input height: `36`
- Icon size: `18`

### Compact
- Row height: `54-56`
- Button height: `34`
- Input height: `30-32`
- Icon size: `16`

## 7) Component Rules

Buttons:
- Filled (Primary): accent background + high contrast text
- Tonal (Secondary): surface3 background + accent/text foreground
- Text: transparent background + accent/text foreground
- Focus state: visible accent ring

Chips:
- Rounded chip with subtle border
- Selected chip uses accent tint and stronger border

Cards:
- Consistent radius + elevation level + padding
- Title/subtitle rhythm is consistent across all pages

Lists/Rows:
- Left icon slot (24 logical px)
- Title + subtitle center block
- Right action slot (icon button or Run/Open)

Drawers:
- Right-side detail surface with consistent section headers

Scrollbars:
- Thin, rounded, subtle track and clear hover/pressed handle states

## 8) Interaction Model

- Single click selects/focuses only
- Double-click or Enter executes selected item
- Primary actions are explicit buttons (`Run`, `Open`, `Preview`)
- Focus rings are visible and consistent

## 9) Page Layout Rules

Every page follows:
- Header area (title/subtitle + primary action)
- Main list/feed surface
- Right detail panel/drawer for current selection

Structure constraints:
- Max 2 content columns (main + detail)
- No mixed 3-column chaos
- No ad-hoc page-specific visual language

## 10) Accessibility and States

- Disabled state: lower contrast but still legible
- Hover state: subtle tonal shift
- Pressed state: stronger tonal shift
- Focus state: accent outline ring

## 11) Implementation Guardrails

- Theme tokens are the single source of truth (`ui/theme.py`)
- QSS builder uses tokens only (`ui/app_qss.py`)
- Avoid per-widget hardcoded style overrides
- Preserve worker-based execution and ToolRunner-only output surface
