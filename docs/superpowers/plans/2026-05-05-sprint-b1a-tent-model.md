# Sprint B1a — Tent Multi-Setup-Datenmodell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Das Tent-Modell von 9 hartkodierter HA-Entity-Felder auf eine flexible TentSensor-Liste umbauen, DB-Schema per Legacy-Drop neu aufsetzen, Repository-Methoden aktualisieren, und alle abhängigen Services/Controllers so stubben, dass `dotnet build` durchläuft.

**Architecture:** Komplett-Reset der Tents-Tabelle beim ersten Start mit Legacy-Schema-Erkennung. Neue TentSensors-Tabelle mit FK auf Tents (CASCADE DELETE). GrowRepository bekommt 5 neue TentSensor-CRUD-Methoden. Alle Services/Controllers die alte Tent-Felder nutzen werden mit Empty-Returns/TODO-Kommentaren gestubbt — keine Logik-Änderungen.

**Tech Stack:** ASP.NET Core 8, SQLite (Microsoft.Data.Sqlite), ADO.NET raw, xUnit

---

## File Map

**Erstellen:**
- `GrowDiary.Web/Models/TentSensor.cs` — neues Domain-Model
- `GrowDiary.Web.Tests/Infrastructure/TentRepositoryTests.cs` — 9 Repository-Tests
- `GrowDiary.Web.Tests/Models/TentTests.cs` — 3 Model-Tests

**Modifizieren:**
- `GrowDiary.Web/Models/Enums.cs` — 4 neue Enums hinzufügen
- `GrowDiary.Web/Models/Tent.cs` — Felder austauschen
- `GrowDiary.Web/Infrastructure/DatabaseInitializer.cs` — Legacy-Drop, neues Schema, neues Seeding
- `GrowDiary.Web/Infrastructure/GrowRepository.cs` — Tent-CRUD anpassen, 5 neue Sensor-Methoden
- `GrowDiary.Web/Infrastructure/HaConfigLoader.cs` — neues ha-config.json Format
- `GrowDiary.Web/App_Data/ha-config.example.json` — neues Format

**Stubben (minimal, damit Build grün ist):**
- `GrowDiary.Web/Services/HomeAssistantService.cs` — GetStatesAsync stub
- `GrowDiary.Web/Services/GrowDashboardComposer.cs` — 3 Stellen die alte Tent-Felder prüfen
- `GrowDiary.Web/Api/Contracts/TentDto.cs` — altes Record durch neues ersetzen
- `GrowDiary.Web/Api/Contracts/UpdateTentRequest.cs` — alte Felder entfernen
- `GrowDiary.Web/Api/Mapping/SettingsMapping.cs` — auf neue TentDto-Felder umstellen
- `GrowDiary.Web/Api/Mapping/RequestMapping.cs` — auf neue Tent-Felder umstellen
- `GrowDiary.Web/Components/Pages/Einstellungen.razor` — alte Entity-ID-Inputs entfernen
- `GrowDiary.Web/Components/Pages/TentDetail.razor` — LightCycle-Referenz entfernen

---

## Task 1: Neue Enums in Models/Enums.cs

**Files:**
- Modify: `GrowDiary.Web/Models/Enums.cs`

- [ ] **Step 1: Enums hinzufügen**

Am Ende von `GrowDiary.Web/Models/Enums.cs` anfügen (nach dem letzten bestehenden Enum):

```csharp
public enum TentType
{
    Production,
    Mother,
    Quarantine,
    Propagation,
    MultiPurpose
}

public enum SensorMetricType
{
    AirTemperature,
    Humidity,
    Vpd,
    Co2,
    Ppfd,
    LightStatus,
    ReservoirPh,
    ReservoirEc,
    ReservoirOrp,
    ReservoirDissolvedOxygen,
    ReservoirWaterTemp,
    ReservoirLevel,
    PumpCirculation,
    PumpAir,
    Chiller,
    UpsBattery,
    UpsStatus
}

public enum LightControllerType
{
    AcInfinityPro69,
    AcInfinityCloudline,
    GenericRelay,
    Manual,
    Other
}

public enum HvacControllerType
{
    AcInfinityPro69,
    AcInfinityCloudline,
    GenericRelay,
    Manual,
    Other
}
```

- [ ] **Step 2: Build-Check (Enums)**

```bash
dotnet build GrowDiary.Web/GrowDiary.Web.csproj --no-restore 2>&1 | tail -5
```

Erwartet: Build läuft durch (es gibt noch Fehler von anderen Dateien — noch nicht erwartet sauber zu sein).

---

## Task 2: TentSensor-Modell erstellen

**Files:**
- Create: `GrowDiary.Web/Models/TentSensor.cs`

- [ ] **Step 1: Datei erstellen**

```csharp
namespace GrowDiary.Web.Models;

public sealed class TentSensor
{
    public int Id { get; set; }
    public int TentId { get; set; }
    public SensorMetricType MetricType { get; set; }
    public string HaEntityId { get; set; } = string.Empty;
    public string? DisplayLabel { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
```

---

## Task 3: Tent-Modell umbauen

**Files:**
- Modify: `GrowDiary.Web/Models/Tent.cs`

- [ ] **Step 1: Tent.cs komplett ersetzen**

Alte Felder (TemperatureEntityId, HumidityEntityId, VpdEntityId, ReservoirPhEntityId, ReservoirEcEntityId, ReservoirLevelEntityId, ReservoirTempEntityId, OrpEntityId, DissolvedOxygenEntityId, Co2EntityId, LightEntityId, PpfdEntityId, PpfdTarget, LightCycle, Co2Type, Co2TargetPpm) entfernen. Neue Felder hinzufügen:

```csharp
namespace GrowDiary.Web.Models;

public sealed class Tent
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "Grow Tent";
    public TentType TentType { get; set; } = TentType.MultiPurpose;
    public string? Notes { get; set; }
    public int DisplayOrder { get; set; }
    public string AccentColor { get; set; } = "#69b578";

    public int? WidthCm { get; set; }
    public int? DepthCm { get; set; }
    public int? TentHeightCm { get; set; }
    public string? LightType { get; set; }
    public int? LightWatt { get; set; }
    public LightControllerType? LightController { get; set; }
    public string? LightControllerEntityId { get; set; }
    public int? ExhaustFanCount { get; set; }
    public int? ExhaustM3h { get; set; }
    public int? CirculationFanCount { get; set; }
    public HvacControllerType? HvacController { get; set; }
    public string? HvacControllerEntityId { get; set; }
    public bool Co2Available { get; set; }
    public string? CameraEntityId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public int ActiveGrowCount { get; set; }
    public int ArchivedGrowCount { get; set; }
    public List<GrowRun> ActiveGrows { get; set; } = new();
    public List<TentSensor> Sensors { get; set; } = new();
}
```

---

## Task 4: Abhängige Files stubben (Build muss grün sein)

Dieser Task bringt alle Files in einen Zustand der kompiliert. Logik wird in B1b ergänzt.

**Files:**
- Modify: `GrowDiary.Web/Api/Contracts/TentDto.cs`
- Modify: `GrowDiary.Web/Api/Contracts/UpdateTentRequest.cs`
- Modify: `GrowDiary.Web/Api/Mapping/SettingsMapping.cs`
- Modify: `GrowDiary.Web/Api/Mapping/RequestMapping.cs`
- Modify: `GrowDiary.Web/Services/HomeAssistantService.cs`
- Modify: `GrowDiary.Web/Services/GrowDashboardComposer.cs`
- Modify: `GrowDiary.Web/Components/Pages/Einstellungen.razor`
- Modify: `GrowDiary.Web/Components/Pages/TentDetail.razor`

