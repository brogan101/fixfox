# STORE READY AUDIT - PRE-IMPLEMENTATION BASELINE (2026-03-04)

## Scope
This audit is the mandatory pre-change inventory for the current store-readiness run.

## Runtime and Marker Scan
- Baseline commit: `e0cebc1335d20bfacb7f1967935437b4dec1e4f3`
- Marker scan command:
  - `rg -n "TODO|FIXME|NotImplemented|\bpass\b" src docs`
- `src/` marker hits from scan:
  - `src/core/settings.py:83` (`pass` in guarded save_settings DB bridge)
  - `src/core/sessions.py:89,102,127,156,175` (`pass` in defensive JSON/session fallback handlers)
  - `src/core/run_events.py:109` (`pass` in DB record guard)
  - `src/core/exporter.py:475` (`pass` in non-fatal export fallback)
- `docs/` marker hits are prose uses of the word `pass`; no action markers were found in executable UI code.

## Page-by-Page Control Inventory + Issues

### Home
Controls:
- Quick Check CTA, guided goal cards, status strip chips, favorites, recent sessions.
Issues:
- Goal cards are functional but minor copy/spacing quality drift remains.
- Needs stronger “what changed since last run” readability polish.

### Playbooks
Controls:
- Basic guided-goal cards; Pro tools/runbooks segmented console; advanced script task drawer.
Issues:
- Core behavior is solid; detail copy still has uneven plain-English consistency.
- Tool directory/detail rhythm needs small spacing/icon consistency pass.

### Diagnose
Controls:
- Search + severity + recommended filters, grouped feed, next-best-action controls.
Issues:
- No major wiring breaks found; empty-state and detail readability still need premium polish.

### Fixes
Controls:
- Recommended/all scope, risk filters, details, rollback center, run/preview actions.
Issues:
- In basic mode behavior is mostly correct but some controls need stronger mode-aware surfacing language.

### Reports
Controls:
- Configure -> Preview -> Generate stepper tabs, redaction preview, evidence list, export actions.
Issues:
- Basic mode still exposes some advanced-looking surfaces that should be more clearly Pro-facing.
- Empty/no-session guidance can be clearer.

### History
Controls:
- Timeline list, session summary, compare drawer, run center.
Issues:
- Layout is functional; case-summary hierarchy can be cleaner for quick scan.

### Settings
Controls:
- Section rail + stack pages, settings search, reset/export/help actions, appearance/safety/privacy/advanced/about/feedback.
Issues:
- Missing UI scale slider (90%-125%) persisted + live-applied.
- Privacy/Safety content is present but still mostly via help dialog language; needs dedicated in-app policy pages and stronger local-storage clarity.
- Export settings action should be Pro-only.

## Style and Layout Audit
- Theme tokenization is mostly centralized (`src/ui/style/theme.py`, `src/ui/style/qss_builder.py`).
- Global scrollbar/splitter/menu/tooltip theming exists and is mostly consistent.
- One global `setStyleSheet(...)` call remains at QApplication scope (expected).
- No widespread ad-hoc per-widget styles found.

## Accidental Execution Audit
- Rows use explicit execution (double-click/Enter/Run button).
- Single click selects and focuses only.
- No immediate accidental execution path found in row components.

## ToolRunner Log Visibility Audit
Current state:
- RunEventBus provides per-run buffering and subscriptions.
- ToolRunner subscribes and appends output via signals into `QPlainTextEdit`.
- Top bar run status consumes global bus events.
Remaining gaps to harden:
- Strengthen top-search driven run discovery path and clearer last-log line mapping in top status.
- Ensure silent-run UX copy is consistently visible in both top bar and ToolRunner.

## Layout Issues and Guardrails
- Minimum window and auto-collapse guardrails exist.
- Additional scale-aware clipping checks are required once UI scale support is added.

## Pre-Implementation Fix Plan
1. Add persisted UI scale (90-125) with live apply and scale-aware density/spacing.
2. Upgrade top search to debounced grouped results with keyboard navigation and mode-priority ranking.
3. Add dedicated in-app Privacy and Safety pages in Settings and help-menu routing.
4. Tighten settings release behavior (Pro-only export settings, improved search descriptors).
5. Polish run-status readability and ToolRunner silent-output hints.
6. Re-run smoke + manual checklist; publish QA docs and proof.
