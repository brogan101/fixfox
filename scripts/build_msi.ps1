Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $repoRoot "dist\FixFox.exe"
$wixTemplate = Join-Path $repoRoot "packaging\msi\README.md"

if (-not (Test-Path $exePath)) {
    Write-Host "Missing dist\FixFox.exe"
    Write-Host "Build EXE first: scripts/build_exe.ps1"
    exit 1
}

Write-Host "MSI build is packaging-only and requires WiX Toolset."
Write-Host "EXE found: $exePath"

if (Test-Path $wixTemplate) {
    Write-Host "Follow instructions in: packaging\msi\README.md"
} else {
    Write-Host "Expected instructions file missing: packaging\msi\README.md"
}

Write-Host "Stub complete. No MSI was built by this script."
exit 0
