# UI Polish QA Checklist

1. Launch app: `python -m src.app`.
2. Confirm shell layout: rail-only nav on left, top app bar, content center, side sheet hidden by default.
3. Verify no duplicate main nav list exists (only `NavRail`).
4. In app bar, confirm Fix Fox mark + wordmark + status pill are visible.
5. Resize to `1024x768`, `1280x720`, `1600x900` and confirm no clipping/overlap.
6. Verify search collapses to icon at narrow width and expands at larger width.
7. Open overflow menu and validate grouped sections: Session, View, Help.
8. Toggle Theme (`light/dark`), Density (`comfortable/compact`), and Palette (`Fix Fox/Graphite/High Contrast`) from overflow; confirm live restyle.
9. Run `Run Quick Check` from app bar and verify:
   - status pill changes to running
   - quick check button enters busy state and disables
   - completion toast includes finding count summary
10. Select a finding/runbook/report item and verify side sheet opens; press `Esc` to close when unpinned.
11. Use keyboard focus navigation and confirm visible focus ring on rail/buttons/inputs/lists.
12. Open `About Fix Fox` from overflow and verify version/build/commit + local-only + logs/exports paths.
13. Relaunch app and verify layout persistence (geometry, splitter, last page, settings subsection).
14. Confirm startup log prints one-time UI audit lines (single NavRail, no legacy nav, side sheet default hidden).
15. Confirm startup has no Qt font warnings and prints selected font once.
