# Fix Fox UI Style Guide

Date: 2026-03-03  
Scope: Release polish baseline for all PySide6 surfaces

## 1) Design Tokens

## Typography
- Base font family: `Segoe UI` stack (`"Segoe UI", "Segoe UI Variable", Arial, sans-serif`)
- Scale:
  - Title: 22px
  - Section: 14px
  - Body: 13px (comfortable) / 12px (compact)
  - Caption: 12px / 11px equivalent in compact surfaces

## Spacing
- Use token scale only:
  - `xs=6`
  - `sm=10`
  - `md=14`
  - `lg=18`
  - `xl=22`
- Card spacing/padding is density-driven from `DensityTokens`.

## Radius
- Small radius: `12`
- Medium radius: `16`
- Controls and cards must use tokenized corner radius values, not ad-hoc values.

## Density
- Supported densities:
  - `comfortable`
  - `compact`
- Density governs:
  - nav row height
  - list row height
  - button/input height
  - icon size
  - card padding

## 2) Color and Theme Rules

- Theme tokens are the source of truth for in-app colors.
- Semantic tones (`ok/warn/crit/info`) are contextual accents only.
- Accent color usage:
  - primary CTAs
  - focused/selected affordances
  - not for full-page fills
- Scrollbars and splitters must inherit theme token colors.

## 3) Component Rules

- `Card` is the default container for page sections.
- `BaseRow` derivatives are the only row pattern in list directories.
- `DrawerCard` is the standard expandable details shell.
- `ToolRunnerWindow` is the only execution/output monitor for tools, fixes, and runbooks.
- Page headers follow one structure:
  - title
  - short subtitle
  - optional primary CTA
  - help icon

## 4) Navigation and Settings

- Main nav and settings nav use the same row-widget pattern (icon + label + shared sizing).
- Nav row height and icon size must follow density tokens.
- Settings search is always available at top of settings tools area.

## 5) Scroll and Layout Policy

- Any page with growable vertical content must be wrapped in themed scroll containers.
- Default Qt scrollbar visuals are not allowed.
- Splitter handles must be themed and have hover feedback.
- Minimum window size guardrail must be enforced and right panel auto-collapse respected.

## 6) Interaction States

- All interactive controls must have:
  - hover state
  - focus state
  - pressed/active state
- Focus outlines use accent-derived token styling.

## 7) Prohibited Patterns

- No widget-level random styling outside tokenized QSS.
- No embedded raw execution output widgets in page bodies.
- No button soup: use directories/search/context actions.
