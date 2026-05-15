# BACKEND-12 Migration Status & Upgrade Preflight

## Ziel

BACKEND-12 ergänzt das Backend um eine sichere Vorstufe für spätere Updates und Migrationen. Es ist noch kein Restore- oder Rollback-System, verhindert aber, dass Updates ohne Schema-Status und validierbares Backup vorbereitet werden.

## Neu

- `AppliedSchemaMigrations` als internes Schema-Migrationsprotokoll.
- `DatabaseInitializer.RequiredMigrations` mit versionierten Backend-Migrationsmarkern.
- `GET /api/system/migration-status`.
- `POST /api/system/upgrade-preflight`.
- `GET /api/system/database-status` prüft jetzt auch `AppliedSchemaMigrations`.
- `backend-health`, `release-readiness` und `api-manifest` wurden auf `backend-core.v0.10-candidate` erweitert.
- AdminAccessPolicy schützt `migration-status` und `upgrade-preflight`.

## Upgrade Preflight

`POST /api/system/upgrade-preflight` prüft:

- Datenbank existiert.
- Datenbankstatus ist aktuell.
- Schema-Migrationen sind vollständig angewendet.
- Backup kann erstellt werden.
- Backup-Validierung ist erfolgreich.

Der Endpoint verändert keine fachlichen Grow-Daten. Er erstellt nur ein lokales Backup und validiert es.

## Grenzen

Noch nicht enthalten:

- Restore-Flow.
- Rollback-Automation.
- Import/Merge in die produktive DB.
- App-eigene Auth-Session-Schicht.

## Tests

Ergänzt/angepasst:

- MigrationStatus liefert angewendete Migrationen.
- UpgradePreflight erstellt und validiert Backup.
- ApiManifest enthält Migration-/Preflight-Endpunkte.
- AdminAccessPolicy schützt neue System-Endpunkte.
