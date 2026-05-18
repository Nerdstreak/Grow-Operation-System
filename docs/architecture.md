# Architektur

## Ueberblick

Grow Operation System besteht aus einem ASP.NET Core 8 Backend, einem React/Vite/TypeScript Frontend und einer lokalen SQLite-Datenbank. Das Backend liefert JSON-APIs unter `/api/*` und hostet die gebaute React-SPA aus `GrowDiary.Web/wwwroot`.

Die App ist API-first und lokal-first. Runtime-Daten liegen standardmaessig unter `GrowDiary.Web/App_Data`.

## Backend

- Projekt: `GrowDiary.Web`
- Framework: ASP.NET Core 8
- Datenbank: SQLite unter `GrowDiary.Web/App_Data/grow-diary.db`
- Datenzugriff: ADO.NET mit `Microsoft.Data.Sqlite`
- Kein ORM
- SPA-Hosting ueber `UseStaticFiles()` und `MapFallbackToFile("index.html")`

Wichtige Backend-Bereiche:

- `Api/Controllers/`: REST-Endpunkte fuer Grows, Setups, Measurements, Tasks, Journal, Workflow, Settings und Knowledge.
- `Controllers/`: Legacy-/Spezialendpunkte, Redirects, Export, Kamera und Live-Dashboard.
- `Api/Contracts/`: Request-/Response-DTOs.
- `Api/Mapping/`: handgeschriebene Mapper.
- `Services/`: Business-Logik, Home Assistant, Dashboard-Komposition, Empfehlungen, Validierung, Charts und Fotos.
- `Services/Knowledge/`: Knowledge Loader und Schema-Klassen.
- `Infrastructure/`: ADO.NET-Repositories, Datenbankinitialisierung, Pfade und Konfigurationsimport.
- `Models/`: Domain-Entities und Enums.

## Frontend

- Projekt: `GrowDiary.React`
- Stack: React 19, Vite, TypeScript
- API-Kommunikation ueber zentrale Fetch-Helfer gegen `/api/*`
- Build-Output: `GrowDiary.Web/wwwroot`

Aktuelle Oberflaechen umfassen Dashboard, Grow-Detail, Grow-Setup, Zelte, Addback, Hardware, Knowledge, Archiv, Analyse, Mobile Action Hub `/action`, Live Dashboard `/live` und Settings.

## Repository-Struktur

Der fruehere `GrowRepository` war ein grosses God Object. Er wurde schrittweise zur Facade umgebaut. Controller und Services koennen weiterhin `GrowRepository` verwenden; die Datenzugriffe liegen in Domain-Repositories.

Aktuelle Domain-Repositories:

- `TentRepository`
- `HydroSetupRepository`
- `AddbackRepository`
- `HardwareRepository`
- `SetupRepository`
- `AutoMeasurementRepository`
- `LightRepository`
- `SopRepository`
- `PhotoRepository`
- `HomeAssistantSettingsRepository`
- `GrowCoreRepository`
- `MeasurementRepository`

Weitere bestehende Repositories:

- `TaskRepository`
- `JournalRepository`
- `AuditRepository`
- `SystemAuditRepository`
- `HarvestRepository`
- `TemplateRepository`
- `SensorReadingRepository`

`RepositoryBase` buendelt gemeinsame Infrastruktur wie `AppPaths`, `OpenConnection()` und wiederverwendbare Mapping-/Parsing-Hilfen. Sub-Repositories duerfen nicht von `GrowRepository` abhaengen.

## Datenbank und Initialisierung

`DatabaseInitializer.Initialize()` legt Tabellen und Indizes an und fuehrt additive Schema-Erweiterungen aus. Es gibt kein separates Migration-Framework; Schema-Evolution passiert kontrolliert im Initializer.

Wichtige Tabellenbereiche:

- `Tents` und `TentSensors`
- `GrowSystems` als HydroSetup-Basis
- `Setups`, `Strains`, `PlantInstances`
- `Grows`
- `Measurements`
- `TentSensorReadings` und `TentSensorDailyStats`
- `AutoMeasurementConfigs`, `AutoMeasurementFieldMappings`, `AutoMeasurementRuns`
- `LightSchedules` und `LightTransitionEvents`
- `AddbackLogs` und `ChangeoutEntries`
- `HardwareItems`, `MaintenanceEvents`, `CalibrationEvents`, `RiskEvents`
- `SopInstances` und `SopStepInstances`

## Knowledge Base

Default-Knowledge wird unter `GrowDiary.Web/wwwroot/knowledge-defaults/` ausgeliefert und beim ersten Start nach `GrowDiary.Web/App_Data/knowledge/` kopiert. Kategorien sind Treatments, SOPs, Nutrient Programs, Setpoints, Pathogens, Symptoms und Wear.

Quellen-Dokumente koennen unter `GrowDiary.Web/wwwroot/docs/` bereitgestellt werden. Runtime-Anpassungen unter `App_Data` sind lokale Daten und gehoeren nicht ins Repository.
