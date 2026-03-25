# FixFox post-build smoke validation.
# Usage:
#   .\Test-FixFox.ps1
#   .\Test-FixFox.ps1 -ExePath .\publish\win-x64\FixFox.exe -Interactive

param(
    [string]$ExePath = ".\\publish\\win-x64\\FixFox.exe",
    [switch]$Interactive
)

$ErrorActionPreference = "Continue"
$pass = 0
$fail = 0
$warn = 0

function Pass([string]$Message) { Write-Host "  v $Message" -ForegroundColor Green; $script:pass++ }
function Fail([string]$Message) { Write-Host "  x $Message" -ForegroundColor Red; $script:fail++ }
function Warn([string]$Message) { Write-Host "  ! $Message" -ForegroundColor Yellow; $script:warn++ }
function Section([string]$Title) { Write-Host ""; Write-Host "  -- $Title --" -ForegroundColor Cyan }

Write-Host ""
Write-Host "FixFox Smoke Validation" -ForegroundColor Cyan
Write-Host "=======================" -ForegroundColor Cyan

$resolvedExe = Resolve-Path $ExePath -ErrorAction SilentlyContinue
Section "Packaged executable"
if (-not $resolvedExe) {
    Fail "FixFox.exe not found at $ExePath"
    exit 1
}

$exe = $resolvedExe.Path
Pass "Executable found at $exe"
$exeSizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Pass "Executable size: $exeSizeMb MB"

Section "Headless verification"
$verifyOut = Join-Path $env:TEMP "fixfox-smoke-out.txt"
$verifyErr = Join-Path $env:TEMP "fixfox-smoke-err.txt"
if (Test-Path $verifyOut) { Remove-Item $verifyOut -Force }
if (Test-Path $verifyErr) { Remove-Item $verifyErr -Force }

$verifyProc = Start-Process $exe `
    -ArgumentList "--verify-headless" `
    -PassThru `
    -Wait `
    -NoNewWindow `
    -RedirectStandardOutput $verifyOut `
    -RedirectStandardError $verifyErr

$verifyText = if (Test-Path $verifyOut) { Get-Content $verifyOut -Raw } else { "" }
if ($verifyProc.ExitCode -eq 0) {
    Pass "Headless verification returned exit code 0"
} else {
    Fail "Headless verification returned exit code $($verifyProc.ExitCode)"
}

$verifyText -split "`r?`n" |
    Where-Object { $_ -match 'PASS|WARN|FAIL' } |
    ForEach-Object { Write-Host ("    " + $_) -ForegroundColor Gray }

Section "Runtime launch"
$runtimeProc = Start-Process $exe -PassThru
Start-Sleep -Seconds 6
if ($runtimeProc.HasExited) {
    Fail "Packaged FixFox exited within 6 seconds"
} else {
    Pass "Packaged FixFox stayed running for 6 seconds"
    Stop-Process -Id $runtimeProc.Id -Force
}

Section "Local data"
$dataDir = Join-Path $env:APPDATA "FixFox"
if (Test-Path $dataDir) {
    Pass "App data folder exists: $dataDir"
} else {
    Warn "App data folder was not found"
}

$verifyLog = Join-Path $dataDir "startup-verify.log"
if (Test-Path $verifyLog) {
    Pass "Startup verify log exists"
} else {
    Warn "Startup verify log was not found"
}

if ($Interactive) {
    Section "Manual checks"
    Write-Host "  Review the packaged app and confirm:" -ForegroundColor Yellow
    Write-Host "  - onboarding is understandable" -ForegroundColor Yellow
    Write-Host "  - tray behavior matches settings" -ForegroundColor Yellow
    Write-Host "  - support package and settings help links open correctly" -ForegroundColor Yellow
    Write-Host "  - no crash or warning dialog appears on startup" -ForegroundColor Yellow
}

Write-Host ""
$summaryColor = if ($fail -eq 0) { "Green" } else { "Red" }
Write-Host "Results: $pass passed, $warn warnings, $fail failed" -ForegroundColor $summaryColor

if ($fail -gt 0) {
    exit 1
}

exit 0
