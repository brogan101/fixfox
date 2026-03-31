# FixFox Release Notes

## 1.1.0 - 2026-03-27

- Pass 1: fixed the app icon across the main window, title bar, Alt+Tab, and tray, verified service registration consistency, normalized source encoding to UTF-8, and added compiled-resource integrity tests.
- Pass 2: expanded the repair catalog from 213 entries to 354 with real executable fixes, filled missing descriptions and category subtitles, and added stronger catalog integrity coverage.
- Pass 3: redesigned the Fix Center around a real category rail, stronger fix cards, scoped search, and better empty states with live automation IDs for UI coverage.
- Pass 4: completed a whole-app fit-and-finish sweep across XAML and user-visible copy, removing redundant labels, tightening spacing and typography, improving empty states, and hardening dialogs and shell surfaces.
- Pass 5: added real product features including the dashboard status bar, receipt compare and export, automation attention queue, recurrence editor, bundle presets and progress, tray quick actions, and onboarding checklist persistence.
- Pass 6: added six deeper rescue runbooks, wired missing Windows deep-links, and expanded thinner support centers with security, file-share, network, and recovery actions.
- Pass 7: added real desktop UI automation with FlaUI, stabilized startup and navigation for user testing, and fixed live UI regressions surfaced by end-to-end smoke flows.
- Pass 8: improved responsiveness by deferring noncritical startup work, reducing command palette churn, separating light and heavy settings saves, and revalidating the real UI flow timings.
- Pass 9: completed release-readiness hardening with policy-state tags, stronger settings recovery, advanced-mode surfacing, accessibility name/id fixes, versioned settings migration, and release pipeline validation.
- Catalog size: 354 fixes across 21 categories, plus 16 maintenance and automation bundles.
- Known limitations:
  - tray and context-menu automation coverage is still lighter than the main page-flow coverage
  - very large history sets now page in incrementally, but a future pass should replace the remaining scroll-hosted receipt list with a fully virtualized control
  - the packaging pipeline skips the installer step when Inno Setup 6 is not installed on the build machine

## 1.0.0

- Hardened repair receipts, guided-flow truthfulness, and evidence export privacy tiers.
- Added support centers, maintenance profiles, and stronger runbooks for common Windows support scenarios.
- Improved shell interaction, tray actions, command palette routing, and desktop-style context actions.
- Added first-run setup, stronger settings recovery, local help docs, and a local release-feed/update configuration.
