# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run (development, auto-opens browser at http://localhost:5076)
dotnet run

# Run with a custom database path
GROWDIARY_DB_PATH="/path/to/grow.db" dotnet run

# Publish for production
dotnet publish -c Release
```

There are no tests or linting tools configured.

## Architecture

**GrowDiary.Web** is an ASP.NET Core 8 MVC application for tracking cannabis grows with optional Home Assistant sensor integration. It uses SQLite (`App_Data/grow-diary.db`, WAL mode) with a single NuGet dependency: `Microsoft.Data.Sqlite`.

### Layer structure

- **Controllers** — orchestrate requests, build view models, return views or JSON
- **ViewModels** — per-page/action DTOs assembled by service composers
- **Services** — all business logic; never touch the DB directly
- **Infrastructure/** — raw ADO.NET repositories over SQLite; no ORM
- **Models/** — domain entities matching DB tables

### Key services

| Service | Role |
|---|---|
| `HomeAssistantService` | HTTP client for HA REST API; fetches entity states, handles auth, degrades gracefully if offline |
| `GrowDashboardComposer` | Assembles the home dashboard view model from HA live data + fallback measurements |
| `TimelineComposer` | Merges measurements, journal entries, tasks, and photos into a chronological timeline |
| `RecommendationEngine` | Produces contextual grow advice based on medium (soil/coco/hydro), stage, and nutrient program |
| `CultivationKnowledgeService` | In-memory knowledge base of nutrient programs (Athena, HESI, GHF, etc.) and medium playbooks |
| `MeasurementSanityService` | Stage-aware sanity checks on pH, EC, temperature, humidity; returns severity-rated alerts |
| `HomeAssistantSnapshotWorker` | Background `IHostedService`; polls HA every 5 min and stores one daily snapshot per sensor |
| `ChartService` | Formats time-series data for front-end charting |

### Database schema highlights

- **Grows**: tracks medium (Soil/Coco/Hydro), feeding (Organic/Mineral/None), hydro style (DWC/RDWC/NFT/…), environment, and stage (Seedling → Cure)
- **Measurements**: covers both air metrics (temp/humidity) and hydro metrics (irrigation/drain/reservoir pH, EC, DO, ORP, reservoir level)
- **Tents**: each tent stores HA entity ID mappings for 9+ sensor types plus light cycle config
- **TentSensorSnapshots**: keeps the latest 18 daily snapshots per metric per tent for historical charts
- **AppSettings**: key-value store for all runtime configuration (HA URL, token, etc.)

### Database initialization

`DatabaseInitializer.Initialize()` runs on startup and handles everything: table creation, `EnsureColumn()` calls for additive migrations, seeding default tents ("Hauptzelt", "Anzuchtzelt") and grow templates, and heuristic auto-assignment of legacy grows to tents. There is no migration framework — schema evolution is done by adding `EnsureColumn()` calls.

### Home Assistant integration

All HA config (URL, token, per-tent entity IDs) lives in the `AppSettings` and `Tents` tables — nothing is hardcoded. The app is fully functional without HA configured; it falls back to manually entered measurements everywhere.

### Localization

All UI text, labels, recommendations, and knowledge base content are in **German**.