- [ ] **Step 1: TentDto.cs neu schreiben**

Altes Record löschen, neues Record mit neuen Feldern:

```csharp
namespace GrowDiary.Web.Api.Contracts;

public sealed record TentDto(
    int Id,
    string Name,
    string Kind,
    string TentType,
    string? Notes,
    int DisplayOrder,
    string AccentColor,
    int? WidthCm,
    int? DepthCm,
    int? TentHeightCm,
    string? LightType,
    int? LightWatt,
    string? LightController,
    string? LightControllerEntityId,
    int? ExhaustFanCount,
    int? ExhaustM3h,
    int? CirculationFanCount,
    string? HvacController,
    string? HvacControllerEntityId,
    bool Co2Available,
    string? CameraEntityId,
    int ActiveGrowCount,
    int ArchivedGrowCount,
    IReadOnlyList<TentSensorDto> Sensors
);

public sealed record TentSensorDto(
    int Id,
    int TentId,
    string MetricType,
    string HaEntityId,
    string? DisplayLabel,
    bool IsActive
);
```

- [ ] **Step 2: UpdateTentRequest.cs neu schreiben**

Altes Klasse ersetzen:

```csharp
namespace GrowDiary.Web.Api.Contracts;

public sealed class UpdateTentRequest
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "Grow Tent";
    public string? TentType { get; set; }
    public string? Notes { get; set; }
    public int DisplayOrder { get; set; }
    public string AccentColor { get; set; } = "#69b578";
    public int? WidthCm { get; set; }
    public int? DepthCm { get; set; }
    public int? TentHeightCm { get; set; }
    public string? LightType { get; set; }
    public int? LightWatt { get; set; }
    public string? LightController { get; set; }
    public string? LightControllerEntityId { get; set; }
    public int? ExhaustFanCount { get; set; }
    public int? ExhaustM3h { get; set; }
    public int? CirculationFanCount { get; set; }
    public string? HvacController { get; set; }
    public string? HvacControllerEntityId { get; set; }
    public bool Co2Available { get; set; }
    public string? CameraEntityId { get; set; }
}
```

- [ ] **Step 3: SettingsMapping.cs aktualisieren**

Die `ToDto(this Tent tent)` Methode auf neue Felder umstellen:

```csharp
using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Mapping;

public static class SettingsMapping
{
    public static HomeAssistantSettingsDto ToDto(this HomeAssistantSettings settings) => new(
        BaseUrl: settings.BaseUrl,
        AccessToken: settings.AccessToken,
        Enabled: settings.Enabled
    );

    public static TentDto ToDto(this Tent tent) => new(
        Id: tent.Id,
        Name: tent.Name,
        Kind: tent.Kind,
        TentType: tent.TentType.ToString(),
        Notes: tent.Notes,
        DisplayOrder: tent.DisplayOrder,
        AccentColor: tent.AccentColor,
        WidthCm: tent.WidthCm,
        DepthCm: tent.DepthCm,
        TentHeightCm: tent.TentHeightCm,
        LightType: tent.LightType,
        LightWatt: tent.LightWatt,
        LightController: tent.LightController?.ToString(),
        LightControllerEntityId: tent.LightControllerEntityId,
        ExhaustFanCount: tent.ExhaustFanCount,
        ExhaustM3h: tent.ExhaustM3h,
        CirculationFanCount: tent.CirculationFanCount,
        HvacController: tent.HvacController?.ToString(),
        HvacControllerEntityId: tent.HvacControllerEntityId,
        Co2Available: tent.Co2Available,
        CameraEntityId: tent.CameraEntityId,
        ActiveGrowCount: tent.ActiveGrowCount,
        ArchivedGrowCount: tent.ArchivedGrowCount,
        Sensors: tent.Sensors.Select(s => new TentSensorDto(
            s.Id, s.TentId, s.MetricType.ToString(), s.HaEntityId, s.DisplayLabel, s.IsActive
        )).ToList()
    );
}
```

- [ ] **Step 4: RequestMapping.cs — ToModel für UpdateTentRequest aktualisieren**

Die `ToModel(this UpdateTentRequest request, int id)` Methode ersetzen. Nur die Methode anfassen, nicht die gesamte Datei:

```csharp
    public static Tent ToModel(this UpdateTentRequest request, int id) => new()
    {
        Id = id,
        Name = string.IsNullOrWhiteSpace(request.Name) ? string.Empty : request.Name.Trim(),
        Kind = string.IsNullOrWhiteSpace(request.Kind) ? "Grow Tent" : request.Kind.Trim(),
        TentType = Enum.TryParse<TentType>(request.TentType, out var tt) ? tt : TentType.MultiPurpose,
        Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        DisplayOrder = request.DisplayOrder,
        AccentColor = string.IsNullOrWhiteSpace(request.AccentColor) ? "#69b578" : request.AccentColor.Trim(),
        WidthCm = request.WidthCm,
        DepthCm = request.DepthCm,
        TentHeightCm = request.TentHeightCm,
        LightType = Normalize(request.LightType),
        LightWatt = request.LightWatt,
        LightController = Enum.TryParse<LightControllerType>(request.LightController, out var lc) ? lc : null,
        LightControllerEntityId = Normalize(request.LightControllerEntityId),
        ExhaustFanCount = request.ExhaustFanCount,
        ExhaustM3h = request.ExhaustM3h,
        CirculationFanCount = request.CirculationFanCount,
        HvacController = Enum.TryParse<HvacControllerType>(request.HvacController, out var hc) ? hc : null,
        HvacControllerEntityId = Normalize(request.HvacControllerEntityId),
        Co2Available = request.Co2Available,
        CameraEntityId = Normalize(request.CameraEntityId)
    };
```

Außerdem `using GrowDiary.Web.Models;` sicherstellen ist oben vorhanden (war es bereits).

- [ ] **Step 5: HomeAssistantService.cs — GetStatesAsync stubben**

Die Methode `GetStatesAsync` gibt jetzt ein leeres Dictionary zurück bis B1b:

Aktuelle Methode (ab Zeile 23) ersetzen. Die Signatur bleibt gleich, Body wird gestubbt:

```csharp
    public async Task<Dictionary<string, HomeAssistantState>> GetStatesAsync(
        HomeAssistantSettings settings,
        Tent tent,
        CancellationToken cancellationToken = default)
    {
        // TODO Sprint B1b: TentSensor-Liste verwenden statt hartkodierter Tent-Felder
        await Task.CompletedTask;
        return new Dictionary<string, HomeAssistantState>();
    }
```

- [ ] **Step 6: GrowDashboardComposer.cs — alte Tent-Feld-Prüfungen stubben**

In `GrowDashboardComposer.cs` gibt es mehrere Stellen die auf alte Tent-Felder zugreifen. Diese werden durch `false`-Guards / leere Returns ersetzt:

Zeile 66: `if (hasActiveHydro || !string.IsNullOrWhiteSpace(tent.ReservoirPhEntityId) || measurements.Any(m => m.ReservoirPh.HasValue))`
→ ersetzen mit: `if (hasActiveHydro || measurements.Any(m => m.ReservoirPh.HasValue))`

Zeile 69: `if (hasActiveHydro || !string.IsNullOrWhiteSpace(tent.ReservoirEcEntityId) || measurements.Any(m => m.ReservoirEc.HasValue))`
→ ersetzen mit: `if (hasActiveHydro || measurements.Any(m => m.ReservoirEc.HasValue))`

