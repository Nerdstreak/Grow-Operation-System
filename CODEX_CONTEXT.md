# Codex Context

Stand: 2026-04-19

## Arbeitsweise

- Sprache: Deutsch.
- Projektordner: `D:\Grow Operation System new`.
- Ziel: Diese Datei dient als schneller Einstieg fuer spaetere Codex-Sessions.
- Wichtige Projekt- und Arbeitskontexte dauerhaft in Repo-Dateien wie `CODEX_CONTEXT.md`, Specs oder Arbeitsnotizen festhalten, damit Kontextverlust durch Token- oder Nutzungslimits abgefedert wird.
- Wenn etwas nicht klappt, nicht beim ersten Fehler stoppen: weiter analysieren, fixen und erneut pruefen, solange es im aktuellen Kontext sinnvoll moeglich ist.
- Vor Ergebnis-Praesentation nach Codeaenderungen muessen Build und Tests erfolgreich sein. Danach `dotnet run` starten, damit der Nutzer die App pruefen kann, und die lokale URL nennen.
- Bei Codeaenderungen zuerst bestehende Struktur und Projektdokumentation lesen, besonders `PRODUCT_SPEC.md`, `UI_REVIEW_2026-04-19.md` und relevante Dateien im jeweiligen App-Ordner.
- Bestehende Nutzer- oder Tool-Aenderungen nicht zuruecksetzen, ausser der Nutzer fordert es ausdruecklich.

## Projektkontext

- Repository enthaelt eine GrowDiary/Grow Operation System Anwendung mit Loesung `GrowDiary.slnx`.
- `GrowDiary.Web` ist eine ASP.NET Core 8 MVC Anwendung zum Tracken von Cannabis-Grows mit optionaler Home-Assistant-Sensorintegration.
- Datenbank: SQLite unter `App_Data/grow-diary.db`, WAL-Modus.
- Keine ORM-Nutzung; Datenzugriff erfolgt ueber rohe ADO.NET-Repositories in `Infrastructure/`.
- Hauptabhaengigkeit laut Claude-Kontext: `Microsoft.Data.Sqlite`.
- Wichtige Ordner:
  - `GrowDiary.Web`
  - `GrowDiary.Web.Tests`
  - `Grow Operation System`
  - `Grow Operation System neue UI`
  - `docs`
  - `design_handoff_raw`

## Befehle

Im Repo-Root:

```bash
$env:DOTNET_CLI_HOME='D:\Grow Operation System new\.dotnet-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='0'
dotnet build GrowDiary.slnx -m:1 -v:minimal
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj --no-restore -v:minimal
dotnet run --project GrowDiary.Web/GrowDiary.Web.csproj --no-build
```

Im App-Ordner `GrowDiary.Web` oder mit Projektpfad aus dem Root:

```bash
dotnet restore
dotnet build
dotnet run
GROWDIARY_DB_PATH="/path/to/grow.db" dotnet run
dotnet publish -c Release
```

Hinweis aus `CLAUDE.md`: Dort steht, dass keine Tests oder Linting-Tools konfiguriert sind. Im Root gibt es aber `GrowDiary.Web.Tests`; die Tests sind nutzbar und zielen auf `net8.0`, passend zur Web-App.

## Tooling und Skills

- `dotnet-skills` wurde global installiert, Version `0.0.70`.
- `dotnet skills recommend` erkannte das Projekt und schlug u. a. `aspnet-core`, `xunit`, `coverlet`, `coverage-analysis`, `microsoft-extensions` vor.
- Per `dotnet skills install dotnet-aspnet-core` wurde der Skill `aspnet-core` in `.claude/skills` installiert.
- Per `dotnet skills install blazor` wurde der Skill `blazor` in `.claude/skills` installiert.
- `dotnet skills install package blazor` ist in dieser CLI-Version falsch; korrekt ist `dotnet skills install blazor`.
- Aus `dotnet/skills` wurden fuer Codex ausserdem nach `C:\Users\mkles\.codex\skills` installiert:
  - `configuring-opentelemetry-dotnet`
  - `minimal-api-file-upload`
  - `optimizing-ef-core-queries`
  - `convert-to-cpm`
  - `dotnet-aot-compat`
  - `migrate-dotnet10-to-dotnet11`
  - `migrate-dotnet8-to-dotnet9`
  - `migrate-dotnet9-to-dotnet10`
  - `migrate-nullable-references`
  - `thread-abort-migration`
- Hinweis: `plugins/dotnet-aspnet-core` existierte im `dotnet/skills` GitHub-Repo nicht; der aktuelle Pluginordner hiess `plugins/dotnet-aspnet`.

## Architektur

- `Controllers`: Requests orchestrieren, ViewModels bauen, Views oder JSON zurueckgeben.
- `ViewModels`: DTOs pro Seite/Aktion, meist durch Composer-Services zusammengesetzt.
- `Services`: Businesslogik; sollen nicht direkt auf die Datenbank zugreifen.
- `Infrastructure/`: rohe ADO.NET-Repositories fuer SQLite; kein ORM.
- `Models/`: Domain-Entities passend zu DB-Tabellen.

## Wichtige Services

- `HomeAssistantService`: HTTP-Client fuer Home Assistant REST API; Entity States, Auth, Graceful Degradation wenn offline.
- `GrowDashboardComposer`: baut das Home-Dashboard-ViewModel aus HA-Livedaten und manuellen Fallback-Messungen.
- `TimelineComposer`: fuehrt Messungen, Journal-Eintraege, Aufgaben und Fotos chronologisch zusammen.
- `RecommendationEngine`: kontextuelle Grow-Empfehlungen nach Medium, Phase und Duengerprogramm.
- `CultivationKnowledgeService`: In-Memory-Wissensbasis fuer Duengerprogramme und Medium-Playbooks.
- `MeasurementSanityService`: phasenabhaengige Plausibilitaetschecks fuer pH, EC, Temperatur und Luftfeuchtigkeit.
- `HomeAssistantSnapshotWorker`: `IHostedService`; pollt HA alle 5 Minuten und speichert einen taeglichen Snapshot pro Sensor.
- `ChartService`: formatiert Zeitreihendaten fuer Frontend-Charts.

