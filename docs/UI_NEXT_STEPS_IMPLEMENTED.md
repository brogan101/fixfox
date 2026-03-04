# UI Next Steps Implemented

Date: 2026-03-03  
Repo: IT Core (PySide6)  
Scope: UI/UX structure overhaul without core behavior changes.

## Phase 0
- Added full walkthrough audit:
  - `docs/UI_WALKTHROUGH_AUDIT.md`

## Phase 1: Global Structure
- Session context bar is now contextual:
  - Full context row is shown only when an active session exists.
  - No-session state shows compact hint: `No active session - start a goal from Home.`
  - Implemented in `src/ui/main_window.py`:
    - `_build_context_bar()`
    - `_has_active_session()`
    - `_sync_context_bar_visibility()`
    - `_on_nav()`, `_update_context_labels()`
- Right panel (Concierge) converted to detail/action surface:
  - Dynamic detail card by page selection.
  - Deterministic action button per page context.
  - Diagnose adds evidence impact card + `Collect Evidence Now`.
  - Implemented in:
    - `_update_concierge()`
    - `_detail_for_page()`
    - `_detail_action_for_page()`
    - `_diagnose_action()`
    - `_evidence_hint_for_finding()`
- Header help now opens local in-app help center dialog (not transient toast):
  - `HelpCenterDialog` with `Start Here`, `Privacy`, `Safety`, `KB Pattern`.
  - Implemented in:
    - `HelpCenterDialog`
    - `_show_page_help()`

## Phase 2: Home
- Goal cards upgraded into mini playbook cards:
  - 1-line intent, `Runs:` bullets, safety label, `Start` + `Learn More`.
- Added `What Changed Since Last Run` card:
  - Pending reboot status.
  - Recent updates status (best effort from session findings).
  - Reliability critical event count (best effort from reliability snapshot payload).
  - Implemented in `_refresh_home_changes()`.
- Favorites renamed to `Quick Actions` with `Manage Favorites`.
- Quick actions remain capped to 6 and run via existing execution paths (ToolRunner-based for tools/runbooks/fixes).

## Phase 3: Playbooks
- Tools layout restructured to master-detail:
  - Left: `Pinned` (Top + Favorites) + `Tool Directory`.
  - Right: `Tool Detail` with context + `Run`, `Dry Run` (task-capable), `Export Pack`.
- Script tasks hidden by default behind explicit toggle:
  - `Show advanced script tasks` / `Hide advanced script tasks`.
- Runbooks embedded output box removed:
  - Removed `rb_out` UI path.
  - Runbook detail now shows summary + steps preview drawer.
  - ToolRunner remains execution/output surface.
- Key handlers:
  - `_toggle_advanced_script_tasks()`
  - `_set_selected_tool()`
  - `_set_selected_script_task()`
  - `_run_selected_tool()`
  - `_dry_run_selected_tool()`
  - `_set_runbook_selection()`
  - `_on_runbook()` (no embedded QTextEdit output sink)

## Phase 4: Diagnose
- Added findings toolbar:
  - Search input.
  - Severity filter.
  - `Recommended only` toggle.
- Enforced single default detail pattern:
  - Removed page-local Diagnose right detail column.
  - Details now use global right panel context.
- Diagnose selection now updates global detail/actions only.
- Evidence hook available from right panel.

## Phase 5: Fixes
- Converted to master-detail:
  - Left: Fix directory with policy/risk controls.
  - Right: Fix detail (plain language, risk, rollback, commands), preview/run actions.
- Risk controls moved from single dropdown feel to policy summary + risk chips:
  - `Safe`, `Admin`, `Advanced`.
  - `Safe-only mode ON/OFF` summary.
- Rollback center implemented as session-driven list for reversible actions:
  - Current reversible mapping: startup launch enable/disable pair.
  - Undo action runs through existing fix execution + ToolRunner flow.

## Phase 6: Reports
- Reports converted into guided 3-step flow via tabbed stepper:
  - `1. Configure`
  - `2. Preview`
  - `3. Generate`
- Redaction UX updated:
  - Before/after preview retained.
  - Token map label added (`PC_1 / USER_1 / SSID_1`).
- Evidence checklist presented as status rows:
  - `Collected ✅ / Missing ⚠️ / Optional ⓘ`
  - `Collect Now` button tied to core evidence collector.
- Existing export behavior preserved (`export_current_session()` / validator path unchanged).

## Phase 7: History
- Session detail updated to case summary content:
  - Goals run, top findings, actions taken, exports generated, evidence collected.
- Compare view expanded:
  - Finding count delta.
  - Disk free delta.
  - Pending reboot delta.
  - Action count delta.
  - Rendered into compare drawer card.

## Phase 8: Settings
- Settings nav row visuals aligned with main-rail row style pattern (icon + label widget rows).
- Added settings search with label/description filtering.
- Added settings management actions:
  - `Reset Defaults`
  - `Export Settings JSON`
  - `Open Help Center`

## Phase 9: ToolRunner
- Control clutter reduced:
  - Primary buttons: `Cancel`, `Copy Summary`, `Export Support Pack`.
  - Secondary actions moved under `More` dropdown:
    - Copy ticket summary
    - Copy raw output
    - Save output
    - Open evidence folder
    - Pause/resume output
    - Re-run
- Overview now uses deterministic structure:
  - What ran
  - What was found
  - What changed
  - Next steps
- Tool-specific context blocks added in overview:
  - Network
  - Updates
  - Printer
  - Storage
  - Integrity

## Phase 10: In-app help content
- Added local “site-like” help center content system via `HelpCenterDialog`:
  - Start Here
  - Privacy
  - Safety
  - KB Pattern
- Reachable from:
  - `?` page header buttons (all pages using shared header helper)
  - Settings tools row / About section help button

## How To Test By Area

1. Home
- Open Home.
- Verify mini playbook cards show `Runs:` bullets + safety line.
- Click `Start` and confirm ToolRunner opens for Quick Check path.
- Verify `Quick Actions` list max is 6 and `Manage Favorites` opens Settings.

2. Playbooks
- Open Playbooks -> Tools.
- Verify left directory + right detail pattern.
- Select a tool and click `Run`.
- Click `Show advanced script tasks` and verify advanced task panel appears.
- Switch to Runbooks and verify no embedded output text box exists.

3. Diagnose
- Run Quick Check.
- Open Diagnose and verify toolbar search/filter controls.
- Select finding and verify detail appears in right global panel.
- Use right-panel evidence action (`Collect Evidence Now`).

4. Fixes
- Open Fixes.
- Verify left list + right detail.
- Toggle risk chips and confirm filtering updates.
- Run a reversible startup action and verify rollback entry appears in Rollback Center.

5. Reports
- Open Reports and verify 3-step tabs.
- Step 2: verify redaction before/after + token map and evidence checklist rows.
- Step 3: run `Generate Export` and verify validation status updates.

6. History
- Open History.
- Select a session and verify case summary fields.
- Click `Compare with Active` and verify multi-metric delta output.

7. Settings
- Use settings search to filter sections.
- Run `Export Settings JSON` and verify file output.
- Run `Reset Defaults` and verify settings are reset live.

8. ToolRunner
- Trigger any tool/fix/runbook.
- Verify primary action row is reduced and secondary actions are in `More`.
- Verify overview template sections are deterministic.

## Core Behavior Safety Notes
- Execution still routes through existing worker-based architecture (`TaskWorker` + cancel/timeout).
- ToolRunner remains the run/output surface; removed runbook embedded output box path.
- No core diagnostics/fix/export/session/masking/validator behavior was intentionally changed.