Zeile 72: `if (hasActiveHydro || !string.IsNullOrWhiteSpace(tent.OrpEntityId) || measurements.Any(m => m.OrpMv.HasValue))`
→ ersetzen mit: `if (hasActiveHydro || measurements.Any(m => m.OrpMv.HasValue))`

Zeile 75: `if (!string.IsNullOrWhiteSpace(tent.ReservoirLevelEntityId) || measurements.Any(m => m.ReservoirLevelLiters.HasValue || m.ReservoirLevelCm.HasValue))`
→ ersetzen mit: `if (measurements.Any(m => m.ReservoirLevelLiters.HasValue || m.ReservoirLevelCm.HasValue))`

Zeile 90: `if (hasActiveHydro || !string.IsNullOrWhiteSpace(tent.ReservoirTempEntityId) || measurements.Any(m => m.ReservoirWaterTempC.HasValue))`
→ ersetzen mit: `if (hasActiveHydro || measurements.Any(m => m.ReservoirWaterTempC.HasValue))`

Die Methoden `BuildLightCycleMetric` und `BuildPpfdMetric` und `ResolveLightCycle` die auf `tent.LightCycle` und `tent.PpfdTarget` zugreifen müssen ebenfalls angepasst werden.

`ResolveLightCycle(Tent tent)` — ersetzt durch leere Implementierung die null zurückgibt:
```csharp
    private static string? ResolveLightCycle(Tent tent)
    {
        // TODO Sprint B1b: LightCycle aus Setup/Phase laden
        return null;
    }
```

`BuildLightCycleMetric(Tent tent)` — der Hint-Zugriff auf `tent.LightCycle` ersetzen:
Den Block `Hint = !string.IsNullOrWhiteSpace(tent.LightCycle) ? ...` durch `Hint = "Kein Lichtzyklus konfiguriert"` ersetzen.

`BuildPpfdMetric` — der Zugriff auf `tent.PpfdTarget`:
Die Zeilen `Value = string.IsNullOrWhiteSpace(tent.PpfdTarget) ? "–" : tent.PpfdTarget` und `Unit = string.IsNullOrWhiteSpace(tent.PpfdTarget) ? null : "µmol/m²/s"` durch `Value = "–"`, `Unit = null`, `Hint = "Kein Sensor konfiguriert"` ersetzen.

- [ ] **Step 7: Einstellungen.razor — alte Entity-ID-Inputs entfernen**

In `Components/Pages/Einstellungen.razor` alle Input-Bindings auf alte Tent-Felder entfernen:
- `@bind="t.LightCycle"` (Zeile ~141)
- `@bind="t.PpfdTarget"` (Zeile ~145)
- `@bind="t.Co2Type"` (Zeile ~175)
- `@bind="t.Co2TargetPpm"` (Zeile ~183)
- `@bind="t.TemperatureEntityId"` (Zeile ~194)
- `@bind="t.HumidityEntityId"` (Zeile ~198)
- `@bind="t.VpdEntityId"` (Zeile ~202)
- `@bind="t.Co2EntityId"` (Zeile ~206)
- `@bind="t.LightEntityId"` (Zeile ~216)
- `@bind="t.PpfdEntityId"` (Zeile ~220)
- `@bind="t.ReservoirPhEntityId"` (Zeile ~230)
- `@bind="t.ReservoirEcEntityId"` (Zeile ~234)
- `@bind="t.ReservoirLevelEntityId"` (Zeile ~238)
- `@bind="t.ReservoirTempEntityId"` (Zeile ~242)
- `@bind="t.OrpEntityId"` (Zeile ~246)
- `@bind="t.DissolvedOxygenEntityId"` (Zeile ~250)

Ganze Formular-Sections die ausschließlich diese alten Felder enthalten können ersetzt werden durch:
```html
<p class="text-muted"><!-- TODO Sprint B1b: Sensor-Konfiguration via TentSensor-Liste --></p>
```

- [ ] **Step 8: TentDetail.razor — LightCycle entfernen**

In `Components/Pages/TentDetail.razor` den Block der auf `_tent.LightCycle` zugreift entfernen (Zeilen ~125-129). Ersetzen durch leeren Kommentar oder nichts.

- [ ] **Step 9: Build-Check nach Stubs**

```bash
cd "D:/Grow Operation System new" && dotnet build GrowDiary.Web/GrowDiary.Web.csproj 2>&1 | grep -E "error|warning|Build succeeded|FAILED"
```

Erwartet: `Build succeeded` mit 0 Errors. Warnings sind ok.

Falls noch Errors: diese beheben bevor weitergemacht wird.

---

## Task 5: DatabaseInitializer.cs — Legacy-Drop + neues Schema

**Files:**
- Modify: `GrowDiary.Web/Infrastructure/DatabaseInitializer.cs`

- [ ] **Step 1: Logger hinzufügen**

`DatabaseInitializer` ist bereits via `builder.Services.AddSingleton<DatabaseInitializer>()` in Program.cs registriert und wird per DI aufgelöst. Konstruktor und Feld hinzufügen — DI injiziert den Logger automatisch:

```csharp
public sealed class DatabaseInitializer
{
    private readonly AppPaths _paths;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(AppPaths paths, ILogger<DatabaseInitializer> logger)
    {
        _paths = paths;
        _logger = logger;
    }
```

`Program.cs` muss **nicht** geändert werden — DI löst `ILogger<DatabaseInitializer>` automatisch auf.

- [ ] **Step 2: DropLegacyTentSchemaIfNeeded() implementieren**

Vor `EnsureSchema()` in `Initialize()` aufrufen. Die Methode:

```csharp
private void DropLegacyTentSchemaIfNeeded()
{
    using var connection = OpenConnection();
    using var cmd = connection.CreateCommand();

    cmd.CommandText = @"
        SELECT COUNT(*) FROM pragma_table_info('Tents')
        WHERE name = 'TemperatureEntityId';";
    var hasLegacyColumn = Convert.ToInt32(cmd.ExecuteScalar()) > 0;

    if (!hasLegacyColumn) return;

    _logger.LogWarning(
        "Legacy Tent-Schema erkannt — Tents und abhängige Daten werden gelöscht und neu aufgebaut.");

    cmd.CommandText = """
        DROP TABLE IF EXISTS TentSensors;
        DROP TABLE IF EXISTS Tents;
        DELETE FROM Grows;
        DELETE FROM Measurements;
        DELETE FROM Photos;
        DELETE FROM JournalEntries;
        DELETE FROM GrowTasks;
        DELETE FROM HarvestEntries;
        DELETE FROM TentSensorReadings;
        DELETE FROM TentSensorSnapshots;
        DELETE FROM TentSensorDailyStats;
        """;
    cmd.ExecuteNonQuery();
}
```

Und in `Initialize()` einfügen:

```csharp
public void Initialize()
{
    Directory.CreateDirectory(Path.GetDirectoryName(_paths.DatabasePath)!);
    Directory.CreateDirectory(_paths.UploadRootPath);
    DropLegacyTentSchemaIfNeeded();  // NEU
    EnsureSchema();
    SeedDefaults();
    AutoAssignExistingGrowsToTents();
}
```

- [ ] **Step 3: EnsureSchema() — Tents CREATE TABLE ersetzen**

Das bestehende `CREATE TABLE IF NOT EXISTS Tents` Statement ersetzen durch:

