# Repo Cleanup Plan (2026-03-05)

## Method
- Candidate discovery via `git ls-files`, `rg --files`, and targeted reference checks.
- Rule used:
  - delete only when clearly transient/unused.
  - otherwise archive or keep.

## Proof snippets
```text
rg -n "from \.toolbar import|components\.toolbar|AppToolbar" src
src\ui\components\toolbar.py:1:from .app_bar import AppToolbar, RunStatusPanel
```

```text
rg -n "legacy_assets/branding|assets_brand|src_assets_branding" docs src scripts
docs/_tracked_files.txt:... archive/legacy_assets/branding/...
docs/REPO_AUDIT.md:... archived legacy branding ...
```

```text
Get-ChildItem docs/screenshots
20260305_141829
20260305_144413
20260305_152753
20260305_153308
20260305_153732
```

```text
Test-Path docs/screenshots/20260305_143145 -> False
Test-Path docs/screenshots/20260305_143645 -> False
```

## Remove / Archive / Keep

| Item | Action | Why | Proof |
|---|---|---|---|
| `docs/screenshots/20260305_143145` | Remove | Intermediate walkthrough output superseded by final passing run. | Directory listing + final run at `20260305_144413`. |
| `docs/screenshots/20260305_143645` | Remove | Intermediate walkthrough output superseded by final passing run. | Directory listing + final run at `20260305_144413`. |
| `archive/legacy_assets/branding/assets_brand/*` | Keep archived | Historical competing logo attempts preserved safely outside runtime paths. | `rg` results only in docs/index/archive references. |
| `archive/legacy_assets/branding/src_assets_branding/*` | Keep archived | Same as above; not referenced by runtime UI. | `rg` results only in docs/index/archive references. |
| `src/ui/components/toolbar.py` | Keep (compat shim) | Existing imports can rely on this path; now re-exports from `app_bar.py`. | `rg` shows live reference and explicit re-export. |
| `docs/screenshots/20260305_141829` | Keep | Prior committed evidence set; not harmful and still valid proof history. | Tracked file set includes this folder. |
| `docs/screenshots/20260305_152753` | Archive/remove later | Intermediate run output superseded by latest pass folder. | New final run is `20260305_153732`. |
| `docs/screenshots/20260305_153308` | Archive/remove later | Intermediate run output superseded by latest pass folder. | New final run is `20260305_153732`. |
| `docs/screenshots/20260305_153732` | Keep | Latest passing walkthrough evidence used in final verification. | Manifest + clipping report both PASS. |

## Execution plan
1. Remove only stale transient walkthrough folder for this run.
2. Keep archived legacy branding and compatibility shim in place.
3. Document results in `docs/REPO_CLEANUP_NOTES.md`.

## 2026-03-05 Execution Update
- Keep: new registry/evidence/wizard modules (src/core/route_registry.py, src/core/play_registry.py, src/core/evidence_model.py, src/ui/components/guided_wizard.py) because they close explicit requirement gaps.
- Keep: scripts/verify_requirements.py as mandatory gate source.
- Keep: icon files src/assets/icons/gear.svg, wrench.svg, open_book.svg for required mapping consistency.
- Archive/Remove: none in this delta; prior archive state remains valid.
