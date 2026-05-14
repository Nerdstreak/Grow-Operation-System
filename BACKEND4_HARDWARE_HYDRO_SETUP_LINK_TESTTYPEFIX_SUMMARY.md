# BACKEND-4 Test Type Fix

Fix für zwei Compile-Fehler in BACKEND-4:

- `GrowSystem.HydroStyle` ist im aktuellen Modell ein `string`.
- Zwei Test-Helfer hatten versehentlich `HydroStyle.RDWC` als Enum zugewiesen.
- Korrigiert auf `HydroStyle.RDWC.ToString()`.

Geänderte Dateien:

- `GrowDiary.Web.Tests/Api/HardwareItemsApiControllerTests.cs`
- `GrowDiary.Web.Tests/Infrastructure/HardwareItemRepositoryTests.cs`

Keine Produktivlogik geändert.
Kein Frontend geändert.
Keine App_Data/wwwroot-Artefakte enthalten.
