# CLAUDE.md

## Commands

```bash
dotnet build GrowDiary.Web/GrowDiary.Web.csproj
dotnet run --project GrowDiary.Web/GrowDiary.Web.csproj
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj

cd GrowDiary.React
npm install
npm run build
```

## Architecture Overview

### Backend (GrowDiary.Web)

- ASP.NET Core 8, SQLite unter `App_Data/grow-diary.db`, WAL aktiviert.
- Kein ORM: Datenzugriff laeuft ueber ADO.NET und `Microsoft.Data.Sqlite`.
- API-first: React spricht mit JSON-Endpunkten unter `/api/*`.
- Die React-App wird statisch ueber `UseStaticFiles()` ausgeliefert; `MapFallbackToFile("index.html")` bedient SPA-Routen.
- `Program.cs` registriert Controller, Repositories, Business-Services, `KnowledgeBaseLoader` und den `HomeAssistantSnapshotWorker`.

### Frontend (GrowDiary.React)

- React 19, Vite und TypeScript.
- Kommunikation laeuft ueber die zentrale `apiFetch`-Fetch-Abstraktion gegen `/api/*`.
- Kernbereiche: `Dashboard`, `GrowDetail`, `GrowSetup`, `Settings`.
- Weitere aktuelle Pages: `Tents`, `TentDetail`, `Knowledge`, `Archive`, `Analysis`, `Addback`, `Harvest`, `MeasurementEdit`.
- Build-Output wird nach `GrowDiary.Web/wwwroot` geschrieben und vom Backend als SPA gehostet.

### Layer-Struktur Backend

- `Api/Controllers/` -> REST-Endpoints fuer Grows, Setups, Measurements, Tasks, Journal, Workflow, Settings und Knowledge.
- `Controllers/` -> Redirect-Shims, Export, Kamera- und `/api/live/*`-Endpoints fuer das React-Dashboard.
- `Api/Contracts/` -> Request-/Response-DTOs.
- `Api/Mapping/` -> Handgeschriebene Mapper, kein AutoMapper.
- `Services/` -> Business-Logik, HA-Integration, Dashboard-Komposition, Empfehlungen, Validierung, Charts, Fotos.
- `Services/Knowledge/` -> `KnowledgeBaseLoader` plus Schema-Klassen fuer die JSON-Catalogs.
- `Infrastructure/` -> ADO.NET-Repositories, DB-Initialisierung, Pfade, HA-Config-Import.
- `Models/` -> Domain-Entities und Enums.

### Knowledge-Base (Sprint A, abgeschlossen)

`App_Data/knowledge/` enthaelt aktive, user-editable JSON-Dateien in 7 Kategorien:

- `treatments/` (30) -> konkrete Massnahmen mit Quellen-Verlinkung.
- `sops/` (10) -> Standard Operating Procedures (`Linear`, `MultiDay`, `Recurring`).
- `nutrient-programs/` (3) -> Athena, Canna Aqua, Hydroponic Research VBX.
- `setpoints/` (1) -> RDWC-Standard-Sollwerte.
- `pathogens/` (8) -> Pythium, Fusarium etc.
- `symptoms/` (20) -> Symptom-Catalog mit Treatment-Mapping.
- `wear/` (12) -> Verschleissteil-Templates.

Defaults werden mit der App unter `wwwroot/knowledge-defaults/` ausgeliefert und beim ersten Start nach `App_Data/knowledge/` kopiert. Quell-Dokumente liegen unter `wwwroot/docs/` und sind als `/docs/{name}.pdf` abrufbar.

### Schluessel-Services

| Service | Rolle |
|---|---|
| `KnowledgeBaseLoader` | Laedt alle 7 JSON-Catalogs beim App-Start in Memory |
| `HomeAssistantService` | HTTP-Client fuer HA REST-API, Sensor-States und Kamera-Snapshots |
| `HomeAssistantSnapshotWorker` | Background-Service: 5-Minuten-Polling, Tagesaggregation, Snapshot- und Cleanup-Job |
| `GrowDashboardComposer` | Baut Metriken, Charts und Deviations fuer Dashboard- und Detail-Views |
| `RecommendationEngine` | Aktuelle Empfehlungs-Engine, wird in Sprint D fachlich aufgesplittet |
| `GrowAlertService` | UI-Fassade, die Empfehlungen in Ampel-Zustaende uebersetzt |
| `DeviationAnalyzerService` | Sollwert-vs-Istwert-Vergleich; fachliche Drift wird in Sprint D geklaert |
| `MeasurementSanityService` | Plausibilitaetschecks und blockierende Messwert-Validierung |
| `CultivationKnowledgeService` | Fassade ueber KnowledgeBaseLoader fuer Programme und Playbooks |
| `TargetValueService` | Fassade ueber KnowledgeBaseLoader fuer Sollwerte |

### Datenbank-Schema-Highlights

- `Tents`: Multi-Tent-faehig mit `TentType` (`Production`, `Mother`, `Quarantine`, `Propagation`, `MultiPurpose`).
- `Setups`: Additives Grundmodell mit `SetupType` (`Production`, `Mother`, `Quarantine`), `SetupStatus` und optionalen Mother-/Quarantine-Basisfeldern.
- `Strains` und `PlantInstances`: Sorten und einzelne Pflanzen/Clones mit optionaler `ParentPlantId`-Lineage; Mother-Plants koennen per API Clone erzeugen, Quarantine-Plants koennen entschieden werden.
- `TentSensors`: flexible Sensor-Liste pro Tent statt hartkodierter Sensor-Felder.
- `Grows`: aktuelles All-in-one Grow-Modell; `SetupId` kann optional ein Production-Setup referenzieren. Mother/Quarantine sind keine GrowRun-Setups.
- `Measurements`: pH, EC, ORP, DO, Reservoir-Werte, Air-Werte, PPFD und CO2.
- `TentSensorReadings`: hochfrequente HA-Messwerte aus dem 5-Minuten-Polling.
- `TentSensorDailyStats`: Tagesaggregation mit Median, P5, P95, Min, Max und Avg.
- `AutoMeasurementConfigs`, `AutoMeasurementFieldMappings` und `AutoMeasurementRuns`: Konfigurations-, Mapping- und Idempotenzgrundlage fuer spaetere automatische Measurements; C1 erzeugt noch keine Messungen.

