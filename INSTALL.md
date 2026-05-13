# Installation

Diese Anleitung beschreibt den lokalen Entwicklerstart und einen einfachen lokalen Production-Start für Grow Operation System.

## Voraussetzungen

Für den Start aus dem Quellcode brauchst du:

- .NET SDK 8
- Node.js und npm
- Git
- Einen modernen Browser

SQLite läuft eingebettet über `Microsoft.Data.Sqlite`. Es ist kein separater SQLite-Server nötig.

Home Assistant ist für den Zielbetrieb empfohlen, weil Grow OS als Interface für Home Assistant Sensordaten gedacht ist. Die App kann technisch auch ohne konfigurierte HA-Verbindung starten, hat dann aber nur eingeschränkten Nutzen.

## Variante A: Entwicklerstart mit Backend und Vite

### 1. Repository vorbereiten

```bash
git clone <repo-url>
cd "Grow Operation System new"
```

Falls du bereits in einer lokalen Arbeitskopie bist, entfällt dieser Schritt.

### 2. Backend starten

```bash
dotnet run --project GrowDiary.Web/GrowDiary.Web.csproj
```

Das Backend läuft standardmäßig lokal unter:

- `http://localhost:5076`

Im Heimnetz kann es je nach Firewall und Netzwerk unter folgender Adresse erreichbar sein:

- `http://<server-ip>:5076`

### 3. Frontend Dev Server starten

In einem zweiten Terminal:

```bash
cd GrowDiary.React
npm install
npm run dev
```

Vite läuft standardmäßig unter:

- `http://127.0.0.1:5173`

Der Dev Server leitet API-Aufrufe unter `/api` an das Backend weiter.

## Variante B: Lokaler Production-Start

Diese Variante baut das Frontend und lässt anschließend das ASP.NET Core Backend die React-App aus `wwwroot` ausliefern.

### 1. Frontend bauen

```bash
cd GrowDiary.React
npm install
npm run build
```

Der Build schreibt nach:

```text
GrowDiary.Web/wwwroot
```

### 2. Backend starten

Zurück im Repo-Root:

```bash
dotnet run --project GrowDiary.Web/GrowDiary.Web.csproj
```

Öffne danach:

- `http://localhost:5076`
- oder im Heimnetz `http://<server-ip>:5076`

Details zu LAN-Betrieb, PWA im Heimnetz und Remote-Zugriff stehen in [SELFHOSTING.md](SELFHOSTING.md).

Security-Hinweise stehen in [SECURITY.md](SECURITY.md).

## Windows-Hinweise

- PowerShell kann lokale npm-Skripte blockieren. Falls `npm run build` wegen `npm.ps1` scheitert, nutze:

```powershell
& 'C:\Program Files\nodejs\npm.cmd' run build
```

- Prüfe bei LAN-Zugriff die Windows-Firewall für Port `5076`.
- Für dauerhaften Betrieb ist später ein Windows-Service oder ein anderer Prozessmanager sinnvoll. Das ist noch nicht Teil dieser Basisinstallation.

## Linux- und Raspberry-Pi-Hinweise

Grob reicht:

- .NET SDK oder Runtime passend zur Betriebsart
- Node.js/npm für Builds aus dem Quellcode
- Schreibrechte auf `GrowDiary.Web/App_Data`
- Netzwerkzugriff auf Home Assistant

Für dauerhaften Betrieb ist später ein `systemd` Service sinnvoll. Eine vollständige `systemd`- oder Docker-Anleitung gehört nicht zu DOC-1 und soll separat dokumentiert werden.

## Erster Start

Beim ersten Start legt die App lokale Runtime-Daten unter `GrowDiary.Web/App_Data` an.

Wichtige Punkte:

- Die SQLite-Datenbank liegt standardmäßig unter `GrowDiary.Web/App_Data/grow-diary.db`.
- Knowledge-Defaults werden beim Start nach `GrowDiary.Web/App_Data/knowledge` kopiert.
- Einstellungen erreichst du unter `/settings`.
- Home Assistant URL und Long-Lived Access Token können in den Einstellungen gepflegt werden.
- Alternativ kann `GrowDiary.Web/App_Data/ha-config.example.json` als Vorlage für eine lokale `ha-config.json` verwendet werden.
- Danach Zelte und Sensor-Mapping konfigurieren.

Details zur Home-Assistant-Verbindung und zum Sensor-Mapping stehen in [HOME_ASSISTANT.md](HOME_ASSISTANT.md).

## PWA-Installation

Grow OS bringt Basic Installability mit:

- Manifest
- Icons
- mobile Meta-Tags
- Startseite `/action`

Installation grob:

1. App im Browser öffnen.
2. Auf `/action` wechseln.
3. Im Browser-Menü „Zum Home-Bildschirm hinzufügen“ oder „App installieren“ wählen.

Hinweise:

- Für lokalen Testbetrieb funktionieren Browser unterschiedlich tolerant.
- Für Remote-Installation ist HTTPS praktisch erforderlich.
- Es gibt aktuell keinen Service Worker und keine vollständige Offline-Funktion.

## Update grob

Vor jedem Update zuerst Backup machen.

Dann:

```bash
git pull
```

Falls sich Frontend-Pakete geändert haben:

```bash
cd GrowDiary.React
npm install
```

Frontend bauen:

```bash
npm run build
```

Backend prüfen:

```bash
cd ..
dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal
```

Danach Backend wieder starten.

## Backup vor Update

Sichere mindestens:

```text
GrowDiary.Web/App_Data
```

Darin liegen lokale Daten wie SQLite-Datenbank, Home Assistant Konfiguration, Knowledge-Daten und Runtime-Dateien. Tokens und lokale Daten sollten privat bleiben und nicht committed werden.

Die Backup-/Restore-Anleitung steht in [BACKUP_RESTORE.md](BACKUP_RESTORE.md).