```sql
CREATE TABLE IF NOT EXISTS Tents (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Kind TEXT NOT NULL DEFAULT 'Grow Tent',
    TentType TEXT NOT NULL DEFAULT 'MultiPurpose',
    Notes TEXT NULL,
    DisplayOrder INTEGER NOT NULL DEFAULT 99,
    AccentColor TEXT NOT NULL DEFAULT '#69b578',
    WidthCm INTEGER NULL,
    DepthCm INTEGER NULL,
    TentHeightCm INTEGER NULL,
    LightType TEXT NULL,
    LightWatt INTEGER NULL,
    LightController TEXT NULL,
    LightControllerEntityId TEXT NULL,
    ExhaustFanCount INTEGER NULL,
    ExhaustM3h INTEGER NULL,
    CirculationFanCount INTEGER NULL,
    HvacController TEXT NULL,
    HvacControllerEntityId TEXT NULL,
    Co2Available INTEGER NOT NULL DEFAULT 0,
    CameraEntityId TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);
```

- [ ] **Step 4: TentSensors Tabelle zu EnsureSchema() hinzufügen**

Nach dem Tents CREATE TABLE (oder nach TentSensorSnapshots) hinzufügen:

```sql
CREATE TABLE IF NOT EXISTS TentSensors (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TentId INTEGER NOT NULL,
    MetricType TEXT NOT NULL,
    HaEntityId TEXT NOT NULL,
    DisplayLabel TEXT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    FOREIGN KEY (TentId) REFERENCES Tents(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_TentSensors_TentId
    ON TentSensors(TentId);
```

- [ ] **Step 5: EnsureColumn-Aufrufe für alte Tents-Spalten entfernen**

Folgende Zeilen aus `EnsureSchema()` entfernen (da neue Tents-Tabelle die Spalten nicht hat):
- `EnsureColumn(connection, "Tents", "CameraEntityId", "TEXT NULL");`
- `EnsureColumn(connection, "Tents", "LightCycle", "TEXT NULL");`
- `EnsureColumn(connection, "Tents", "PpfdEntityId", "TEXT NULL");`
- `EnsureColumn(connection, "Tents", "PpfdTarget", "TEXT NULL");`
- `EnsureColumn(connection, "Tents", "OrpEntityId", "TEXT NULL");`
- `EnsureColumn(connection, "Tents", "DissolvedOxygenEntityId", "TEXT NULL");`
- `EnsureColumn(connection, "Tents", "Co2EntityId", "TEXT NULL");`
- `EnsureColumn(connection, "Tents", "WidthCm", "INTEGER NULL");`
- `EnsureColumn(connection, "Tents", "DepthCm", "INTEGER NULL");`
- `EnsureColumn(connection, "Tents", "TentHeightCm", "INTEGER NULL");`
- `EnsureColumn(connection, "Tents", "LightType", "TEXT NULL");`
- `EnsureColumn(connection, "Tents", "LightWatt", "INTEGER NULL");`
- `EnsureColumn(connection, "Tents", "ExhaustFanCount", "INTEGER NULL");`
- `EnsureColumn(connection, "Tents", "ExhaustM3h", "INTEGER NULL");`
- `EnsureColumn(connection, "Tents", "CirculationFanCount", "INTEGER NULL");`
- `EnsureColumn(connection, "Tents", "Co2Type", "TEXT NULL");`
- `EnsureColumn(connection, "Tents", "Co2TargetPpm", "INTEGER NULL");`

- [ ] **Step 6: SeedDefaults() anpassen**

Bestehenden Tent-Seeding-Code (Hauptzelt + Anzuchtzelt mit alten Feldern) durch neuen ersetzen:

```csharp
private void SeedDefaults()
{
    using var connection = OpenConnection();

    // Tents
    using var countCommand = connection.CreateCommand();
    countCommand.CommandText = "SELECT COUNT(*) FROM Tents;";
    var tentCount = Convert.ToInt32((long)(countCommand.ExecuteScalar() ?? 0L));
    if (tentCount == 0)
    {
        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO Tents (Name, Kind, TentType, AccentColor, DisplayOrder,
                               Co2Available, CreatedAtUtc, UpdatedAtUtc)
            VALUES ('Hauptzelt', 'Grow Tent', 'MultiPurpose', '#69b578', 1,
                    0, datetime('now'), datetime('now'));
            """;
        insert.ExecuteNonQuery();
    }

    // GrowTemplates (bestehender Code bleibt)
    using var deleteNonHydro = connection.CreateCommand();
    deleteNonHydro.CommandText = "DELETE FROM GrowTemplates WHERE MediumType != 'Hydro';";
    deleteNonHydro.ExecuteNonQuery();

    using var templateCount = connection.CreateCommand();
    templateCount.CommandText = "SELECT COUNT(*) FROM GrowTemplates;";
    var growTemplateCount = Convert.ToInt32((long)(templateCount.ExecuteScalar() ?? 0L));
    if (growTemplateCount == 0)
    {
        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO GrowTemplates (Name, Description, MediumType, FeedingStyle, HydroStyle, MediumDetail, Environment, SuggestedTentKind, Light, ContainerSize, ReservoirSize, IrrigationStyle, Nutrients, Notes, AccentColor)
            VALUES
                ('RDWC Standard', 'Für rezirkulierende Hydro-Runs mit Reservoir-Tracking, Addback und Kamera-Überblick.', 'Hydro', 'None', 'RDWC', 'RDWC', 'Indoor', 'Grow Tent', 'LED Vollspektrum', 'Netztopf / RDWC Site', '60 L Reservoir', null, 'Athena / Hydroponic Research', 'Ideal für dein Hauptzelt mit Home-Assistant-Monitoring.', '#7dd3a6');
            """;
        insert.ExecuteNonQuery();
    }
}
```

- [ ] **Step 7: AutoAssignExistingGrowsToTents() anpassen**

Die Methode referenziert "Anzuchtzelt" — das gibt es nach dem Reset nicht mehr. Methode anpassen: nur noch "Hauptzelt" als fallback, und wenn kein "Anzuchtzelt" gefunden wird, alle Grows dem ersten Tent zuweisen:

```csharp
private void AutoAssignExistingGrowsToTents()
{
    using var connection = OpenConnection();
    var mainTentId = GetTentId(connection, "Hauptzelt");
    if (mainTentId == 0) return;

    using var select = connection.CreateCommand();
    select.CommandText = "SELECT Id FROM Grows WHERE TentId IS NULL;";
    using var reader = select.ExecuteReader();
    var ids = new List<int>();
    while (reader.Read())
        ids.Add(Convert.ToInt32((long)reader["Id"]));
    reader.Close();

    foreach (var id in ids)
    {
        using var update = connection.CreateCommand();
        update.CommandText = "UPDATE Grows SET TentId = $tentId WHERE Id = $id;";
        update.Parameters.AddWithValue("$tentId", mainTentId);
        update.Parameters.AddWithValue("$id", id);
        update.ExecuteNonQuery();
    }
}
```

- [ ] **Step 8: ILogger-DI prüfen und beheben**

`DatabaseInitializer` wird in `Program.cs` aufgerufen. Prüfen wie es aufgerufen wird:

```bash
grep -n "DatabaseInitializer" "D:/Grow Operation System new/GrowDiary.Web/Program.cs"
```

Falls es direkt als `new DatabaseInitializer(paths)` aufgerufen wird (ohne DI), dann entweder:
- Als DI-Service registrieren: `builder.Services.AddSingleton<DatabaseInitializer>();`
- Oder ILoggerFactory via `app.Services.GetRequiredService<ILoggerFactory>()` holen und manuell einen Logger erstellen

