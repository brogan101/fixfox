# UI Rebuild Checklist (2026-03-05)

## 0) Codex Reliability Harness
- [x] Ran `git fetch --all`
- [x] Printed starting commit hash
- [x] Appended run start data to `docs/CODEX_RUN_LOG.md`
- [x] Created this checklist with all requirement sections
- [ ] Printed ending commit hash
- [ ] Printed `git diff --stat`
- [ ] Added brief change notes
- [ ] Confirmed checklist 100% complete or failed loudly

## 1) Baseline Run + Real File Discovery
- [x] Installed dependencies and ran app as repo expects
- [x] Located app shell layout implementation
- [x] Located nav rail icon wiring
- [x] Located top app bar identity/logo
- [x] Located global search dropdown + command palette
- [x] Located onboarding wizard screens
- [x] Located tool runner styling
- [x] Located settings nav + sections
- [x] Located reports duplicate buttons
- [x] Recorded key file paths in run log

## 2) Primary Goal: Modern Material-ish UI
- [x] UI is visibly and meaningfully different across app
- [x] Generous spacing, hierarchy, soft surfaces, consistent radii
- [x] Limited and purposeful accent usage
- [x] Removed random orange focus/selection artifacts
- [x] Consistent component styling across pages
- [x] Proper scaling on resize with no clipping/dead zones
- [x] Rail-only nav + top app bar + optional right details panel

## 3) Remove Splitter Handles + Responsive Layout
- [x] Removed primary `QSplitter` nav/content/details usage
- [x] Replaced with responsive layout (fixed rail, fill center, optional right panel)
- [x] Details panel closed by default
- [x] Visible button opens details panel
- [x] Optional context open supported
- [x] Close via X and Esc implemented
- [x] Optional pin behavior supported
- [x] No draggable splitter handles visible

## 4) Real Icons + Asset/Branding Fixes
- [x] Added consistent SVG icon set in `src/assets/icons/`
- [x] Rebuilt `src/ui/icons.py` with QtSvg render+tint+cache
- [x] Nav + toolbar icons migrated to real icon keys
- [x] Removed glyph-based nav resolver path
- [x] Settings icon shown as gear
- [x] Branding/logo in top bar renders correctly (no black background)
- [x] Top bar branding includes larger mark + wordmark + subtle subtitle
- [x] Removed stray Qt/non-production icon/button from header

## 5) Search + Command Palette Stability
- [x] Top bar search dropdown remains open while typing and hovering
- [x] Dropdown closes on Esc and outside click
- [x] Fuzzy ranked closest matches implemented
- [x] Results grouped by Goals/Tools/Runbooks/Fixes/Sessions
- [x] Match segments highlighted in results
- [x] Keyboard nav Up/Down/Enter implemented
- [x] Ctrl+K focuses search
- [x] Result click routes correctly and may open details panel
- [x] Command palette restyled/merged to match theme

## 6) Onboarding Rework
- [x] Onboarding redesigned with same theme as app
- [x] Stepper present: Welcome -> Preferences -> First Action -> Finish
- [x] Buttons visible with correct enabled states
- [x] Good contrast in light and dark mode
- [x] Uses real icons + brand mark
- [x] Preferences controls aligned and consistent
- [x] First action choices clear (Quick Check, Open Settings, Resume Session)
- [x] Reset onboarding reachable and clearly labeled
- [x] Saved theme/mode/density/scale applied immediately after finish

## 7) Page Overhaul + Consistent Styling
- [x] Home page rebuilt as dashboard with quick actions/status/recent/recommended
- [x] Diagnose page rebuilt with professional toolbar/list/details integration
- [x] Playbooks page rebuilt with search/filters/catalog rows/actions
- [x] Reports page rebuilt with clear export flow and duplicate buttons removed
- [x] Settings page rebuilt with clean left nav sections/icons and no overlaps
- [x] Settings search filters/jumps to matching settings
- [x] Settings re-style operations are debounced/smooth
- [x] Removed/replaced useless About Qt vanity content
- [x] History + Fixes pages restyled with list/search/filter/empty/details

## 8) Tool Runner Consistency
- [x] Tool runner visuals match app theme
- [x] Progress bar/tabs/buttons styled and unclipped
- [x] Removed thick black framing artifacts
- [x] Streaming logs remain responsive

## 9) Performance + Responsiveness
- [x] Heavy work kept off UI thread
- [x] Theme/scale style reapply debounced
- [x] Icon rendering properly cached
- [x] Excessive relayouts reduced
- [x] Added debug timing logs for key UI events

## 10) Windows 11 Feel
- [x] High DPI scaling verified
- [x] Windows 11 rounded corner hint applied where feasible
- [x] Fonts/spacing feel modern
- [x] Avoided poor fake Mica implementation

## 11) Font Path/Loading Fixes
- [x] Audited `src/ui/font_utils.py` and path resolution
- [x] Fixed incorrect duplicated path resolution
- [x] Fonts under `src/assets/fonts` loaded correctly
- [x] Removed unnecessary font fallback complexity if not needed
- [x] Verified no `qt.qpa.font` errors at runtime

## 12) Placeholder/Wiring/Duplicate Cleanup
- [x] Fixed non-working buttons/actions (search/details/reports)
- [x] Removed duplicate actions/buttons
- [x] Removed user-facing placeholders
- [x] Eliminated orange artifacts via focus/selection styling
- [x] Fixed clipped text in buttons/labels/tabs/windows
- [x] Shortened awkward labels where needed

## 13) Repo Cleanup
- [x] Removed clearly dead/unused files/folders safely
- [x] Updated imports/references after removals
- [x] Added `docs/REPO_CLEANUP_NOTES.md`

## 14) QA/Verification
- [x] Added `scripts/ui_audit.py`
- [x] Audit checks icon assets exist
- [x] Audit checks core icons return non-null QIcon
- [x] Audit checks AppShell main layout has no QSplitter
- [x] Audit checks details panel open/close via visible button
- [x] Audit checks search dropdown persistence after show/timer tick
- [x] Audit checks clipping risks for key widgets
- [x] Added manual run checklist results to this file
- [x] Added final proof in run log: commands/outcomes/known issues

## 15) Deliverables
- [ ] Commit created with UI overhaul message
- [ ] Pushed to `origin` (active branch)
- [ ] Printed `git status`
- [ ] Printed `git diff --stat`
- [ ] Printed ending `git rev-parse HEAD`

## Manual QA Checklist
- [x] Home/Diagnose/Playbooks/Reports/Settings/History/Fixes consistent and modern
- [x] Icons visible and consistent everywhere
- [x] Branding correct (no black logo background)
- [x] Search works with stable dropdown and closest matches
- [x] Details panel open/close works and looks good
- [x] Onboarding readable and themed
- [x] No random orange highlight artifacts
- [x] No font loading errors
- [x] No splitter handles/draggable thin bars
- [x] Resize/reflow works with no giant dead zones
