Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$version = @'
from src.core.version import APP_VERSION
print(APP_VERSION)
'@ | .venv\Scripts\python.exe -
$version = $version.Trim()
if (-not $version) { $version = "0.0.0" }

$releaseRoot = Join-Path $repoRoot ("release\FixFox_v" + $version)
$docsDir = Join-Path $releaseRoot "docs"
$licensesDir = Join-Path $releaseRoot "licenses"
$scriptsDir = Join-Path $releaseRoot "scripts"
$samplesDir = Join-Path $releaseRoot "samples"

New-Item -ItemType Directory -Force $releaseRoot | Out-Null
New-Item -ItemType Directory -Force $docsDir | Out-Null
New-Item -ItemType Directory -Force $licensesDir | Out-Null
New-Item -ItemType Directory -Force $scriptsDir | Out-Null
New-Item -ItemType Directory -Force $samplesDir | Out-Null

$exePath = Join-Path $repoRoot "dist\FixFox.exe"
if (Test-Path $exePath) {
    Copy-Item $exePath (Join-Path $releaseRoot "FixFox.exe") -Force
} else {
    "Build output path: dist\FixFox.exe (run scripts/build_exe.ps1)" | Set-Content (Join-Path $releaseRoot "FixFox_EXE_PATH.txt") -Encoding UTF8
}

$docsToCopy = @(
    "docs\USER_GUIDE.md",
    "docs\IT_GUIDE.md",
    "docs\PRIVACY.md",
    "docs\SAFETY.md",
    "docs\TROUBLESHOOTING.md",
    "docs\CHANGELOG.md",
    "docs\SUPPORT.md",
    "docs\RELEASE_AUDIT.md",
    "docs\UI_QA_RESULTS.md"
)
foreach ($doc in $docsToCopy) {
    $src = Join-Path $repoRoot $doc
    if (Test-Path $src) {
        Copy-Item $src $docsDir -Force
    }
}

$licenseFiles = @("docs\LICENSE.txt", "docs\DISCLAIMER.txt")
foreach ($lic in $licenseFiles) {
    $src = Join-Path $repoRoot $lic
    if (Test-Path $src) {
        Copy-Item $src $licensesDir -Force
    }
}

$scriptsToCopy = @(
    "scripts\run.ps1",
    "scripts\setup_venv.ps1",
    "scripts\build_exe.ps1",
    "scripts\prepare_release_bundle.ps1"
)
foreach ($sc in $scriptsToCopy) {
    $src = Join-Path $repoRoot $sc
    if (Test-Path $src) {
        Copy-Item $src $scriptsDir -Force
    }
}

$sampleReadme = Join-Path $samplesDir "README.txt"
"Optional sample support packs can be copied here for seller bundle demos." | Set-Content $sampleReadme -Encoding UTF8

Write-Host "Release bundle prepared at: $releaseRoot"
