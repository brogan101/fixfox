Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$venvPath = Join-Path $repoRoot ".venv"

if (-not (Test-Path $venvPath)) {
    py -m venv $venvPath
}

. (Join-Path $venvPath "Scripts\Activate.ps1")

python -m pip install --upgrade pip
python -m pip install -r (Join-Path $repoRoot "requirements.txt")
