# BACKEND-8 Schema Status & Backup Validation

## Kurzfazit

Backend-8 ergänzt eine risikoarme Release-/Upgrade-Grundlage, ohne Restore, UI, PWA oder Deployment anzufassen.

## Neue Funktionen

### Datenbankstatus

Neuer Endpoint:

- `GET /api/system/database-status`

Liefert:

- erwartete Backend-Schema-Version
- gespeicherte Schema-Version aus `AppSettings`
- ob die Datenbank existiert
- ob zentrale Tabellen vorhanden sind
- ob zentrale Pflichtspalten vorhanden sind
- Warnungen bei Versionsabweichung oder fehlender Datenbank

Die Schema-Version wird bei der Initialisierung in `AppSettings` geschrieben:

- `backend:schemaVersion`
- `backend:lastMigrationUtc`

### Backup-Validierung

Neuer Endpoint:

- `GET /api/system/backup/{fileName}/validate`

Prüft:

- sichere Backup-Dateinamen
- Backup existiert
- Hauptdatenbank enthalten
- WAL enthalten, falls vorhanden
- keine HA-Config / Tokens / Secrets
- keine DataProtectionKeys
- keine Uploads
- Entry-Anzahl und Warnungen

### Release Readiness aktualisiert

`GET /api/system/release-readiness` meldet nun `backend.v0.6-ready-not-v1.0` und listet zusätzlich:

- `database-status`
- `backup-validation`

als erledigte Foundations.

### Admin Guardrails

`/api/system/database-status` ist jetzt wie Backup/Release-Readiness lokal/admin-geschützt.

## Nicht geändert

- Kein Frontend
- Kein Restore
- Kein destruktives DB-Schreiben außer Schema-Version-Metadaten
- Keine Import-/Merge-Logik
- Keine Auth-Schicht
- Keine Deployment-/PWA-Änderungen

## Tests

Ergänzt für:

- Datenbankstatus mit aktueller Schema-Version
- Backup-Validierung für erzeugte Backups
- unsichere Backup-Dateinamen
- AdminAccessPolicy für database-status und Backup-Validate
