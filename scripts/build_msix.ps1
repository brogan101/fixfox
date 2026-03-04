Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $repoRoot "dist\FixFox.exe"
$msixGuide = Join-Path $repoRoot "packaging\msix\README.md"

if (-not (Test-Path $exePath)) {
    Write-Host "Missing dist\FixFox.exe"
    Write-Host "Build EXE first: scripts/build_exe.ps1"
    exit 1
}

Write-Host "MSIX packaging is optional and requires Windows MSIX tooling."
Write-Host "EXE found: $exePath"

if (Test-Path $msixGuide) {
    Write-Host "Follow instructions in: packaging\msix\README.md"
} else {
    Write-Host "Expected instructions file missing: packaging\msix\README.md"
}

Write-Host "Stub complete. No MSIX was built by this script."
exit 0
