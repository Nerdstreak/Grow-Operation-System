# ADR 0002: GrowRepository als Facade nach Repository-Refactor

## Status

Akzeptiert

## Kontext

`GrowRepository` war ein grosses God Object mit vielen unabhaengigen Domaenen, darunter Tents, HydroSetups, Addback, Hardware, Setups, AutoMeasurements, Light, SOPs, Photos, HomeAssistantSettings, Grows und Measurements.

Controller und Services haengen historisch an `GrowRepository`. Ein harter Schnitt haette viele API- und Service-Aenderungen erzwungen.

## Entscheidung

`GrowRepository` bleibt vorerst als Facade bestehen. Die eigentlichen Datenzugriffe werden schrittweise in Domain-Repositories ausgelagert.

Die Domain-Repositories duerfen nicht von `GrowRepository` abhaengen. Gemeinsame Infrastruktur liegt in `RepositoryBase`.

## Aktuelle Domain-Repositories

- `TentRepository`
- `HydroSetupRepository`
- `AddbackRepository`
- `HardwareRepository`
- `SetupRepository`
- `AutoMeasurementRepository`
- `LightRepository`
- `SopRepository`
- `PhotoRepository`
- `HomeAssistantSettingsRepository`
- `GrowCoreRepository`
- `MeasurementRepository`

## Konsequenzen

- Controller und Services bleiben kompatibel.
- Der Refactor kann inkrementell erfolgen.
- SQL und Mapper liegen naeher an der jeweiligen Domaene.
- Zirkulaere Repository-Abhaengigkeiten werden vermieden.
- Die Facade darf erst entfernt werden, wenn Aufrufer bewusst migriert werden.

## Leitplanken

- Keine Transactions ueber Repository-Grenzen aufbrechen.
- Keine neuen Interfaces nur fuer den Refactor einfuehren.
- Build gruen halten ist wichtiger als maximale Kuerzung.
- Bereits extrahierte Repositories nicht ohne Grund erneut umbauen.
