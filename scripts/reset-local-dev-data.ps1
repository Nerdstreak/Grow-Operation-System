Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

function Assert-InRepo {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $fullRepo = [System.IO.Path]::GetFullPath($repoRoot)
    if (-not $fullPath.StartsWith($fullRepo, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to touch path outside repository: $fullPath"
    }
    return $fullPath
}

$appData = Assert-InRepo (Join-Path $repoRoot 'GrowDiary.Web\App_Data')
$dbPath = Join-Path $appData 'grow-diary.db'
$backupRoot = Assert-InRepo (Join-Path $repoRoot 'backups\dev-reset')
$uploadsRoot = Assert-InRepo (Join-Path $repoRoot 'GrowDiary.Web\wwwroot\uploads')
$artifactsRoot = Assert-InRepo (Join-Path $repoRoot 'artifacts')

New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null

if (Test-Path -LiteralPath $dbPath) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $backupPath = Join-Path $backupRoot "grow-diary-before-reset-$stamp.db"
    Copy-Item -LiteralPath $dbPath -Destination $backupPath -Force
    Write-Host "Backup erstellt: $backupPath"
}

foreach ($file in @($dbPath, "$dbPath-wal", "$dbPath-shm")) {
    $safeFile = Assert-InRepo $file
    if (Test-Path -LiteralPath $safeFile) {
        Remove-Item -LiteralPath $safeFile -Force
    }
}

foreach ($dir in @($uploadsRoot, $artifactsRoot)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Get-ChildItem -LiteralPath $dir -Force | ForEach-Object {
        $safeChild = Assert-InRepo $_.FullName
        Remove-Item -LiteralPath $safeChild -Recurse -Force
    }
}

New-Item -ItemType Directory -Force -Path $appData | Out-Null
New-Item -ItemType Directory -Force -Path $uploadsRoot | Out-Null
New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null

Write-Host 'Lokale Entwicklungsdaten wurden zurückgesetzt. Knowledge Defaults bleiben erhalten.'
