# UI Audit And Plan

## Scope
- Repository: `IT Core` PySide6 desktop app.
- Audit date: 2026-03-03.
- Objective: elevate UI structure/execution UX to pro-grade without changing diagnostic/fix/runbook/export/masking/session outcomes.

## Current UI Map (src/ui/main_window.py)
- Shell composition:
  - `MainWindow.__init__`: top bar + left nav + center stack + right concierge panel.
  - `_build_top_bar`: search, cancel task, export/help/menu/panel buttons.
  - `_build_nav`: left rail rows.
  - `_build_context_bar`: active session context strip.
  - `_build_pages`: mounts Home/Playbooks/Diagnose/Fixes/Reports/History/Settings.
- Home:
  - `_build_home`, `_refresh_home_history`, `_refresh_home_favorites`.
- Playbooks/Tools:
  - `_build_toolbox`, `_refresh_toolbox`, `_refresh_runbooks`, `run_selected_runbook`, `_launch_tool_payload`.
- Diagnose:
  - `_build_diagnose`, `_rebuild_diagnose_sections`, `_update_diagnose_context`.
- Fixes:
  - `_build_fixes`, `_refresh_fixes`, `run_fix_action`.
- Reports:
  - `_build_reports`, `_refresh_evidence_items`, `export_current_session`, `_on_export`.
- History:
  - `_build_history`, `_refresh_history`, `_load_session`, `reexport_selected_session`.
- Settings:
  - `_build_settings`, `_sync_settings_ui`, `save_settings_from_ui`.
- Global execution plumbing:
  - `_start_task`, `_cancel_task`, `_finish_task` using `TaskWorker` from `src/core/workers.py`.

## What Is Jumbled (specific)
- Tools and runbooks are co-located in one page (`_build_toolbox`) with three columns competing for attention.
- Settings is a grid of cards (`_build_settings`) rather than category-driven settings IA; advanced/feedback/appearance controls are visually mixed.
- Execution UX is fragmented:
  - fixes: toast + implicit action append
  - runbooks: JSON dump into `rb_out`
  - exports: status card text
  - no single, reusable execution surface.
- Command palette uses plain `QListWidgetItem` text rows; not aligned with app row system.

## Visual/System Inconsistencies
- Typography: mostly consistent but some ad-hoc control heights and text block density differ by page.
- Iconography: icon buttons are standardized, but command palette/list rows still fallback to plain item text.
- Row formatting: most lists are row widgets, but command palette is still raw list text.
- Minor defect: `KebabMenuButton` label in `src/ui/components/rows.py` is mojibake (`⋯`) instead of ellipsis glyph.
- Settings page does not match Windows-style left-category/right-content structure.

## Tool Execution / Output UX Issues
- `TaskWorker` emits `progress`, `partial`, `result`, but no dedicated `log_line` stream.
- Output presentation varies by feature and often lands in generic cards/text areas rather than a focused runner.
- No unified controls (copy summary/raw output, open evidence folder, rerun, export pack) at run time.
- Runbook output is currently a JSON block, which is technically complete but not operator-friendly.

## Target UI Shell
- Keep 7-nav shell: Home, Playbooks, Diagnose, Fixes, Reports, History, Settings.
- Top command bar remains global and consistent.
- Right concierge panel remains max 3 cards and auto-collapses on narrow widths.
- Enforce row widgets for all high-frequency lists including command palette.

## Target Page Layouts
- Diagnose:
  - keep current summary + accordion + context pattern; tighten spacing and runner hooks.
- Playbooks/Tools:
  - segmented control (`Tools | Runbooks`) to separate concerns while staying in same nav page.
- Settings:
  - left category list + right section stack:
    - Safety
    - Privacy/Masking
    - Appearance
    - Advanced
    - About
    - Feedback
- Reports:
  - retain export controls + evidence list, add quick launch into unified Tool Runner for long-running actions.

## Migration Plan (safe order)
1. Confirm run-mode and launch invariants still hold (`python -m src.app`, direct file guard).
2. Finish design tokens:
   - add elevation tokens in `ThemeTokens` and consume in QSS.
3. Shell refinements and IA cleanup:
   - segmented Tools/Runbooks view in Playbooks page.
4. Settings hub redesign:
   - migrate `_build_settings` from grid cards to category navigation + stacked content.
5. Command palette row-widget migration.
6. Add unified Tool Runner component and worker log-line plumbing.
7. Integrate Tool Runner entry points for fixes/runbooks/exports and tool task runs.
8. Consistency sweep:
   - reduce modal usage to destructive confirms only
   - ensure skeleton/empty-state consistency.
9. QA gates after each major phase (`tests.smoke`, startup sanity, quick manual flows).

## Do-Not-Break Checklist
- Quick Check worker run + findings render.
- Diagnose row selection + right-click actions.
- Fix execution behavior and rollback messaging.
- Runbook step execution semantics and evidence outputs.
- Export structure + validator rules + masking behavior.
- History reopen/re-export.
- Session persistence and favorites persistence.
- Canonical run mode (`python -m src.app`) and direct file guard.

## Notes
- Core command behavior, export manifest contract, masking policy, and session schema remain untouched.
- UI refactor will use adapter layers and shared execution presentation; core task logic remains source-of-truth in `src/core/*`.
