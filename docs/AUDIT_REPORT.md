# AUDIT REPORT

Generated: 2026-03-03
Repo: IT Core (PySide6)

## Scope and Method

Audit actions performed:
1. File inventory of `src/`, `scripts/`, `docs/`.
2. Grep/search pass:
   - `rg -n "task-markers|placeholder|not-implemented|raise marker|pass\\b" src docs`
   - targeted searches for registry wiring, command palette routing, export validation, masking, and run mode semantics.
3. Runtime run pass:
   - `.venv\\Scripts\\python.exe -m src.app` (process started; GUI loop active; no startup traceback observed in timeout window).
   - `.venv\\Scripts\\python.exe src\\app.py` (also starts GUI due fallback import mode path-hack; this is a policy violation).
4. Test pass:
   - `.venv\\Scripts\\python.exe -m src.tests.test_unit` -> PASS
   - `.venv\\Scripts\\python.exe -m src.tests.smoke` -> PASS
5. Compile pass:
   - recursive `py_compile` over all `src/**/*.py` -> PASS

## Repo Tree and Entrypoints

High-level tree (excluding `.venv`):
- `src/`
  - `app.py` (main GUI entry)
  - `core/` (diagnostics, fixes, runbooks, exporter, workers, settings, masking, registry)
  - `ui/` (main window, theme/QSS, components)
  - `tests/` (smoke + unit)
- `scripts/`
  - `setup_venv.ps1`, `run.ps1`, `build_exe.ps1`, `generate_catalogs.py`
- root legacy scripts
  - `run_windows.bat`, `build_exe.bat`
- `docs/`
  - includes prior catalogs/checklists and this audit

Entrypoints observed:
- GUI canonical: `python -m src.app`
- GUI non-canonical currently still works: `python src/app.py` via sys.path fallback in `src/app.py:37-85`
- Script catalogs: `scripts/generate_catalogs.py` with sys.path injection (`scripts/generate_catalogs.py:7-8`)

## Current Feature Inventory

### Present (implemented at baseline)
- Theme engine with palettes/mode/density in `src/ui/theme.py` and tokenized QSS builder in `src/ui/app_qss.py`.
- Structured list rows + feed renderer + accordion components:
  - `src/ui/components/rows.py`
  - `src/ui/components/feed_renderer.py`
  - `src/ui/components/accordion.py`
- Main pages in widgets shell: Home, Diagnose, Fixes, Reports, History, Toolbox, Settings (`src/ui/main_window.py:219`).
- Worker execution with cancel signal, timeouts (cooperative), progress/partial/result signals (`src/core/workers.py`).
- Export pipeline with manifest + hashes + validation and zip generation (`src/core/exporter.py`).
- Runbooks + script tasks implemented (12 runbooks, 20 tasks):
  - `src/core/runbooks.py`
  - `src/core/script_tasks.py`
- Search + command palette with index over capabilities/fixes/runbooks/tools/kb/sessions/exports (`src/core/search.py`).
- Tests pass: smoke + unit under `src/tests`.

### Missing / Not Yet Implemented vs requested target
- No `Playbooks` nav page (still `Toolbox`) (`src/ui/main_window.py:219`).
- No Runbook Studio (template builder/editor/import-export/validator UI).
- No plugin SDK/loader/manager UI:
  - missing `src/core/plugin_loader.py`
  - missing plugin docs generation path.
- No CLI package:
  - missing `src/cli/cli.py` and no `pcconcierge-cli` headless flow.
- No fleet aggregator:
  - missing `src/core/fleet_aggregator.py`.
- No MSI/MSIX/signing scripts:
  - missing `scripts/build_msi.ps1`, `scripts/build_msix.ps1`, `scripts/sign.ps1`.
- No optional QML app path:
  - missing `src/qml_app.py` and `src/ui/qml/`.
- Export structure does not match requested nested support-pack layout.
- Masking placeholders are not deterministic indexed placeholders (`<PC_1>`, `<USER_1>`, etc.).

