# Design: HA-Prefill für MeasurementForm

**Datum:** 2026-04-19  
**Status:** Genehmigt

---

## Ziel

Wenn der User eine neue Messung öffnet (`/grows/{id}/messung`), werden die aktuellen Home-Assistant-Sensorwerte automatisch in das Formular eingetragen. Der User kann alle Werte manuell überschreiben oder ergänzen, bevor er speichert.

---

## Änderungen

### MeasurementForm.razor

**Neue Dependency:**
```razor
@inject HomeAssistantService HaService
```

**`OnParametersSetAsync` auf `async Task` umstellen** und HA-Prefill-Logik ergänzen:

```csharp
protected override async Task OnParametersSetAsync()
{
    if (_loadedId == Id) return;

    _loadedId = Id;
    _loading = true;
    _saving = false;
    _saved = false;
    _error = null;
    _journalTitle = null;
    _journalBody = null;
    _takenAtLocal = DateTime.Now;

    _grow = GrowRepo.GetGrow(Id);
    if (_grow is null)
    {
        _latest = null;
        _measurement = new();
        _loading = false;
        return;
    }

    _latest = GrowRepo.GetLatestMeasurement(Id);
    _measurement = new Measurement
    {
        GrowId = Id,
        Stage = _latest?.Stage ?? GrowStage.Veg,
        Source = ValueOrigin.Manual,
        TakenAt = _takenAtLocal
    };

    // Fallback: letzte Messung (bestehende Logik)
    if (_latest is not null)
    {
        _measurement.ReservoirPh           = _latest.ReservoirPh;
        _measurement.ReservoirEc           = _latest.ReservoirEc;
        _measurement.AirTemperatureC       = _latest.AirTemperatureC;
        _measurement.HumidityPercent       = _latest.HumidityPercent;
        _measurement.ReservoirWaterTempC   = _latest.ReservoirWaterTempC;
        _measurement.OrpMv                 = _latest.OrpMv;
        _measurement.DissolvedOxygenMgL    = _latest.DissolvedOxygenMgL;
        _measurement.PpfdMol               = _latest.PpfdMol;
        _measurement.Co2Ppm                = _latest.Co2Ppm;
    }

    // HA-Prefill: überschreibt Fallback-Werte wenn HA verfügbar
    await TryPrefillFromHaAsync();

    _loading = false;
}
```

**Neue private Methode `TryPrefillFromHaAsync`:**

```csharp
private async Task TryPrefillFromHaAsync()
{
    var tent = GrowRepo.GetTentForGrow(Id);
    if (tent is null) return;

    var settings = GrowRepo.GetHomeAssistantSettings();
    if (!settings.IsConfigured) return;

    try
    {
        var haStates = await HaService.GetStatesAsync(settings, tent);

        void Fill(string key, Action<double> setter)
        {
            if (haStates.TryGetValue(key, out var state) && state.NumericValue.HasValue)
                setter(Math.Round(state.NumericValue.Value, 3));
        }

        Fill("temperature",      v => _measurement.AirTemperatureC           = v);
        Fill("humidity",         v => _measurement.HumidityPercent            = v);
        Fill("reservoir-ph",     v => _measurement.ReservoirPh                = v);
        Fill("reservoir-ec",     v => _measurement.ReservoirEc                = v);
        Fill("reservoir-temp",   v => _measurement.ReservoirWaterTempC        = v);
        Fill("orp",              v => _measurement.OrpMv                      = v);
        Fill("dissolved-oxygen", v => _measurement.DissolvedOxygenMgL         = v);
        Fill("co2",              v => _measurement.Co2Ppm                     = v);
        Fill("ppfd",             v => _measurement.PpfdMol                    = v);
    }
    catch
    {
        // HA offline oder Fehler → Fallback (letzte Messung) bleibt
    }
}
```

---

## Verhalten

| Situation | Ergebnis |
|-----------|----------|
| Kein Zelt zugewiesen | Letzte Messung als Fallback |
| Zelt vorhanden, HA nicht konfiguriert | Letzte Messung als Fallback |
| HA konfiguriert, aber offline (Circuit Breaker) | `GetStatesAsync` gibt leeres Dict zurück → keine Überschreibung |
| HA konfiguriert, Abruf wirft Exception | try/catch → Fallback bleibt |
| HA liefert Werte | HA-Werte überschreiben Fallback-Werte für die entsprechenden Felder |
| HA liefert nur manche Werte | HA-Werte für bekannte Keys, Fallback bleibt für den Rest |

---

## Nicht im Scope

- HA-Badge ("von HA übernommen") im Formular — kann später ergänzt werden
- Automatisches Refresh der HA-Werte während das Formular offen ist
- `Source = ValueOrigin.HomeAssistant` — bleibt `Manual`, da der User explizit speichert

---

## Betroffene Dateien

| Datei | Änderung |
|-------|----------|
| `GrowDiary.Web/Components/Pages/MeasurementForm.razor` | `@inject HomeAssistantService`, `OnParametersSetAsync` async, `TryPrefillFromHaAsync` |

Keine weiteren Dateien.
