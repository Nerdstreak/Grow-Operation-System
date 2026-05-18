# BACKEND-21 Restore mit Safety

## Ziel

BACKEND-21 macht aus dem bisherigen Restore-Plan einen echten, kontrollierten Restore-Flow.

## Geänderte Dateien

- `GrowDiary.Web/Api/Contracts/GrowExportContracts.cs`
- `GrowDiary.Web/Api/Controllers/SystemApiController.cs`
- `GrowDiary.Web.Tests/Api/SystemApiControllerTests.cs`

## Umsetzung

- Neuer Endpoint: `POST /api/system/backup/{fileName}/restore`
- Neuer DTO: `BackupRestoreResultDto`
- Backup-Manifest meldet `RestoreSupported = true`
- Restore-Plan meldet `RestoreSupported = true`, wenn Backup valide und schema-kompatibel ist.
- Restore-Plan bleibt Dry-Run und schreibt keine Dateien.
- Echter Restore:
  - validiert sicheren Backup-Dateinamen
  - erzeugt zuerst automatisch ein Safety-Backup
  - erstellt Restore-Plan und blockiert bei Preflight-Blockern
  - extrahiert Backup in ein temporäres Verzeichnis
  - prüft SQLite mit `PRAGMA quick_check`
  - stellt Datenbank, WAL/SHM und Knowledge-Runtime-Kopie kontrolliert wieder her
  - hält Rollback-Dateien während des Swaps vor
  - schreibt SystemAuditEvents für Erfolg/Blocker/Fehler
- Backup-Dateinamen sind jetzt eindeutiger durch Ticks im Zeitstempel, damit ein Safety-Backup nicht ein gerade ausgewähltes Backup überschreibt.
- Release Readiness steigt auf `backend.v0.19-ready-not-v1.0`, DB-Schema bleibt `backend-core.v0.18-candidate`.

## Tests

Ergänzt/angepasst in `SystemApiControllerTests`:

- `RestoreBackup_RestoresDatabaseAndCreatesSafetyBackup`
- `RestoreBackup_BlocksSchemaMismatchAndDoesNotChangeDatabase`
- `RestoreBackup_RejectsUnsafeFileNames`
- RestorePlan erwartet jetzt `RestoreSupported = true` und `RequiresManualStop = false`.
- BackupManifest erwartet `RestoreSupported = true`.
- ReleaseReadiness erwartet `restore_api = pass`.

## Anwendung

```powershell
cd "D:\Grow Operation System new"

git apply --check BACKEND21_RESTORE_WITH_SAFETY.patch
git apply BACKEND21_RESTORE_WITH_SAFETY.patch

dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal
```

## Commit

```powershell
git add GrowDiary.Web/Api/Contracts/GrowExportContracts.cs `
        GrowDiary.Web/Api/Controllers/SystemApiController.cs `
        GrowDiary.Web.Tests/Api/SystemApiControllerTests.cs

git commit -m "Add safe backup restore execution"
git push origin main
```

## Hinweis

Ich konnte in der Sandbox kein `dotnet build/test` ausführen, weil kein .NET SDK installiert ist. Der Patch wurde aber mit `git apply --check` gegen den hochgeladenen Stand geprüft.
