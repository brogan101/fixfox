# MODE SPEC (Basic vs Pro)

## Source of Truth
UI mode policy is centralized in `src/ui/ui_state.py`.
- `is_basic(...)`
- `is_pro(...)`
- `layout_policy(settings)`

All page-variant visibility is driven from that policy in `MainWindow._apply_mode_visibility()`.

## Basic Mode (default)
Purpose: consumer-safe guided troubleshooting with minimal noise.

### Layout + behavior
- Right concierge panel defaults to collapsed.
- Playbooks uses **Guided Goals** layout only:
  - Goals shown: Wi-Fi, Space, Speed, Printer, Browser, Crashes.
  - Each goal card shows `Runs:` bullets and one `Start` action.
  - Pro console (tools directory/runbooks/task walls) is hidden.
- Advanced script tasks are hidden.
- Fixes:
  - Recommended scope is enforced.
  - Risk filter chips are hidden.
  - Admin/Advanced items are hidden unless `Allow admin tools` is enabled.
- Reports:
  - Preset locked to `home_share`.
  - Advanced export controls (`Include logs`, override button) hidden.
- Settings:
  - Advanced section hidden.
  - Safety/Privacy/Appearance remain primary.

## Pro Mode
Purpose: IT console with full visibility and operational controls.

### Layout + behavior
- Right concierge panel defaults to visible.
- Playbooks uses full Pro console:
  - Segmented Tools/Runbooks view.
  - Tool/runbook directories and detail panes.
  - Advanced script tasks toggle available.
- Fixes shows scope controls, risk chips, and rollback center.
- Reports exposes all presets and advanced evidence options.
- Settings shows Advanced section.

## Implementation notes
- `AppSettings.ui_mode` remains persisted in `core/settings.py`.
- `layout_policy` flags are consumed by:
  - `_apply_settings_mode_visibility`
  - `_apply_playbooks_mode_visibility`
  - `_apply_reports_mode_visibility`
  - `_refresh_fixes`
- Basic vs Pro is now a true layout variant (not just list filtering).
