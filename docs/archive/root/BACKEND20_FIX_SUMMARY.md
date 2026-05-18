# BACKEND-20-FIX Grow Snapshots Wiring

## Kurzfazit

Dieser Fix verdrahtet BACKEND-20 vollständig. Die Snapshot-Modelle lagen bereits im Projekt, wurden aber noch nicht durchgängig von GrowRun, Datenbank, Repository und Export verwendet.

## Geänderte Dateien

- `GrowDiary.Web/Models/GrowRun.cs`
- `GrowDiary.Web/Models/GrowSnapshots.cs`
- `GrowDiary.Web/Infrastructure/DatabaseInitializer.cs`
- `GrowDiary.Web/Infrastructure/GrowRepository.cs`
- `GrowDiary.Web/Api/Controllers/GrowExportsApiController.cs`
- `GrowDiary.Web/Api/Controllers/SystemApiController.cs`
- `GrowDiary.Web.Tests/Infrastructure/GrowRepositoryTests.cs`
- `GrowDiary.Web.Tests/Api/GrowExportsApiControllerTests.cs`
- `GrowDiary.Web.Tests/Api/SystemApiControllerTests.cs`

## Inhalt

### 1. GrowRun-Snapshotfelder

`GrowRun` enthält jetzt:

- `TentSnapshotJson`
- `HydroSetupSnapshotJson`
- `SnapshotsCapturedAtUtc`

### 2. Datenbank / Migration

`DatabaseInitializer` wurde auf `backend-core.v0.18-candidate` angehoben und enthält die Migration:

- `0019-grow-run-snapshots`

Die `Grows`-Tabelle bekommt additiv:

- `TentSnapshotJson TEXT NULL`
- `HydroSetupSnapshotJson TEXT NULL`
- `SnapshotsCapturedAtUtc TEXT NULL`

### 3. Repository-Wiring

`CreateGrow(...)` erzeugt beim Grow-Start feste Snapshots von:

- Zelt inkl. Sensor-Mapping
- HydroSetup inkl. Volumen, Layout, Tankposition und Technikdaten

`UpdateGrow(...)` verändert diese Snapshots bewusst nicht. Dadurch bleibt ein Grow historisch vergleichbar.

### 4. Export-Wiring

`GrowExportsApiController` nutzt bevorzugt gespeicherte Snapshots.

Fallback nur bei Legacy-Grows:

- wenn kein Snapshot vorhanden ist, werden aktuelle Zelt-/HydroSetup-Daten genutzt
- der Export schreibt dann eine Warnung

### 5. Tests

Neue/angepasste Tests prüfen:

- Grow-Erstellung speichert Zelt- und HydroSetup-Snapshots
- gespeichertes HydroSetup-Volumen bleibt stabil, wenn das Live-HydroSetup später geändert wird
- Export nutzt gespeicherte Snapshots statt aktuelle Live-Daten
- System-Schema und Pflichtspalten enthalten Snapshot-Spalten

## Lokal anwenden

Variante A: Patch anwenden

```powershell
cd "D:\Grow Operation System new"
git apply --reject BACKEND20_FIX_GROW_SNAPSHOTS_WIRING.patch
```

Variante B: Dateien aus `source/` in dein Repo kopieren.

## Danach testen

```powershell
cd "D:\Grow Operation System new"

dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal
```

## Wenn grün

```powershell
git add GrowDiary.Web/Models/GrowRun.cs `
        GrowDiary.Web/Models/GrowSnapshots.cs `
        GrowDiary.Web/Infrastructure/DatabaseInitializer.cs `
        GrowDiary.Web/Infrastructure/GrowRepository.cs `
        GrowDiary.Web/Api/Controllers/GrowExportsApiController.cs `
        GrowDiary.Web/Api/Controllers/SystemApiController.cs `
        GrowDiary.Web.Tests/Infrastructure/GrowRepositoryTests.cs `
        GrowDiary.Web.Tests/Api/GrowExportsApiControllerTests.cs `
        GrowDiary.Web.Tests/Api/SystemApiControllerTests.cs

git commit -m "Wire grow snapshots into repository and exports"
git push origin main
```

## Hinweis

Ich konnte in dieser Umgebung kein `dotnet build/test` ausführen, weil das .NET SDK hier nicht installiert ist. Der Patch wurde deshalb statisch auf Klammern/Dateikonsistenz geprüft, muss aber lokal gebaut werden.
