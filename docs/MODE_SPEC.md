# Fix Fox Mode Specification

Date: 2026-03-03

## Source Of Truth

- Setting key: `ui_mode`
- Location: `src/core/settings.py`
- Allowed values:
  - `basic`
  - `pro`
- Default: `basic`
- Persistence: stored with standard app settings payload and loaded on startup.

## Visibility Engine

- Global filter API: `get_visible_capabilities(ui_mode, safety_policy, admin_enabled)`
- Location: `src/core/registry.py`
- Capability metadata fields:
  - `visibility_basic`
  - `visibility_pro`
  - `requires_pro`
  - `requires_admin`
- All UI directories query capability visibility through prefix filters:
  - fixes: `fix_action.<id>`
  - tools: `tool.<id>`
  - runbooks: `runbook.<id>`
  - script tasks: `script_task.<id>`

## Basic Mode

Intent: home-first guided troubleshooting with low-risk defaults.

Exposed navigation:
- Home
- Diagnose
- Fixes
- Reports
- History
- Settings

Hidden navigation:
- Playbooks

Capability scope:
- Fixes: safe fixes only
- Tools: safe top subset (first 8 tools in directory metadata)
- Runbooks: home audience only
- Script tasks: hidden
- Export presets: `home_share` only

Settings behavior:
- Defaults when switching to Basic:
  - `safe_only_mode = true`
  - `show_admin_tools = false`
  - `show_advanced_tools = false`
- Explicit admin override allowed in Basic:
  - `Enable Admin Tools` remains available in Settings.
  - Enabling it automatically disables `safe_only_mode` so admin fixes can run.
  - Disabling it re-enables `safe_only_mode`.
- `show_advanced_tools` remains off in Basic.
- settings "Advanced" section hidden.

Reports defaults:
- preset forced to `home_share`
- include logs remains available but defaults off

## Pro Mode

Intent: full IT/MSP coverage and advanced workflows.

Exposed navigation:
- All pages including Playbooks

Capability scope:
- Fixes: safe + admin + advanced
- Tools: full directory
- Runbooks: home + IT
- Script tasks: visible (with explicit advanced section)
- Export presets: all presets

Settings behavior:
- admin/advanced toggles enabled
- settings "Advanced" section visible

## Runtime Behavior

- Top bar segmented toggle (`Basic` / `Pro`) updates `ui_mode` immediately.
- Settings -> Appearance contains mirrored `ui_mode` selector.
- Mode switch applies live without restart:
  - nav visibility
  - directory lists
  - command palette search scope
  - report preset options
  - settings section gating

## Empty State Rules

- If a directory becomes empty due to mode filtering, the view shows a friendly empty state with a clear message to switch to Pro.
- No blank list panels are shown.
