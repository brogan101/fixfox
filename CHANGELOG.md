# Changelog

## 0.9.1-beta3 - 2026-03-05
- UI polish pass for D-style shell: unified spacing/radius tokens, consistent page section rhythm, and tighter typography hierarchy.
- Added inline callout pattern for non-modal error/status messaging on Home/Diagnose/Fixes/Playbooks/Reports/History pages.
- Added busy-state behavior for Quick Check (app bar button busy/disabled, running status pill, completion summary toast).
- Improved interaction state coverage (focus, selected, hover/pressed consistency) including rail buttons and app bar icon buttons.
- Added About Fix Fox dialog from overflow with version/build date/commit plus local-only/logs/exports info.
- Consolidated runtime branding assets to `src/assets/branding/` and updated build scripts/docs to match.
- Removed unused runtime assets (`src/assets/mascot.svg`) and legacy onboarding dialog code path.
- Added `docs/repo-structure.md` and refreshed `docs/ui-polish-checklist.md` for post-polish QA.

## 0.9.1-beta2 - 2026-03-04
- Rebuilt page architecture to class-based page widgets under `src/ui/pages/*_page.py`; removed legacy wrapper page modules.
- Rebuilt top header for responsive behavior with compact search fallback and overflow actions menu.
- Implemented real palette system with 3 actual palettes (`Fix Fox`, `Graphite`, `High Contrast`) plus live theme mode/density switching.
- Split nav styling targets (`MainNav` and `SettingsNav`) and improved selected/hover/focus accessibility states in QSS.
- Added persistent layout state: window geometry, splitter sizes, last page, and last settings section.
- Added `Privacy & Safety` settings section with explicit local-only/telemetry/storage behavior copy.
- Bundled `Noto Sans` and loaded fonts at startup; added font licensing docs and QA checklist.

## 0.9.0-beta1 - 2026-03-02
- Full audit documented.
- App shell UI overhaul with left rail, top command bar, and concierge panel.
- Session context bar and shared Diagnose/Fixes/Reports workflow.
- Export pipeline rebuilt with presets, manifest, hashes, and validator.
- Share-safe masking added to exports and copy actions.
- Runbook engine added (12 runbooks, 20 script tasks).
- Safety policy + diagnostic mode + logging/crash handler added.
- Onboarding and local feedback capture added.
- Smoke and unit tests added.
