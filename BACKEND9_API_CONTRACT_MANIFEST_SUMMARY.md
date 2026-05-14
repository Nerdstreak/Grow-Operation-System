# BACKEND-9 API Contract Manifest

## Ziel

Das Backend stellt jetzt ein maschinenlesbares API-Manifest bereit, damit Frontend, Tests und spätere Dokumentation nicht mehr raten müssen, welche Kernendpunkte und Produktregeln gelten.

## Neu

- `GET /api/system/api-manifest`
- `ApiManifestDto`, `ApiAreaDto`, `ApiEndpointDto`
- Manifest-Schema: `grow-os.api-manifest.v1`
- Backend-Schema: `backend-core.v0.7-candidate`

## Enthaltene Bereiche

- Zelte
- HydroSetups
- Grows
- Addback/Changeout/Messungen
- Hardware/Wartung/Kalibrierung/Risiken
- Export/Backup/System

## Guardrails

- `/api/system/api-manifest` ist lokal/admin-geschützt.
- Release Readiness und Backend Health listen `api-contract-manifest` als Foundation.
- Keine UI-, PWA-, CI- oder Deployment-Änderungen.
