# UI Polish Audit

Date: 2026-03-04  
Scope: Basic/Pro mode behavior, run-status visibility, title/version labeling, style consistency, clipping/wiring checks.

## 1) Page-By-Page Audit

### Home
- Controls:
  - Goal cards (Start + Learn More)
  - Quick Actions (favorites)
  - Recent Sessions
  - Export Last Pack
- Findings:
  - Structure is clean and home-first.
  - Required mode behavior: Quick Actions must only show capabilities visible in current mode.
- Fixes applied:
  - Favorites now filtered by capability visibility.

### Playbooks
- Controls:
  - Search, category filters, tool directory, tool detail actions
  - Script Tasks panel and Advanced toggle
  - Runbooks directory and audience filter
- Findings:
  - In Basic mode, page visibility needed strict gating.
  - Script Tasks must be hidden in Basic.
- Fixes applied:
  - Playbooks nav hidden in Basic.
  - Script Tasks stay hidden in Basic; friendly empty-state messaging added.
  - Tools/runbooks lists now filtered through capability visibility.

### Diagnose
- Controls:
  - Findings toolbar (search, severity, recommended-only)
  - Finding cards and detail pane linkage
- Findings:
  - No execution-surface violations found.
  - ToolRunner routing intact.
- Fixes applied:
  - No new functional wiring required in this pass.

### Fixes
- Controls:
  - Scope selector, risk chips, fix directory, detail pane, run/preview
  - Rollback center
- Findings:
  - Basic-mode admin behavior needed explicit override path.
- Fixes applied:
  - Basic mode defaults to safe-only.
  - "Show admin tools" remains available in Basic and can explicitly enable admin flow.
  - Fix list filtered by visibility registry.

### Reports
- Controls:
  - 3-step flow, preset selector, masking toggles, evidence checklist, generate/export actions
- Findings:
  - Basic mode must limit presets and default logs off.
- Fixes applied:
  - Basic mode preset list forced to `home_share`.
  - Include logs kept available and defaults off in Basic.

### History
- Controls:
  - Session list, reopen/compare/re-export
  - Run Center
- Findings:
  - Run Center should be pro-focused when mode is Basic.
- Fixes applied:
  - Run Center card hidden in Basic mode.

### Settings
- Controls:
  - Safety, Privacy/Masking, Appearance, Advanced, About, Feedback
  - Search filter
- Findings:
  - Needed mirrored mode control + clearer Basic admin behavior text.
- Fixes applied:
  - Mode selector in Appearance applies immediately.
  - Safety section now shows explicit admin toggle guidance in Basic.
  - Advanced section hidden in Basic.

## 2) Style/System Audit

- Typography and spacing: tokenized and consistent through shared QSS.
- Scrollbars: themed vertical/horizontal scrollbars present; no default Qt scrollbar found in audited pages.
- Splitters: themed handles and hover style present.
- Row/card/drawer usage: unified via shared components; no new ad-hoc styling introduced in this pass.

## 3) Run Status / Log Visibility Audit

Issue found:
- Top bar status area was previously not large/readable enough for live status.

Fixes applied:
- Increased top-bar run-status card width.
- Increased status icon size.
- Kept two-line status:
  - Line 1: `Ready` / `Running: <tool>`
  - Line 2: last log line + elapsed + spinner while running
- Status card and button open ToolRunner.
- Top bar updates from run lifecycle callbacks (`start/progress/log/result/error/cancel`).

## 4) Window Title / Version Audit

Requirement:
- Window title must be exactly `Fix Fox` and not include version/build text.

Result:
- Main window title is `Fix Fox`.
- Version remains in About card only.

## 5) Resize / Clipping Audit

- Guardrails present (`MIN_WINDOW_SIZE`, responsive right-panel collapse).
- No clipping regressions introduced by mode toggle/status card changes in smoke/offscreen checks.
- Manual 1080p/min-size validation remains tracked in UI QA docs.

## 6) ToolRunner Wiring Audit

Checked execution entrypoints:
- Fix actions
- Tool launches
- Script tasks
- Runbooks
- Evidence collection
- Exports

Result:
- All audited runs route via ToolRunner with event streaming.
- No embedded run-output box reintroduced on pages in this pass.

## 7) Marker Scan (TODO/FIXME/NotImplemented/pass)

Command:
- `rg -n "TODO|FIXME|NotImplemented|placeholder|\\bpass\\b" src docs`

Result summary:
- No `TODO`/`FIXME`/`NotImplemented`/`pass` markers in `src` implementation code after this pass.
- Documentation files may still contain the word `pass` in QA status prose.

## 8) Smoke Baseline

Command:
- `python -m src.tests.smoke`

Result:
- Pass (includes Basic->Pro toggle checks and ToolRunner/event assertions).
