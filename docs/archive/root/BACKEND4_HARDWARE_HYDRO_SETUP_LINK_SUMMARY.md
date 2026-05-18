# BACKEND-4 Hardware bekommt HydroSetupId

## Ziel

Hydro-relevante Hardware soll backendseitig nicht nur an Zelt, Grow oder altes Setup gekoppelt werden können, sondern direkt an ein HydroSetup/GrowSystem.

## Geänderte Dateien

- GrowDiary.Web/Models/HardwareItem.cs
- GrowDiary.Web/Api/Contracts/HardwareItemContracts.cs
- GrowDiary.Web/Api/Mapping/HardwareItemMapping.cs
- GrowDiary.Web/Api/Controllers/HardwareItemsApiController.cs
- GrowDiary.Web/Infrastructure/DatabaseInitializer.cs
- GrowDiary.Web/Infrastructure/GrowRepository.cs
- GrowDiary.Web.Tests/Infrastructure/HardwareItemRepositoryTests.cs
- GrowDiary.Web.Tests/Api/HardwareItemsApiControllerTests.cs

## Backend-Änderungen

- `HardwareItem.HydroSetupId` ergänzt.
- `HardwareItemDto`, `CreateHardwareItemRequest` und `UpdateHardwareItemRequest` um `HydroSetupId` ergänzt.
- Mapping Create/Update/DTO erweitert.
- SQLite-Tabelle `HardwareItems` bekommt additiv `HydroSetupId INTEGER NULL`.
- Index `IX_HardwareItems_HydroSetupId` ergänzt.
- Repository speichert, lädt und aktualisiert `HydroSetupId`.
- Repository-Methode `GetHardwareItemsByHydroSetup(int hydroSetupId)` ergänzt.
- API-Endpoint `GET /api/hardware-items?hydroSetupId=...` ergänzt.
- API/Repository validieren:
  - HydroSetupId muss existieren.
  - Wenn `TentId` und `HydroSetupId` gesetzt sind, muss das HydroSetup zum Zelt gehören.

## Tests

- HardwareItem Repository-Test prüft `HydroSetupId`, Index und Filter.
- HardwareItems API-Test prüft Create/Update/List mit `HydroSetupId`.
- API-Test prüft ungültige `HydroSetupId`.

## Nicht geändert

- Kein Frontend.
- Kein Addback-Umbau.
- Kein Grow-Umbau.
- Keine PWA/CI/Deployment-Änderungen.
