# BACKEND-22 Kontrollierter Grow-Import

## Ziel

Aus dem bisherigen Import-Dry-Run wird ein kontrollierter echter Import fuer Grow-Exports.
Der Import legt bewusst einen neuen lokalen Vergleichs-Grow an und ueberschreibt keine bestehenden Grows, Zelte oder HydroSetups.

## Geaendert

- Neuer Endpoint: `POST /api/exports/grows/import`
- Neuer Contract: `GrowImportResultDto`
- Import-Plan meldet fuer valide Exports jetzt `ImportSupported = true`
- Echter Import erstellt vor jedem Import ein Safety-Backup unter `App_Data/backups`
- Import erzeugt neue lokale Grow-Id
- Zelt- und HydroSetup-Daten werden als Snapshots am importierten Grow gespeichert
- Measurements werden fuer den neuen Grow importiert
- Journal-Eintraege werden importiert; MeasurementId wird auf neue lokale Measurement-Ids gemappt
- Tasks werden als Historie importiert und nicht als aktive Reminder geoeffnet
- AddbackLogs und Changeouts werden importiert, aber ohne lokale HydroSetupId, weil kein Live-HydroSetup angelegt wird
- Harvest wird importiert, falls vorhanden
- Hardware und Foto-Metadaten werden bewusst nicht als aktive lokale Daten angelegt
- Bei Importfehler wird der neu erzeugte Grow best-effort geloescht; durch Cascades werden angelegte Kinddaten entfernt
- AuditEvents fuer Import-Plan, blockierten Import, Safety-Backup-Fehler, erfolgreichen Import und Importfehler
- Backend-Readiness/Health/API-Manifest auf Import-Execute erweitert
- GrowRepository bewahrt vorgegebene SnapshotJson-Werte beim CreateGrow, statt sie bei Import-Grows ohne Tent/System zu loeschen

## Geaenderte Dateien

- `GrowDiary.Web/Api/Contracts/GrowExportContracts.cs`
- `GrowDiary.Web/Api/Controllers/GrowExportsApiController.cs`
- `GrowDiary.Web/Api/Controllers/SystemApiController.cs`
- `GrowDiary.Web/Infrastructure/GrowRepository.cs`
- `GrowDiary.Web.Tests/Api/GrowExportsApiControllerTests.cs`
- `GrowDiary.Web.Tests/Api/SystemApiControllerTests.cs`

## Verifikation

In dieser Umgebung ist kein .NET SDK installiert, daher konnte ich `dotnet build/test` nicht ausfuehren.
Der Patch wurde mit `git apply --check` gegen den hochgeladenen Stand geprueft.

Lokal ausfuehren:

```powershell
cd "D:\Grow Operation System new"

git apply --check BACKEND22_CONTROLLED_IMPORT.patch
git apply BACKEND22_CONTROLLED_IMPORT.patch

dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal
```

## Commit-Vorschlag

```powershell
git add GrowDiary.Web/Api/Contracts/GrowExportContracts.cs `
        GrowDiary.Web/Api/Controllers/GrowExportsApiController.cs `
        GrowDiary.Web/Api/Controllers/SystemApiController.cs `
        GrowDiary.Web/Infrastructure/GrowRepository.cs `
        GrowDiary.Web.Tests/Api/GrowExportsApiControllerTests.cs `
        GrowDiary.Web.Tests/Api/SystemApiControllerTests.cs

git commit -m "Add controlled grow import execution"
git push origin main
```
