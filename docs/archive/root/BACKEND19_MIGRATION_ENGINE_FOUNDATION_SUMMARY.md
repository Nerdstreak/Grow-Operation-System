# BACKEND-19 Migration Engine Foundation

## Kurzfazit

Dieses Paket ergänzt ein sicheres Migration-Engine-Fundament, ohne destructive Migrationen automatisch auszuführen. Ziel ist mehr Upgrade-Sicherheit, ohne bestehende Daten durch aggressive Schema-Umbauten zu riskieren.

## Änderungen

- Backend-Schema auf `backend-core.v0.17-candidate` angehoben.
- Neue Migration-Metadaten `0018-migration-engine-foundation`.
- `AppliedSchemaMigrations` wurde additiv erweitert um Status, Start/Completion-Zeitpunkte, Backup-/Destructive-Flags, Checksum und EngineVersion.
- Neuer Endpoint `GET /api/system/migration-plan`.
- Migration-Plan ist ein Dry-Run und führt keine Migration aus.
- Migration-Plan zeigt Pending/Applied, Backup-Pflicht, destructive Guardrails und ExecutionMode.
- Legacy-Tent-Schema wird nicht mehr gelöscht. Alte Tabellen werden bei Erkennung als Legacy-Backup-Tabellen umbenannt und Grow-Daten werden nicht gelöscht.
- AdminAccessPolicy schützt `/api/system/migration-plan`.
- Backend-Health, Release-Readiness und API-Manifest wurden aktualisiert.

## Bewusst nicht enthalten

- Kein echter destructive Migration Runner.
- Kein Rollback.
- Kein echter Restore.
- Kein Import/Merge.
- Keine UI-Änderungen.

## Tests

Ergänzt/angepasst für MigrationPlan, ReleaseReadiness, BackendHealth, ApiManifest, MigrationStatus und AdminAccessPolicy.