## Runtime Warnings, Crashes, Import Issues

Observed:
- `python -m src.app` starts without immediate traceback in timeout window.
- `python src/app.py` starts the app due fallback import branch and sys.path mutation (`src/app.py:37-85`).

Import mode issues to fix:
- Non-canonical script mode is still supported by path hacks; target requirement is to print a friendly message and exit for direct file execution.
- `scripts/generate_catalogs.py` mutates `sys.path` directly (`scripts/generate_catalogs.py:7-8`).

## Placeholder / Unfinished / Risky Silent Paths

Search run (`task-markers/placeholder/not-implemented/pass`) found no pending markers, but these silent handlers should be reviewed:
- `src/ui/main_window.py:621` -> broad `except Exception: pass` while applying density setters.
- `src/core/diagnostics.py:382` -> `except OSError: pass` in hosts parsing path.

These are not direct pending markers but can hide runtime defects.

## Implemented but Unreachable (or weakly reachable)

1. Capability search entries are not actionable
- `src/core/search.py:25` adds `SearchItem("capability", ...)`
- `src/ui/main_window.py:1219-1231` handles `session/fix/runbook/tool/export/kb` only.
- Selecting capability items falls through to default nav action (Home), not capability execution.

2. Diagnostic capability payload exists but is not surfaced in Diagnose UX
- `src/core/diagnostics.py:193` includes `capability_results`
- No consumer references in UI (`rg "capability_results" src` only returns diagnostics).

3. Registry catalog exists but is not the execution backbone
- `src/core/registry.py` contains static metadata only; it does not drive runtime dispatch, policy gates, or UI directory binding end-to-end.

## UI Audit (Structure / Readability / Clutter)

Strengths:
- Row widgets replace multiline plain text in major lists.
- Diagnose uses grouped accordion sections (no category tab widget).
- Right concierge panel limited to 3 cards in current implementation.

Issues:
- Nav taxonomy mismatch: spec requires `Playbooks`; app still uses `Toolbox` (`src/ui/main_window.py:219`).
- Runbook actions are embedded in Toolbox column, not a dedicated Playbooks information architecture.
- Context menu logic is duplicated inline inside `main_window.py` (`_finding_menu`, `_fix_menu`, `_tool_menu`, `_session_menu`, `_runbook_menu`) rather than centralized component module.
- No explicit Runbook Studio UI surface.

---

# 2026-03-05 Audit Scaffold (Current Run)

## Repo structure & hygiene

## Branding & assets

## UI architecture & theming

## Search & navigation

## Pages & features

## Performance

## Tests & QA

## Cleanup removals/archives

## Final verification checklist

## Export / Masking Audit

Current exporter behavior (`src/core/exporter.py`):
- Writes flat files in export folder root:
  - `session.json`, `report.json`, `summary.md`, ticket summaries, `manifest.json`, `hashes.txt`.
- Validator required set is minimal (`manifest.json`, `hashes.txt`, `summary.md`, `session.json`) (`src/core/exporter.py:146`).

Gaps vs target:
- Missing nested canonical structure (`/report/*`, `/data/*`, `/logs/*`, `/manifest/*`).
- No HTML/CSV/text report rendering pipeline under structured report package modules.
- Manifest is rebuilt twice (`src/core/exporter.py:228` and `src/core/exporter.py:232`).
- Masking uses generic placeholders (`<pc-name>`, `<redacted>`, `<ip>`) (`src/core/masking.py:35-43`), not deterministic indexed tokens.
- Share-safe validator checks raw token occurrence but does not include richer secret/identifier scanners.

## Tests Status and Gaps

Current status:
- Unit tests: PASS (`src/tests/test_unit.py`)
- Smoke tests: PASS (`src/tests/smoke.py`)

