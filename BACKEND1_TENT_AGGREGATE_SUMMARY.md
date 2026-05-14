# BACKEND-1 Tent-Aggregat

## Zweck
Backend-seitige Zeltverwaltung wurde gegen das bestehende Tent-Modell vervollständigt. Ziel: keine UI-Neuerfindung, sondern die vorhandenen Backend-Felder sauber über API/Contracts nutzbar machen.

## Geänderte Bereiche

- `GrowDiary.Web/Api/Contracts/UpdateTentRequest.cs`
- `GrowDiary.Web/Api/Controllers/SettingsApiController.cs`
- `GrowDiary.Web/Api/Mapping/RequestMapping.cs`
- `GrowDiary.Web.Tests/Api/SettingsApiControllerTests.cs`
- `GrowDiary.Web.Tests/Infrastructure/TentRepositoryTests.cs`

## Inhalt

- `CreateTentRequest` kann jetzt dieselben Zelt-Detailfelder wie `UpdateTentRequest` transportieren.
- `POST /api/settings/tents` kann jetzt vollständige Zelt-Details anlegen:
  - Maße
  - Lichtdaten
  - LightController
  - Abluft/Umluft
  - HVAC
  - CO2
  - Kamera
  - Sensor-Mappings
  - Status
- `GET /api/settings/tents/{id}` ergänzt, um ein einzelnes Zelt mit Details zu laden.
- `GET /api/settings/tents?includeArchived=true` ergänzt, damit archivierte Zelte explizit ladbar sind.
- Create/Update validiert jetzt technische Felder:
  - TentType
  - Status
  - LightController
  - HvacController
  - Maße/Leistungswerte
  - Sensor-Metriken
  - aktive Sensoren brauchen HA Entity ID
- Delete-Verhalten bleibt:
  - ohne Abhängigkeiten: echtes Delete
  - mit Abhängigkeiten: Archive
- Tests wurden um Detail-Persistenz, Einzelabruf, Validierung und Delete/Archive ergänzt.

## Nicht geändert

- Kein Frontend.
- Keine DB-Migration.
- Keine PWA/CI/Deployment-Änderung.
- Keine App_Data-Dateien.
- Kein HydroSetup-/Grow-/Addback-Umbau.

## Lokal bitte prüfen

```powershell
cd "D:\Grow Operation System new"
dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal
```
