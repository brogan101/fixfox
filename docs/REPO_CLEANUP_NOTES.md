# Repo Cleanup Notes (2026-03-05)

## Removed/Consolidated
- Removed legacy main-layout `QSplitter` usage from `src/ui/components/app_shell.py`.
  - Why: required rail + content + optional details panel without draggable splitter handles.
- Removed `QSplitter` handle styling from `src/ui/style/qss_builder.py`.
  - Why: prevent visual splitter artifacts and enforce non-draggable shell layout.
- Removed unused `_header(...)` helper from `src/ui/main_window_impl.py`.
  - Why: dead code after page headers were standardized through `src/ui/pages/common.py`.
- Removed Settings "About" page section from `src/ui/pages/settings_page.py`.
  - Why: consolidate to practical sections (Safety, Privacy, Appearance, Advanced, Feedback) and avoid redundant vanity content.

## Notes
- Core behavior/outcomes for diagnostics, fixes, runbooks, exports, masking, validator, and sessions were preserved.

## 2026-03-05 Additional Cleanup Execution
- Removed stale walkthrough artifact folder:
  - `docs/screenshots/20260305_143145`
  - `docs/screenshots/20260305_143645`
- Kept archived branding history:
  - `archive/legacy_assets/branding/assets_brand/*`
  - `archive/legacy_assets/branding/src_assets_branding/*`
- Kept compatibility module:
  - `src/ui/components/toolbar.py` (re-export shim to `app_bar.py`)

Validation:
- `scripts/ui_walkthrough.py` PASS with final artifact folder `docs/screenshots/20260305_144413`.
- `src.tests.smoke` PASS.
- `src.tests.test_unit` PASS.
