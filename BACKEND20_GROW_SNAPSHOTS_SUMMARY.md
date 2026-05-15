# BACKEND-20 Grow Snapshots

## Kurzfazit

BACKEND-20 ergänzt ein Snapshot-Fundament für vergleichbare Grow-Exporte.

Neue Grows speichern beim Anlegen feste JSON-Snapshots von:

- Zelt / Tent
- HydroSetup / DWC-RDWC-System

Damit werden alte Runs später nicht rückwirkend verfälscht, wenn ein Zelt umbenannt, Sensoren geändert oder ein RDWC-System von z. B. 4 Sites auf 6 Sites umgebaut wird.

## Enthalten

- neue GrowRun-Felder:
  - `TentSnapshotJson`
  - `HydroSetupSnapshotJson`
  - `SnapshotsCapturedAtUtc`
- neues Snapshot-Modell:
  - `GrowTentSnapshot`
  - `GrowTentSensorSnapshot`
  - `GrowHydroSetupSnapshot`
- automatische Snapshot-Erstellung in `CreateGrow(...)`
- Schema-Bump auf `backend-core.v0.18-candidate`
- neue Migration-Metadaten `0019-grow-run-snapshots`
- additive SQLite-Spalten für bestehende Datenbanken

## Bewusst nicht enthalten

- kein Hardware-Snapshot
- kein Import
- kein Restore
- keine UI
- keine destructive Migration
- keine Änderung an UpdateGrow, damit Snapshots nach Grow-Start stabil bleiben

## Wichtig

Der Patch enthält am Ende einen TODO-Hinweis für den System/API-Controller, weil der konkrete Controller-Pfad über die GitHub-Suche nicht eindeutig gefunden wurde. Dort müssen ReleaseReadiness/API-Manifest/BackendHealth noch auf `v0.18` angehoben werden.

## Anwendung

Im Repo-Root:

```bash
git apply --reject BACKEND20_GROW_SNAPSHOTS.patch
dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal
```

Wenn `git apply` eine `.rej` erzeugt, zuerst die Reject-Datei öffnen und die dortigen Stellen manuell nachziehen.

## Commit-Vorschlag

```bash
git add GrowDiary.Web/Models/GrowRun.cs \
        GrowDiary.Web/Models/GrowSnapshots.cs \
        GrowDiary.Web/Infrastructure/DatabaseInitializer.cs \
        GrowDiary.Web/Infrastructure/GrowRepository.cs

git commit -m "Add grow snapshots for comparison-safe exports"
git push origin main
```
