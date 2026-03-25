# FixFox Setup Guide

FixFox is a Windows 10/11 desktop support app for diagnostics, repair workflows, safe maintenance, and support-package export.

## Minimum requirements

- Windows 10 version 1903 or later, or Windows 11
- 64-bit Windows recommended
- Administrator rights only when a specific repair asks for them
- Internet access only for update checks, web troubleshooting, or support resources you choose to open

## Install options

### Portable build

1. Download the packaged `FixFox_v<version>_win-x64.zip`.
2. Extract it to a normal writable folder such as `C:\Apps\FixFox`.
3. Run `FixFox.exe`.

### Installer build

If the release includes `FixFox_Setup_<version>.exe`:

1. Run the installer.
2. Choose the install location.
3. Launch FixFox from the Start menu or desktop shortcut.

## First launch

On first run, FixFox lets you choose:

- a behavior profile
- tray and startup behavior
- notification level
- safe maintenance preference
- whether to run the first health check immediately

If you are not sure, use `Standard`.

## What FixFox stores locally

FixFox stores app data under:

`%APPDATA%\FixFox`

That includes:

- settings
- repair receipts
- support packages you explicitly create
- logs
- interrupted-work recovery state

FixFox does not require an account and does not upload support packages automatically.

## Update behavior

- FixFox can check the configured release feed on launch.
- If the update source is unavailable, the app stays usable and shows a clear status message instead of failing hard.
- Release notes ship with the app in `CHANGELOG.md`.

## Uninstall

### Portable build

- Close FixFox.
- Delete the extracted FixFox folder.
- Remove `%APPDATA%\FixFox` only if you also want to remove settings, logs, receipts, and support packages.

### Installer build

- Uninstall from `Installed Apps` in Windows Settings.
- Local FixFox data under `%APPDATA%\FixFox` may remain so you do not lose history unexpectedly.

## Support and troubleshooting

- Open `Docs\Quick-Start.md` for a short guided overview.
- Open `Docs\Privacy-and-Data.md` to review local storage and support-package behavior.
- Open `Docs\Support-Packages.md` before sharing evidence externally.
- Open `Docs\Recovery-and-Resume.md` if a repair was interrupted.
- Open `Docs\Troubleshooting-and-FAQ.md` for common launch and usage questions.
