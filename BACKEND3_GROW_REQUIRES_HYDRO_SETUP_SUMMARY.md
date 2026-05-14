# BACKEND-3 Grow muss HydroSetup nutzen

## Ziel

Neue Grow-API-Create-Flows muessen ein HydroSetup/SystemId verwenden. Das HydroSetup ist die technische Wahrheit fuer DWC/RDWC, Volumen und Chiller-Status. Legacy-Grows ohne SystemId bleiben updatefaehig.

## Geaenderte Dateien

- GrowDiary.Web/Api/Controllers/GrowsApiController.cs
- GrowDiary.Web.Tests/Api/GrowsApiControllerSetupTests.cs

## Verhalten

- POST /api/grows verlangt jetzt SystemId.
- SystemId muss ein vorhandenes aktives HydroSetup sein.
- HydroSetup muss einem Tent zugeordnet sein.
- Wenn TentId gesetzt ist, muss HydroSetup.TentId dazu passen.
- Request.HydroStyle muss weiterhin DWC/RDWC sein.
- Der gespeicherte Grow uebernimmt HydroStyle, TentId, MediumType, FeedingStyle, IrrigationType, MediumDetail, HasChiller, ContainerSize und ReservoirSize aus dem HydroSetup.
- PUT /api/grows bewahrt eine bestehende SystemId, wenn der Request sie nicht erneut mitsendet.
- Legacy-Grows ohne SystemId koennen weiterhin ohne SystemId aktualisiert werden.

## Tests

Ergaenzt/geaendert wurden Tests fuer:

- Create ohne SystemId wird abgelehnt.
- Create ohne Plant-Setup bleibt gueltig, wenn HydroSetup gewaehlt wurde.
- Create mit DWC/RDWC HydroSetup wird akzeptiert.
- technische Grow-Felder werden aus HydroSetup abgeleitet.
- Non-DWC/RDWC Request-HydroStyle wird abgelehnt.
- HydroSetup aus anderem Tent wird abgelehnt.
- archiviertes HydroSetup wird abgelehnt.
- Update bewahrt bestehende SystemId.
- Legacy-Update ohne SystemId bleibt moeglich.

## Nicht geaendert

- Kein Frontend.
- Keine DB-Migration.
- Keine HardwareItem-HydroSetupId.
- Kein Addback-Umbau.
- Keine PWA/CI/Deployment-Dateien.
