# BACKEND-16 API Error Format

## Ziel

API-Fehler sollen backendweit einheitlicher werden, damit Frontend, PWA und spätere externe Clients nicht zwischen leeren 404s, Strings, ProblemDetails und verschiedenen Error-Objekten unterscheiden müssen.

## Änderungen

- `ApiError` erweitert um:
  - `Status`
  - `TraceId`
  - `SchemaVersion = grow-os.api-error.v1`
- `ApiErrorFactory` ergänzt für:
  - Validation
  - BadRequest
  - NotFound
  - Conflict
  - Forbidden
  - ServerError
- `ApiControllerBase` bietet jetzt einheitliche Helper:
  - `ValidationError`
  - `BadRequestError`
  - `NotFoundError`
  - `ConflictError`
  - `ForbiddenError`
- Direkte `new ApiError(...)`-Antworten in kritischen API-Controllern wurden auf Helper umgestellt.
- `KnowledgeApiController` liefert bei fehlenden Einträgen jetzt ebenfalls `ApiError` statt leerem 404.
- `ApiErrorController` ergänzt `/api/error` für unerwartete Fehler im ExceptionHandler.
- Admin-Access-Middleware nutzt ebenfalls `ApiErrorFactory.Forbidden`.
- Neuer Endpoint:
  - `GET /api/system/error-contract`
- API-Manifest enthält den Error-Contract-Endpoint.
- Backend-Status angehoben:
  - `backend-core.v0.14-candidate`
  - `backend.v0.14-ready-not-v1.0`
- Migration-Metadaten ergänzt:
  - `0015-api-error-format`

## Tests

- `ApiErrorContractTests` ergänzt.
- `SystemApiControllerTests` um Error-Contract, Status, Manifest und Migration erweitert.
- `AdminAccessPolicyTests` schützt `/api/system/error-contract`.

## Nicht geändert

- Kein Frontend.
- Keine UI.
- Kein Restore/Import-Execute.
- Keine Auth-/Login-Schicht.
- Keine DB-Strukturänderung außer Schema-Metadaten.
