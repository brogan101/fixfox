# MSI Build Stub

This repo does not require WiX for normal development.

## Prerequisites
- Build the EXE first: `scripts/build_exe.ps1`
- Install WiX Toolset v4+ on the build machine.

## Intended flow
1. Prepare `dist/FixFox.exe`.
2. Author/update WiX project files (`.wixproj`, `.wxs`) in this folder.
3. Build with WiX CLI/MSBuild.

`scripts/build_msi.ps1` validates prerequisites and points here. It does not build MSI yet.
