# BACKEND-17 Legacy/MVC Endpoint Containment

## Ziel

Alte MVC-Endpunkte duerfen die neuen Sicherheits-, Backup- und Exportregeln nicht mehr umgehen.

## Umgesetzt

- `/settings/backup` liefert keinen rohen SQLite-Download mehr, sondern `410 Gone` mit `ApiError`.
- `/grows/{id}/export` leitet auf den versionierten API-Export `/api/exports/grows/{id}` um.
- Alte mutierende MVC-POST-Routen wurden deaktiviert:
  - `/grows/{id}/confirm-germination`
  - `/grows/{id}/confirm-rooting`
  - `/grows/{id}/flip-to-flower`
- Legacy-Kamera-/Snapshot-Routen sind jetzt local/admin-geschuetzt:
  - `/tents/{id}/camera.jpg`
  - `/tents/{id}/camera-stream`
  - `/tents/{id}/latest-snapshot`
- Backend-Status wurde auf `backend-core.v0.15-candidate` angehoben.
- Release Readiness, Backend Health und API Manifest kennen die neue Capability `legacy-mvc-endpoint-containment`.

## Bewusst nicht geaendert

- Keine Frontend-Aenderungen.
- Keine DB-Struktur-Aenderung ausser Migrationsmetadaten.
- Kein echter Restore/Import.
- Keine Remote-Protection fuer alle Produkt-APIs; das bleibt BACKEND-18.

## Tests

Ergaenzt/angepasst:

- `AdminAccessPolicyTests` fuer Kamera-/Snapshot-Routen.
- `LegacyMvcEndpointContainmentTests` fuer Legacy-Backup und Legacy-Export.
- System-Status-Tests fuer v0.15/Capability.

Bitte lokal ausfuehren:

```powershell
dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal
```
