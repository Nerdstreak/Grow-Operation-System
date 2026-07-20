# Grow Operation System

Grow Operation System ist eine kostenlose, selfhosted Grow-Management-App mit Fokus auf RDWC/DWC, Home Assistant Sensordaten, Grow-Dokumentation, SOPs, Hardware, Wartung und Risiko-Tracking.

Die App ist lokal-first: keine Cloud-Pflicht, kein SaaS-Modell und keine native App-Store-Abhaengigkeit. Mobile Nutzung erfolgt als PWA.

<p align="center">
  <img src="docs/images/live-dashboard-desktop.png" alt="Grow OS Live-Dashboard" width="100%">
</p>

## Installation (fuer Nutzer)

Keine Programmierkenntnisse noetig. Ausfuehrliche Anleitung: [docs/install.md](docs/install.md).

### Empfohlen: als Home Assistant Add-on

Grow OS bezieht seine Sensordaten aus Home Assistant - deshalb ist das Add-on der
einfachste Weg: ein Klick, keine URL, kein Token. Voraussetzung ist eine
**Home Assistant OS**- (oder Supervised-)Installation.

1. In Home Assistant: **Einstellungen -> Add-ons -> Add-on-Store**
2. Oben rechts **... -> Repositories**, dieses Repository hinzufuegen:

   ```
   https://github.com/Nerdstreak/Grow-Operation-System
   ```

3. **Grow OS** installieren und starten - es erscheint in der HA-Seitenleiste und ist
   automatisch mit Home Assistant verbunden. Sensoren waehlst du per Dropdown aus.

### Alternativ: eigenstaendig (Pi / Windows)

Fuer den Betrieb neben einer bestehenden Home-Assistant-Instanz (Verbindung wird dann
einmalig ueber URL + Token eingerichtet).

**Raspberry Pi / Linux:**

```bash
curl -fsSL https://raw.githubusercontent.com/Nerdstreak/Grow-Operation-System/main/scripts/install.sh | bash
```

**Windows-PC (PowerShell):**

```powershell
irm https://raw.githubusercontent.com/Nerdstreak/Grow-Operation-System/main/scripts/install.ps1 | iex
```

Danach im Browser oeffnen: `http://<geraete-ip>:5076` (am Geraet selbst `http://localhost:5076`). Grow OS startet ab dann automatisch mit.

## Schnellstart (fuer Entwickler)

Voraussetzungen:

- .NET SDK 8
- Node.js und npm
- Git
- optional, aber empfohlen: Home Assistant

Backend starten:

```powershell
dotnet run --project GrowDiary.Web/GrowDiary.Web.csproj
```

Frontend im Entwicklungsmodus starten:

```powershell
cd GrowDiary.React
npm install
npm run dev
```

Frontend fuer Production bauen:

```powershell
cd GrowDiary.React
npm run build
```

Backend-Tests:

```powershell
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal
```

## Wichtige URLs

- Backend / Production App: `http://localhost:5076`
- LAN-Zugriff: `http://<server-ip>:5076`
- Frontend Dev Server: `http://127.0.0.1:5173`
- Mobile Action Hub: `/action`
- Live Dashboard: `/live`
- Einstellungen: `/settings`

## Dokumentation

Der zentrale Einstieg liegt unter [docs/README.md](docs/README.md).

Wichtige Seiten:

- [Setup](docs/setup.md)
- [Architektur](docs/architecture.md)
- [Entwicklung](docs/development.md)
- [Deployment](docs/deployment.md)
- [Grow-Domaene](docs/grow-domain-notes.md)

## Lizenz

Veroeffentlicht unter der [MIT-Lizenz](LICENSE) - frei nutzbar, anpassbar und weiterverteilbar.
