# Repo Structure (UI + Assets)

## UI
- `src/ui/main_window.py`: thin entrypoint for main window orchestration.
- `src/ui/main_window_impl.py`: runtime orchestration/state/event wiring.
- `src/ui/components/`: reusable shell and shared UI primitives.
  - `app_shell.py`: rail + app bar + side sheet + status bar
  - `nav.py`: rail-only navigation
  - `toolbar.py`: top app bar
  - `onboarding.py`: 3-step first-run flow
- `src/ui/pages/`: page widgets (home/diagnose/fixes/playbooks/reports/history/settings).
- `src/ui/style/`: theme tokens, density, and QSS builder.

## Assets
- `src/assets/branding/`: runtime brand assets.
  - `fixfox.png`
  - `fixfox.ico`
  - `fixfox_mark.svg`
- `src/assets/fonts/`: bundled UI font + license.
- `src/assets/icons/`: reserved for future non-brand icon assets.

## Build + Packaging
- `scripts/build_exe.ps1`: local PyInstaller build.
- `tools/make_icons.py`: generates/mirrors icon derivatives.

## Test Entry Points
- `python -m src.tests.smoke`
- `python -m src.tests.test_unit`
- `python -m src.tests.test_ui_layout_sanity`
