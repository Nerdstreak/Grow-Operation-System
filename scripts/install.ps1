<#
  Grow OS - Installer fuer Windows (Desktop)

    irm https://raw.githubusercontent.com/Nerdstreak/Grow-Operation-System/main/scripts/install.ps1 | iex

  Installiert bei Bedarf die .NET-8-Runtime, laedt das fertige Grow-OS-Release
  herunter, richtet den Autostart ein und startet die App. Erneutes Ausfuehren
  aktualisiert auf die neueste Version. Kein Docker noetig.
#>

$ErrorActionPreference = 'Stop'
$Repo    = 'Nerdstreak/Grow-Operation-System'
$AppRoot = Join-Path $env:LOCALAPPDATA 'GrowOS'
$AppDir  = Join-Path $AppRoot 'app'
$DataDir = Join-Path $AppRoot 'data'
$Port    = 5076

function Info($m){ Write-Host "`n==> $m" -ForegroundColor Green }
function Warn($m){ Write-Host "[!] $m" -ForegroundColor Yellow }

# 1) .NET 8 ASP.NET Runtime sicherstellen ------------------------------------
function Test-AspNet8 {
  try { return ((& dotnet --list-runtimes 2>$null) -match 'Microsoft\.AspNetCore\.App 8\.') } catch { return $false }
}
if (-not (Test-AspNet8)) {
  Info "Installiere die .NET-8-Runtime..."
  if (Get-Command winget -ErrorAction SilentlyContinue) {
    winget install --id Microsoft.DotNet.AspNetCore.8 -e --silent `
      --accept-source-agreements --accept-package-agreements
    $env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' +
                [Environment]::GetEnvironmentVariable('Path','User')
  } else {
    throw "winget fehlt. Bitte die '.NET 8 ASP.NET Core Runtime' manuell installieren: https://dotnet.microsoft.com/download/dotnet/8.0"
  }
  if (-not (Test-AspNet8)) { throw "Die .NET-8-Runtime konnte nicht gefunden werden. Bitte das Fenster neu oeffnen und erneut versuchen." }
}

# 2) Neuestes Release herunterladen ------------------------------------------
Info "Suche die neueste Grow-OS-Version..."
$headers = @{ 'User-Agent' = 'grow-os-installer'; 'Accept' = 'application/vnd.github+json' }
$release = Invoke-RestMethod "https://api.github.com/repos/$Repo/releases/latest" -Headers $headers
$asset   = $release.assets | Where-Object { $_.name -like '*portable*.zip' } | Select-Object -First 1
if (-not $asset) { throw "Kein portables Release-Paket gefunden." }

$zip = Join-Path $env:TEMP $asset.name
Info "Lade $($asset.name) ($([math]::Round($asset.size/1MB,1)) MB)..."
Invoke-WebRequest $asset.browser_download_url -OutFile $zip -Headers $headers

# Laufenden Prozess stoppen (bei Update), dann neu entpacken
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
  Where-Object { $_.CommandLine -like '*GrowDiary.Web.dll*' } |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

if (Test-Path $AppDir) { Remove-Item $AppDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $AppDir, $DataDir | Out-Null
Info "Entpacke..."
Expand-Archive -Path $zip -DestinationPath $AppDir -Force
Remove-Item $zip -Force

$dll = Get-ChildItem $AppDir -Recurse -Filter 'GrowDiary.Web.dll' | Select-Object -First 1
if (-not $dll) { throw "GrowDiary.Web.dll wurde im Paket nicht gefunden." }

# 3) Start-Skript + Autostart ------------------------------------------------
$startCmd = Join-Path $AppRoot 'Grow OS starten.cmd'
@"
@echo off
set ASPNETCORE_URLS=http://0.0.0.0:$Port
set GROWDIARY_DB_PATH=$DataDir\grow-diary.db
start "" http://localhost:$Port
dotnet "$($dll.FullName)"
"@ | Set-Content -Path $startCmd -Encoding ASCII

# Autostart-Verknuepfung (startet minimiert beim Login)
$startup = [Environment]::GetFolderPath('Startup')
$lnk = Join-Path $startup 'Grow OS.lnk'
$ws = New-Object -ComObject WScript.Shell
$sc = $ws.CreateShortcut($lnk)
$sc.TargetPath = $startCmd
$sc.WorkingDirectory = $AppRoot
$sc.WindowStyle = 7
$sc.Description = 'Grow OS'
$sc.Save()

# Desktop-Verknuepfung zum manuellen Start
$desktop = [Environment]::GetFolderPath('Desktop')
$sc2 = $ws.CreateShortcut((Join-Path $desktop 'Grow OS.lnk'))
$sc2.TargetPath = $startCmd
$sc2.WorkingDirectory = $AppRoot
$sc2.Save()

# 4) Starten -----------------------------------------------------------------
Info "Starte Grow OS..."
$env:ASPNETCORE_URLS = "http://0.0.0.0:$Port"
$env:GROWDIARY_DB_PATH = (Join-Path $DataDir 'grow-diary.db')
Start-Process -FilePath 'dotnet' -ArgumentList "`"$($dll.FullName)`"" -WindowStyle Minimized
Start-Sleep -Seconds 3
Start-Process "http://localhost:$Port"

Write-Host ""
Write-Host "=========================================================" -ForegroundColor Green
Write-Host " Grow OS laeuft! 🌱" -ForegroundColor Green
Write-Host "   Im Browser:  http://localhost:$Port"
Write-Host "   Startet ab jetzt automatisch mit Windows."
Write-Host "   Manuell starten: Desktop-Verknuepfung 'Grow OS'"
Write-Host "=========================================================" -ForegroundColor Green
