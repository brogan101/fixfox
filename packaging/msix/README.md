# MSIX Packaging Stub

This repo keeps MSIX packaging optional.

## Prerequisites
- Build EXE first: `scripts/build_exe.ps1`
- Install Microsoft MSIX Packaging Tool (or equivalent enterprise packaging pipeline).

## Intended flow
1. Use `dist/FixFox.exe` as source payload.
2. Configure app identity/publisher/version in MSIX tooling.
3. Build and test install/uninstall in a clean VM.

`scripts/build_msix.ps1` only validates prerequisites and points here.