Den tatsächlichen Aufrufkontext lesen und entsprechend anpassen.

---

## Task 6: GrowRepository.cs — Tent-CRUD und Sensor-Methoden

**Files:**
- Modify: `GrowDiary.Web/Infrastructure/GrowRepository.cs`

- [ ] **Step 1: MapTent() auf neue Felder umstellen**

Die private Methode `MapTent` (aktuell Zeile ~978) komplett ersetzen:

```csharp
private static Tent MapTent(SqliteDataReader reader)
{
    return new Tent
    {
        Id = Convert.ToInt32((long)reader["Id"]),
        Name = reader["Name"]?.ToString() ?? string.Empty,
        Kind = reader["Kind"]?.ToString() ?? "Grow Tent",
        TentType = ParseEnum(reader["TentType"]?.ToString(), TentType.MultiPurpose),
        Notes = NullString(reader["Notes"]),
        DisplayOrder = Convert.ToInt32(reader["DisplayOrder"], CultureInfo.InvariantCulture),
        AccentColor = reader["AccentColor"]?.ToString() ?? "#69b578",
        WidthCm             = reader["WidthCm"] is DBNull or null ? null : Convert.ToInt32(reader["WidthCm"], CultureInfo.InvariantCulture),
        DepthCm             = reader["DepthCm"] is DBNull or null ? null : Convert.ToInt32(reader["DepthCm"], CultureInfo.InvariantCulture),
        TentHeightCm        = reader["TentHeightCm"] is DBNull or null ? null : Convert.ToInt32(reader["TentHeightCm"], CultureInfo.InvariantCulture),
        LightType           = NullString(reader["LightType"]),
        LightWatt           = reader["LightWatt"] is DBNull or null ? null : Convert.ToInt32(reader["LightWatt"], CultureInfo.InvariantCulture),
        LightController     = reader["LightController"] is DBNull or null ? null : ParseEnumNullable<LightControllerType>(reader["LightController"]?.ToString()),
        LightControllerEntityId = NullString(reader["LightControllerEntityId"]),
        ExhaustFanCount     = reader["ExhaustFanCount"] is DBNull or null ? null : Convert.ToInt32(reader["ExhaustFanCount"], CultureInfo.InvariantCulture),
        ExhaustM3h          = reader["ExhaustM3h"] is DBNull or null ? null : Convert.ToInt32(reader["ExhaustM3h"], CultureInfo.InvariantCulture),
        CirculationFanCount = reader["CirculationFanCount"] is DBNull or null ? null : Convert.ToInt32(reader["CirculationFanCount"], CultureInfo.InvariantCulture),
        HvacController      = reader["HvacController"] is DBNull or null ? null : ParseEnumNullable<HvacControllerType>(reader["HvacController"]?.ToString()),
        HvacControllerEntityId = NullString(reader["HvacControllerEntityId"]),
        Co2Available        = reader["Co2Available"] is not DBNull && Convert.ToInt32(reader["Co2Available"], CultureInfo.InvariantCulture) == 1,
        CameraEntityId      = NullString(reader["CameraEntityId"]),
        CreatedAtUtc        = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
        UpdatedAtUtc        = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
        ActiveGrowCount     = reader["ActiveGrowCount"] is DBNull ? 0 : Convert.ToInt32(reader["ActiveGrowCount"], CultureInfo.InvariantCulture),
        ArchivedGrowCount   = reader["ArchivedGrowCount"] is DBNull ? 0 : Convert.ToInt32(reader["ArchivedGrowCount"], CultureInfo.InvariantCulture)
    };
}
```

Hilfs-Methode `ParseEnumNullable` hinzufügen (nach `ParseEnum`):

```csharp
private static TEnum? ParseEnumNullable<TEnum>(string? raw) where TEnum : struct
    => Enum.TryParse<TEnum>(raw, out var parsed) ? parsed : null;
```

- [ ] **Step 2: AddTentParameters() auf neue Felder umstellen**

Die Methode `AddTentParameters` (aktuell Zeile ~1127) komplett ersetzen:

```csharp
private static void AddTentParameters(SqliteCommand command, Tent tent)
{
    command.Parameters.AddWithValue("$name", tent.Name);
    command.Parameters.AddWithValue("$kind", tent.Kind);
    command.Parameters.AddWithValue("$tentType", tent.TentType.ToString());
    command.Parameters.AddWithValue("$notes", (object?)tent.Notes ?? DBNull.Value);
    command.Parameters.AddWithValue("$displayOrder", tent.DisplayOrder);
    command.Parameters.AddWithValue("$accentColor", tent.AccentColor);
    command.Parameters.AddWithValue("$widthCm", (object?)tent.WidthCm ?? DBNull.Value);
    command.Parameters.AddWithValue("$depthCm", (object?)tent.DepthCm ?? DBNull.Value);
    command.Parameters.AddWithValue("$tentHeightCm", (object?)tent.TentHeightCm ?? DBNull.Value);
    command.Parameters.AddWithValue("$lightType", (object?)tent.LightType ?? DBNull.Value);
    command.Parameters.AddWithValue("$lightWatt", (object?)tent.LightWatt ?? DBNull.Value);
    command.Parameters.AddWithValue("$lightController", (object?)tent.LightController?.ToString() ?? DBNull.Value);
    command.Parameters.AddWithValue("$lightControllerEntityId", (object?)tent.LightControllerEntityId ?? DBNull.Value);
    command.Parameters.AddWithValue("$exhaustFanCount", (object?)tent.ExhaustFanCount ?? DBNull.Value);
    command.Parameters.AddWithValue("$exhaustM3h", (object?)tent.ExhaustM3h ?? DBNull.Value);
    command.Parameters.AddWithValue("$circulationFanCount", (object?)tent.CirculationFanCount ?? DBNull.Value);
    command.Parameters.AddWithValue("$hvacController", (object?)tent.HvacController?.ToString() ?? DBNull.Value);
    command.Parameters.AddWithValue("$hvacControllerEntityId", (object?)tent.HvacControllerEntityId ?? DBNull.Value);
    command.Parameters.AddWithValue("$co2Available", tent.Co2Available ? 1 : 0);
    command.Parameters.AddWithValue("$cameraEntityId", (object?)tent.CameraEntityId ?? DBNull.Value);
    command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(tent.UpdatedAtUtc));
}
```

- [ ] **Step 3: UpdateTent() SQL und Parameter anpassen**

Das UPDATE-Statement (aktuell Zeile ~140) durch neues ersetzen:

```csharp
public void UpdateTent(Tent tent)
{
    tent.UpdatedAtUtc = DateTime.UtcNow;
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = """
        UPDATE Tents SET
            Name = $name,
            Kind = $kind,
            TentType = $tentType,
            Notes = $notes,
            DisplayOrder = $displayOrder,
            AccentColor = $accentColor,
            WidthCm = $widthCm,
            DepthCm = $depthCm,
            TentHeightCm = $tentHeightCm,
            LightType = $lightType,
            LightWatt = $lightWatt,
            LightController = $lightController,
            LightControllerEntityId = $lightControllerEntityId,
            ExhaustFanCount = $exhaustFanCount,
            ExhaustM3h = $exhaustM3h,
            CirculationFanCount = $circulationFanCount,
            HvacController = $hvacController,
            HvacControllerEntityId = $hvacControllerEntityId,
            Co2Available = $co2Available,
            CameraEntityId = $cameraEntityId,
            UpdatedAtUtc = $updatedAtUtc
        WHERE Id = $id;
        """;
    AddTentParameters(command, tent);
    command.Parameters.AddWithValue("$id", tent.Id);
    command.ExecuteNonQuery();
}
```

