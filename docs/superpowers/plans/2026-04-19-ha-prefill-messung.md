# HA-Prefill MeasurementForm Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Beim Öffnen von `/grows/{id}/messung` werden aktuelle Home-Assistant-Sensorwerte automatisch in das Formular eingetragen und können manuell überschrieben werden.

**Architecture:** Einzige Änderung ist `MeasurementForm.razor` — `HomeAssistantService` wird injiziert, `OnParametersSetAsync` wird async und ruft eine neue `TryPrefillFromHaAsync`-Methode auf, die HA-Werte in `_measurement` schreibt. Fallback auf letzte Messung bleibt bestehen. Kein neuer Service, keine neue Datei.

**Tech Stack:** Blazor Server (.NET 8), HomeAssistantService (bereits im DI), GrowRepository (bereits injiziert). Kein Test-Framework konfiguriert.

---

### Task 1: HA-Prefill in MeasurementForm.razor

**Files:**
- Modify: `GrowDiary.Web/Components/Pages/MeasurementForm.razor:1-10` (inject)
- Modify: `GrowDiary.Web/Components/Pages/MeasurementForm.razor:176-224` (OnParametersSetAsync)
- Modify: `GrowDiary.Web/Components/Pages/MeasurementForm.razor` @code-Block (neue Methode)

**Hintergrund für den Implementer:**
- `GrowRepository.GetTentForGrow(int growId)` existiert — gibt `Tent?` zurück
- `GrowRepository.GetHomeAssistantSettings()` existiert — gibt `HomeAssistantSettings` zurück mit Property `IsConfigured`
- `HomeAssistantService.GetStatesAsync(HomeAssistantSettings settings, Tent tent, CancellationToken ct = default)` existiert — gibt `Dictionary<string, HomeAssistantState>` zurück; bei Circuit-Open gibt es ein leeres Dict zurück (kein Throw)
- `HomeAssistantState.NumericValue` ist `double?`
- HA-State-Keys die befüllt werden: `"temperature"`, `"humidity"`, `"reservoir-ph"`, `"reservoir-ec"`, `"reservoir-temp"`, `"orp"`, `"dissolved-oxygen"`, `"co2"`, `"ppfd"`
- Alle Felder die befüllt werden sind `double?` auf `_measurement`

- [ ] **Step 1: `@inject HomeAssistantService HaService` hinzufügen**

In `GrowDiary.Web/Components/Pages/MeasurementForm.razor`, nach Zeile 5 (`@inject NavigationManager Nav`) einfügen:

```razor
@inject HomeAssistantService HaService
```

Die Inject-Zeilen sollen so aussehen:
```razor
@inject GrowRepository GrowRepo
@inject JournalRepository JournalRepo
@inject NavigationManager Nav
@inject HomeAssistantService HaService
```

- [ ] **Step 2: `OnParametersSetAsync` auf `async Task` umstellen**

Aktuelle Methode (Zeilen 176–224):
```csharp
protected override Task OnParametersSetAsync()
{
    if (_loadedId == Id)
    {
        return Task.CompletedTask;
    }

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
        return Task.CompletedTask;
    }

    _latest = GrowRepo.GetLatestMeasurement(Id);
    _measurement = new Measurement
    {
        GrowId = Id,
        Stage = _latest?.Stage ?? GrowStage.Veg,
        Source = ValueOrigin.Manual,
        TakenAt = _takenAtLocal
    };

    if (_latest is not null)
    {
        _measurement.ReservoirPh = _latest.ReservoirPh;
        _measurement.ReservoirEc = _latest.ReservoirEc;
        _measurement.AirTemperatureC = _latest.AirTemperatureC;
        _measurement.HumidityPercent = _latest.HumidityPercent;
        _measurement.ReservoirWaterTempC = _latest.ReservoirWaterTempC;
        _measurement.OrpMv = _latest.OrpMv;
        _measurement.DissolvedOxygenMgL = _latest.DissolvedOxygenMgL;
        _measurement.PpfdMol = _latest.PpfdMol;
        _measurement.Co2Ppm = _latest.Co2Ppm;
    }

    _loading = false;
    return Task.CompletedTask;
}
```

Ersetzen durch:
```csharp
protected override async Task OnParametersSetAsync()
{
    if (_loadedId == Id)
    {
        return;
    }

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

    await TryPrefillFromHaAsync();

    _loading = false;
}
```

- [ ] **Step 3: `TryPrefillFromHaAsync` Methode hinzufügen**

Im `@code`-Block, direkt nach `OnParametersSetAsync` und vor `SaveAsync`, einfügen:

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

        Fill("temperature",      v => _measurement.AirTemperatureC         = v);
        Fill("humidity",         v => _measurement.HumidityPercent          = v);
        Fill("reservoir-ph",     v => _measurement.ReservoirPh              = v);
        Fill("reservoir-ec",     v => _measurement.ReservoirEc              = v);
        Fill("reservoir-temp",   v => _measurement.ReservoirWaterTempC      = v);
        Fill("orp",              v => _measurement.OrpMv                    = v);
        Fill("dissolved-oxygen", v => _measurement.DissolvedOxygenMgL       = v);
        Fill("co2",              v => _measurement.Co2Ppm                   = v);
        Fill("ppfd",             v => _measurement.PpfdMol                  = v);
    }
    catch
    {
        // HA offline oder Fehler → Fallback (letzte Messung) bleibt
    }
}
```

- [ ] **Step 4: Build prüfen**

```bash
dotnet build "GrowDiary.slnx" -m:1 -v:minimal
```
(Ausführen aus `D:\Grow Operation System new`)

Expected: `Build succeeded. 0 Warning(en) 0 Fehler`

- [ ] **Step 5: Commit**

```bash
git add GrowDiary.Web/Components/Pages/MeasurementForm.razor
git commit -m "feat: HA-Sensorwerte beim Öffnen der Messung vorausfüllen"
```
