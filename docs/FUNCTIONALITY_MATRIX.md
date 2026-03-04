# FUNCTIONALITY MATRIX (2026-03-04)

Status keys: `OK`, `PARTIAL`, `BROKEN`

| Capability | Status | Notes | Planned Fix |
|---|---|---|---|
| Global search (top bar) | PARTIAL | Exists via command palette launch, but lacks debounced grouped dropdown + keyboard UX. | Implement grouped debounced search popup with Up/Down/Enter behavior. |
| Settings search | OK | Filters section rail by label/desc. | Expand descriptors for new Privacy/Safety pages. |
| Basic/Pro mode toggle | OK | Layout policy exists and drives major visibility changes. | Further polish language and settings gating by mode. |
| Playbooks basic guided goals | OK | Distinct guided goals layout is present. | Minor copy/visual polish. |
| Playbooks pro tools/runbooks console | OK | Segmented tools/runbooks + detail panes present. | Maintain behavior, polish spacing consistency. |
| Fixes mode behavior | PARTIAL | Basic/Pro filtering is active but needs stronger simplified surface behavior in basic copy/UI. | Tighten mode labels and advanced control visibility. |
| Reports stepper/export flow | PARTIAL | Configure/Preview/Generate exists; empty/basic guidance can be clearer. | Improve no-session guidance and basic/pro messaging. |
| ToolRunner live logs | OK | Uses event bus + QPlainTextEdit signal append; output streams during runs. | Minor silent-run UX copy polish. |
| Top bar run status | PARTIAL | Live status + elapsed exist; needs stricter last-log readability and integration consistency. | Harden mapping + spinner/log line formatting. |
| Run event bus buffering | OK | Per-run buffer + subscribe/replay is implemented. | Keep as-is. |
| Evidence collection/export/validator | OK | Core exports and validator paths are wired and covered by smoke tests. | Keep as-is. |
| Sessions/history/run center | OK | Session creation/load/history and run center are functional. | Improve card readability polish. |
| Theme tokens + QSS global apply | OK | Global Material-like tokens/QSS exist. | Add scale-aware token application and spacing multiplier. |
| UI scale setting (90-125) | BROKEN | Not implemented. | Add slider, persist setting, apply live to typography/density/spacing. |
| Privacy in-app page | PARTIAL | Exists as dialog tab/help text, not a dedicated settings content page. | Add dedicated Privacy docs page in Settings and help access route. |
| Safety in-app page | PARTIAL | Exists as dialog tab/help text, not a dedicated settings content page. | Add dedicated Safety docs page in Settings and help access route. |
| Reset settings action | OK | Present and wired. | Keep as-is. |
| Export settings JSON (Pro only) | PARTIAL | Export exists for all modes. | Restrict to Pro mode and explain in basic mode. |
| Layout debug toggle (Ctrl+Alt+L) | OK | Implemented with overlay. | Keep as-is. |

## Execution Note
Core diagnostics/fixes/runbooks/exports/masking/validator/sessions paths are preserved by design in this run. UI changes are scoped to shell/search/settings/theme and presentation behavior.
