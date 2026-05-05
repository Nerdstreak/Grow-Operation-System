# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

## Commands

```bash
# Restore dependencies
dotnet restore

# Build backend
dotnet build GrowDiary.Web/GrowDiary.Web.csproj

# Run backend
dotnet run --project GrowDiary.Web/GrowDiary.Web.csproj

# Run tests
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj
```

For the React frontend:

```bash
cd GrowDiary.React
npm install
npm run build
```

The React build writes its output into `GrowDiary.Web/wwwroot`.

## Architecture

**GrowDiary.Web** is an ASP.NET Core 8 application that exposes JSON APIs and serves the built React SPA from `wwwroot`. The active runtime path is React `-> /api/* -> services/repositories -> SQLite`.

### Layer structure

- **Api/Controllers** - JSON endpoints for the React frontend
- **Api/Contracts** - request and response DTOs
- **Api/Mapping** - translation between DTOs, form models, and domain models
- **Services/** - business logic, dashboard composition, recommendations, background workers
- **Infrastructure/** - raw ADO.NET repositories over SQLite; no ORM
- **Models/** - domain entities matching the persisted data model

### Key services

| Service | Role |
|---|---|
| `HomeAssistantService` | HTTP client for Home Assistant REST API; fetches configured tent sensor states and degrades gracefully if HA is unavailable |
| `GrowDashboardComposer` | Builds the live home and tent dashboard payload from HA data plus repository fallbacks |
| `RecommendationEngine` | Produces contextual grow advice from stage, measurements, and target values |
| `TargetValueService` | Resolves profile- and stage-specific target ranges |
| `DeviationAnalyzerService` | Evaluates measurements against targets and emits findings |
| `CultivationKnowledgeService` | Serves the in-app knowledge base content |
| `HomeAssistantSnapshotWorker` | Background worker that polls configured tent sensors and stores daily snapshots |

### Database schema highlights

- **Grows**: grow setup, timing, status, tent assignment, and profile metadata
- **Measurements**: air, reservoir, irrigation, drain, and lighting-related metrics
- **Tents**: tent identity, hardware metadata, sizing, camera, and device context
- **TentSensors**: per-tent sensor mappings with metric type, entity id, label, and active flag
- **TentSensorSnapshots**: historical live data snapshots for charts
- **AppSettings**: runtime configuration such as Home Assistant URL and token

### Database initialization

`DatabaseInitializer.Initialize()` runs on startup and handles table creation, additive schema upgrades, default content seeding, and knowledge-base bootstrapping. There is no separate migration framework; schema evolution is implemented in code.

### Home Assistant integration

Home Assistant connection settings live in `AppSettings`. Sensor mappings live on each tent via the `TentSensors` table and are edited through the React settings flow. The app remains usable without HA; manual measurements still drive core grow tracking.

### Frontend

`GrowDiary.React` is the source frontend. Vite builds directly into `GrowDiary.Web/wwwroot`, and ASP.NET Core serves the resulting SPA with `MapFallbackToFile("index.html")`.

### Testing

Backend tests are in `GrowDiary.Web.Tests` and cover repositories, schema behavior, services, recommendations, and Home Assistant-related flows. There is no separate frontend test suite configured at the moment; frontend validation is currently based on TypeScript compilation and Vite production builds.

### Localization

The product UI and most domain content are primarily German.