## Datenbank

- `Grows`: Medium (`Soil`, `Coco`, `Hydro`), Feeding (`Organic`, `Mineral`, `None`), Hydro-Stil (`DWC`, `RDWC`, `NFT`, ...), Umgebung und Phase (`Seedling` bis `Cure`).
- `Measurements`: Luftwerte sowie Hydro-Werte wie Irrigation/Drain/Reservoir pH, EC, DO, ORP und Reservoir-Level.
- `Tents`: Home-Assistant-Entity-ID-Mappings fuer mehrere Sensortypen plus Lichtzyklus-Konfiguration.
- `TentSensorSnapshots`: speichert die letzten 18 taeglichen Snapshots pro Metrik und Zelt fuer historische Charts.
- `AppSettings`: Key-Value-Store fuer Runtime-Konfiguration wie HA-URL und Token.

## Initialisierung und Migration

- `DatabaseInitializer.Initialize()` laeuft beim Start.
- Verantwortlich fuer Tabellenanlage, additive Migrationen per `EnsureColumn()`, Default-Zelte (`Hauptzelt`, `Anzuchtzelt`), Grow-Templates und heuristische Zuordnung alter Grows zu Zelten.
- Es gibt kein Migration-Framework; Schemaentwicklung erfolgt additiv ueber neue `EnsureColumn()`-Aufrufe.

## Home Assistant

- HA-Konfiguration liegt in `AppSettings` und `Tents`, nicht hardcodiert.
- Die App muss ohne HA-Konfiguration voll nutzbar bleiben und dann auf manuell eingetragene Messungen zurueckfallen.

## Lokalisierung

- UI-Texte, Labels, Empfehlungen und Wissensbasis-Inhalte sind auf Deutsch.

## Letzter Arbeitsstand 2026-04-19

- Build war zuletzt erfolgreich: `dotnet build GrowDiary.slnx -m:1 -v:minimal` mit `0 Warnung(en), 0 Fehler`.
- Tests waren zuletzt erfolgreich: `dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj --no-restore -v:minimal` mit 68 bestandenen Tests.
- Dev-Server wurde zuletzt erfolgreich gestartet und per HTTP geprueft: `http://localhost:5076`, Status 200.
- Damit `dotnet run` im Sandbox-/Windows-Kontext sauber startet, wurde in `Program.cs` Logging auf Console/Debug begrenzt und DataProtection auf lokale Keys unter `GrowDiary.Web/App_Data/DataProtectionKeys` gesetzt.
- `MeasurementForm.razor` wurde repariert: `datetime-local` bindet an `DateTime`, Stage kommt aus der letzten Messung oder `GrowStage.Veg`.
- Neue Blazor-Seite `GrowDiary.Web/Components/Pages/JournalForm.razor` deckt `/grows/{Id:int}/journal/create` ab.
- `GrowDiary.Web.Tests` wurde auf `net8.0` gesetzt und `Microsoft.Data.Sqlite` auf `8.0.6` angeglichen.
- Google-Fonts- und ungenutzter Chart.js-CDN-Link wurden aus der Blazor-App entfernt; alte MVC-Views koennen noch Chart.js nutzen.
- `.gitignore` wurde erweitert fuer `.dotnet-home/`, `.appdata/`, `.localappdata/`, DataProtectionKeys, SQLite WAL/SHM, Logs und verschachtelte Test-`obj`-Artefakte.
- Bereits getrackte generierte Artefakte wurden per `git rm --cached` aus dem Index genommen:
  - `Grow Operation System/GrowDiary.Web.Tests/obj/...`
  - `GrowDiary.Web/App_Data/grow-diary.db-wal`
  - `GrowDiary.Web/App_Data/grow-diary.db-shm`
- Diese erscheinen im Git-Status als `D`, sind aber absichtlich aus dem Tracking entfernt; lokale Dateien sollen nicht geloescht werden.

## Offene Risiken / Naechste Review-Ziele

- Nutzer sagt, es seien noch Fehler in der App. Nach Neustart zuerst UI-Smoke-Test mit laufendem Server machen.
- Besonders pruefen:
  - kaputte Routen/Buttons in Blazor-UI (`/`, `/grows`, `/grows/{id}`, `/grows/{id}/messung`, `/grows/{id}/journal/create`, `/zelte`, `/einstellungen`)
  - Encoding-Probleme wie `NÃ¤hrstoffe`, `Ladeâ€¦`, `COâ‚‚`
  - Home-Assistant-Kamera liefert im Log fuer `camera.kamera_hauptzelt` HTTP 500; App sollte dadurch nicht crashen.
  - Produkt-Spec sagt HA sei Pflicht, alter Claude-Kontext sagt optional/fallbackfaehig; diese Produktentscheidung klaeren.
  - Verschachtelte Projektkopie `Grow Operation System/...` nicht versehentlich bearbeiten, solange Root-Projekt aktiv ist.

## Hinweise fuer kuenftige Arbeit

- Erst lokal suchen (`rg`, `rg --files`) und vorhandene Patterns uebernehmen.
- Tests ausfuehren, wenn Aenderungen am Verhalten oder an der UI vorgenommen werden.
- Abschluss nach Codeaenderungen erst nach erfolgreichem Build, erfolgreichen Tests und gestartetem `dotnet run`; lokale URL nennen.
