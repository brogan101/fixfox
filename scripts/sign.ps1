Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$target = Join-Path $repoRoot "dist\FixFox.exe"

if (-not (Test-Path $target)) {
    Write-Host "No EXE to sign at dist\FixFox.exe"
    Write-Host "Build first: scripts/build_exe.ps1"
    exit 1
}

$signtool = Get-Command signtool -ErrorAction SilentlyContinue
if ($null -eq $signtool) {
    Write-Host "signtool not found in PATH."
    Write-Host "Install Windows SDK signing tools or configure enterprise build agent."
    Write-Host "Unsigned artifact remains at: $target"
    exit 0
}

Write-Host "Signing stub detected signtool."
Write-Host "Set your certificate parameters and invoke signtool manually as needed."
Write-Host "Target: $target"
exit 0
