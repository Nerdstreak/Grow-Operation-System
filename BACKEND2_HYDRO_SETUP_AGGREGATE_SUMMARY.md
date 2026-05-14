# BACKEND-2 HydroSetup-Aggregat

## Kurzfazit

HydroSetup/GrowSystem ist backendseitig stärker als DWC/RDWC-System abgesichert.
Die Änderung betrifft nur Backend/API/Repository/Tests und den bestehenden xUnit-Warning-Fix.

## Änderungen

- `GET /api/hydro-setups` listet standardmäßig nur aktive Hydro-Setups.
- `GET /api/hydro-setups?includeArchived=true` liefert aktive und archivierte Hydro-Setups.
- `GET /api/hydro-setups?tentId={id}` filtert weiterhin nach Zelt und berücksichtigt ebenfalls `includeArchived`.
- Create/Update validiert zusätzlich:
  - `HydroStyle` muss DWC oder RDWC sein.
  - `LayoutType` muss gültig sein.
  - `ReservoirPosition` muss gültig sein.
  - `Status` bei Update muss gültig sein.
  - `DisplayOrder` darf nicht negativ sein.
  - RDWC darf nicht `SingleBucket` als Layout nutzen.
  - RDWC braucht eine Tankposition ungleich `None`.
- Repository-Validierung wurde parallel zur Controller-Validierung gehärtet, damit Regeln nicht nur in der API gelten.
- Bestehende xUnit2031-Warnung in `SopInstanceRepositoryTests` wurde korrigiert.

## Nicht geändert

- Kein Frontend.
- Keine DB-Migration.
- Kein Grow-Umbau.
- Kein Hardware-HydroSetupId.
- Kein Addback-Umbau.
- Keine App_Data- oder wwwroot-Artefakte.

## Lokale Prüfung

Bitte lokal ausführen:

```powershell
cd "D:\Grow Operation System new"
dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal
```
