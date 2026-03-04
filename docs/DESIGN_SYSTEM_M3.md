# DESIGN SYSTEM - MATERIAL 3 (Fix Fox)

Date: 2026-03-04
Default mode: **Light**

## 1) Color Tokens (Material 3 Light)
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
- `ok`: `#16A34A`
- `warn`: `#D97706`
- `crit`: `#DC2626`
- `info`: `#2563EB`

## 2) Optional Dark Tokens
- `bg`: `#0B0D12`
- `surface`: `#101521`
- `surface2`: `#141B2B`
- `surface3`: `#1A2336`
- `border`: `#2A344A`
- `text`: `#E9EDF5`
- `text_muted`: `#AAB3C5`
- `accent`: `#FEA643` (same family)

## 3) Shape System
Only these radii are allowed:
- `12`
- `16`
- `20`

## 4) Spacing System
Only these spacing units are allowed:
- `4`, `8`, `12`, `16`, `24`, `32`

## 5) Typography (pt only)
Font family:
- `"Segoe UI", "Segoe UI Variable", Arial, sans-serif`

Scale:
- `H1`: `16pt`
- `H2`: `13pt`
- `Body`: `11pt`
- `Caption`: `9pt`

Rule:
- Do not use `px` font sizes; all font sizes are points (`pt`).

## 6) Elevation Model (Qt-Friendly)
- `elev0` -> `surface`
- `elev1` -> `surface2`
- `elev2` -> `surface3`

Because Qt shadows are limited, elevation is represented by tonal contrast + borders.

## 7) Component Rules
- Buttons: Filled / Tonal / Text variants.
- Chips: selected state uses accent tint + stronger border.
- Focus: subtle accent focus ring for interactive controls.
- Scrollbars: fully themed (vertical/horizontal, track/handle/hover/pressed).
- Splitters: themed handles with hover/pressed states.
- Menu/Tooltip: tokenized surface/border/text.

## 8) Interaction Rules
- Single-click = select only.
- Execute only via explicit action (`Run/Open`) or double-click/Enter.
- No hidden execution on selection.

## 9) Scaling Rules
- UI scale range: `90%` to `125%`.
- Scale influences:
  - base font point size
  - control heights (rows/buttons/inputs)
  - icon size
  - small spacing multiplier
- Scale is persisted in settings and applies live.