### DB-Initialisierung

`DatabaseInitializer.Initialize()` laeuft beim Start:

- `DropLegacyTentSchemaIfNeeded()` erkennt alte Tent-Spalten und baut das Tent-Schema neu auf.
- `EnsureSchema()` legt Tabellen und Indizes an und nutzt additive `EnsureColumn()`-Upgrades.
- `SeedDefaults()` erzeugt beim Erststart das Default-Tent `Hauptzelt` und Standard-Templates.
- Es gibt kein Migration-Framework; Schema-Evolution passiert kontrolliert im Initializer.

### Lokalisierung

UI-Texte, Empfehlungen und Knowledge-Inhalte sind primaer deutsch.

## Sprint-Status

- Sprint A ABGESCHLOSSEN: Knowledge-Base extrahiert (84 JSONs).
- Sprint B1a ABGESCHLOSSEN: Tent-Modell mit `TentSensor`.
- Sprint B1b ABGESCHLOSSEN: HA-Service, Snapshot-Worker und React-Settings fuer Sensor-Mapping.
- Sprint B1c ABGESCHLOSSEN: App-Start-Fix und Knowledge-API.
- Sprint B2a-1 ABGESCHLOSSEN: Setup-Grundmodell additiv mit `Setups` und `Grows.SetupId`.
- Sprint B2a-2 ABGESCHLOSSEN: Minimale JSON-API fuer Setups (`/api/setups`).
- Sprint B2a-3 ABGESCHLOSSEN: Grow-API transportiert und validiert optionale Production-Setup-Zuordnung.
- Sprint B2c-1 ABGESCHLOSSEN: Mother-/Quarantine-Basisdaten fuer Setups und Detaildarstellung.
- Sprint B2d-1 ABGESCHLOSSEN: Strain- und PlantInstance-Grundfunktion mit einfacher ParentPlantId-Lineage.
- Sprint B2d-2 ABGESCHLOSSEN: Clone-from-Mother Workflow mit optionalem Quarantine-Ziel und MotherSetup-Counter.
- Sprint B2d-3 ABGESCHLOSSEN: Quarantine-Plant Decision Workflow fuer Cleared/Rejected mit optionaler Production-Uebernahme.
- Sprint C1 ABGESCHLOSSEN: AutoMeasurement-Konfigurationen, FieldMappings und Run-Idempotenzgrundlage ohne Job-Ausfuehrung.
- Sprint B2 PENDING: Setup-Hierarchie fachlich weiter ausbauen.

## Sprint-Workflow

Die Architektur wird in geplanten Sprints umgesetzt. Tickets kommen mit klarem Scope und expliziten Files. Vor jedem Sprint wird ggf. ein Inventur-Bericht erstellt.

REGELN fuer Coding-Agents (Codex, Claude Code):

1. Du arbeitest NUR was im aktuellen Ticket steht. Andere Files nicht anfassen, auch wenn du dort Verbesserungs-Potenzial siehst.
2. KEIN selbstaendiges Refactoring ausserhalb des Ticket-Scopes. Wenn du Drift siehst: dokumentiere am Ende, repariere nicht.
3. KEINE Architektur-Entscheidungen treffen. Bei Unklarheit: stoppe und frage zurueck, statt zu raten.
4. Bei jedem Schema- oder Schluessel-Service-Wechsel: CLAUDE.md im selben Commit aktualisieren.
5. Test-Suite muss am Ende gruen sein. Bei roten Tests: Status dokumentieren, nicht stillschweigend Tests deaktivieren.
6. Build-Status: dotnet build muss durchlaufen.
7. Bei grossen Aenderungen (>20 Files in einem Commit): stoppe und frage zurueck, ob das wirklich gewollt ist.

ZUSAMMENFASSUNGS-PFLICHT:
Nach jedem Ticket: kurze Zusammenfassung mit

- Liste angelegter/geaenderter/geloeschter Files
- Test- und Build-Status
- Etwaige Probleme oder Abweichungen vom Ticket
- Auffaelligkeiten die nicht im Ticket waren

## Architektur-Vision (9-Domaenen-Modell)

Die App wird systematisch zu einem Multi-Setup-Grow-Operations-System ausgebaut. Jeder Sprint muss klar einer oder mehreren Domaenen zugeordnet werden:

1. Wasser & Naehrloesung
2. Sensorik & Messung
3. System & Hardware
4. Klima & Licht
5. Pflanze & Genetik, inklusive Mutter- und Quarantaene-Setups
6. Workflows & SOPs
7. Hygiene & Pathogene
8. Risiko & Wartung
9. Behandlungen & Massnahmen

## Regeln

- `CLAUDE.md` bleibt aktuell; Schema- oder Schluessel-Service-Aenderungen aktualisieren diese Datei im selben Commit.
- Maximal zwei Seiten, keine Code-Beispiele, keine tiefen Implementierungsdetails.
- Bei Konflikt zwischen Code und `CLAUDE.md` gewinnt der Code; `CLAUDE.md` wird direkt nachgezogen.
