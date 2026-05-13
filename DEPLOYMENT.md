# Deployment

Diese Anleitung beschreibt den ersten einfachen Release-Weg für Grow Operation System: ein lokales Release-ZIP. Für Linux/Raspberry Pi/Mini-PC gibt es zusätzlich ein einfaches `systemd`-Beispiel. Docker, Windows Service und GitHub Release Automation sind bewusst spätere Schritte.

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

## Linux/systemd Betrieb

Für dauerhaften Betrieb auf Linux, Raspberry Pi oder Mini-PC kann ein framework-dependent Release als `systemd` Dienst laufen. Das Beispiel liegt unter:

```text
deploy/systemd/grow-os.service.example
```

Voraussetzungen auf dem Zielsystem:

- passende .NET Runtime 8
- Release-ZIP, zum Beispiel `grow-os-0.1.0-linux-x64.zip` oder `grow-os-0.1.0-linux-arm64.zip`
- eigener Service-Nutzer, zum Beispiel `growos`
- Datenordner außerhalb der App-Dateien, zum Beispiel `/var/lib/grow-os`

Beispielablauf:

```bash
sudo useradd --system --home /var/lib/grow-os --shell /usr/sbin/nologin growos
sudo mkdir -p /opt/grow-os/app /var/lib/grow-os
sudo chown -R growos:growos /var/lib/grow-os
```

Release entpacken und die Inhalte des `app`-Ordners nach `/opt/grow-os/app` kopieren. Danach Rechte setzen:

```bash
sudo chown -R root:root /opt/grow-os
sudo chmod -R a+rX /opt/grow-os
sudo chown -R growos:growos /var/lib/grow-os
```

Service-Datei aus dem Repository nach `/etc/systemd/system/grow-os.service` kopieren. Wenn du nur ein Release-ZIP verwendest, übernimm den Inhalt aus `deploy/systemd/grow-os.service.example` aus dem Projekt-Repository und speichere ihn auf dem Zielsystem als `/etc/systemd/system/grow-os.service`.

```bash
sudo cp deploy/systemd/grow-os.service.example /etc/systemd/system/grow-os.service
sudo systemctl daemon-reload
sudo systemctl enable --now grow-os
```

Status und Logs prüfen:

```bash
systemctl status grow-os
journalctl -u grow-os -f
```

Die Beispiel-Unit setzt:

- `ASPNETCORE_URLS=http://0.0.0.0:5076`
- `GROWDIARY_DB_PATH=/var/lib/grow-os/grow-diary.db`
- `User=growos`
- `WorkingDirectory=/opt/grow-os/app`

Damit liegen App-Dateien und lokale Daten getrennt. `App_Data` im Release-Ordner wird nicht benötigt, wenn `GROWDIARY_DB_PATH` auf `/var/lib/grow-os/grow-diary.db` zeigt. Prüfe Firewall und Reverse-Proxy/VPN-Setup, bevor du die App aus dem LAN heraus erreichbar machst.

## systemd Update und Backup

Vor einem Update:

1. Dienst stoppen.
2. `/var/lib/grow-os` sichern.
3. Neue App-Dateien nach `/opt/grow-os/app` kopieren.
4. Rechte prüfen.
5. Dienst starten.
6. Logs prüfen.

Beispiel:

```bash
sudo systemctl stop grow-os
sudo cp -a /var/lib/grow-os "$HOME/grow-os-backup-$(date +%Y%m%d-%H%M%S)"
sudo systemctl start grow-os
journalctl -u grow-os -n 100
```

Details zu Backup und Restore stehen in [BACKUP_RESTORE.md](BACKUP_RESTORE.md). Security-Hinweise zum Remote-Betrieb stehen in [SECURITY.md](SECURITY.md) und [SELFHOSTING.md](SELFHOSTING.md).

## Grenzen von DEPLOY-1/DEPLOY-3

- kein Dockerfile
- kein `docker-compose.yml`
- kein Windows-Service-Setup
- keine GitHub Release Automation

Docker, Windows Service und GitHub Release Automation folgen in späteren Deployment-Tickets.