- [ ] **Step 4: CreateTent() anpassen**

```csharp
public Tent CreateTent(string name)
{
    var now = DateTime.UtcNow;
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = """
        INSERT INTO Tents (Name, Kind, TentType, Notes, DisplayOrder, AccentColor,
                           Co2Available, CreatedAtUtc, UpdatedAtUtc)
        VALUES ($name, 'Grow Tent', 'MultiPurpose', NULL, 99, '#69b578',
                0, $createdAtUtc, $updatedAtUtc);
        SELECT last_insert_rowid();
        """;
    command.Parameters.AddWithValue("$name", name);
    command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(now));
    command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(now));
    var id = Convert.ToInt32((long)(command.ExecuteScalar() ?? 0L));
    return new Tent { Id = id, Name = name, CreatedAtUtc = now, UpdatedAtUtc = now };
}
```

- [ ] **Step 5: GetTents() und GetTent() — Sensors laden**

Nach dem Laden der Tents in `GetTents()` die Sensor-Liste laden. Nach dem Block der `tent.ActiveGrows` setzt:

```csharp
// Sensors laden
var tentIds = tents.Select(t => t.Id).ToList();
if (tentIds.Count > 0)
{
    var sensorsByTentId = LoadSensorsByTentIds(connection, tentIds);
    foreach (var tent in tents)
    {
        tent.Sensors = sensorsByTentId.TryGetValue(tent.Id, out var sensors) ? sensors : new();
    }
}
```

In `GetTent(int id)` nach `tent.ActiveGrows = GetActiveGrowsForTent(tent.Id);`:

```csharp
tent.Sensors = GetTentSensors(id);
```

Hilfsmethode `LoadSensorsByTentIds` hinzufügen (gibt Dictionary zurück damit das Grouping-Lookup funktioniert):

```csharp
private static Dictionary<int, List<TentSensor>> LoadSensorsByTentIds(SqliteConnection connection, List<int> tentIds)
{
    var placeholders = string.Join(", ", tentIds.Select((_, i) => $"$s{i}"));
    using var cmd = connection.CreateCommand();
    cmd.CommandText = $"SELECT * FROM TentSensors WHERE TentId IN ({placeholders}) ORDER BY TentId, Id;";
    for (var i = 0; i < tentIds.Count; i++)
        cmd.Parameters.AddWithValue($"$s{i}", tentIds[i]);
    var result = new Dictionary<int, List<TentSensor>>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var sensor = MapTentSensor(reader);
        if (!result.ContainsKey(sensor.TentId))
            result[sensor.TentId] = new();
        result[sensor.TentId].Add(sensor);
    }
    return result;
}
```

- [ ] **Step 6: Neue Sensor-CRUD-Methoden hinzufügen**

```csharp
public List<TentSensor> GetTentSensors(int tentId)
{
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT * FROM TentSensors WHERE TentId = $tentId ORDER BY Id;";
    command.Parameters.AddWithValue("$tentId", tentId);
    var list = new List<TentSensor>();
    using var reader = command.ExecuteReader();
    while (reader.Read())
        list.Add(MapTentSensor(reader));
    return list;
}

public TentSensor AddTentSensor(TentSensor sensor)
{
    sensor.CreatedAtUtc = DateTime.UtcNow;
    sensor.UpdatedAtUtc = DateTime.UtcNow;
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = """
        INSERT INTO TentSensors (TentId, MetricType, HaEntityId, DisplayLabel, IsActive, CreatedAtUtc, UpdatedAtUtc)
        VALUES ($tentId, $metricType, $haEntityId, $displayLabel, $isActive, $createdAtUtc, $updatedAtUtc);
        SELECT last_insert_rowid();
        """;
    AddTentSensorParameters(command, sensor);
    sensor.Id = Convert.ToInt32((long)command.ExecuteScalar()!);
    return sensor;
}

public void UpdateTentSensor(TentSensor sensor)
{
    sensor.UpdatedAtUtc = DateTime.UtcNow;
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = """
        UPDATE TentSensors SET
            MetricType = $metricType,
            HaEntityId = $haEntityId,
            DisplayLabel = $displayLabel,
            IsActive = $isActive,
            UpdatedAtUtc = $updatedAtUtc
        WHERE Id = $id;
        """;
    AddTentSensorParameters(command, sensor);
    command.Parameters.AddWithValue("$id", sensor.Id);
    command.ExecuteNonQuery();
}

public void DeleteTentSensor(int sensorId)
{
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = "DELETE FROM TentSensors WHERE Id = $id;";
    command.Parameters.AddWithValue("$id", sensorId);
    command.ExecuteNonQuery();
}

public TentSensor? GetTentSensorByMetric(int tentId, SensorMetricType metricType)
{
    using var connection = OpenConnection();
    using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT * FROM TentSensors
        WHERE TentId = $tentId AND MetricType = $metricType
        ORDER BY Id LIMIT 1;
        """;
    command.Parameters.AddWithValue("$tentId", tentId);
    command.Parameters.AddWithValue("$metricType", metricType.ToString());
    using var reader = command.ExecuteReader();
    return reader.Read() ? MapTentSensor(reader) : null;
}

private static TentSensor MapTentSensor(SqliteDataReader reader)
{
    return new TentSensor
    {
        Id           = Convert.ToInt32((long)reader["Id"]),
        TentId       = Convert.ToInt32((long)reader["TentId"]),
        MetricType   = ParseEnum(reader["MetricType"]?.ToString(), SensorMetricType.AirTemperature),
        HaEntityId   = reader["HaEntityId"]?.ToString() ?? string.Empty,
        DisplayLabel = NullString(reader["DisplayLabel"]),
        IsActive     = reader["IsActive"] is not DBNull && Convert.ToInt32(reader["IsActive"]) == 1,
        CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
        UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
    };
}

private static void AddTentSensorParameters(SqliteCommand command, TentSensor sensor)
{
    command.Parameters.AddWithValue("$tentId", sensor.TentId);
    command.Parameters.AddWithValue("$metricType", sensor.MetricType.ToString());
    command.Parameters.AddWithValue("$haEntityId", sensor.HaEntityId);
    command.Parameters.AddWithValue("$displayLabel", (object?)sensor.DisplayLabel ?? DBNull.Value);
    command.Parameters.AddWithValue("$isActive", sensor.IsActive ? 1 : 0);
    command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(sensor.CreatedAtUtc));
    command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(sensor.UpdatedAtUtc));
}
```

- [ ] **Step 7: Build-Check**

```bash
cd "D:/Grow Operation System new" && dotnet build GrowDiary.Web/GrowDiary.Web.csproj 2>&1 | grep -E "^.*error|Build succeeded|FAILED"
```

Erwartet: `Build succeeded` mit 0 Errors.

---

## Task 7: HaConfigLoader.cs — neues Format

**Files:**
- Modify: `GrowDiary.Web/Infrastructure/HaConfigLoader.cs`
- Modify: `GrowDiary.Web/App_Data/ha-config.example.json`

- [ ] **Step 1: HaConfigLoader.cs komplett neu schreiben**

