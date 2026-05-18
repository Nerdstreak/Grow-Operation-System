# BACKEND-6 Product Hardening

## Ziel

Backend-seitiges V1.0-Fundament weiter ausbauen, ohne Frontend, PWA, CI oder Deployment zu verändern.

## Umgesetzt

### 1. Addback-Protokoll

Neu:

- `AddbackLogEntry`
- `AddbackLogs` SQLite-Tabelle
- `GET /api/grows/{id}/addback/logs`
- `POST /api/grows/{id}/addback/logs`

Erfasst werden u. a.:

- GrowId
- HydroSetupId
- Art: Addback, TopOff, Correction
- Zeitpunkt
- Reservoirvolumen
- EC vorher / Ziel / Stock / nachher
- pH vorher / nachher
- zugegebene Liter
- neues Reservoirvolumen
- ob HydroSetup-Volumen verwendet wurde
- Notizen

### 2. Changeout-Protokoll

Neu:

- `ChangeoutEntry`
- `ChangeoutEntries` SQLite-Tabelle
- `GET /api/grows/{id}/changeouts`
- `POST /api/grows/{id}/changeouts`

Erfasst werden u. a.:

- GrowId
- HydroSetupId
- Art: Partial oder Full
- Zeitpunkt
- gewechselte Liter
- gewechselter Prozentanteil
- EC/pH vorher und nachher
- Notizen

### 3. Grow-Export v1

Neu:

- `GET /api/exports/grows/{id}`
- optional `?anonymize=true`
- optional `?includePhotoMetadata=false`

Der Export enthält:

- SchemaVersion `grow-os.grow-export.v1`
- Grow-Detaildaten
- Tent-Snapshot
- HydroSetup-Snapshot
- Measurements
- JournalEntries
- Tasks
- HardwareItems
- Harvest
- AddbackLogs
- Changeouts
- Photo-Metadaten optional
- Warnings

Der anonymisierte Export entfernt Namen, freie Notizen, HA-Entity-IDs, Hardware-Identifikatoren und Fotopfade.

### 4. Backend Health

Neu:

- `GET /api/system/backend-health`

Liefert:

- BackendSchema
- TentCount
- HydroSetupCount
- GrowCount
- ZeroTentStartupSupported
- Capabilities

### 5. Lokales Backup ohne Secrets

Neu:

- `POST /api/system/backup`

Erstellt ein ZIP unter `App_Data/backups/` mit:

- SQLite DB
- WAL/SHM falls vorhanden
- Runtime-Knowledge-Kopie falls vorhanden

Bewusst ausgeschlossen:

- `ha-config.json`
- Home-Assistant Token
- DataProtectionKeys
- Uploads
- Logs

### 6. Home Assistant Token-Schutz

`HomeAssistantSettingsDto` gibt AccessToken nicht mehr im Klartext aus, sondern nur noch maskiert als `********`.

Beim Speichern wird `********` als Platzhalter erkannt und der bestehende Token bleibt erhalten.

## Nicht geändert

- Kein Frontend
- Keine PWA
- Keine CI/Deployment-Dateien
- Keine Auth/Login-Schicht
- Keine Import-Merge-Logik
- Keine echte Migration-Engine

## Hinweise

Dieses Paket macht das Backend robuster und produktnäher, ist aber noch nicht automatisch V1.0. Für V1.0 fehlen danach weiterhin mindestens:

- echte Migration-Engine mit Schema-Versionen
- Import-Flow für Grow-Exports
- Restore-Flow für Backups
- Auth/Remote-Admin-Konzept
- vollständige API-Dokumentation/OpenAPI
- Upgrade-Test mit alter DB
