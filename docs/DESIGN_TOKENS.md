# FixFox Design Tokens

## Canonical Token Module

- Source: `src/ui/style/tokens2.py`

## Scales

- Spacing: `4, 8, 12, 16, 24, 32`
  - keys: `xxs, xs, sm, md, lg, xl`
- Radii: `10, 14, 18`
  - keys: `sm, md, lg`
- Typography (pt): `12, 13, 15, 18, 22`
  - keys: `xs, sm, body, h3, h2`
- Elevation recipes:
  - `0`: flat
  - `1`: subtle raised surface
  - `2`: overlay surface

## Qt Font Weights (valid 0-99 scale)

- normal: `50`
- medium: `57`
- demibold: `63`
- bold: `75`
- extrabold: `81`
- black: `87`

## Semantic Color Contract

- `bg`, `surface`, `surface2`
- `text`, `text2`
- `border`
- `accent`, `accent2`
- `ok`, `warn`, `crit`

Semantic values are resolved from active theme palette with:
- `semantic_colors_from_theme(tokens)`

## Usage Rules

1. Do not hardcode margins/padding/font sizes in new UI code.
2. Use `spacing(...)` and token helpers from `tokens2.py`.
3. Use only Qt-valid font weights listed above.
4. Keep semantic state colors (`ok/warn/crit`) from theme tokens, not ad-hoc hex literals.
5. New components should expose density-safe sizing and avoid magic constants unless icon geometry requires it.