```csharp
using System.Text.Json;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Infrastructure;

public static class HaConfigLoader
{
    public static void Apply(AppPaths paths, GrowRepository repository)
    {
        var configPath = Path.Combine(paths.ContentRootPath, "App_Data", "ha-config.json");
        if (!File.Exists(configPath)) return;

        using var stream = File.OpenRead(configPath);
        JsonDocument? doc;
        try { doc = JsonDocument.Parse(stream); }
        catch { return; }

        using (doc)
        {
            var root = doc.RootElement;

            if (root.TryGetProperty("homeAssistant", out var ha))
            {
                var url   = ha.TryGetProperty("url",   out var u) ? u.GetString() : null;
                var token = ha.TryGetProperty("token", out var t) ? t.GetString() : null;
                if (!string.IsNullOrWhiteSpace(url) || !string.IsNullOrWhiteSpace(token))
                {
                    var existing = repository.GetHomeAssistantSettings();
                    repository.SaveHomeAssistantSettings(new HomeAssistantSettings
                    {
                        BaseUrl     = !string.IsNullOrWhiteSpace(url)   ? url   : existing.BaseUrl,
                        AccessToken = !string.IsNullOrWhiteSpace(token) ? token : existing.AccessToken,
                        Enabled     = true
                    });
                }
            }

            if (!root.TryGetProperty("tents", out var tentsEl)) return;

            var tentsByName = repository.GetTents()
                .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var tentEl in tentsEl.EnumerateArray())
            {
                var name = tentEl.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (!tentsByName.TryGetValue(name, out var tent))
                {
                    tent = repository.CreateTent(name);
                    tentsByName[name] = tent;
                }

                // Optional: TentType setzen
                if (tentEl.TryGetProperty("tentType", out var ttEl) &&
                    Enum.TryParse<TentType>(ttEl.GetString(), out var tentType))
                {
                    tent.TentType = tentType;
                }

                // CameraEntityId direkt auf Tent
                if (tentEl.TryGetProperty("cameraEntityId", out var camEl))
                    tent.CameraEntityId = camEl.GetString();

                repository.UpdateTent(tent);

                // Sensoren verarbeiten
                if (!tentEl.TryGetProperty("sensors", out var sensorsEl)) continue;

                var existingSensors = repository.GetTentSensors(tent.Id)
                    .ToDictionary(s => s.MetricType);

                foreach (var sensorEl in sensorsEl.EnumerateArray())
                {
                    var metricRaw  = sensorEl.TryGetProperty("metricType",  out var m) ? m.GetString() : null;
                    var haEntityId = sensorEl.TryGetProperty("haEntityId",  out var e) ? e.GetString() : null;
                    var label      = sensorEl.TryGetProperty("displayLabel", out var l) ? l.GetString() : null;

                    if (string.IsNullOrWhiteSpace(metricRaw) || string.IsNullOrWhiteSpace(haEntityId)) continue;
                    if (!Enum.TryParse<SensorMetricType>(metricRaw, out var metricType)) continue;

                    if (existingSensors.TryGetValue(metricType, out var existing))
                    {
                        existing.HaEntityId   = haEntityId;
                        existing.DisplayLabel  = label;
                        existing.IsActive      = true;
                        repository.UpdateTentSensor(existing);
                    }
                    else
                    {
                        repository.AddTentSensor(new TentSensor
                        {
                            TentId       = tent.Id,
                            MetricType   = metricType,
                            HaEntityId   = haEntityId,
                            DisplayLabel = label,
                            IsActive     = true
                        });
                    }
                }
            }
        }
    }
}
```

- [ ] **Step 2: ha-config.example.json aktualisieren**

```json
{
  "homeAssistant": {
    "url": "http://YOUR_HA_IP:8123/api/",
    "token": "YOUR_LONG_LIVED_ACCESS_TOKEN"
  },
  "tents": [
    {
      "name": "Hauptzelt",
      "tentType": "MultiPurpose",
      "cameraEntityId": "camera.your_camera_entity",
      "sensors": [
        { "metricType": "AirTemperature",          "haEntityId": "sensor.your_temp_entity" },
        { "metricType": "Humidity",                "haEntityId": "sensor.your_humidity_entity" },
        { "metricType": "Vpd",                     "haEntityId": "sensor.your_vpd_entity" },
        { "metricType": "Ppfd",                    "haEntityId": "sensor.your_ppfd_entity" },
        { "metricType": "LightStatus",             "haEntityId": "binary_sensor.your_light_entity" },
        { "metricType": "Co2",                     "haEntityId": "sensor.your_co2_entity" },
        { "metricType": "ReservoirPh",             "haEntityId": "sensor.your_reservoir_ph_entity" },
        { "metricType": "ReservoirEc",             "haEntityId": "sensor.your_reservoir_ec_entity" },
        { "metricType": "ReservoirLevel",          "haEntityId": "sensor.your_reservoir_level_entity" },
        { "metricType": "ReservoirWaterTemp",      "haEntityId": "sensor.your_reservoir_temp_entity" },
        { "metricType": "ReservoirOrp",            "haEntityId": "sensor.your_orp_entity" },
        { "metricType": "ReservoirDissolvedOxygen","haEntityId": "sensor.your_do_entity" }
      ]
    }
  ]
}
```

---

## Task 8: Tests — Models

**Files:**
- Create: `GrowDiary.Web.Tests/Models/TentTests.cs`

- [ ] **Step 1: Testdatei erstellen**

Zuerst prüfen welches Namespace und Test-Framework das Projekt nutzt:

```bash
grep -n "using\|namespace" "D:/Grow Operation System new/GrowDiary.Web.Tests/AddbackCalculatorTests.cs" | head -10
```

Dann erstellen:

```csharp
using GrowDiary.Web.Models;
using Xunit;

namespace GrowDiary.Web.Tests.Models;

public sealed class TentTests
{
    [Fact]
    public void Tent_DefaultTentType_IsMultiPurpose()
    {
        var tent = new Tent();
        Assert.Equal(TentType.MultiPurpose, tent.TentType);
    }

    [Fact]
    public void Tent_SensorList_DefaultsToEmpty()
    {
        var tent = new Tent();
        Assert.Empty(tent.Sensors);
    }

    [Fact]
    public void Tent_Co2Available_DefaultsToFalse()
    {
        var tent = new Tent();
        Assert.False(tent.Co2Available);
    }
}
```

- [ ] **Step 2: Tests ausführen**

```bash
cd "D:/Grow Operation System new" && dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj --filter "FullyQualifiedName~TentTests" -v minimal 2>&1 | tail -15
```

Erwartet: 3 Tests grün.

---

## Task 9: Tests — TentRepository

**Files:**
- Create: `GrowDiary.Web.Tests/Infrastructure/TentRepositoryTests.cs`

- [ ] **Step 1: Tests erstellen**

