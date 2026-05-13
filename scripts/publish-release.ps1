param(
    [string]$Version = "dev",
    [string]$Runtime = "portable",
    [bool]$SelfContained = $false,
    [string]$OutputDir = "artifacts/releases"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-Tool {
    param([string[]]$Names)

    foreach ($name in $Names) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($command) {
            return $command.Source
        }
    }

    throw "Required tool not found: $($Names -join ' or ')"
}

function Remove-IfExists {
    param(
        [string]$Path,
        [string]$AllowedRoot
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $resolvedRoot = [System.IO.Path]::GetFullPath($AllowedRoot)
    if (-not $resolvedPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove path outside output directory: $resolvedPath"
    }

    Remove-Item -LiteralPath $resolvedPath -Recurse -Force
}

function Remove-ReleasePrivateData {
    param([string]$AppDir)

    $relativePaths = @(
        "App_Data",
        "wwwroot/uploads",
        ".git",
        ".claude",
        "node_modules",
        "bin",
        "obj"
    )

    foreach ($relativePath in $relativePaths) {
        $target = Join-Path $AppDir $relativePath
        if (Test-Path -LiteralPath $target) {
            Remove-Item -LiteralPath $target -Recurse -Force
        }
    }

    $secretPatterns = @(
        "*.db",
        "*.db-wal",
        "*.db-shm",
        "ha-config.json",
        "settings.local.json"
    )

    foreach ($pattern in $secretPatterns) {
        Get-ChildItem -LiteralPath $AppDir -Recurse -Force -File -Filter $pattern -ErrorAction SilentlyContinue |
            Remove-Item -Force
    }
}

function Remove-StaleViteEntryAssets {
    param([string]$AppDir)

    $wwwroot = Join-Path $AppDir "wwwroot"
    $indexPath = Join-Path $wwwroot "index.html"
    $assetsPath = Join-Path $wwwroot "assets"
    if (-not (Test-Path -LiteralPath $indexPath) -or -not (Test-Path -LiteralPath $assetsPath)) {
        return
    }

    $indexHtml = Get-Content -LiteralPath $indexPath -Raw
    $referencedAssets = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)
    [regex]::Matches($indexHtml, 'assets/([^"''<> ]+)') | ForEach-Object {
        [void]$referencedAssets.Add($_.Groups[1].Value)
    }

    Get-ChildItem -LiteralPath $assetsPath -File -Force |
        Where-Object {
            $_.Name -like "index-*.js" -or $_.Name -like "index-*.css"
        } |
        Where-Object {
            -not $referencedAssets.Contains($_.Name)
        } |
        Remove-Item -Force
}

function Invoke-Checked {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$StepName
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$StepName failed with exit code $LASTEXITCODE"
    }
}

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..")
$repoRootPath = $repoRoot.Path

$dotnet = Resolve-Tool @("dotnet")
$npm = Resolve-Tool @("npm.cmd", "npm")

$versionSlug = ($Version.Trim() -replace "[^A-Za-z0-9._-]", "-")
if ([string]::IsNullOrWhiteSpace($versionSlug)) {
    $versionSlug = "dev"
}

$runtimeSlug = ($Runtime.Trim() -replace "[^A-Za-z0-9._-]", "-")
if ([string]::IsNullOrWhiteSpace($runtimeSlug)) {
    $runtimeSlug = "portable"
}

$releaseName = "grow-os-$versionSlug-$runtimeSlug"
if ($SelfContained) {
    $releaseName = "$releaseName-self-contained"
}

$outputRoot = Join-Path $repoRootPath $OutputDir
$outputRoot = [System.IO.Path]::GetFullPath($outputRoot)
$releaseDir = Join-Path $outputRoot $releaseName
$appDir = Join-Path $releaseDir "app"
$zipPath = Join-Path $outputRoot "$releaseName.zip"

Write-Host "Repo root: $repoRootPath"
Write-Host "Release:   $releaseName"
Write-Host "Output:    $outputRoot"

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
Remove-IfExists -Path $releaseDir -AllowedRoot $outputRoot
Remove-IfExists -Path $zipPath -AllowedRoot $outputRoot
New-Item -ItemType Directory -Path $appDir -Force | Out-Null

Push-Location (Join-Path $repoRootPath "GrowDiary.React")
try {
    Write-Host "Installing frontend dependencies..."
    Invoke-Checked -FilePath $npm -Arguments @("ci") -StepName "Frontend install"

    Write-Host "Building frontend..."
    Invoke-Checked -FilePath $npm -Arguments @("run", "build") -StepName "Frontend build"
}
finally {
    Pop-Location
}

$projectPath = Join-Path $repoRootPath "GrowDiary.Web/GrowDiary.Web.csproj"
Write-Host "Restoring backend..."
Invoke-Checked -FilePath $dotnet -Arguments @("restore", $projectPath) -StepName "Backend restore"

$publishArgs = @(
    "publish",
    $projectPath,
    "-c",
    "Release",
    "--no-restore",
    "-o",
    $appDir
)

if ($Runtime -ne "portable") {
    $publishArgs += @("-r", $Runtime, "--self-contained", $SelfContained.ToString().ToLowerInvariant())
}

Write-Host "Publishing backend..."
Invoke-Checked -FilePath $dotnet -Arguments $publishArgs -StepName "Backend publish"

Write-Host "Removing private/runtime data from release..."
Remove-ReleasePrivateData -AppDir $appDir
Remove-StaleViteEntryAssets -AppDir $appDir

$docFiles = @(
    "README.md",
    "INSTALL.md",
    "SELFHOSTING.md",
    "SECURITY.md",
    "BACKUP_RESTORE.md",
    "HOME_ASSISTANT.md",
    "DEPLOYMENT.md"
)

foreach ($docFile in $docFiles) {
    $sourceDoc = Join-Path $repoRootPath $docFile
    if (Test-Path -LiteralPath $sourceDoc) {
        Copy-Item -LiteralPath $sourceDoc -Destination $releaseDir -Force
    }
}

$startHerePath = Join-Path $releaseDir "START_HERE.txt"
$startCommand = if ($Runtime -eq "portable" -or -not $SelfContained) {
    "dotnet GrowDiary.Web.dll"
} else {
    if ($Runtime.StartsWith("win-")) { "GrowDiary.Web.exe" } else { "./GrowDiary.Web" }
}

$startHere = @"
Grow Operation System Release
=============================

Start:
- Windows self-contained: GrowDiary.Web.exe
- Framework-dependent/portable: dotnet GrowDiary.Web.dll
- This release command hint: $startCommand

Default URLs:
- Local: http://localhost:5076
- LAN:   http://<server-ip>:5076

Data:
- Runtime data is created in App_Data after first start.
- App_Data is intentionally not included in this ZIP.
- Back up App_Data before updates.
- Keep existing App_Data when replacing app files.

Security:
- Do not expose Grow OS publicly without VPN, reverse proxy auth, or comparable protection.
- Keep Home Assistant tokens private.

Docs:
- README.md
- INSTALL.md
- SELFHOSTING.md
- SECURITY.md
- BACKUP_RESTORE.md
- DEPLOYMENT.md
"@

Set-Content -LiteralPath $startHerePath -Value $startHere -Encoding UTF8

Write-Host "Creating ZIP..."
Compress-Archive -Path (Join-Path $releaseDir "*") -DestinationPath $zipPath -Force

Write-Host "Release directory: $releaseDir"
Write-Host "Release ZIP:       $zipPath"
