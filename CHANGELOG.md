# Changelog

## 0.9.1-beta2 - 2026-03-04
- Rebuilt page architecture to class-based page widgets under `src/ui/pages/*_page.py`; removed legacy wrapper page modules.
- Rebuilt top header for responsive behavior with compact search fallback and overflow actions menu.
- Implemented real palette system with 3 actual palettes (`Fix Fox`, `Graphite`, `High Contrast`) plus live theme mode/density switching.
- Split nav styling targets (`MainNav` and `SettingsNav`) and improved selected/hover/focus accessibility states in QSS.
- Added persistent layout state: window geometry, splitter sizes, last page, and last settings section.
- Added `Privacy & Safety` settings section with explicit local-only/telemetry/storage behavior copy.
- Bundled `DejaVu Sans` and loaded fonts at startup; added font licensing docs and QA checklist.

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
