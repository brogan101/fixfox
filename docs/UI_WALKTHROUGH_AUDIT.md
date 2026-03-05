# UI Walkthrough Audit (Phase 0)

Date: 2026-03-03  
Scope: `src/ui/main_window.py`, `src/ui/components/tool_runner.py`, `src/ui/widgets.py`, row/feed components.

## Global Shell

- Nav rail (`Home`, `Playbooks`, `Diagnose`, `Fixes`, `Reports`, `History`, `Settings`)
  - Implementation: `MainWindow.NAV_ITEMS`, `_build_nav()`, `_nav_item_widget()`
- Top bar (search, cancel task, reports, help, panel toggle, menu)
  - Implementation: `_build_top_bar()`
- Session context bar (session/symptom/share-safe/preset/last run + Export/Copy/End Session)
  - Implementation: `_build_context_bar()`, `_update_context_labels()`, `_on_nav()`
- Right panel (Details Side Sheet)
  - Implementation: `SideSheet`, `_update_concierge()`

Problems:
- Context bar is only page-gated by nav (`Diagnose`, `Fixes`, `Reports`) instead of session state.
- Right panel duplicates information already rendered inside some pages (notably Diagnose).
- Header structure exists, but chip/filter row behavior is inconsistent per page.

---

## Home

Visible controls:
- `Start Quick Check`
- Goal cards with `Start` buttons (`Speed`, `Space`, `Wi-Fi`)
- Favorites feed (context menu)
- Recent sessions feed (activate/context)
- `Export Last Pack`

Implementation:
- `_build_home()`
- Handlers: `run_quick_check()`, `_launch_home_favorite()`, `export_last_session()`, `_refresh_home_favorites()`, `_refresh_home_history()`

Problems:
- Goal cards are light on “what this runs” context.
- Favorites card naming inconsistent with quick-action intent.
- “What changed since last run” is not surfaced as a dedicated card.
- Information hierarchy is broad but not strongly guided for first-time users.

Target plan:
- Upgrade goals into mini-playbook cards with runs/safety badges.
- Rename Favorites to Quick Actions, cap at 6, keep manageable.
- Add “What changed since last run” card (pending reboot, updates, reliability critical count).

---

## Playbooks

Visible controls:
- Search box
- Segment switch (`Tools`/`Runbooks`)
- Tools view:
  - Category filter
  - Top Tools feed
  - Favorite Tools feed
  - Tool Directory feed
  - Script Tasks feed + category filter + `Run Selected` + `Collect Core Evidence`
- Runbooks view:
  - Audience filter
  - Curated runbooks feed
  - Runbook directory feed
  - `Run Dry-Run`, `Run`
  - Embedded runbook output box

Implementation:
- `_build_toolbox()`, `_switch_playbooks_segment()`
- Refresh/selection: `_refresh_toolbox()`, `_refresh_script_tasks()`, `_refresh_runbooks()`, `_set_runbook_selection()`
- Actions: `_launch_tool_payload()`, `_run_script_task()`, `run_selected_runbook()`, `_collect_core_evidence()`

Problems:
- Tools/Runbooks/ScriptTasks form a dense control wall.
- Script Tasks are exposed by default for all users.
- Runbooks include embedded `QTextEdit` output (`self.rb_out`) instead of ToolRunner-only flow.
- Tool detail is weak; list-first behavior without strong right-side detail model.

Target plan:
- Convert Tools to two-column master-detail.
- Consolidate Top/Favorites into pinned collapsible sections.
- Hide script tasks behind explicit advanced toggle.
- Remove embedded runbook output; keep preview/details and run actions only.

---

## Diagnose

Visible controls:
- Findings sections by category (accordion rows)
- Left summary cards (`No active session`, severity snapshot, top 3)
- Right-side page-local detail cards + drawer (`What this means`, `Next best action`, `KB and related`, `Finding Detail`)

Implementation:
- `_build_diagnose()`
- Data/render: `_rebuild_diagnose_sections()`, `_update_diagnose_context()`, `_deterministic_next_action()`, `_related_kb()`
- Menus: `_finding_menu()`

Problems:
- No dedicated findings toolbar (search/severity/recommended only).
- Detail is duplicated between page-local right column and global concierge panel.
- Next-best action is mostly text; not always directly actionable.
- Evidence collection hooks are present elsewhere, not inline in Diagnose context.

Target plan:
- Add findings toolbar (search, severity chips/filter, recommended-only toggle).
- Use one default detail surface (global right panel).
- Make next-best action include direct run button.
- Add evidence impact + collect-now action in detail context.

---

## Fixes

