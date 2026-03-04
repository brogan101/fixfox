# UI Mega Sprint Notes

## Scope
Safe UI refactor for `python -m src.app` with core behaviors preserved (diagnostics/fixes/runbooks/export/masking/validator/sessions).

## Step 0 Baseline

### Startup and smoke
- `python -m src.app`: launches successfully (process remains active in UI loop; command timed out in automated check by design).
- `python -m src.tests.smoke`: passed.
- `py src/app.py`: prints friendly message `Run via: python -m src.app` and exits.

### Baseline UI pain points captured
- Tool execution UX existed but was not exposed for standalone Script Tasks and evidence collection workflows.
- Tool Runner lacked output pause and saved-output attachment back into session evidence.
- Settings hub had categories but many items lacked explicit one-line explanations and collapsed details.
- Playbooks page needed clearer separation between tools directory, favorites, script tasks, curated runbooks, and full runbook directory.
- Diagnose "Top 3" summary used multiline string formatting instead of a cleaner one-line summary style.

### Baseline tool output pain points captured
- No first-class UI route to run Script Tasks with full Tool Runner context.
- No one-click core evidence collection flow using Tool Runner.
- Re-run action in Tool Runner was informational only.
- Saved Tool Runner output did not automatically attach into evidence export flow.

### Baseline multiline `"Title\nSubtitle"` inventory
- No remaining `QListWidget` rows were built from newline-concatenated title/subtitle strings; row widgets were already in use via `FeedRenderer` + `setItemWidget`.
- Multiline strings still existed in detail/output text surfaces (expected):
  - Diagnose context details (`_update_diagnose_context`)
  - Runbook detail output (`_set_runbook_selection`)
  - Redaction preview (`_update_redaction_preview`)
  - Confirmation/detail drawers (fix/runbook)

### Import/run warnings observed
- No import warnings during module startup/smoke.
- Environment note: `git` CLI is not available in current shell session.

## What Changed

### 1) Tool Runner execution UX upgrades (`src/ui/components/tool_runner.py`)
- Added `Pause Output`/`Resume Output` control with buffered log handling.
- Added monospace live output font for readability.
- Added `output_saved` signal.
- Save dialog now defaults to evidence root when available.
- Saved outputs emit path for session evidence attachment.
- Evidence folder open fallback supports preconfigured evidence root.
- Partial updates now set explicit status text.

### 2) Global task runner plumbing (`src/ui/main_window.py`)
- `_start_task(...)` extended with:
  - `rerun_cb`
  - `evidence_root`
- Tool Runner now wires:
  - real rerun callback when provided
  - output-saved callback to attach output into current session evidence
- Added `_on_tool_runner_output_saved` to persist saved output into evidence list.

### 3) Playbooks/Tools structure cleanup (`src/ui/main_window.py`)
- Tools segment now has:
  - `Top Tools` (max 10)
  - `Favorite Tools`
  - full `Tool Directory`
  - `Script Tasks` directory (search/filter-aware)
  - `Run Selected` and `Collect Core Evidence` actions
- Runbooks segment now has:
  - `Curated Runbooks` (3 home + key IT, max 6)
  - full `Runbook Directory`
  - existing dry-run/run actions preserved

### 4) Script Tasks + Evidence Collection in Tool Runner (`src/ui/main_window.py`)
- Added standalone Script Task execution path:
  - `_run_script_task(...)`
  - `_on_script_task(...)`
  - `_run_selected_script_task(...)`
  - `_refresh_script_tasks(...)`
- Added core evidence collection path:
  - `_collect_core_evidence(...)`
  - `_on_evidence_collection(...)`
- Added deterministic next-step hints per task category (`_task_next_steps`).

### 5) Context menus + command palette integration (`src/ui/main_window.py`)
- Added Script Task right-click menu (`_task_menu`) with copy/preview/dry-run/run/pin actions.
- Command palette `task` selection now routes to Tools segment and selects matching Script Task.
- Command palette result ordering now explicitly groups by kind precedence.

### 6) Settings hub polish (`src/ui/main_window.py`)
- Added per-setting one-line explanations using helper labels.
- Added collapsed details drawers for Safety, Privacy/Masking, and Appearance.
- Advanced section now includes `Export Diagnostics` shortcut (core evidence collection flow).

### 7) Diagnose summary cleanup (`src/ui/main_window.py`)
- Replaced multiline bullet text in top findings summary with one-line joined summary (`_top_findings_text`).

## How To Test

1. Launch app:
   - `python -m src.app`
2. Validate direct-run guard:
   - `py src/app.py`
3. Run smoke test:
   - `python -m src.tests.smoke`
4. In UI, verify:
   - Playbooks -> Tools: Top/Favorites/Directory/Script Tasks all render.
   - Script Task run opens Tool Runner with live output and pause/resume.
   - `Collect Core Evidence` opens Tool Runner and populates Reports evidence list.
   - Tool Runner `Save Output` attaches saved file into evidence.
   - Tool Runner `Re-run` works for quick check/fix/export/runbook/script task/evidence collection.
   - Diagnose shows grouped accordion feed and right context updates on selection.
   - Settings -> Appearance live applies palette/mode/density.
   - Settings entries show one-line explanations and collapsible details.
   - Right-click menus still work on findings/fixes/tools/runbooks/sessions/favorites/script tasks.

## Known Follow-Ups
- Command palette is grouped by kind ordering and chips; explicit visual group headers can be added later if desired.
- Core evidence collection currently uses a fixed default task bundle; optional selectable presets can be added without core behavior changes.
- Tool execution for `ms-settings` tools remains launch-based (no long-running logs by design).
