# Deployment

Diese Anleitung beschreibt die einfachen Selfhosting-Betriebswege für Grow Operation System: Release-ZIP, Docker Compose und ein `systemd`-Beispiel für Linux/Raspberry Pi/Mini-PC. Windows Service und GitHub Release Automation sind bewusst spätere Schritte.

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

PWA-Installation und Offline-Minimum sind in [PWA_INSTALL.md](PWA_INSTALL.md) beschrieben.

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

## Docker Compose Betrieb

DEPLOY-2 ergänzt ein einfaches lokales Docker-Beispiel. Es baut React im Container, published das ASP.NET Core Backend und startet die App mit persistentem Datenordner unter `/data`.

Voraussetzungen:

- Docker
- Docker Compose
- Netzwerkzugriff vom Container auf Home Assistant

Start aus dem Repository-Root:

```bash
docker compose -f docker-compose.example.yml up -d --build
```

Zugriff:

- `http://localhost:5076`
- `http://<server-ip>:5076`

Das Compose-Beispiel nutzt:

- Port-Mapping `5076:5076`
- Volume `./docker-data/grow-os:/data`
- `ASPNETCORE_URLS=http://0.0.0.0:5076`
- `GROWDIARY_DB_PATH=/data/grow-diary.db`
- `GROWDIARY_ALLOW_REMOTE_ADMIN=false`

Lokale Daten liegen dadurch nicht im Image, sondern auf dem Host unter:

```text
docker-data/grow-os
```

Die SQLite-Datenbank liegt im Container unter:

```text
/data/grow-diary.db
```

Sichere für Backups den Host-Ordner `docker-data/grow-os`. Dieser Ordner ist per `.gitignore` ausgeschlossen und darf keine Git-Daten werden.

### Home Assistant aus Docker

Grow OS muss Home Assistant aus dem Container erreichen, nicht nur dein Browser oder Handy. Je nach Docker-Netz funktioniert `homeassistant.local` nicht zuverlässig. Verwende für die Home Assistant Base URL im Zweifel eine feste IP oder einen DNS-Namen, den der Container auflösen kann, zum Beispiel:

```text
http://192.168.x.x:8123
```

Home Assistant Tokens gehören nicht in das Image und nicht in `docker-compose.example.yml`. Konfiguriere sie in der App oder über lokale Runtime-Daten.

### Docker Update

Vor einem Update:

1. Container stoppen.
2. `docker-data/grow-os` sichern.
3. Quellen aktualisieren.
4. Image neu bauen und Container starten.
5. Logs prüfen.

Beispiel:

```bash
docker compose -f docker-compose.example.yml down
cp -a docker-data/grow-os "$HOME/grow-os-docker-backup-$(date +%Y%m%d-%H%M%S)"
git pull
docker compose -f docker-compose.example.yml up -d --build
docker compose -f docker-compose.example.yml logs -f
```

Setze den Container nicht ungeschützt ins Internet. Für Remote-Zugriff gelten dieselben Regeln wie außerhalb von Docker: Tailscale/VPN bevorzugt, Reverse Proxy nur mit HTTPS und vorgeschalteter Auth.

Grenzen von DEPLOY-2:

- kein offizielles Registry Image
- kein GitHub Release Workflow für Docker Images
- kein Docker-Production-Hardening über das einfache Compose-Beispiel hinaus

## GitHub Releases

DEPLOY-4 ergänzt einen Tag-basierten GitHub Actions Workflow unter `.github/workflows/release.yml`. Er läuft nicht bei jedem Push, sondern nur bei Versionstags im Format `v*.*.*`, zum Beispiel `v0.1.0`.

Release erstellen:

```bash
git tag v0.1.0
git push origin v0.1.0
```

Der Workflow nutzt `scripts/publish-release.ps1` und erstellt aktuell ein portable framework-dependent ZIP:

```text
artifacts/releases/grow-os-0.1.0-portable.zip
```

Das Release wird als Draft und Prerelease angelegt. Maintainer sollen den erzeugten Release prüfen, Release Notes ergänzen und ihn erst danach veröffentlichen.

Wichtig:

- Es werden keine Secrets benötigt.
- Die App wird im Workflow nicht gestartet.
- `App_Data`, Datenbanken, Uploads, Snapshots und lokale Secrets sind nicht Teil des ZIPs.
- Vor einem Update immer lokale Daten sichern. Details stehen in [BACKUP_RESTORE.md](BACKUP_RESTORE.md).

RID-spezifische ZIPs wie `win-x64`, `linux-x64` oder `linux-arm64` können später ergänzt werden, wenn der Publish-Prozess dafür im CI-Kontext separat verifiziert ist. Self-contained Releases sind in DEPLOY-4 bewusst nicht vorgesehen, damit Artefakte klein bleiben.

## Grenzen von DEPLOY-1/DEPLOY-2/DEPLOY-3/DEPLOY-4

- kein Windows-Service-Setup
- kein Docker Registry Push
- keine self-contained Release-Artefakte
- keine automatischen Releases ohne Versionstag

Windows Service, Docker Registry Builds und erweiterte Release-Artefakte folgen in späteren Deployment-Tickets.
