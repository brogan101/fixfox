# FixFox build pipeline: validate -> build -> test -> publish -> verify -> package -> optional installer

param(
    [string]$Version = "1.0.0",
    [switch]$SkipValidation,
    [switch]$SkipTests,
    [switch]$SkipVerify,
    [switch]$SkipInstaller,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

$projectDir = $PSScriptRoot
$publishDir = Join-Path $projectDir "publish\\win-x64"
$distDir = Join-Path $projectDir "dist"
$stageDir = Join-Path $distDir "FixFox_v${Version}_win-x64"
$zipPath = Join-Path $distDir "FixFox_v${Version}_win-x64.zip"
$csproj = Join-Path $projectDir "HelpDesk.csproj"
$constantsPath = Join-Path $projectDir "Shared\\Constants.cs"
$releaseFeedPath = Join-Path $projectDir "Configuration\\release-feed.json"
$exe = Join-Path $publishDir "FixFox.exe"
$startTime = Get-Date

function Step([string]$Message) {
    Write-Host ""
    Write-Host ("[{0,4}s] {1}" -f [int]((Get-Date) - $startTime).TotalSeconds, $Message) -ForegroundColor Cyan
}

function Ok([string]$Message) {
    Write-Host ("  OK  {0}" -f $Message) -ForegroundColor Green
}

function Warn([string]$Message) {
    Write-Host ("  !   {0}" -f $Message) -ForegroundColor Yellow
}

function Fail([string]$Message) {
    Write-Host ("  X   {0}" -f $Message) -ForegroundColor Red
    exit 1
}

function Update-VersionText([string]$Path, [string]$Pattern, [string]$Replacement) {
    $content = Get-Content -Path $Path -Raw
    $updated = [regex]::Replace($content, $Pattern, $Replacement)
    Set-Content -Path $Path -Value $updated -Encoding UTF8
}

function Get-InnoSetupCompiler {
    $candidates = @(
        "$env:ProgramFiles(x86)\\Inno Setup 6\\ISCC.exe",
        "$env:ProgramFiles\\Inno Setup 6\\ISCC.exe"
    )

    return $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

Write-Host ""
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "  FixFox Build and Packaging Pipeline v$Version" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

if (-not $SkipValidation) {
    Step "Validating the source tree"
    & "$projectDir\\Validate-Source.ps1" -ProjectDir $projectDir
    if ($LASTEXITCODE -ne 0) {
        Fail "Source validation failed."
    }
    Ok "Source validation passed"
}

if ($Clean) {
    Step "Cleaning build output"
    @(
        $publishDir,
        $distDir,
        (Join-Path $projectDir "bin"),
        (Join-Path $projectDir "obj")
    ) | Where-Object { Test-Path $_ } | ForEach-Object { Remove-Item $_ -Recurse -Force }
    Ok "Previous build output removed"
}

Step "Updating product version metadata"
Update-VersionText $csproj '<Version>[^<]*</Version>' "<Version>$Version</Version>"
Update-VersionText $csproj '<AssemblyVersion>[^<]*</AssemblyVersion>' "<AssemblyVersion>$Version.0</AssemblyVersion>"
Update-VersionText $csproj '<FileVersion>[^<]*</FileVersion>' "<FileVersion>$Version.0</FileVersion>"
Update-VersionText $csproj '<InformationalVersion>[^<]*</InformationalVersion>' "<InformationalVersion>$Version</InformationalVersion>"
Update-VersionText $constantsPath 'AppVersion\s*=\s*"[^"]*"' "AppVersion = `"$Version`""
if (Test-Path $releaseFeedPath) {
    $feed = Get-Content -Path $releaseFeedPath -Raw | ConvertFrom-Json
    $feed.LatestVersion = $Version
    $feed | ConvertTo-Json -Depth 6 | Set-Content -Path $releaseFeedPath -Encoding UTF8
}
Ok "Version metadata updated to $Version"

Step "Restoring NuGet packages"
$restoreOutput = dotnet restore $csproj -r win-x64 --nologo 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host $restoreOutput -ForegroundColor Red
    Fail "NuGet restore failed."
}
Ok "Packages restored"

Step "Building Release"
$buildOutput = dotnet build $csproj -c Release --nologo --no-restore 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host $buildOutput -ForegroundColor Red
    Fail "Release build failed."
}
$buildWarnings = ($buildOutput | Where-Object { $_ -match ' warning ' }).Count
Ok ("Release build succeeded ({0})" -f ($(if ($buildWarnings -gt 0) { "$buildWarnings warnings" } else { "0 warnings" })))

if (-not $SkipTests) {
    Step "Running automated tests"
    $testOutput = dotnet test ".\\HelpDesk.Tests\\HelpDesk.Tests.csproj" -c Release --nologo --no-build 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host $testOutput -ForegroundColor Red
        Fail "Automated tests failed."
    }
    Ok "Tests passed"
}

Step "Publishing self-contained Win64 build"
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
$publishOutput = dotnet publish $csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $publishDir `
    --nologo `
    --no-restore 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host $publishOutput -ForegroundColor Red
    Fail "Publish failed."
}

if (-not (Test-Path $exe)) {
    Fail "The published executable was not found at $exe"
}

$exeSizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Ok "Published FixFox.exe ($exeSizeMb MB)"

Step "Checking publish contents"
$requiredPaths = @(
    $exe,
    (Join-Path $publishDir "Configuration"),
    (Join-Path $publishDir "Docs"),
    (Join-Path $publishDir "CHANGELOG.md")
)

foreach ($requiredPath in $requiredPaths) {
    if (-not (Test-Path $requiredPath)) {
        Fail "Missing publish artifact: $requiredPath"
    }
}
Ok "Publish output contains the app, docs, config, and release notes"

if (-not $SkipVerify) {
    Step "Running FixFox headless verification"
    $verifyOut = Join-Path $env:TEMP "fixfox-verify.txt"
    $verifyErr = Join-Path $env:TEMP "fixfox-verify.err.txt"
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
    $verifyText -split "`r?`n" |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { Write-Host ("  " + $_) -ForegroundColor Gray }

    if ($verifyProc.ExitCode -ne 0) {
        Warn "Headless verification returned exit code $($verifyProc.ExitCode). Check startup-verify.log for details."
    }
    else {
        Ok "Headless verification passed"
    }
}

Step "Preparing distribution folder"
New-Item -ItemType Directory -Path $distDir -Force | Out-Null
if (Test-Path $stageDir) {
    Remove-Item $stageDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $stageDir -Recurse -Force
Copy-Item -Path (Join-Path $projectDir "README.md") -Destination $stageDir -Force
Copy-Item -Path (Join-Path $projectDir "SETUP.md") -Destination $stageDir -Force
Ok "Distribution folder staged at $stageDir"

Step "Creating portable zip package"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $zipPath -Force
$zipSizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Ok "Portable package created ($zipSizeMb MB)"

if (-not $SkipInstaller) {
    $iscc = Get-InnoSetupCompiler
    $iss = Join-Path $projectDir "Packaging\\FixFox.iss"
    if ($iscc -and (Test-Path $iss)) {
        Step "Building installer with Inno Setup"
        & $iscc "/DAppVersion=$Version" "/DSourceDir=$stageDir" "/DOutputDir=$distDir" $iss | Out-Host
        if ($LASTEXITCODE -eq 0) {
            Ok "Installer build completed"
        }
        else {
            Warn "Installer build failed. Portable package is still available."
        }
    }
    else {
        Warn "Inno Setup was not found or Packaging\\FixFox.iss is missing. Skipping installer build."
    }
}

$elapsed = [int]((Get-Date) - $startTime).TotalSeconds
Write-Host ""
Write-Host "===============================================" -ForegroundColor Green
Write-Host "  FixFox packaging completed in ${elapsed}s" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Publish folder : $publishDir"
Write-Host "  Portable zip   : $zipPath"
Write-Host "  Run app        : & '$exe'"
Write-Host "  Verify app     : & '$exe' --verify-headless"
Write-Host ""
