# BACKEND-7 Release Readiness & Backup Guardrails

## Ziel

Backend-7 ergänzt keine UI und keine neuen Grow-Features. Der Sprint härtet die bestehende Backend-6-Basis für Release-/Update-Readiness und dokumentiert klar, dass der Backendstand eher v0.5-candidate als v1.0-stable ist.

## Änderungen

- `GET /api/system/release-readiness` ergänzt.
  - Liefert Completed Foundations und Remaining Before V1.
  - Kennzeichnet den Stand als `backend.v0.5-ready-not-v1.0`.
- `POST /api/system/backup` Manifest erweitert.
  - DownloadUrl
  - ExcludesHomeAssistantConfig
  - ExcludesDataProtectionKeys
  - ExcludesUploads
  - RestoreSupported = false
- `GET /api/system/backup/{fileName}` ergänzt.
  - Sichere Dateinamenprüfung.
  - Kein Pfad-Traversal.
  - Nur lokale Backup-ZIPs unter `App_Data/backups`.
- `AdminAccessPolicy` erweitert.
  - `/api/system/backup` und `/api/system/release-readiness` sind lokale/Admin-Pfade.
  - `/api/system/backend-health` bleibt absichtlich als leichter Readiness-/Health-Endpunkt lesbar.
- Tests ergänzt für:
  - ReleaseReadiness DTO
  - BackendHealth Capabilities
  - Backup ohne Secrets/DataProtectionKeys/Uploads/Logs
  - Backup-Download mit sicherem Dateinamen
  - AdminAccessPolicy-Routen

## Nicht geändert

- Kein Frontend
- Kein Restore-Flow
- Keine echte Migration-Engine
- Keine Auth/Login-Schicht
- Kein Import/Merge von Grow-Exports
- Keine App_Data-Dateien im Diff

## Erwarteter Testlauf

```powershell
dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal
```
