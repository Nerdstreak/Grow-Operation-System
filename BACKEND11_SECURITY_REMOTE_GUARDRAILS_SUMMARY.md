# BACKEND-11 Security & Remote Access Guardrails

## Ziel

Backend-seitige Sicherheitsleitplanken fuer Selfhosting, PWA und spaeteren Remote-Zugriff vorbereiten, ohne ein vollstaendiges Login-/User-System einzufuehren.

## Umgesetzt

- Neuer Endpoint: `GET /api/system/security-status`
- Admin-/System-/Settings-/Export-Endpunkte bleiben standardmaessig local-only.
- Remote-Adminzugriff ist nur moeglich ueber:
  - lokalen Zugriff / Loopback
  - gueltigen Admin-Key via Header `X-GrowOS-Admin-Key`
  - bewusste Override-Variable `GROWDIARY_ALLOW_REMOTE_ADMIN=true`
- Neuer Environment-Key:
  - `GROWDIARY_ADMIN_KEY`
- Export-Endpunkte sind jetzt ebenfalls durch `AdminAccessPolicy` geschuetzt:
  - `/api/exports/grows/{id}`
  - `/api/exports/grows/validate`
- Middleware liefert bei blockiertem Admin-Zugriff JSON im bestehenden `ApiError`-Format.
- Security-Header ergaenzt:
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: SAMEORIGIN`
  - `Referrer-Policy: no-referrer`
- Backend Health, Release Readiness und API Manifest auf `backend-core.v0.9-candidate` aktualisiert.
- API Manifest dokumentiert Security-Status und geschuetzte Export-Endpunkte.

## Tests

Ergaenzt/angepasst:

- SecurityStatus liefert Local-Only-Guardrail-Status ohne Secrets.
- AdminAccessPolicy schuetzt Settings, System, Backup, Security und Export-Routen.
- Remote-Zugriff ohne Key wird abgelehnt.
- Remote-Zugriff mit gueltigem Admin-Key wird erlaubt.
- Remote-Zugriff mit falschem Admin-Key wird abgelehnt.
- Bewusste Remote-Override-Variable erlaubt Zugriff, wird aber als unsicherer Override erkannt.

## Bewusst nicht umgesetzt

- Kein vollstaendiges Login-/User-System.
- Keine Sessions/JWT/OAuth.
- Keine UI-Aenderungen.
- Kein Cloudflare-/Domain-/QR-Code-Frontend.
- Kein Restore-/Import-Merge.

## Bewertung

Backend-Status nach diesem Schritt: `backend.v0.9-ready-not-v1.0`.

Weiterhin offen vor V1.0:

- echtes User-/Session-/Auth-Konzept fuer Remote-Betrieb
- versionierte Migration-Engine
- validierter Restore-Flow
- Import-/Merge-Flow fuer Grow-Exports
- einheitliches Fehlerformat in allen Controllern
- Release-Upgrade-Test mit bestehender `App_Data`
