# UI QA RESULTS (2026-03-04)

## Automated
- Command: `.\\.venv\\Scripts\\python.exe -m src.tests.smoke`
- Result: **PASS** (`Smoke test passed.`)

## Resize and Layout Checks
Run via offscreen Qt harness (`MainWindow` instantiated and resized programmatically):
- Requested `1100x700` -> actual `1210x770` (expected with active UI scale > 100% and scaled minimum window guardrail).
- Requested `1280x800` -> actual `1280x800`.
- Requested `1920x1080` -> actual `1920x1080`.
- Right pane auto-collapse logic remains controlled by width threshold and mode policy; no crash or clipping exception observed during resize transitions.

## Manual Checklist (release gate)
- Basic/Pro switch changes layout meaningfully: **PASS** (smoke assertions verify guided-basic vs pro-console surfaces).
- Single-click row selection does not execute: **PASS** (smoke assertion verifies no worker/tool runner launch on single click).
- Explicit run opens ToolRunner and shows live output path: **PASS** (smoke verifies ToolRunner creation + run events).
- Scrollbars themed across app: **PASS** (global QSS includes complete vertical/horizontal scrollbar theming).
- Global search works with grouping + keyboard nav: **PASS** (debounced popup + grouped sections + up/down key handling validated in smoke).
- Settings search filters by labels/descriptions: **PASS** (offscreen check returned visible `Privacy`/`About` for query `privacy`).
- Privacy/Safety in-app pages are readable/local-first: **PASS** (dedicated Settings sections include local storage, masking, not-collected, and safety/rollback guidance copy).

## Notes
- Minimum window floor is now scale-aware; with scale above 100%, effective minimum can exceed `1100x700` by design.
