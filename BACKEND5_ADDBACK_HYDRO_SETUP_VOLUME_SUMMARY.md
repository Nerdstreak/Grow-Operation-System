# BACKEND-5 Addback nutzt HydroSetup-Volumen

## Ziel

Addback soll nicht mehr primär aus dem Freitextfeld `GrowRun.ReservoirSize` arbeiten, wenn ein Grow mit einem HydroSetup/GrowSystem verknüpft ist.

## Umsetzung

- `GET /api/grows/{id}/addback` nutzt jetzt zuerst das Gesamtvolumen des verknüpften HydroSetups:
  - `(PotCount * PotSizeLiters) + ReservoirLiters`
- `POST /api/grows/{id}/addback/calculate` kann `ReservoirLiters` leer lassen.
  - Wenn leer: Backend nimmt HydroSetup-Gesamtvolumen.
  - Wenn kein HydroSetup-Volumen vorhanden ist: Fallback auf Legacy `GrowRun.ReservoirSize`.
  - Wenn beides fehlt: Validierungsfehler.
- `EcIst`, `EcZiel` und `EcStock` bleiben fachlich erforderlich.
- Bestehende Legacy-Grows ohne SystemId bleiben kompatibel.

## Tests

Ergänzt:

- Defaults nutzen HydroSetup-Gesamtvolumen vor Legacy-ReservoirSize.
- Defaults fallen bei Legacy-Grows auf `GrowRun.ReservoirSize` zurück.
- Addback-Berechnung nutzt HydroSetup-Gesamtvolumen, wenn Request-Reservoir leer ist.
- Fehlendes Volumen ohne HydroSetup/Legacy gibt Validierungsfehler.

## Nicht geändert

- Kein Frontend.
- Keine DB-Migration.
- Kein AddbackCalculator-Algorithmuswechsel.
- Kein Hardware-/Grow-/HydroSetup-Umbau.
