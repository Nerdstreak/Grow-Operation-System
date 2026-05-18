# Deployment

## Grundprinzip

Grow OS ist fuer lokalen Betrieb und Selfhosting gedacht. Standard ist LAN oder ein privater Zugriff ueber VPN/Tailscale. Die App sollte nicht direkt und ungeschuetzt ins Internet gestellt werden.

## Lokales Hosting

Backend starten:

```powershell
dotnet run --project GrowDiary.Web/GrowDiary.Web.csproj
```

Zugriff:

- `http://localhost:5076`
- `http://<server-ip>:5076`

Pruefe bei LAN-Betrieb die lokale Firewall fuer Port `5076`.

## PWA

Grow OS ist als Progressive Web App gedacht. Mobile Nutzung erfolgt ohne App Store:

1. App im Browser oeffnen, idealerweise `/action`.
2. Browser-Menue oeffnen.
3. `Zum Home-Bildschirm` oder `App installieren` waehlen.

PWA-Status:

- Manifest, Icons und mobile Meta-Tags sind vorhanden.
- `start_url` ist `/action`.
- App-Shell und statische Assets koennen vorsichtig gecacht werden.
- API-Daten, Home Assistant Livewerte, Speichern, Uploads und Snapshots funktionieren nicht offline.

Fuer Remote-PWA-Nutzung ist HTTPS praktisch erforderlich.

## Remote-Zugriff

Bevorzugt:

- Tailscale oder VPN

Moeglich, aber nur mit Schutz:

- Reverse Proxy mit HTTPS und vorgeschalteter Auth
- Cloudflare Tunnel mit Cloudflare Access oder vergleichbarer Auth

Nicht empfohlen:

- direktes Port Forwarding auf `5076`
- oeffentliche, frei erreichbare URL ohne Zugriffsschutz

Es gibt aktuell keine vollstaendige eingebaute Nutzerverwaltung fuer alle Bereiche.

## Release-ZIP

Ein lokales Release-ZIP kann mit PowerShell erstellt werden:

```powershell
.\scripts\publish-release.ps1 -Version "0.1.0"
```

Optionale Runtime-Beispiele:

```powershell
.\scripts\publish-release.ps1 -Version "0.1.0" -Runtime "win-x64"
.\scripts\publish-release.ps1 -Version "0.1.0" -Runtime "linux-x64"
.\scripts\publish-release.ps1 -Version "0.1.0" -Runtime "linux-arm64"
```

Das Skript baut React, published das Backend und erzeugt ein ZIP unter `artifacts/releases`. Runtime-Daten wie `App_Data`, Datenbanken, Uploads und Secrets gehoeren nicht ins Release.

## Docker Compose

Lokales Compose-Beispiel:

```bash
docker compose -f docker-compose.example.yml up -d --build
```

Das Beispiel nutzt Port `5076` und persistente Daten unter `docker-data/grow-os`. Home Assistant muss aus Sicht des Containers erreichbar sein; bei Problemen eine feste IP oder einen aufloesbaren DNS-Namen verwenden.

## Linux/systemd

Ein Beispiel liegt unter:

```text
deploy/systemd/grow-os.service.example
```

Empfohlen ist ein eigener Service-Nutzer, zum Beispiel `growos`, und die Trennung von App-Dateien und lokalen Daten:

- App: `/opt/grow-os/app`
- Daten: `/var/lib/grow-os`

Typische Umgebungswerte:

- `ASPNETCORE_URLS=http://0.0.0.0:5076`
- `GROWDIARY_DB_PATH=/var/lib/grow-os/grow-diary.db`

## Backup und Updates

Vor Updates:

1. App oder Dienst stoppen.
2. `App_Data` sichern.
3. Uploads/Snapshots sichern, falls genutzt.
4. Neue App-Dateien einspielen.
5. Bestehende lokalen Daten behalten.
6. App starten und Logs pruefen.

Mindestens sichern:

- SQLite DB plus WAL/SHM, falls vorhanden
- `App_Data/knowledge`
- `ha-config.json`, falls genutzt
- DataProtectionKeys, falls vorhanden
- Snapshots und Uploads, falls genutzt

## GitHub Releases

Der Release-Workflow laeuft fuer Versionstags im Format `v*.*.*`, zum Beispiel:

```bash
git tag v0.1.0
git push origin v0.1.0
```

Der erzeugte Release ist als Draft/Prerelease zur manuellen Pruefung gedacht.

## Grenzen

- kein offizielles Registry Image
- kein Windows-Service-Setup
- keine self-contained Release-Artefakte als Standard
- keine automatische Veroeffentlichung ohne Versionstag