Coverage gaps:
- No CLI smoke coverage (`pcconcierge-cli` absent).
- No plugin loader tests.
- No runbook template schema validation tests.
- No fleet aggregation tests.
- Test package is `src/tests`, while target gate expects `python -m tests.smoke` style.

## Risk List

1. Run mode ambiguity
- Risk: users continue running `python src/app.py`; package semantics and relative imports remain fragile.
- Mitigation: enforce module-only run path and friendly script-mode exit message.

2. Export contract drift
- Risk: downstream support tooling may break because output structure is not fixed to requested support-pack schema.
- Mitigation: implement structured exporter layout + strict validator + hash completeness checks.

3. Registry not authoritative
- Risk: metadata claims capability coverage but execution/discoverability is inconsistent.
- Mitigation: move to typed registry objects and drive command palette/directories/execution from registry IDs.

4. Missing major extensibility tracks
- Risk: no plugin, CLI, fleet paths; large requested use-cases blocked.
- Mitigation: add incremental modules (`plugin_loader`, `cli`, `fleet_aggregator`) with smoke gates.

5. UI taxonomy mismatch
- Risk: user-facing IA conflict (`Toolbox` vs required `Playbooks`) and discoverability friction.
- Mitigation: rename/restructure nav while keeping <=7 items.

## Target Architecture (Incremental)

Adopt this staged target while keeping app runnable:
- `src/app.py` as sole GUI entrypoint.
- `src/core` split by bounded contexts:
  - diagnostics packs, fixes catalog/runners, report renderers/export/validator, plugin loader, fleet aggregator.
- `src/ui` split into shell + pages + components; keep Widgets as baseline.
- `src/cli/cli.py` for headless run/export/list.
- `packaging/` for EXE/MSI/MSIX/signing assets.
- `tests/` top-level module for smoke/unit gates.

## Target UI Shell

- Left nav (max 7): Home, Playbooks, Diagnose, Fixes, Reports, History, Settings.
- Top command bar: global search, export, help, profile/menu.
- Main content responsive cards/feed.
- Right concierge panel (max 3 cards), auto-collapse on narrow widths and persisted preference.
- Progressive disclosure: plain-English first, technical drawers second.

## PUNCHLIST (Phase-ordered)

### Phase 1 (critical)
1. Enforce module-only run (`python -m src.app`), remove sys.path fallback from `src/app.py`.
2. Make `python src/app.py` print friendly guidance and exit non-zero.
3. Remove sys.path mutation in `scripts/generate_catalogs.py`.
4. Keep global QSS apply at QApplication level (already present) and verify no launch warnings.

### Phase 2
1. Rename nav IA to include `Playbooks` (replace Toolbox label and related strings).
2. Centralize context menu builder into `ui/components/context_menu.py`.
3. Add page-level local help affordances (`?`) in headers.

### Phase 3+
1. Structured registry types + authoritative execution wiring.
2. Structured export layout + stronger validator + deterministic masking placeholders.
3. Plugin loader + manager UI.
4. CLI headless runner.
5. Fleet aggregator.
6. Packaging scripts/assets (MSI/MSIX/signing stubs).
7. Optional parallel QML surface.

## Post-Audit Execution Update (same session)

The following Phase 1/early Phase 2 items were implemented immediately after this audit snapshot:
- `src/app.py` now enforces module-mode launch (`python -m src.app`) and prints:
  `Run this app via: python -m src.app` when run as `python src/app.py`.
- `scripts/generate_catalogs.py` no longer mutates `sys.path`; it now requires:
  `python -m scripts.generate_catalogs` and prints a friendly message in direct-script mode.
- Added packaging stubs:
  - `scripts/build_msi.ps1`
  - `scripts/build_msix.ps1`
  - `scripts/sign.ps1`
  - `packaging/msi/README.md`
  - `packaging/msix/README.md`
- `build_exe.bat` converted to instruction-only stub; `run_windows.bat` remains instruction-only.
- Navigation taxonomy changed from `Toolbox` to `Playbooks` with nav count still 7.
