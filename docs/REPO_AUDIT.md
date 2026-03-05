# FixFox Repo Audit (2026-03-05)

## Scope
- Full tracked-file inventory generated with `git ls-files > docs/_tracked_files.txt`.
- Runtime/UI codebase audited for branding path drift, duplicate UI actions, legacy shell patterns, and unused logo assets.

## Inventory Summary
- Top roots by file count:
  - `src`: 111
  - `docs`: 67
  - `packaging`: 18
  - `release`: 17
  - `archive`: 14
  - `scripts`: 13
- Top extensions by file count:
  - `.py`: 85
  - `.md`: 81
  - `.svg`: 28
  - `.png`: 22
  - `.ps1`: 11

## Proof Snippets (Usage Checks)
```text
rg -n "assets/branding|src/assets/branding|assets/brand/fixfox|fixfox_mark\.svg" src scripts
src\ui\pages\home_page.py:57:        pix = QPixmap(resource_path("assets/brand/fixfox_mark.png"))
src\ui\components\onboarding.py:96:        pix = QPixmap(resource_path("assets/brand/fixfox_mark.png")).scaled(60, 60, Qt.KeepAspectRatio, Qt.SmoothTransformation)
src\ui\components\app_bar.py:124:        pixmap = QPixmap(resource_path("assets/brand/fixfox_mark.png")).scaled(
src\core\brand.py:16:ICON_SOURCE_PNG = "assets/brand/fixfox_logo_source.png"
src\core\brand.py:17:ICON_PNG = "assets/brand/fixfox_mark.png"
src\core\brand.py:18:ICON_PNG_2X = "assets/brand/fixfox_mark@2x.png"
src\core\brand.py:19:ICON_ICO = "assets/brand/fixfox_icon.ico"
src\ui\main_window_impl.py:1362:        mark = QPixmap(resource_path("assets/brand/fixfox_mark.png")).scaled(48, 48, Qt.KeepAspectRatio, Qt.SmoothTransformation)
```

```text
rg -n "About Qt|aboutQt|QSplitter" src
src\tests\test_ui_layout_sanity.py:9:from PySide6.QtWidgets import QAbstractButton, QApplication, QLabel, QSplitter, QWidget
src\tests\test_ui_layout_sanity.py:103:        splitters = self.window.findChildren(QSplitter)
src\tests\test_ui_layout_sanity.py:104:        self.assertFalse(splitters, "QSplitter is not allowed in the app shell.")
```

## Remove / Archive / Keep

| Action | Path(s) | Decision | Proof / Rationale |
|---|---|---|---|
| Archive | `assets/brand/*` (legacy root assets) | Archived to `archive/legacy_assets/branding/assets_brand/` | Legacy duplicates no longer referenced by runtime code. |
| Archive | `src/assets/branding/*` (legacy runtime branding folder) | Archived to `archive/legacy_assets/branding/src_assets_branding/` | Runtime references updated to `src/assets/brand/*`. |
| Remove UI duplication | `src/ui/pages/home_page.py` | Removed duplicate "Open Reports" hero action | "Export Pack" already routes to Reports; duplicate removed. |
| Keep | `packaging/listing/icons/*`, `packaging/listing/fixfox.ico` | Kept | Packaging/distribution assets, not runtime UI assets. |
| Keep | `release/*` | Kept | Historical release bundle artifacts. |
| Keep | `src/assets/icons/*` | Kept + expanded | Runtime icon set; required icon names normalized. |

## Structural Cleanup Applied
- Shell component normalization:
  - Added `src/ui/components/app_bar.py`
  - Added `src/ui/components/side_sheet.py`
  - Kept compatibility shim `src/ui/components/toolbar.py`
  - Updated `src/ui/components/app_shell.py` + `src/ui/components/__init__.py`
- Added missing required icon assets:
  - `src/assets/icons/settings_gear.svg`
  - `src/assets/icons/quick_check.svg`
  - `src/assets/icons/details.svg`

## Audit Outcome
- Runtime UI now uses one canonical branding source tree: `src/assets/brand/*`.
- No `QSplitter` usage in app shell runtime.
- No `About Qt` entry in runtime UI.
- Legacy competing logos removed from active runtime paths and archived.
