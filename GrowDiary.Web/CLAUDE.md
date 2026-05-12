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
- Kernbereiche: `Dashboard`, `GrowDetail`, `GrowSetup`, `Hardware`, `Settings`.
- Weitere aktuelle Pages: `Tents`, `TentDetail`, `Knowledge`, `Archive`, `Analysis`, `Addback`, `Harvest`, `MeasurementEdit`.
- UX-2: `DashboardPage` ist die Operations-Tageszentrale mit Status Summary, offenen RiskEvents, faelligen Maintenance-/CalibrationEvents, aktiven SOPs, offenen Tasks und aktiven Grows.
- UX-3: `GrowDetailPage` ist intern in Ueberblick, Messungen, Diagnose, SOPs, Journal/Fotos/Tasks und Automatisierung gegliedert; vorhandene Grow-Aktionen bleiben dort erreichbar.
- UX-4: `KnowledgePage` zeigt die vorhandenen Knowledge-Catalogs lesend als Browser mit Kategorien fuer Treatments, SOPs, Symptoms, Wear, Programs, Setpoints und Pathogens; keine Knowledge-Bearbeitung.
- UX-5: `SettingsPage` bleibt Konfigurationsseite; `HardwarePage` ist in Inventar, Wartung, Kalibrierung und Risiken gegliedert.
- UX-6: `/action` ist ein Mobile Action Hub mit Statuskopf, priorisierten Action-Cards, schnellen Grow-/Hardware-Links und isolierten Ladefehlern; keine neuen Backend-Endpunkte.
- UX-7: `/live` ist ein Growraum-Live-Dashboard mit Alarmband, grossen Tent-Live-Karten, kompakten aktiven Grows und einfachem 60-Sekunden-Refresh; keine neuen Backend-Endpunkte.
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
| `HomeAssistantSnapshotWorker` | Background-Service: 5-Minuten-Polling, Tagesaggregation, Snapshot- und Cleanup-Job; wertet LightStatus nicht-numerisch fuer Transition-Events aus |
| `LightStatusTransitionService` | Normalisiert HA-LightStatus und erzeugt Light-On/Light-Off-Transition-Events als Trigger-Grundlage |
| `AutoMeasurementExecutionService` | Erzeugt faellige HomeAssistant-Measurements aus LightTransitionEvents und TentSensorReadings |
| `AutoMeasurementValueGuard` | Prueft automatisch erzeugte Measurement-Werte gegen harte Plausibilitaetsgrenzen vor dem Speichern |
| `AutoMeasurementStatusService` | Liefert Diagnose-/Statusdaten fuer AutoMeasurement-Configs, Runs und relevante LightTransitions |
| `AutoMeasurementWorker` | Background-Service: periodische Ausfuehrung der AutoMeasurement-Configs |
| `GrowDashboardComposer` | Baut Metriken, Charts und Deviations fuer Dashboard- und Detail-Views |
| `RecommendationEngine` | UI-Card-Fassade; kann strukturierte Deviations und TreatmentRecommendations in RecommendationCards uebersetzen |
| `GrowAlertService` | UI-/Live-Fassade; nutzt strukturierte Hydro-Deviations bevorzugt und ergaenzt Legacy-Hinweise fuer noch nicht migrierte Live-Cards |
| `DeviationAnalyzerService` | Zentrale Hydro-Deviation-Engine mit strukturierten Abweichungen, Quellen und Consecutive-Counts |
| `TreatmentRecommender` | Verknuepft strukturierte Deviations mit Knowledge-Symptoms, Treatments und SOPs als reine Empfehlungen |
| `RiskEventSopRecommender` | Verknuepft RiskEvents mit vorhandenen Knowledge-Emergency-SOPs als manuell startbare Empfehlungen |
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
- `AutoMeasurementConfigs`, `AutoMeasurementFieldMappings` und `AutoMeasurementRuns`: Konfiguration, Mapping, Ausfuehrungsstatus, Hard-Limit-Hinweise und Idempotenz fuer automatische Measurements.
- `LightSchedules`: additive Lichtplaene pro Tent mit HH:mm-On/Off-Zeiten, Source und optionaler TimeZoneId.
- `LightTransitionEvents`: LightOn-/LightOff-Events pro Tent als Trigger- und Idempotenzgrundlage; keine automatische Measurement-Erzeugung.
- `HardwareItems`: Hardware-Inventar fuer echte Komponenten, primaer optional an `TentId` verortet; `SetupId`, `GrowId`, `TentSensorId` und `HaEntityId` sind optionale Bezuege. `WearTemplateId` verweist auf Knowledge-Wear-Templates, ist aber kein DB-FK. F1 umfasst nur Inventar/API/UI, noch keine Maintenance-, Calibration- oder Risk-Events.
- `MaintenanceEvents`: Wartungsplanung und Wartungshistorie pro HardwareItem. `GrowTaskId` ist nur optionale Reminder-Projektion fuer geplante Events mit DueAtUtc und HardwareItem.GrowId, nicht die fachliche Wahrheit. F2 umfasst noch keine Calibration- oder Risk-Events und keine automatische Reminder-/Recurring-Synchronisation.
- `CalibrationEvents`: Kalibrierungshistorie und -planung fuer Sensor-Hardware. `GrowTaskId` ist nur optionale Reminder-Projektion. F3 nutzt Default-NextDue-Regeln: pH 14 Tage, EC/ORP/DO 30 Tage; diese koennen spaeter durch strukturierte WearTemplate-Felder ersetzt werden. Noch kein SensorTrustScore.
- `RiskEvents`: Persistente Risiko-/Ausfallereignisse mit optionalen Bezuegen auf HardwareItem, Tent, Grow, TentSensor, SopInstance und GrowTask. `DedupeKey` verhindert doppelte offene/bestaetigte Events und aktualisiert LastSeenAtUtc; Resolved/Ignored blockieren neue Events nicht. F5 ergaenzt SOP-Empfehlungen und manuellen SOP-Start ueber RiskEvents; noch keine automatische HA-Erkennung und keine automatische SOP-Ausfuehrung.
- `SopInstances` und `SopStepInstances`: aus Knowledge-SOPs gestartete Workflow-Koepfe und materialisierte Steps; Step-Status kann aktualisiert werden, SubSOPs werden nur referenziert. E4: Steps haben DueAtUtc/AvailableAtUtc und optionalen ReminderTaskId-Verweis; SopInstances haben NextStepDueAtUtc, IsRecurring, RecurrenceIntervalDays, DueAtUtc. RecurrenceIntervalDays kommt bevorzugt aus triggers[type=Schedule].intervalDays (Fallback: Root-Level SopDefinition.IntervalDays).

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
- Sprint C2 ABGESCHLOSSEN: LightSchedule-API, LightTransitionEvent-Grundlage und LightStatus-Normalisierung fuer spaetere AutoMeasurement-Trigger ohne Job-Ausfuehrung.
- Sprint C3 ABGESCHLOSSEN: AutoMeasurementWorker erzeugt Measurements aus LightTransitionEvents und TentSensorReadings mit Run-Status/Idempotenz.
- Sprint C4 ABGESCHLOSSEN: AutoMeasurementValueGuard blockiert harte Ausreisser und dokumentiert Warnungen/Rejections in AutoMeasurementRuns.
- Sprint C5 ABGESCHLOSSEN: AutoMeasurement-Status-Endpoint und GrowDetail-Diagnose zeigen Config-, Run- und LightTransition-Status.
- Sprint D1 ABGESCHLOSSEN: DeviationAnalyzerService v2 liefert strukturierte Hydro-Deviations ueber `GET /api/grows/{growId}/deviations`.
- Sprint D2 ABGESCHLOSSEN: TreatmentRecommender liefert Knowledge-basierte Empfehlungen ueber `GET /api/grows/{growId}/treatment-recommendations`.
- Sprint D3 ABGESCHLOSSEN: RecommendationEngine und GrowAlertService nutzen D1/D2-Diagnosen als bevorzugte Fassadenbasis.
- Sprint D5 ABGESCHLOSSEN: GrowAlertService kombiniert D1/D2-Diagnosen mit Legacy-Evaluate-Hinweisen ohne dominante Healthy-Card bei echten Warnungen.
- Sprint E1 ABGESCHLOSSEN: SOP-Instanzen koennen aus Knowledge-SOPs gestartet und mit materialisierten Steps per API gelesen werden.
- Sprint E2 ABGESCHLOSSEN: SOP-Empfehlungen koennen aus der GrowDetail-Diagnose gestartet werden; SopInstances speichern Recommendation-Bezug, ohne Step-Ausfuehrung.
- Sprint E3 ABGESCHLOSSEN: SOP-Steps koennen gestartet, abgeschlossen oder uebersprungen werden; SopInstances werden automatisch Completed, wenn alle Steps Done/Skipped sind.
- Sprint E4 ABGESCHLOSSEN: SOP-Scheduling (DueAtUtc, NextStepDueAtUtc, IsRecurring, RecurrenceIntervalDays); GrowTask-Reminder fuer Steps mit DueAtUtc; Recurring wird markiert, kein automatischer Neustart.
- Sprint F1 ABGESCHLOSSEN: HardwareItem-Grundmodell mit additiver Tabelle, `/api/hardware-items`, WearTemplate-Default-Uebernahme beim Create und minimaler Inventar-UI. UX-1 verschiebt Hardware/Maintenance/Calibration/Risk aus Settings auf die eigene HardwarePage. Noch keine Maintenance-/Calibration-/Risk-Events und keine GrowTask-Projektion in F1.
- Sprint F2 ABGESCHLOSSEN: MaintenanceEvent-Grundmodell mit additiver Tabelle, `/api/maintenance-events`, optionaler GrowTask-Reminder-Projektion bei Planned+DueAtUtc+HardwareItem.GrowId und NextDueAtUtc-Ableitung aus InspectionIntervalDays. Keine Calibration-/Risk-Events, keine Background-Engine und keine GrowTask-Status-Synchronisation.
- Sprint F3 ABGESCHLOSSEN: CalibrationEvent-Grundmodell mit additiver Tabelle, `/api/calibration-events`, optionaler GrowTask-Reminder-Projektion und Default-NextDue-Regeln fuer pH/EC/ORP/DO. Kein SensorTrustScore, keine RiskEvents und keine GrowTask-Status-Synchronisation.
- Sprint F4 ABGESCHLOSSEN: RiskEvent-Grundmodell mit additiver Tabelle, `/api/risk-events`, DedupeKey-Grundlage, Acknowledge/Resolve-Aktionen und minimaler UI auf der HardwarePage. Keine automatische HA-Erkennung, keine BackgroundWorker und kein automatischer SOP-Start.
- Sprint F5 ABGESCHLOSSEN: RiskEvents koennen ueber `GET /api/risk-events/{id}/sop-recommendations` passende vorhandene Emergency-SOPs vorschlagen und ueber `POST /api/risk-events/{id}/start-sop` manuell eine SOP starten; RiskEvent.SopInstanceId wird gesetzt. Keine automatische HA-Erkennung und keine automatische SOP-Ausfuehrung.
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
