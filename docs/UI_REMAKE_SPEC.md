# UI Remake Spec (Store-Ready Baseline)

Date: 2026-03-04

## 1) Target Shell Layout

Primary shell regions:
- `NavRail` (left, fixed width): page icon + label rows, one consistent row height per density.
- `CommandBar` (top): global search, Basic/Pro segmented mode toggle, run status block, help/menu actions.
- `ContentStack` (center): routed page content only.
- `DetailPane` (right, collapsible): selection-context details and next-best actions.

Shell rules:
- Shell owns primary framing; pages do not create competing top-level header bars.
- Splitter behavior is centralized and consistent across all pages.
- Right pane defaults collapsed in Basic, expanded in Pro.

## 2) Typography Scale

Font family:
- `Segoe UI` (single source of truth from theme tokens)

Type scale (pt):
- H1: body + 9pt
- H2/Section: body + 1pt
- Body: 13pt comfortable / 12pt compact
- Caption/Subtext: body - 1pt

Rules:
- Use `pt` only in QSS (`px` for font sizes is not allowed).
- Monospace is restricted to logs/details surfaces (ToolRunner live output).

## 3) Spacing Scale and Rhythm

Spacing tokens:
- `6 / 10 / 14 / 18 / 22`

Rules:
- No ad-hoc margins/paddings outside tokenized spacing.
- Card padding, row spacing, toolbar spacing, and section spacing must be density-aware and tokenized.
- Section headers align to a single rhythm grid per page.

## 4) Density Metrics

Comfortable:
- nav row: 40
- list row: 66
- button: 36
- input: 34
- icon: 18

Compact:
- nav row: 34
- list row: 56
- button: 32
- input: 30
- icon: 16

## 5) Color Palette Plan

Palettes:
- Fix Fox Graphite (default)
- Fix Fox Slate
- Fix Fox Indigo
- Fix Fox Mono

Color usage:
- Accent (fox orange) is sparse: primary CTA, focused state, high-signal emphasis only.
- Surfaces use subtle contrast and border separation, not saturated fills.
- Scrollbars and splitters inherit active theme tokens and states.

## 6) Interaction Model (Explicit Execution)

Global interaction contract:
- Single click: select/focus only.
- Double click: execute (where applicable).
- Enter/Return: execute selected row.
- Space: no execution.
- Explicit buttons (`Run`, `Open`, `Preview`) always execute.

Safety constraints:
- Selection changes must never execute tools/fixes/runbooks/tasks.
- ToolRunner remains the only execution/output surface.

## 7) Per-Page Layout Sketches

### Home
- Top: status strip + “What changed since last run”.
- Main: 4–6 guided goal cards (Wi-Fi, Space, Speed, Printer, Browser, Crashes).
- Bottom: recent sessions + export-last-pack.
- DetailPane: selected goal details or last run summary.

### Playbooks
Basic:
- guided goals only, no full directory wall.
- passive “Browse tools (Pro)” cue.

Pro:
- segmented Tools/Runbooks.
- script tasks under explicit Advanced toggle.
- left list + right detail with explicit Run/Open controls.

### Diagnose
- chips/count toolbar, grouped findings feed.
- DetailPane: finding meaning, next action button, evidence actions.

### Fixes
- recommended/safe first in Basic.
- Pro exposes broader filters.
- DetailPane: risk/rollback context + Preview/Run buttons.

### Reports
- strict 3-step model:
  1) configure preset/masking/logs
  2) preview tree + redaction before/after
  3) generate/validate + post-export actions

### History
- left timeline list.
- right case summary and compare drawer.

### Settings
- left category rail + right content.
- Basic hides advanced categories by default.
- search persists across categories.

## 8) Component Rules

Rows:
- select on single click only.
- row action buttons are always visible and explicit.

Cards:
- subtle border, consistent radius, density-aware padding.

Status block:
- 2-line run state with last log line, elapsed, spinner.
- click opens/focuses ToolRunner.

## 9) QA Acceptance Criteria

Required:
- no accidental launches from single-click selection.
- themed scrollbars and splitters visible on all pages.
- Basic mode materially simpler than Pro mode.
- min-size and 1080p layouts have no clipped critical controls.
- smoke tests pass (`python -m src.tests.smoke`).
