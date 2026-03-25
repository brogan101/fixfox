# Checks the FixFox source tree for obvious packaging and source issues before building.

param([string]$ProjectDir = $PSScriptRoot)

$errors = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$ok = [System.Collections.Generic.List[string]]::new()

function Check($label, $pass, $detail = "") {
    if ($pass) {
        $ok.Add("  v $label")
        return
    }

    $suffix = if ($detail) { ": $detail" } else { "" }
    $errors.Add("  x $label$suffix")
}

function WarnItem($label, $detail = "") {
    $suffix = if ($detail) { ": $detail" } else { "" }
    $warnings.Add("  ! $label$suffix")
}

Write-Host ""
Write-Host "FixFox Source Validator" -ForegroundColor Cyan
Write-Host "=======================" -ForegroundColor Cyan

$csproj = Join-Path $ProjectDir "HelpDesk.csproj"
Check "HelpDesk.csproj exists" (Test-Path $csproj)
if (Test-Path $csproj) {
    $csprojContent = Get-Content $csproj -Raw
    Check "AssemblyName is FixFox" ($csprojContent -match '<AssemblyName>FixFox</AssemblyName>')
    Check "Target framework is net8.0-windows" ($csprojContent -match 'net8.0-windows')
    Check "UseWPF is true" ($csprojContent -match '<UseWPF>true</UseWPF>')
    Check "WPF-UI is referenced" ($csprojContent -match 'WPF-UI')
    Check "Icons are declared as resources" (($csprojContent -match 'FixFoxLogo.png') -and ($csprojContent -match 'FixFoxLogo.ico'))
}

$required = @(
    "App.xaml",
    "App.xaml.cs",
    "Program.cs",
    "FixFoxLogo.png",
    "FixFoxLogo.ico",
    "app.manifest",
    "HelpDesk.csproj",
    "Build-FixFox.ps1",
    "Test-FixFox.ps1",
    "Validate-Source.ps1",
    "SETUP.md",
    "CHANGELOG.md",
    "Configuration\\branding.json",
    "Configuration\\knowledge-base.json",
    "Configuration\\release-feed.json",
    "Configuration\\update.json",
    "Docs\\Quick-Start.md",
    "Docs\\Privacy-and-Data.md",
    "Docs\\Recovery-and-Resume.md",
    "Docs\\Support-Packages.md",
    "Docs\\Troubleshooting-and-FAQ.md",
    "Packaging\\FixFox.iss",
    "Application\\Interfaces\\IServices.cs",
    "Application\\Services\\AppServiceRegistrar.cs",
    "Domain\\Enums\\Enums.cs",
    "Domain\\Models\\Models.cs",
    "Infrastructure\\Services\\InfraServices.cs",
    "Infrastructure\\Services\\ProductizationPolicies.cs",
    "Infrastructure\\Services\\ProductizationServices.cs",
    "Infrastructure\\Services\\WorkspaceServices.cs",
    "Presentation\\ViewModels\\MainViewModel.cs",
    "Presentation\\Views\\MainWindow.xaml",
    "Presentation\\Views\\MainWindow.xaml.cs",
    "Themes\\Dark.xaml",
    "Themes\\Light.xaml"
)

foreach ($item in $required) {
    Check "File exists: $item" (Test-Path (Join-Path $ProjectDir $item))
}

$sourceFiles = Get-ChildItem $ProjectDir -Recurse -File |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|publish|dist)\\' }

$csFiles = $sourceFiles | Where-Object { $_.Extension -eq ".cs" } | Select-Object -ExpandProperty FullName
$xamlFiles = $sourceFiles | Where-Object { $_.Extension -eq ".xaml" } | Select-Object -ExpandProperty FullName

$restrictedPatterns = @(
    "Anthropic",
    "AnthropicApiKey",
    "AiAssistant",
    "IAiAssistant",
    "claude-sonnet",
    "claude-opus",
    "api.anthropic.com",
    "ChatBusy",
    "AskAsync"
)

foreach ($pattern in $restrictedPatterns) {
    $hits = ($csFiles + $xamlFiles) | Select-String -Pattern $pattern -List
    if ($hits) {
        $files = $hits | ForEach-Object { Split-Path $_.Path -Leaf }
        $errors.Add("  x Restricted term '$pattern' found in: $($files -join ', ')")
    }
}

if (-not ($errors | Where-Object { $_ -match "Restricted term" })) {
    $ok.Add("  v No restricted brand references found in source files")
}

$wrongNamespace = $csFiles | Select-String -Pattern 'namespace FixFox\b' -List
if ($wrongNamespace) {
    WarnItem "Some files still use namespace FixFox" "Prefer HelpDesk.* namespaces until the internal rename is completed"
} else {
    $ok.Add("  v Source namespaces stay inside HelpDesk.*")
}

$catalogFiles = Get-ChildItem (Join-Path $ProjectDir "Infrastructure\\Fixes") -Filter "FixCatalog_*.cs" -File
$allFixIds = foreach ($catalogFile in $catalogFiles) {
    $matches = [regex]::Matches((Get-Content $catalogFile.FullName -Raw), 'Id\s*=\s*"([^"]+)"')
    foreach ($match in $matches) { $match.Groups[1].Value }
}

Check "Fix catalog contains a substantial repair set" (($allFixIds | Measure-Object).Count -gt 100)
$duplicateFixIds = $allFixIds | Group-Object | Where-Object { $_.Count -gt 1 } | Select-Object -ExpandProperty Name
if ($duplicateFixIds) {
    foreach ($duplicateFixId in $duplicateFixIds) {
        $errors.Add("  x Duplicate fix ID: $duplicateFixId")
    }
} else {
    $ok.Add("  v No duplicate fix IDs across catalog files")
}

$registrarPath = Join-Path $ProjectDir "Application\\Services\\AppServiceRegistrar.cs"
if (Test-Path $registrarPath) {
    $registrarContent = Get-Content $registrarPath -Raw
    $mustRegister = @(
        "ISettingsService",
        "IFixCatalogService",
        "IRepairExecutionService",
        "IRunbookExecutionService",
        "IEvidenceBundleService",
        "IKnowledgeBaseService",
        "IAppUpdateService",
        "IToolboxService"
    )

    foreach ($serviceName in $mustRegister) {
        if ($registrarContent -notmatch $serviceName) {
            WarnItem "DI registration missing" $serviceName
        }
    }

    $ok.Add("  v Composition root registration check complete")
}

$packagingFiles = @(
    (Join-Path $ProjectDir "Build-FixFox.ps1"),
    (Join-Path $ProjectDir "Packaging\\FixFox.iss")
)
foreach ($packagingFile in $packagingFiles) {
    if (Test-Path $packagingFile) {
        $content = Get-Content $packagingFile -Raw
        if ($content -match 'TODO|coming soon|placeholder') {
            WarnItem "Packaging file contains unfinished wording" (Split-Path $packagingFile -Leaf)
        }
    }
}

Write-Host ""
foreach ($line in $ok) { Write-Host $line -ForegroundColor Green }
foreach ($line in $warnings) { Write-Host $line -ForegroundColor Yellow }
foreach ($line in $errors) { Write-Host $line -ForegroundColor Red }
Write-Host ""

$summaryColor = if ($errors.Count -eq 0) { "Green" } else { "Red" }
Write-Host "Results: $($ok.Count) passed, $($warnings.Count) warnings, $($errors.Count) errors" -ForegroundColor $summaryColor

if ($errors.Count -gt 0) {
    Write-Host "Fix the errors above before building." -ForegroundColor Red
    exit 1
}

exit 0
