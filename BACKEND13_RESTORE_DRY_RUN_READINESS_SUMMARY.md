# BACKEND-13 Restore Dry-Run & Restore Readiness

## Ziel

BACKEND-13 ergänzt einen sicheren Restore-Vorbereitungsflow, ohne produktive Dateien zu überschreiben. Der Restore bleibt bewusst ein Dry-Run.

## Neue API

- `POST /api/system/backup/{fileName}/restore-plan`

Der Endpoint analysiert ein vorhandenes Backup und liefert einen Restore-Plan mit:

- Backup-Validität
- enthaltene Datenbank/WAL/SHM/Knowledge-Dateien
- Schema-Version des Backups
- Kompatibilität zur aktuellen Backend-Schema-Version
- betroffene Zielpfade
- ob vorhandene lokale Dateien überschrieben würden
- Blocker und Warnungen

## Verhalten

- Kein echter Restore.
- Keine Dateien werden verändert.
- Unsichere Backup-Dateinamen werden abgelehnt.
- Backup-Pfade bleiben auf `App_Data/backups/grow-os-backup-*.zip` begrenzt.
- Backup-Datenbank wird in ein temporäres Verzeichnis extrahiert und nur lesend geprüft.
- WAL/SHM-Dateien werden für die Schema-Prüfung mit berücksichtigt.

## Release-/Status-Anpassungen

- Backend-Health: `backend-core.v0.11-candidate`
- Release-Readiness: `backend.v0.11-ready-not-v1.0`
- Capability ergänzt: `backup-restore-plan`
- API-Manifest listet den Restore-Plan-Endpoint.
- Schema-Migration ergänzt: `0012-restore-plan`.

## Tests

Ergänzt bzw. angepasst:

- Restore-Plan für gültiges Backup
- unsichere Restore-Plan-Dateinamen
- Schema-Mismatch im Backup
- API-Manifest enthält Restore-Plan
- ReleaseReadiness/BackendHealth/MigrationStatus auf v0.11
- AdminAccessPolicy schützt Restore-Plan-Pfad

## Nicht umgesetzt

- Kein echter Restore.
- Kein Rollback.
- Kein Import/Merge.
- Kein Frontend.
