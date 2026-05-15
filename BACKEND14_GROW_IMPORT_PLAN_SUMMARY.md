# BACKEND-14 Grow Import Plan

## Kurzfazit

BACKEND-14 ergänzt eine sichere Import-Vorstufe für Grow-Exports. Der neue Endpoint analysiert einen validierten Grow-Export und erstellt einen Import-Plan, ohne Daten in die lokale Datenbank zu schreiben.

## Neue API

- `POST /api/exports/grows/import-plan`

Der Endpoint ist über den bestehenden `/api/exports`-Admin-Guard lokal/admin-geschützt.

## Import-Plan Verhalten

Der Plan prüft und beschreibt:

- ob der Export valide ist
- ob SchemaVersion, SectionCounts und IntegrityHash passen
- ob potenzielle Secrets enthalten sind
- ob der Export anonymisiert ist
- welche Daten später importiert würden
- welche Daten nur als Snapshot dienen würden
- mögliche Konflikte, z. B. gleicher Grow-Name mit gleichem Startdatum
- dass aktuell keine Datenbankänderung erfolgt

## Wichtig

- Kein echter Import
- Kein Merge
- Keine DB-Änderung
- Keine produktiven Zelte/HydroSetups aus Export-Snapshots
- Nur sicherer Dry-Run als Vorbereitung für späteren kontrollierten Import

## Backend-Status

- `backend-core.v0.12-candidate`
- `backend.v0.12-ready-not-v1.0`
- neue Capability: `grow-import-plan`
- neue Schema-Migration-Metadaten: `0013-grow-import-plan`

## Geänderte Dateien

- `GrowDiary.Web/Api/Contracts/GrowExportContracts.cs`
- `GrowDiary.Web/Api/Controllers/GrowExportsApiController.cs`
- `GrowDiary.Web/Api/Controllers/SystemApiController.cs`
- `GrowDiary.Web/Infrastructure/DatabaseInitializer.cs`
- `GrowDiary.Web.Tests/Api/GrowExportsApiControllerTests.cs`
- `GrowDiary.Web.Tests/Api/SystemApiControllerTests.cs`
- `GrowDiary.Web.Tests/Infrastructure/AdminAccessPolicyTests.cs`

## Nicht geändert

- Kein Frontend
- Kein echter Import
- Kein Restore
- Keine PWA-/CI-/Deployment-Änderung
