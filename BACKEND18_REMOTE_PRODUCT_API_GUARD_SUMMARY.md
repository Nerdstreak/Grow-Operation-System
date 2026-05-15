# BACKEND-18 Remote Product API Guard

## Kurzfazit

Produktnahe API-Endpunkte sind jetzt nicht mehr als ungeschuetzte Remote-Oberflaeche erreichbar. Neben den bereits geschuetzten System-/Settings-/Export-Endpunkten werden nun auch Grow-, HydroSetup-, Hardware-, Mess-, Pflanzen-, Risiko-, SOP-, Wartungs-, Kalibrierungs-, Licht- und Knowledge-APIs ueber die bestehende Local/Admin-Guardrail gefuehrt.

## Geaenderte Dateien

- GrowDiary.Web/Infrastructure/AdminAccessPolicy.cs
- GrowDiary.Web/Api/Controllers/SystemApiController.cs
- GrowDiary.Web/Infrastructure/DatabaseInitializer.cs
- GrowDiary.Web.Tests/Infrastructure/AdminAccessPolicyTests.cs
- GrowDiary.Web.Tests/Api/SystemApiControllerTests.cs

## Was wurde geaendert?

- Neue Produkt-API-Prefixe in AdminAccessPolicy:
  - /api/grows
  - /api/hydro-setups
  - /api/hardware-items
  - /api/measurements
  - /api/tasks
  - /api/journal
  - /api/plants
  - /api/strains
  - /api/setups
  - /api/risk-events
  - /api/sop-instances
  - /api/maintenance-events
  - /api/calibration-events
  - /api/auto-measurements
  - /api/light-schedules
  - /api/light-transitions
  - /api/knowledge

- AdminAccessPolicy stellt die Produkt-API-Prefixe separat bereit:
  - ProtectedProductApiRoutePrefixes

- SecurityStatus, BackendHealth, ReleaseReadiness und ApiManifest wurden auf backend-core.v0.16-candidate angehoben.

- ApiManifest markiert produktnahe API-Endpunkte als local/admin-geschuetzt.

- Migration-Metadaten erweitert:
  - 0017-product-api-remote-guard

## Verhalten

- Lokale Requests bleiben erlaubt.
- Remote Requests auf geschuetzte Produkt-APIs brauchen die bestehende AdminAccessPolicy:
  - Admin-Key via X-GrowOS-Admin-Key
  - oder bewusste Remote-Freigabe GROWDIARY_ALLOW_REMOTE_ADMIN=true
- /api/system/backend-health und /api/error bleiben bewusst nicht geschuetzt.

## Nicht geaendert

- Keine UI-/Frontend-Aenderungen.
- Kein Login-/Session-System.
- Kein echter User-Account-Flow.
- Keine PWA-/Cloudflare-/Domain-Konfiguration.
- Keine Datenbankdaten oder App_Data-Dateien.

## Testhinweis

Ausfuehren:

```powershell
cd "D:\Grow Operation System new"
dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal
```
