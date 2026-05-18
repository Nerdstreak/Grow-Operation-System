# BACKEND-8 Testfix

## Fixes

- `ReleaseReadiness`-Test erwartet jetzt korrekt `backend.v0.6-ready-not-v1.0`.
- `DatabaseStatus` prüft jetzt die tatsächlich vorhandene Tabelle `ChangeoutEntries` statt des falschen Namens `Changeouts`.

## Geänderte Dateien

- `GrowDiary.Web/Api/Controllers/SystemApiController.cs`
- `GrowDiary.Web.Tests/Api/SystemApiControllerTests.cs`

## Scope

Kein Frontend, keine App_Data-Dateien, keine Deployment-/CI-Änderungen.
