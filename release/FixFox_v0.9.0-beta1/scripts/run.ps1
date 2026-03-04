Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$venvPath = Join-Path $repoRoot ".venv"

if (-not (Test-Path $venvPath)) {
    throw ".venv not found. Run scripts/setup_venv.ps1 first."
}

. (Join-Path $venvPath "Scripts\Activate.ps1")
Set-Location $repoRoot
python -m src.app
