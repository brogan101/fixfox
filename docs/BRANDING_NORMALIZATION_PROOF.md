# Branding Normalization Proof (2026-03-05)

## Required Goal State
- `src/assets/brand/fixfox_logo_source.png` exists with a single `.png` extension.
- Source-of-truth logo input is normalized to `src/assets/brand/*`.
- UI/runtime branding references resolve through `src/assets/brand/*` via `resource_path(...)` and brand constants.

## Current State Evidence
- `assets/brand` directory: not present in active runtime tree (legacy assets previously archived under `archive/legacy_assets/branding/assets_brand`).
- `src/assets/brand` directory contains:
  - `fixfox_logo_source.png`
  - `fixfox_mark.png`
  - `fixfox_mark@2x.png`
  - `fixfox_icon.ico`

## Source Image Validation
- Exists: `True`
- Size: `379965` bytes
- PNG magic: `89504e470d0a1a0a`
- Pillow load: `format=PNG`, `mode=RGBA`, `size=(1024, 1024)`

## Code Reference Validation
Active code references use `assets/brand/*` paths in `src` runtime context:
- `src/core/brand.py`
- `src/ui/components/app_bar.py`
- `src/ui/components/onboarding.py`
- `src/ui/pages/home_page.py`
- `src/ui/main_window_impl.py`
- `scripts/build_exe.ps1` (packaging icon path)

No double-extension source file (`fixfox_logo_source.png.png`) exists in runtime brand paths.
