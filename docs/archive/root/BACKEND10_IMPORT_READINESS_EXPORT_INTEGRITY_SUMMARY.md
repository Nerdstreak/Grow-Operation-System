# BACKEND-10 Import-Readiness & Export-Integrity

## Ziel

Der Grow-Export ist jetzt besser als Austauschformat vorbereitet, ohne bereits einen riskanten Import/Merge in die Datenbank einzubauen.

## Änderungen

- Grow-Export v1 enthält jetzt zusätzlich:
  - `ExportId`
  - `IntegrityHash`
  - `SectionCounts`
- Neuer Endpoint:
  - `POST /api/exports/grows/validate`
- Die Validierung prüft:
  - unterstützte SchemaVersion
  - vorhandene ExportId
  - SectionCounts gegen tatsächliche Abschnittsgrößen
  - IntegrityHash gegen den kanonischen Exportinhalt
  - potenzielle Secrets im Export
- Der Validate-Endpoint importiert keine Daten und verändert die Datenbank nicht.
- Backend-Health, Release-Readiness und API-Manifest wurden auf `backend-core.v0.8-candidate` aktualisiert.
- API-Manifest dokumentiert jetzt Export-Integrity und Export-Validation.

## Bewusst nicht umgesetzt

- Kein echter Import/Merge in die Datenbank.
- Kein Restore-Flow.
- Keine UI.
- Keine DB-Migration.
- Keine Auth-Änderung.

## Erwartete Tests

- Export enthält IntegrityHash und SectionCounts.
- Frischer Export validiert erfolgreich.
- Manipulierte SectionCounts / Hash werden abgelehnt.
- Potenzielle Secrets werden erkannt.
- System-Manifest und Release-Readiness enthalten die neue v0.8-Fähigkeit.
