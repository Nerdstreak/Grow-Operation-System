# Deployment

Diese Anleitung beschreibt den ersten einfachen Release-Weg für Grow Operation System: ein lokales Release-ZIP. Docker, `systemd`, Windows Service und GitHub Release Automation sind bewusst spätere Schritte.

## Release-ZIP erstellen

Voraussetzungen auf dem Build-Rechner:

- .NET SDK 8
- Node.js/npm
- PowerShell

Beispiele aus dem Repository-Root:

```powershell
.\scripts\publish-release.ps1 -Version "0.1.0"
.\scripts\publish-release.ps1 -Version "0.1.0" -Runtime "win-x64"
.\scripts\publish-release.ps1 -Version "0.1.0" -Runtime "linux-x64"
.\scripts\publish-release.ps1 -Version "0.1.0" -Runtime "linux-arm64"
```

Falls PowerShell die Skriptausführung lokal blockiert, kann das Skript für diesen Prozess so gestartet werden:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1 -Version "0.1.0"
```

Standardwerte:

- `Version`: `dev`
- `Runtime`: `portable`
- `SelfContained`: `false`
- `OutputDir`: `artifacts/releases`

Das Skript führt aus:

1. `npm ci` in `GrowDiary.React`
2. `npm run build` in `GrowDiary.React`
3. `dotnet restore GrowDiary.Web/GrowDiary.Web.csproj`
4. `dotnet publish GrowDiary.Web/GrowDiary.Web.csproj -c Release --no-restore`
5. Erzeugen eines Release-Ordners
6. Entfernen lokaler Runtime-/Privatdaten aus dem Release
7. Erzeugen eines ZIP-Archivs

## Runtime-Varianten

`portable` baut framework-dependent ohne Runtime Identifier. Der Zielrechner braucht eine passende .NET Runtime.

Mit `-Runtime "win-x64"`, `-Runtime "linux-x64"` oder `-Runtime "linux-arm64"` wird für diese Plattform veröffentlicht. Standardmäßig bleibt auch das framework-dependent, außer `-SelfContained $true` wird gesetzt.

Self-contained Releases sind größer, können aber ohne vorinstallierte .NET Runtime laufen.

## Release-Struktur

Beispiel:

```text
artifacts/releases/
  grow-os-0.1.0-win-x64/
    app/
      GrowDiary.Web.dll
      wwwroot/
      appsettings.json
      ...
    START_HERE.txt
  grow-os-0.1.0-win-x64.zip
```

`START_HERE.txt` enthält kurze Start-, URL-, Update- und Security-Hinweise für Nutzer.

## Start aus dem ZIP

ZIP entpacken und in den `app`-Ordner wechseln.

Framework-dependent:

```bash
dotnet GrowDiary.Web.dll
```

Windows self-contained:

```powershell
.\GrowDiary.Web.exe
```

Standard-URLs:

- `http://localhost:5076`
- `http://<server-ip>:5076`

## Daten und Updates

`App_Data` ist nicht im ZIP enthalten. Die App legt lokale Daten beim Start an.

Vor Updates:

1. App stoppen.
2. `App_Data` sichern.
3. Falls vorhanden, Uploads/Snapshots sichern.
4. Neue App-Dateien aus dem ZIP einspielen.
5. Bestehendes `App_Data` behalten.
6. App starten und prüfen.

Private Daten und Secrets gehören nicht ins Release:

- `App_Data`
- SQLite DB, WAL und SHM
- `ha-config.json`
- DataProtectionKeys
- Snapshots
- Uploads
- lokale Claude-/Codex-Dateien

Weitere Details stehen in [BACKUP_RESTORE.md](BACKUP_RESTORE.md) und [SECURITY.md](SECURITY.md).

## Grenzen von DEPLOY-1

- kein Dockerfile
- kein `docker-compose.yml`
- keine `systemd` Unit
- kein Windows-Service-Setup
- keine GitHub Release Automation

Diese Themen folgen in späteren Deployment-Tickets.
