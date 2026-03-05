# Fix Fox Branding

## Brand Source Of Truth

Brand constants live in `src/core/brand.py`.

Key values:
- `APP_NAME = "Fix Fox"`
- `APP_TAGLINE = "Fast fixes. Clean support packs."`
- `EXPORT_PREFIX = "FixFox_SupportPack"`
- `REPORT_TITLE = "Fix Fox - Support Pack"`
- `ICON_PNG = "assets/branding/fixfox.png"`
- `ICON_ICO = "assets/branding/fixfox_icon.ico"`
- `DESKTOP_LOGO_FILENAME = "FixFoxLogo.png"`

## Icon Pipeline

Source icon:
- `assets/branding/fixfox.png`

Generate icons:

```powershell
py tools/make_icons.py
```

Generated outputs:
- `assets/branding/png/fixfox_16.png`
- `assets/branding/png/fixfox_24.png`
- `assets/branding/png/fixfox_32.png`
- `assets/branding/png/fixfox_48.png`
- `assets/branding/png/fixfox_64.png`
- `assets/branding/png/fixfox_128.png`
- `assets/branding/png/fixfox_256.png`
- `assets/branding/png/fixfox_512.png`
- `assets/branding/fixfox_icon.ico`

The script also mirrors runtime icons to `src/assets/branding/`.

## Desktop Logo Behavior

Helper: `src/core/brand_assets.py`
- `ensure_logo_on_desktop(overwrite=False)` copies `src/assets/branding/fixfox.png` to Desktop as `FixFoxLogo.png`.
- By default, it does not overwrite an existing Desktop logo.
- On first launch, app startup attempts this copy once (safe no-overwrite mode).
- In Settings -> Advanced:
  - `Create Desktop Logo` creates it if missing.
  - `Recreate Desktop Logo` overwrites intentionally.

The action is logged as a utility action (`type=tool_open/utility`) in app logs and session actions (if a session is active).