```csharp
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GrowDiary.Web.Tests.Infrastructure;

public sealed class TentRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppPaths _paths;

    public TentRepositoryTests()
    {
        // AppPaths reads DatabasePath from env var GROWDIARY_DB_PATH
        _dbPath = Path.Combine(Path.GetTempPath(), $"grow-test-{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", _dbPath);
        _paths = new AppPaths(Path.GetTempPath());
        var initializer = new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance);
        initializer.Initialize();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        File.Delete(_dbPath);
    }

    private GrowRepository Repo() => new(_paths);

    [Fact]
    public void DefaultTent_IsCreatedOnFirstStart()
    {
        var tents = Repo().GetTents();
        Assert.Single(tents);
        Assert.Equal("Hauptzelt", tents[0].Name);
        Assert.Equal(TentType.MultiPurpose, tents[0].TentType);
    }

    [Fact]
    public void CreateTent_PersistsAllFields()
    {
        var repo = Repo();
        var created = repo.CreateTent("Testzelt");
        Assert.True(created.Id > 0);
        Assert.Equal("Testzelt", created.Name);

        var loaded = repo.GetTent(created.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Testzelt", loaded!.Name);
        Assert.Equal(TentType.MultiPurpose, loaded.TentType);
    }

    [Fact]
    public void GetTent_LoadsSensorsCorrectly()
    {
        var repo = Repo();
        var tent = repo.GetTents().First();

        repo.AddTentSensor(new TentSensor
        {
            TentId     = tent.Id,
            MetricType = SensorMetricType.AirTemperature,
            HaEntityId = "sensor.temp_test",
            IsActive   = true
        });

        var loaded = repo.GetTent(tent.Id);
        Assert.NotNull(loaded);
        Assert.Single(loaded!.Sensors);
        Assert.Equal(SensorMetricType.AirTemperature, loaded.Sensors[0].MetricType);
        Assert.Equal("sensor.temp_test", loaded.Sensors[0].HaEntityId);
    }

    [Fact]
    public void AddTentSensor_PersistsCorrectly()
    {
        var repo = Repo();
        var tent = repo.GetTents().First();

        var sensor = repo.AddTentSensor(new TentSensor
        {
            TentId       = tent.Id,
            MetricType   = SensorMetricType.Humidity,
            HaEntityId   = "sensor.humidity_test",
            DisplayLabel = "Luftfeuchte Haupt",
            IsActive     = true
        });

        Assert.True(sensor.Id > 0);
        var sensors = repo.GetTentSensors(tent.Id);
        Assert.Single(sensors);
        Assert.Equal("sensor.humidity_test", sensors[0].HaEntityId);
        Assert.Equal("Luftfeuchte Haupt", sensors[0].DisplayLabel);
    }

    [Fact]
    public void UpdateTentSensor_UpdatesValue()
    {
        var repo = Repo();
        var tent = repo.GetTents().First();

        var sensor = repo.AddTentSensor(new TentSensor
        {
            TentId     = tent.Id,
            MetricType = SensorMetricType.Co2,
            HaEntityId = "sensor.co2_old",
            IsActive   = true
        });

        sensor.HaEntityId = "sensor.co2_new";
        repo.UpdateTentSensor(sensor);

        var sensors = repo.GetTentSensors(tent.Id);
        Assert.Equal("sensor.co2_new", sensors[0].HaEntityId);
    }

    [Fact]
    public void DeleteTentSensor_RemovesEntry()
    {
        var repo = Repo();
        var tent = repo.GetTents().First();

        var sensor = repo.AddTentSensor(new TentSensor
        {
            TentId     = tent.Id,
            MetricType = SensorMetricType.Vpd,
            HaEntityId = "sensor.vpd_test",
            IsActive   = true
        });

        repo.DeleteTentSensor(sensor.Id);
        var sensors = repo.GetTentSensors(tent.Id);
        Assert.Empty(sensors);
    }

    [Fact]
    public void GetTentSensorByMetric_ReturnsNullWhenMissing()
    {
        var repo = Repo();
        var tent = repo.GetTents().First();

        var result = repo.GetTentSensorByMetric(tent.Id, SensorMetricType.ReservoirPh);
        Assert.Null(result);
    }

    [Fact]
    public void GetTentSensorByMetric_ReturnsCorrectEntry()
    {
        var repo = Repo();
        var tent = repo.GetTents().First();

        repo.AddTentSensor(new TentSensor
        {
            TentId     = tent.Id,
            MetricType = SensorMetricType.ReservoirEc,
            HaEntityId = "sensor.ec_test",
            IsActive   = true
        });

        var result = repo.GetTentSensorByMetric(tent.Id, SensorMetricType.ReservoirEc);
        Assert.NotNull(result);
        Assert.Equal("sensor.ec_test", result!.HaEntityId);
    }

    [Fact]
    public void DeleteTent_CascadeDeletesSensors()
    {
        var repo = Repo();
        var tent = repo.CreateTent("ZeltZumLöschen");

        repo.AddTentSensor(new TentSensor
        {
            TentId     = tent.Id,
            MetricType = SensorMetricType.AirTemperature,
            HaEntityId = "sensor.temp_delete_test",
            IsActive   = true
        });

        repo.DeleteTent(tent.Id);

        // Sensors müssen durch CASCADE gelöscht sein
        var sensors = repo.GetTentSensors(tent.Id);
        Assert.Empty(sensors);
    }
}
```

**Hinweis:** `AppPaths` muss einen Konstruktor haben der `databasePath`, `contentRootPath` und `uploadRootPath` direkt akzeptiert. Falls der bestehende Konstruktor anders aussieht, den Test entsprechend anpassen.

- [ ] **Step 2: AppPaths-Konstruktor prüfen**

```bash
cat "D:/Grow Operation System new/GrowDiary.Web/Infrastructure/AppPaths.cs"
```

Falls `AppPaths` keinen parametrisierten Konstruktor hat sondern Environment-Variablen liest: einen Test-Helper erstellen oder die Tests direkt auf dem echten AppPaths aufbauen.

- [ ] **Step 3: Tests ausführen**

```bash
cd "D:/Grow Operation System new" && dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj --filter "FullyQualifiedName~TentRepositoryTests" -v normal 2>&1 | tail -30
```

Erwartet: 9 Tests grün.

---

## Task 10: Finaler Build- und Test-Check

- [ ] **Step 1: Vollständiger Build**

```bash
cd "D:/Grow Operation System new" && dotnet build 2>&1 | grep -E "error|warning|Build succeeded|FAILED|Error"
```

Erwartet: `Build succeeded` mit 0 Errors.

- [ ] **Step 2: Alle Tests ausführen**

```bash
cd "D:/Grow Operation System new" && dotnet test 2>&1 | tail -30
```

Erwartet:
- `TentTests` (3 Tests): grün
- `TentRepositoryTests` (9 Tests): grün
- Andere Tests: dokumentieren welche rot sind und warum (typischerweise Tests die HA-Integration oder alte Tent-Felder testen)

- [ ] **Step 3: Zusammenfassung schreiben**

Folgendes dokumentieren:
1. Schema-Änderungen (neue Tents-Tabelle, neue TentSensors-Tabelle)
2. Liste der angelegten/geänderten/gelöschten Files
3. Test-Output (grün/rot mit Begründung)
4. Liste der Stub-Files in Services/Controllers für B1b
5. Etwaige Abweichungen vom Plan

---

## Stub-Files Übersicht für B1b

Nach B1a sind folgende Files gestubbt (müssen in B1b fertig implementiert werden):

| File | Was gestubbt wurde |
|---|---|
| `Services/HomeAssistantService.cs` | `GetStatesAsync` gibt leeres Dict zurück |
| `Services/GrowDashboardComposer.cs` | LightCycle/PpfdTarget/EntityId-Guards entfernt |
| `Api/Contracts/TentDto.cs` | Neues Record ohne alte Felder — Sensor-Liste leer bis SettingsApiController angepasst |
| `Api/Mapping/SettingsMapping.cs` | Mapping auf neue Felder, Sensor-Liste aus tent.Sensors |
| `Api/Mapping/RequestMapping.cs` | Mapping von UpdateTentRequest → Tent mit neuen Feldern |
| `Components/Pages/Einstellungen.razor` | Alte Entity-ID-Inputs entfernt |
| `Components/Pages/TentDetail.razor` | LightCycle-Anzeige entfernt |
