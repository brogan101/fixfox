# UI Acceptance Checklist

All checks executed on 2026-03-05 in local repo.

## Core Shell + Navigation
- [x] Rail-only nav (no duplicate app-level nav)
  - Evidence: runtime audit line from app startup shows `nav_widgets=1 expected=1 (NavRail)` and `legacy_nav_widgets=0`.
  - Code: `src/ui/components/nav.py`, `src/ui/main_window_impl.py`.
- [x] Right side sheet hidden by default
  - Evidence: runtime audit line `side_sheet_visible_default=False`.
  - Code: `src/ui/components/app_shell.py`, `src/ui/main_window_impl.py`.

## Icons + Branding
- [x] Custom icon system loads from `src/assets/icons`
  - Evidence: icon loader prioritizes asset files in `src/ui/icons.py`.
  - Assets: `src/assets/icons/*.svg`.
- [x] No placeholder nav/header glyphs
  - Evidence: toolbar/nav use `get_icon(...)` calls in `src/ui/components/toolbar.py` and `src/ui/components/nav.py`.
- [x] About Qt removed from user-facing overflow/help
  - Evidence: removed from overflow menu construction in `src/ui/main_window_impl.py`.

## Search + Settings + Onboarding
- [x] Search control wired in app bar with consistent behavior
  - Evidence: app bar search interactions in `src/ui/components/toolbar.py`; dispatch and popup handling in `src/ui/main_window_impl.py`.
- [x] Settings section list redesigned to avoid overlap
  - Evidence: icon+label row for settings nav and updated section IA in `src/ui/pages/settings_page.py`.
- [x] Onboarding rebuilt and styled consistently
  - Evidence: new 3-step flow component in `src/ui/components/onboarding.py`; onboarding QSS in `src/ui/style/qss_builder.py`.

## Styling + Responsiveness
- [x] QSS updated for consistent control states and softer focus/splitter visuals
  - Evidence: `src/ui/style/qss_builder.py` updates for rail/app bar/onboarding/splitter focus styling.
- [x] App instantiation and layout sanity pass at common sizes
  - Evidence: `python -m src.tests.test_ui_layout_sanity` passed.

## Fonts
- [x] No invalid DejaVu path usage; startup font loading is validated and clean
  - Evidence: `src/app.py` loads bundled font via `QFontDatabase.addApplicationFontFromData` and validates blob.
  - Runtime output: `[FixFox] UI font: Noto Sans` with no `qt.qpa.fonts` warnings.

## Smoke Gate Script
- [x] Lightweight UI smoke script exists and enforces critical checks
  - Evidence: `scripts/ui_smoke_check.py` exits non-zero on missing icons/forbidden labels/shell construction failures.
  - Runtime: `UI smoke check: PASS`.
