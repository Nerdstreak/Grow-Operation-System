# BACKEND-15 System Audit Events

## Kurzfazit

Backend-15 ergänzt ein separates System-Audit-Log für kritische Backend-Operationen. Das bestehende growbezogene `AuditEntries` bleibt unverändert. Kritische Systemvorgänge wie Backup, Backup-Validierung, Restore-Plan, Upgrade-Preflight, Grow-Export, Export-Validierung, Import-Plan und Remote-Admin-Zugriffe werden jetzt backendseitig nachvollziehbar protokolliert.

## Geänderte Bereiche

- Neues Modell: `SystemAuditEvent`
- Neues Repository: `SystemAuditRepository`
- Neue DTOs/Mapping: `SystemAuditEventDto`, `SystemAuditMapping`
- Neue Tabelle: `SystemAuditEvents`
- Neuer Endpoint: `GET /api/system/audit-events`
- Middleware protokolliert Remote-Admin-Zugriff erlaubt/blockiert
- System- und Export-Endpunkte schreiben Audit-Events
- Backend-Status angehoben auf `backend-core.v0.13-candidate`
- Release-Readiness angehoben auf `backend.v0.13-ready-not-v1.0`

## Protokollierte Aktionen

- `backup-created`
- `backup-downloaded`
- `backup-validated`
- `restore-plan-created`
- `upgrade-preflight-run`
- `release-readiness-read`
- `security-status-read`
- `api-manifest-read`
- `migration-status-read`
- `audit-events-read`
- `grow-export-created`
- `grow-export-validated`
- `grow-import-plan-created`
- `remote-admin-access-allowed`
- `remote-admin-access-blocked`

## Nicht geändert

- Kein Frontend
- Kein echter Restore
- Kein echter Grow-Import
- Keine Auth-/Login-Schicht
- Keine Änderung am growbezogenen `AuditEntries`-Log

## Testhinweise

Ausführen:

```powershell
cd "D:\Grow Operation System new"
dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal
```
