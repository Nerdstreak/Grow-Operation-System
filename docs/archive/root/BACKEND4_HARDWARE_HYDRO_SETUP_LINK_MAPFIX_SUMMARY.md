# BACKEND-4 Map-Fix

Fix: `MapHardwareItem` liest `HydroSetupId` jetzt aus der Datenbankspalte `HardwareItems.HydroSetupId`.

Damit bleibt die HydroSetup-Zuordnung nach Create/Load/List erhalten.

Geändert:

- GrowDiary.Web/Infrastructure/GrowRepository.cs
