# Grow Operation System

Grow Operation System ist eine kostenlose, selfhosted Grow-Management-App mit Fokus auf RDWC/DWC. Die App verbindet Home Assistant Sensordaten, Grow-Dokumentation, SOPs, Hardware, Wartung und Risiko-Tracking in einem lokalen System.

Grow OS ist keine Cloud- oder SaaS-App. Der lokale Betrieb im eigenen Netzwerk steht zuerst. Mobile Nutzung ist als PWA gedacht, nicht als native iOS- oder Android-App und ohne App-Store-Zwang.

## Hauptfunktionen

- Operations Dashboard als Tageszentrale
- Mobile Action Hub unter `/action`
- Live Dashboard für den Growraum unter `/live`
- Zeltübersicht und Live-Zustand
- Grow-Dokumentation mit Messungen, Journal, Fotos und Tasks
- AutoMeasurements aus Home Assistant Sensordaten
- Diagnose und strukturierte Deviations
- Treatment- und SOP-Empfehlungen aus der Knowledge Base
- Ausführbare SOPs mit Steps
- Hardware-Inventar
- MaintenanceEvents
- CalibrationEvents
- RiskEvents mit SOP-Empfehlungen
- Lesender Knowledge-Browser
- PWA Basic Installability mit Manifest, Icons und mobilen Meta-Tags

## Tech Stack

- ASP.NET Core 8
- SQLite
- React, Vite und TypeScript
- Home Assistant Integration
- ADO.NET mit `Microsoft.Data.Sqlite`
- Kein ORM

## Quick Start für Entwickler

### Voraussetzungen

- .NET SDK 8
- Node.js und npm
- Optional, aber für den Zielbetrieb empfohlen: Home Assistant

### Backend starten

```bash
dotnet run --project GrowDiary.Web/GrowDiary.Web.csproj
```

Standard-URL lokal:

- `http://localhost:5076`

Das Backend ist laut `appsettings.json` standardmäßig auch auf `0.0.0.0:5076` gebunden. Im Heimnetz ist die App dadurch typischerweise unter folgender Adresse erreichbar:

- `http://<server-ip>:5076`

### Frontend im Entwicklungsmodus starten

```bash
cd GrowDiary.React
npm install
npm run dev
```

Vite startet standardmäßig unter:

- `http://127.0.0.1:5173`

Der Vite-Dev-Server proxyt `/api` an das Backend auf Port `5076`.

### Frontend für Production bauen

```bash
cd GrowDiary.React
npm run build
```

Der Build schreibt die React-SPA nach `GrowDiary.Web/wwwroot`. Das ASP.NET Core Backend liefert diese Dateien anschließend direkt aus.

### Tests ausführen

```bash
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal
```

## Wichtige URLs

- Backend / Production App: `http://localhost:5076`
- LAN-Zugriff: `http://<server-ip>:5076`
- Frontend Dev: `http://127.0.0.1:5173`
- Mobile Action Hub: `/action`
- Live Dashboard: `/live`
- Einstellungen: `/settings`

## Daten und Backup

Lokale Runtime-Daten liegen unter `GrowDiary.Web/App_Data`. Dazu gehören insbesondere die SQLite-Datenbank, Home Assistant Konfigurationen, Knowledge-Daten und lokale Runtime-Dateien.

Wichtig:

- `App_Data` enthält lokale und potenziell sensible Daten.
- `App_Data` sollte nicht committed werden.
- Sichere vor Updates mindestens `GrowDiary.Web/App_Data`.
- Halte Home Assistant Tokens privat.
- Details: [BACKUP_RESTORE.md](BACKUP_RESTORE.md) und [SECURITY.md](SECURITY.md).

## Sicherheit und Remote-Zugriff

Grow OS ist zuerst für lokalen Selfhosting-Betrieb gedacht. Es ist keine öffentliche Cloud-App.

Setze die App nicht ungeschützt ins Internet. Für Fernzugriff ist ein privater Zugriff über Tailscale oder VPN die bevorzugte Richtung. Cloudflare Tunnel, eigene Domain oder Reverse Proxy können später sinnvoll sein, sollten aber nur mit HTTPS und zusätzlichem Schutz/Auth verwendet werden.

Details zu LAN-Betrieb, PWA im Heimnetz und Remote-Optionen stehen in [SELFHOSTING.md](SELFHOSTING.md).

Aktuell nicht versprechen:

- Es gibt keine vollständige Login-/User-Authentifizierung.
- Es gibt keine vollständige Offline-PWA.
- Es gibt keinen App-Store-Zwang.

## PWA-Status

PWA Basic Installability ist vorbereitet:

- Web App Manifest
- PWA Icons
- iOS/Android Meta-Tags
- Start-URL `/action`

Es gibt aktuell keinen Service Worker und keine Offline-Strategie. Offline-Minimum und App-Shell-Cache sind für ein späteres Ticket vorgesehen.

## Aktueller Status

Grow OS ist eine MVP/Community Preview mit Fokus auf RDWC/DWC. Soil, Coco und weitere Medien sind später geplant, sobald die jeweiligen Workflows und Expertendaten sauber modelliert sind.

## Weitere Dokumente

- [SELFHOSTING.md](SELFHOSTING.md)
- [SECURITY.md](SECURITY.md)
- [BACKUP_RESTORE.md](BACKUP_RESTORE.md)
- [HOME_ASSISTANT.md](HOME_ASSISTANT.md)
- [ROADMAP.md](ROADMAP.md)
- [CONTRIBUTING.md](CONTRIBUTING.md)
- [DEPLOYMENT.md](DEPLOYMENT.md)

Release-ZIP, Docker Compose und `systemd`-Beispiel sind in [DEPLOYMENT.md](DEPLOYMENT.md) beschrieben.

## Installation

Siehe [INSTALL.md](INSTALL.md).