Visible controls:
- Scope filter (`Recommended`/`All`)
- Risk filter (`Any Risk`, `Safe`, `Admin`, `Advanced`)
- `Undo Center` single button (`Disable Startup Launch`)
- Fix feed rows with preview/run

Implementation:
- `_build_fixes()`
- Refresh/action: `_refresh_fixes()`, `run_fix_action()`, `_on_fix()`
- Menu: `_fix_menu()`

Problems:
- Page is filter-heavy on left with no clear selected fix detail pane.
- Risk communication is dropdown-centric rather than policy+chips.
- Undo center is hard-coded to one action; not a real session rollback list.

Target plan:
- Move to master-detail with explicit selected fix details.
- Show policy summary + risk chips with tooltips.
- Build rollback list from reversible session actions and run undo through ToolRunner.

---

## Reports

Visible controls:
- Preset combo
- Share-safe, mask IP, include logs toggles
- Redaction preview `QTextEdit`
- Export preview tree
- `Generate Export`
- Quick actions (open folder/copy path/copy summaries)
- Evidence feed + checklist text

Implementation:
- `_build_reports()`
- Logic: `_rebuild_report_tree()`, `export_current_session()`, `_on_export()`, `_update_redaction_preview()`, `_refresh_evidence_items()`, `_evidence_menu()`

Problems:
- Single-page control panel rather than guided flow.
- Step order is not strongly signposted.
- Evidence checklist is plain text and not clear status rows.
- Redaction mapping tokens are not explicitly explained next to preview.

Target plan:
- Convert to a 3-step guided export layout (config -> preview -> generate/post-actions).
- Keep before/after redaction preview with token map context.
- Present evidence checklist as status rows with collect-now action.

---

## History

Visible controls:
- Search sessions
- Sessions feed
- Detail card (`Reopen Session`, `Compare with Active`, `Re-export`)
- Run center feed

Implementation:
- `_build_history()`
- Refresh/detail/actions: `_refresh_history()`, `_update_history_detail()`, `reopen_selected_session()`, `compare_with_active_session()`, `reexport_selected_session()`, `_refresh_run_center()`

Problems:
- Session detail card is terse; no “case summary” structure.
- Compare output is text-only (single finding delta) and not visual/multi-metric.
- Timeline concept is present but under-communicated.

Target plan:
- Expand detail into case summary (goals/findings/actions/exports/evidence).
- Add visual compare drawer/card with key deltas.

---

## Settings

Visible controls:
- Left settings nav (Safety, Privacy/Masking, Appearance, Advanced, About, Feedback)
- Multiple settings cards and toggles
- Advanced operational buttons
- Local feedback form (`QTextEdit`)

Implementation:
- `_build_settings()`
- State sync/save: `_sync_settings_ui()`, `save_settings_from_ui()`

Problems:
- Nav rows are less aligned with main rail styling conventions.
- No settings search.
- No reset-to-defaults or export-settings action.

Target plan:
- Align nav row style with main rail widgeting.
- Add settings search filter.
- Add reset/export settings controls.

---

## Duplicate Detail Surfaces

- Diagnose has both:
  - page-local right detail column (`_build_diagnose()` cards + drawer)
  - global right concierge panel (`_update_concierge()`)
- These should be unified to avoid cognitive split.

## Embedded Output Widgets To Remove (ToolRunner-first policy)

- Runbooks page embedded output: `self.rb_out = QTextEdit()` in `_build_toolbox()`
- Partial updates currently push into `rb_out` in `_start_task()`
- Runbook completion writes JSON into `rb_out` in `_on_runbook()`

Action:
- Replace with runbook description/steps preview and keep execution output in ToolRunner only.

## Resize / Clipping / Overlap Risk Areas

- Multi-column pages with dense controls:
  - Playbooks tools triple-column + large feeds + button rows
  - Diagnose triple-column
  - Reports three columns plus long quick action stacks
- Fixed-width settings nav (`220`) and main nav max width (`260`) can crowd center in narrow windows.
- Context bar label row can compress with long values.
- Guardrails exist (`MIN_WINDOW_SIZE`, right panel auto-collapse), but per-page scroll/action reachability requires QA passes.

---

## Refactor Order (Target)

1. Session context behavior + right panel detail model + standard headers/help.
2. Home starter UX and quick actions.
3. Playbooks master-detail + advanced gating + runbook output cleanup.
4. Diagnose toolbar + single-detail-surface behavior.
5. Fixes master-detail + risk/policy clarity + rollback list.
6. Reports 3-step flow + redaction/evidence status UX.
7. History case timeline + visual compare.
8. Settings search/nav parity + reset/export.
9. ToolRunner control simplification + deterministic overview + context blocks.
10. In-app help content center.
11. UI control coverage QA and smoke/regression tests.
