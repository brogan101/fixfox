Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$venvPath = Join-Path $repoRoot ".venv"

if (-not (Test-Path $venvPath)) {
    & (Join-Path $PSScriptRoot "setup_venv.ps1")
} else {
    . (Join-Path $venvPath "Scripts\Activate.ps1")
}

$hasPyInstaller = python -c "import importlib.util; print('1' if importlib.util.find_spec('PyInstaller') else '0')"
if ($hasPyInstaller.Trim() -ne "1") {
    python -m pip install pyinstaller
}

Set-Location $repoRoot
python tools/write_version_info.py
python -m PyInstaller `
    --noconfirm `
    --onefile `
    --windowed `
    --name FixFox `
    --icon "src\assets\brand\fixfox.ico" `
    --version-file "packaging\windows_version.txt" `
    --add-data "src\assets;assets" `
    src\app.py

$distPath = Join-Path $repoRoot "dist"
$distDocs = Join-Path $distPath "docs"
$distLicenses = Join-Path $distPath "licenses"
New-Item -ItemType Directory -Force $distDocs | Out-Null
New-Item -ItemType Directory -Force $distLicenses | Out-Null

$docsToCopy = @(
    "docs\USER_GUIDE.md",
    "docs\IT_GUIDE.md",
    "docs\PRIVACY.md",
    "docs\SAFETY.md",
    "docs\TROUBLESHOOTING.md",
    "docs\CHANGELOG.md",
    "docs\SUPPORT.md",
    "docs\LICENSE.txt",
    "docs\DISCLAIMER.txt"
)
foreach ($doc in $docsToCopy) {
    $src = Join-Path $repoRoot $doc
    if (Test-Path $src) {
        Copy-Item $src $distDocs -Force
    }
}

$licensesToCopy = @(
    "docs\LICENSE.txt",
    "docs\DISCLAIMER.txt"
)
foreach ($lic in $licensesToCopy) {
    $src = Join-Path $repoRoot $lic
    if (Test-Path $src) {
        Copy-Item $src $distLicenses -Force
    }
}

Write-Host ""
Write-Host "Built: dist\FixFox.exe"
Write-Host "Docs: dist\docs"
Write-Host "Licenses: dist\licenses"
