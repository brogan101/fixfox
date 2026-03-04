# UI Polish QA Checklist

1. Launch with `python -m src.app`.
2. Open `Settings -> Appearance` and switch palette across `Fix Fox`, `Graphite`, and `High Contrast`.
3. Switch `Mode` (`light`/`dark`) and `Density` (`comfortable`/`compact`) and verify the whole app updates immediately.
4. Resize the window down to narrow widths:
5. Confirm the top search collapses to a search icon.
6. Confirm the top bar stays usable (no clipped/overlapping controls).
7. Open `Top Bar -> More actions` and verify help/settings/command actions open.
8. Use keyboard shortcuts:
9. `Ctrl+K` opens command palette.
10. `Ctrl+E` jumps to Reports.
11. `Ctrl+R` opens ToolRunner.
12. `Ctrl+\` toggles right panel.
13. Navigate with keyboard focus through main nav and settings nav; selected/hover/focus states should be clear.
14. In Settings, verify `Privacy & Safety` section shows local-only behavior and storage paths.
15. Change page, settings subsection, window size/position, and splitter handles; close and relaunch.
16. Confirm the app restores last page, last settings section, geometry, and splitter layout.
