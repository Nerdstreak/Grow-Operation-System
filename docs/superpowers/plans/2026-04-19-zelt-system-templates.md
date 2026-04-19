# Zelt & System Templates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Beliebig viele Zelte mit physischem Setup (Abmessungen, Licht, Lüftung, CO₂) anlegen + löschen, wiederverwendbare Hydro-System-Vorlagen als eigenes CRUD-Konzept, Grow-Erstellen mit System-Dropdown das HydroStyle/Mengen automatisch befüllt.

**Architecture:** Reine Datenbankschema + Repository + Razor-Änderungen. Neue Tabelle `GrowSystems`. Tent-Modell bekommt 10 physische Felder via `EnsureColumn`. GrowRun bekommt nullable `SystemId`. Keine neuen Services, keine neuen Razor-Pages — alles in `Einstellungen.razor` und `GrowForm.razor`.

**Tech Stack:** Blazor Server (.NET 8), SQLite via raw ADO.NET (`Microsoft.Data.Sqlite`), `EnsureColumn` für additive Schema-Migrationen, keine ORM, kein Test-Framework.

---

### Task 1: Schema-Migration + neue Models

**Files:**
- Modify: `GrowDiary.Web/Infrastructure/DatabaseInitializer.cs:249` (nach letztem EnsureColumn-Block)
- Modify: `GrowDiary.Web/Models/Tent.cs`
- Modify: `GrowDiary.Web/Models/GrowRun.cs`
- Create: `GrowDiary.Web/Models/GrowSystem.cs`

- [ ] **Step 1: `GrowSystem.cs` anlegen**

```csharp
namespace GrowDiary.Web.Models;

public sealed class GrowSystem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string HydroStyle { get; set; } = string.Empty;
    public int? PotCount { get; set; }
    public double? PotSizeLiters { get; set; }
    public double? ReservoirLiters { get; set; }
    public string? Notes { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
```

Speichern unter: `GrowDiary.Web/Models/GrowSystem.cs`

- [ ] **Step 2: `Tent.cs` — 10 physische Felder ergänzen**

Nach der bestehenden Property `public string? PpfdTarget { get; set; }` (Zeile 26) folgende Properties einfügen:

```csharp
    // Physisches Setup
    public int? WidthCm { get; set; }
    public int? DepthCm { get; set; }
    public int? HeightCm { get; set; }
    public string? LightType { get; set; }
    public int? LightWatt { get; set; }
    public int? ExhaustFanCount { get; set; }
    public int? ExhaustM3h { get; set; }
    public int? CirculationFanCount { get; set; }
    public string? Co2Type { get; set; }
    public int? Co2TargetPpm { get; set; }
```

- [ ] **Step 3: `GrowRun.cs` — `SystemId` ergänzen**

Nach der bestehenden Property `public int? TentId { get; set; }` (Zeile 6) einfügen:

```csharp
    public int? SystemId { get; set; }
```

- [ ] **Step 4: `DatabaseInitializer.cs` — Schema-Migration**

In `EnsureSchema()`, nach dem letzten `EnsureColumn`-Aufruf (aktuell `EnsureColumn(connection, "Grows", "RootedAt", "TEXT NULL");`), folgendes einfügen:

```csharp
        // Group D — Zelt physisches Setup
        EnsureColumn(connection, "Tents", "WidthCm",             "INTEGER NULL");
        EnsureColumn(connection, "Tents", "DepthCm",             "INTEGER NULL");
        EnsureColumn(connection, "Tents", "HeightCm",            "INTEGER NULL");
        EnsureColumn(connection, "Tents", "LightType",           "TEXT NULL");
        EnsureColumn(connection, "Tents", "LightWatt",           "INTEGER NULL");
        EnsureColumn(connection, "Tents", "ExhaustFanCount",     "INTEGER NULL");
        EnsureColumn(connection, "Tents", "ExhaustM3h",          "INTEGER NULL");
        EnsureColumn(connection, "Tents", "CirculationFanCount", "INTEGER NULL");
        EnsureColumn(connection, "Tents", "Co2Type",             "TEXT NULL");
        EnsureColumn(connection, "Tents", "Co2TargetPpm",        "INTEGER NULL");
        // Group D — GrowSystems
        EnsureColumn(connection, "Grows", "SystemId", "INTEGER NULL");
```

Direkt darunter (noch in `EnsureSchema()`), den CREATE TABLE für GrowSystems hinzufügen. Dafür nach dem letzten `command.ExecuteNonQuery()` (Ende des großen CREATE TABLE-Blocks, Zeile ~247) im `EnsureSchema`-Body einen zweiten Command-Block hinzufügen. Einfachste Umsetzung: Direkt nach dem `EnsureColumn`-Block:

```csharp
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS GrowSystems (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                Name            TEXT    NOT NULL,
                HydroStyle      TEXT    NOT NULL,
                PotCount        INTEGER NULL,
                PotSizeLiters   REAL    NULL,
                ReservoirLiters REAL    NULL,
                Notes           TEXT    NULL,
                DisplayOrder    INTEGER NOT NULL DEFAULT 99,
                CreatedAtUtc    TEXT    NOT NULL
            );
        """;
        command.ExecuteNonQuery();
```

- [ ] **Step 5: Build prüfen**

```bash
cd "D:/Grow Operation System new" && dotnet build GrowDiary.slnx -m:1 -v:minimal
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 6: Commit**

```bash
git add GrowDiary.Web/Models/GrowSystem.cs GrowDiary.Web/Models/Tent.cs GrowDiary.Web/Models/GrowRun.cs GrowDiary.Web/Infrastructure/DatabaseInitializer.cs
git commit -m "feat: Schema + Models — GrowSystems, Tent physische Felder, GrowRun.SystemId"
```

---

### Task 2: Repository — GrowSystems CRUD

**Files:**
- Modify: `GrowDiary.Web/Infrastructure/GrowRepository.cs`

- [ ] **Step 1: `GetSystems()` hinzufügen**

In `GrowRepository.cs`, nach der `CreateTent`-Methode (Zeile ~181) einfügen:

```csharp
    public List<GrowSystem> GetSystems()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM GrowSystems ORDER BY DisplayOrder, Name;
        """;
        var list = new List<GrowSystem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            list.Add(MapGrowSystem(reader));
        return list;
    }

    public GrowSystem? GetSystem(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM GrowSystems WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapGrowSystem(reader) : null;
    }

    public GrowSystem CreateSystem(GrowSystem system)
    {
        system.CreatedAtUtc = DateTime.UtcNow;
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO GrowSystems (Name, HydroStyle, PotCount, PotSizeLiters, ReservoirLiters, Notes, DisplayOrder, CreatedAtUtc)
            VALUES ($name, $hydroStyle, $potCount, $potSizeLiters, $reservoirLiters, $notes, $displayOrder, $createdAtUtc);
            SELECT last_insert_rowid();
        """;
        AddGrowSystemParameters(command, system);
        system.Id = Convert.ToInt32((long)command.ExecuteScalar()!);
        return system;
    }

    public void UpdateSystem(GrowSystem system)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE GrowSystems SET
                Name            = $name,
                HydroStyle      = $hydroStyle,
                PotCount        = $potCount,
                PotSizeLiters   = $potSizeLiters,
                ReservoirLiters = $reservoirLiters,
                Notes           = $notes,
                DisplayOrder    = $displayOrder
            WHERE Id = $id;
        """;
        AddGrowSystemParameters(command, system);
        command.Parameters.AddWithValue("$id", system.Id);
        command.ExecuteNonQuery();
    }

    public void DeleteSystem(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM GrowSystems WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }
```

- [ ] **Step 2: `MapGrowSystem` private Hilfsmethode hinzufügen**

In `GrowRepository.cs`, nach `AddTentParameters` (Zeile ~1043), vor oder nach dem anderen Map-Methoden:

```csharp
    private static GrowSystem MapGrowSystem(SqliteDataReader reader)
    {
        return new GrowSystem
        {
            Id              = Convert.ToInt32((long)reader["Id"]),
            Name            = reader["Name"]?.ToString() ?? string.Empty,
            HydroStyle      = reader["HydroStyle"]?.ToString() ?? string.Empty,
            PotCount        = reader["PotCount"] is DBNull or null ? null : Convert.ToInt32(reader["PotCount"], CultureInfo.InvariantCulture),
            PotSizeLiters   = reader["PotSizeLiters"] is DBNull or null ? null : Convert.ToDouble(reader["PotSizeLiters"], CultureInfo.InvariantCulture),
            ReservoirLiters = reader["ReservoirLiters"] is DBNull or null ? null : Convert.ToDouble(reader["ReservoirLiters"], CultureInfo.InvariantCulture),
            Notes           = NullString(reader["Notes"]),
            DisplayOrder    = Convert.ToInt32(reader["DisplayOrder"], CultureInfo.InvariantCulture),
            CreatedAtUtc    = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static void AddGrowSystemParameters(SqliteCommand command, GrowSystem system)
    {
        command.Parameters.AddWithValue("$name", system.Name);
        command.Parameters.AddWithValue("$hydroStyle", system.HydroStyle);
        command.Parameters.AddWithValue("$potCount", (object?)system.PotCount ?? DBNull.Value);
        command.Parameters.AddWithValue("$potSizeLiters", (object?)system.PotSizeLiters ?? DBNull.Value);
        command.Parameters.AddWithValue("$reservoirLiters", (object?)system.ReservoirLiters ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)system.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$displayOrder", system.DisplayOrder);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(system.CreatedAtUtc));
    }
```

- [ ] **Step 3: Build prüfen**

```bash
cd "D:/Grow Operation System new" && dotnet build GrowDiary.slnx -m:1 -v:minimal
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 4: Commit**

```bash
git add GrowDiary.Web/Infrastructure/GrowRepository.cs
git commit -m "feat: GrowRepository — GrowSystems CRUD + MapGrowSystem"
```

---

### Task 3: Repository — Tent physische Felder + DeleteTent

**Files:**
- Modify: `GrowDiary.Web/Infrastructure/GrowRepository.cs`

- [ ] **Step 1: `DeleteTent` Methode hinzufügen**

Nach `UpdateTent` (Zeile ~167), vor `CreateTent`:

```csharp
    public void DeleteTent(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Tents WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }
```

- [ ] **Step 2: `UpdateTent` — neue Felder ins UPDATE-Statement**

Den bestehenden `UpdateTent`-SQL-String (Zeile ~140–162) ersetzen durch:

```csharp
        command.CommandText = """
            UPDATE Tents SET
                Name = $name,
                Kind = $kind,
                Notes = $notes,
                DisplayOrder = $displayOrder,
                AccentColor = $accentColor,
                TemperatureEntityId = $temperatureEntityId,
                HumidityEntityId = $humidityEntityId,
                VpdEntityId = $vpdEntityId,
                ReservoirPhEntityId = $reservoirPhEntityId,
                ReservoirEcEntityId = $reservoirEcEntityId,
                ReservoirLevelEntityId = $reservoirLevelEntityId,
                ReservoirTempEntityId = $reservoirTempEntityId,
                OrpEntityId = $orpEntityId,
                DissolvedOxygenEntityId = $dissolvedOxygenEntityId,
                Co2EntityId = $co2EntityId,
                LightEntityId = $lightEntityId,
                CameraEntityId = $cameraEntityId,
                LightCycle = $lightCycle,
                PpfdEntityId = $ppfdEntityId,
                PpfdTarget = $ppfdTarget,
                WidthCm = $widthCm,
                DepthCm = $depthCm,
                HeightCm = $heightCm,
                LightType = $lightType,
                LightWatt = $lightWatt,
                ExhaustFanCount = $exhaustFanCount,
                ExhaustM3h = $exhaustM3h,
                CirculationFanCount = $circulationFanCount,
                Co2Type = $co2Type,
                Co2TargetPpm = $co2TargetPpm
            WHERE Id = $id;
        """;
```

- [ ] **Step 3: `AddTentParameters` — neue Felder ergänzen**

In `AddTentParameters` (Zeile ~1021), nach dem letzten `command.Parameters.AddWithValue("$ppfdTarget", ...)` folgendes hinzufügen:

```csharp
        command.Parameters.AddWithValue("$widthCm", (object?)tent.WidthCm ?? DBNull.Value);
        command.Parameters.AddWithValue("$depthCm", (object?)tent.DepthCm ?? DBNull.Value);
        command.Parameters.AddWithValue("$heightCm", (object?)tent.HeightCm ?? DBNull.Value);
        command.Parameters.AddWithValue("$lightType", (object?)tent.LightType ?? DBNull.Value);
        command.Parameters.AddWithValue("$lightWatt", (object?)tent.LightWatt ?? DBNull.Value);
        command.Parameters.AddWithValue("$exhaustFanCount", (object?)tent.ExhaustFanCount ?? DBNull.Value);
        command.Parameters.AddWithValue("$exhaustM3h", (object?)tent.ExhaustM3h ?? DBNull.Value);
        command.Parameters.AddWithValue("$circulationFanCount", (object?)tent.CirculationFanCount ?? DBNull.Value);
        command.Parameters.AddWithValue("$co2Type", (object?)tent.Co2Type ?? DBNull.Value);
        command.Parameters.AddWithValue("$co2TargetPpm", (object?)tent.Co2TargetPpm ?? DBNull.Value);
```

- [ ] **Step 4: `MapTent` — neue Felder lesen**

In `MapTent` (Zeile ~883), nach `PpfdTarget = NullString(reader["PpfdTarget"]),` folgendes hinzufügen:

```csharp
            WidthCm             = reader["WidthCm"] is DBNull or null ? null : Convert.ToInt32(reader["WidthCm"], CultureInfo.InvariantCulture),
            DepthCm             = reader["DepthCm"] is DBNull or null ? null : Convert.ToInt32(reader["DepthCm"], CultureInfo.InvariantCulture),
            HeightCm            = reader["HeightCm"] is DBNull or null ? null : Convert.ToInt32(reader["HeightCm"], CultureInfo.InvariantCulture),
            LightType           = NullString(reader["LightType"]),
            LightWatt           = reader["LightWatt"] is DBNull or null ? null : Convert.ToInt32(reader["LightWatt"], CultureInfo.InvariantCulture),
            ExhaustFanCount     = reader["ExhaustFanCount"] is DBNull or null ? null : Convert.ToInt32(reader["ExhaustFanCount"], CultureInfo.InvariantCulture),
            ExhaustM3h          = reader["ExhaustM3h"] is DBNull or null ? null : Convert.ToInt32(reader["ExhaustM3h"], CultureInfo.InvariantCulture),
            CirculationFanCount = reader["CirculationFanCount"] is DBNull or null ? null : Convert.ToInt32(reader["CirculationFanCount"], CultureInfo.InvariantCulture),
            Co2Type             = NullString(reader["Co2Type"]),
            Co2TargetPpm        = reader["Co2TargetPpm"] is DBNull or null ? null : Convert.ToInt32(reader["Co2TargetPpm"], CultureInfo.InvariantCulture),
```

- [ ] **Step 5: Build prüfen**

```bash
cd "D:/Grow Operation System new" && dotnet build GrowDiary.slnx -m:1 -v:minimal
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 6: Commit**

```bash
git add GrowDiary.Web/Infrastructure/GrowRepository.cs
git commit -m "feat: GrowRepository — DeleteTent, Tent physische Felder in MapTent/UpdateTent/AddTentParameters"
```

---

### Task 4: Repository — GrowRun.SystemId persistieren

**Files:**
- Modify: `GrowDiary.Web/Infrastructure/GrowRepository.cs`

- [ ] **Step 1: `CreateGrow` — SystemId in INSERT**

In `CreateGrow` (Zeile ~249), das INSERT-Statement erweitern. Die Spalten-Liste ergänzen mit `SystemId` und die Values-Liste mit `$systemId`:

```csharp
        command.CommandText = """
            INSERT INTO Grows
            (
                TentId, SystemId, Name, Strain, Breeder, Status, MediumType, FeedingStyle, HydroStyle, MediumDetail,
                Environment, Light, ContainerSize, ReservoirSize, IrrigationStyle, IrrigationType, WaterSource,
                SeedType, StartMaterial, GerminationMethod, CloneSource, CloneIsRooted,
                BreederFlowerWeeksMin, BreederFlowerWeeksMax, PlantCount, PhenoNumber,
                PropagationMedium, HasChiller, EntryPoint, DaysAlreadyInPhase,
                AutoflowerDaysSinceGermination, FlipDate, GerminatedAt, RootedAt,
                Nutrients, Notes, StartDate, EndDate, CreatedAtUtc, UpdatedAtUtc
            )
            VALUES
            (
                $tentId, $systemId, $name, $strain, $breeder, $status, $mediumType, $feedingStyle, $hydroStyle, $mediumDetail,
                $environment, $light, $containerSize, $reservoirSize, $irrigationStyle, $irrigationType, $waterSource,
                $seedType, $startMaterial, $germinationMethod, $cloneSource, $cloneIsRooted,
                $breederFlowerWeeksMin, $breederFlowerWeeksMax, $plantCount, $phenoNumber,
                $propagationMedium, $hasChiller, $entryPoint, $daysAlreadyInPhase,
                $autoflowerDaysSinceGermination, $flipDate, $germinatedAt, $rootedAt,
                $nutrients, $notes, $startDate, $endDate, $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
```

- [ ] **Step 2: `UpdateGrow` — SystemId in UPDATE**

In `UpdateGrow` (Zeile ~282), nach `TentId = $tentId,` hinzufügen: `SystemId = $systemId,`

- [ ] **Step 3: `AddGrowParameters` — $systemId ergänzen**

In `AddGrowParameters` (Zeile ~978), nach der Zeile `command.Parameters.AddWithValue("$tentId", ...)` einfügen:

```csharp
        command.Parameters.AddWithValue("$systemId", (object?)grow.SystemId ?? DBNull.Value);
```

- [ ] **Step 4: `MapGrow` — SystemId lesen**

In `MapGrow` (Zeile ~833), nach `TentId = reader["TentId"] is DBNull ? null : Convert.ToInt32(...)` hinzufügen:

```csharp
            SystemId = reader["SystemId"] is DBNull or null ? null : Convert.ToInt32((long)reader["SystemId"]),
```

- [ ] **Step 5: Build prüfen**

```bash
cd "D:/Grow Operation System new" && dotnet build GrowDiary.slnx -m:1 -v:minimal
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 6: Commit**

```bash
git add GrowDiary.Web/Infrastructure/GrowRepository.cs
git commit -m "feat: GrowRepository — GrowRun.SystemId in CreateGrow/UpdateGrow/MapGrow"
```

---

### Task 5: Einstellungen.razor — Zelt Create/Delete + physische Felder + Systeme-Sektion

**Files:**
- Modify: `GrowDiary.Web/Components/Pages/Einstellungen.razor`

- [ ] **Step 1: Code-Block — `_systems` und `_newTentName` ergänzen**

Im `@code`-Block (ab Zeile ~201) folgende Felder hinzufügen:

```csharp
    private List<GrowSystem> _systems = new();
    private string _newTentName = string.Empty;
    private bool _showNewTentForm;
    private GrowSystem _newSystem = new();
    private bool _showNewSystemForm;
    private HashSet<int> _systemSaved = new();
```

- [ ] **Step 2: `OnInitializedAsync` — Systeme laden**

`OnInitializedAsync` (aktuell `protected override Task OnInitializedAsync`) ersetzen durch:

```csharp
    protected override Task OnInitializedAsync()
    {
        _ha      = GrowRepo.GetHomeAssistantSettings();
        _tents   = GrowRepo.GetTents();
        _systems = GrowRepo.GetSystems();
        _stats   = GrowRepo.GetDashboardStats();
        return Task.CompletedTask;
    }
```

- [ ] **Step 3: `CreateTent` und `DeleteTent` Methoden hinzufügen**

Im `@code`-Block, nach `SaveTent`:

```csharp
    private void AddTent()
    {
        if (string.IsNullOrWhiteSpace(_newTentName)) return;
        var tent = GrowRepo.CreateTent(_newTentName.Trim());
        _tents.Add(tent);
        _newTentName = string.Empty;
        _showNewTentForm = false;
    }

    private void DeleteTent(Tent tent)
    {
        if (tent.ActiveGrowCount > 0) return;
        GrowRepo.DeleteTent(tent.Id);
        _tents.Remove(tent);
        _tentSaved.Remove(tent.Id);
    }
```

- [ ] **Step 4: System-CRUD Methoden hinzufügen**

```csharp
    private void SaveSystem(GrowSystem system)
    {
        GrowRepo.UpdateSystem(system);
        _systemSaved.Add(system.Id);
    }

    private void AddSystem()
    {
        if (string.IsNullOrWhiteSpace(_newSystem.Name)) return;
        _newSystem.HydroStyle = string.IsNullOrWhiteSpace(_newSystem.HydroStyle) ? "RDWC" : _newSystem.HydroStyle;
        var created = GrowRepo.CreateSystem(_newSystem);
        _systems.Add(created);
        _newSystem = new GrowSystem();
        _showNewSystemForm = false;
    }

    private void DeleteSystem(GrowSystem system)
    {
        GrowRepo.DeleteSystem(system.Id);
        _systems.Remove(system);
        _systemSaved.Remove(system.Id);
    }
```

- [ ] **Step 5: Zelte-Sektion in Razor ersetzen**

Den bestehenden `@* Zelte *@`-Block (Zeilen ~51–145) vollständig ersetzen durch:

```razor
        @* Zelte *@
        <div>
            <div class="section-label" style="display:flex; justify-content:space-between; align-items:center">
                <span>Zelte</span>
                <button class="btn" style="font-size:11px; padding:4px 10px" @onclick="() => _showNewTentForm = !_showNewTentForm">+ Neues Zelt</button>
            </div>

            @if (_showNewTentForm)
            {
                <div class="panel-card" style="margin-bottom:10px">
                    <div style="padding:14px; display:flex; gap:8px; align-items:flex-end">
                        <div class="field" style="flex:1">
                            <label>Name des neuen Zelts</label>
                            <input @bind="_newTentName" placeholder="z. B. Sideroom Tent" />
                        </div>
                        <button class="btn btn-primary" @onclick="AddTent">Anlegen</button>
                        <button class="btn" @onclick="() => _showNewTentForm = false">Abbrechen</button>
                    </div>
                </div>
            }

            @foreach (var tent in _tents)
            {
                var t = tent;
                <div class="panel-card" style="margin-bottom:12px">
                    <div class="panel-card-header">
                        <span class="panel-card-title">@t.Name</span>
                        <div style="display:flex; align-items:center; gap:10px">
                            <span class="panel-card-count">@t.ActiveGrowCount aktive Runs</span>
                            @if (t.ActiveGrowCount == 0)
                            {
                                <button class="btn" style="font-size:11px; padding:3px 8px; color:var(--red); border-color:var(--red)" @onclick="() => DeleteTent(t)">Löschen</button>
                            }
                        </div>
                    </div>
                    <div style="padding:16px 14px; display:grid; gap:16px">

                        @* Abmessungen *@
                        <div>
                            <div class="section-label" style="font-size:9px; margin-bottom:8px">Abmessungen</div>
                            <div class="two-col-grid">
                                <div class="field">
                                    <label>Name</label>
                                    <input @bind="t.Name" />
                                </div>
                                <div class="field">
                                    <label>Breite (cm)</label>
                                    <input type="number" @bind="t.WidthCm" placeholder="120" />
                                </div>
                                <div class="field">
                                    <label>Tiefe (cm)</label>
                                    <input type="number" @bind="t.DepthCm" placeholder="120" />
                                </div>
                                <div class="field">
                                    <label>Höhe (cm)</label>
                                    <input type="number" @bind="t.HeightCm" placeholder="200" />
                                </div>
                            </div>
                        </div>

                        @* Beleuchtung *@
                        <div>
                            <div class="section-label" style="font-size:9px; margin-bottom:8px">Beleuchtung</div>
                            <div class="two-col-grid">
                                <div class="field">
                                    <label>Lichttyp</label>
                                    <select @bind="t.LightType">
                                        <option value="">— wählen —</option>
                                        <option value="LED">LED</option>
                                        <option value="HPS">HPS</option>
                                        <option value="CMH">CMH / LEC</option>
                                        <option value="T5">T5 / CFL</option>
                                    </select>
                                </div>
                                <div class="field">
                                    <label>Lichtstärke (Watt)</label>
                                    <input type="number" @bind="t.LightWatt" placeholder="630" />
                                </div>
                                <div class="field">
                                    <label>Lichtzyklus</label>
                                    <input @bind="t.LightCycle" placeholder="18/6 oder 12/12" />
                                </div>
                                <div class="field">
                                    <label>PPFD Zielwert</label>
                                    <input @bind="t.PpfdTarget" placeholder="650" />
                                </div>
                            </div>
                        </div>

                        @* Lüftung *@
                        <div>
                            <div class="section-label" style="font-size:9px; margin-bottom:8px">Lüftung</div>
                            <div class="two-col-grid">
                                <div class="field">
                                    <label>Abluft-Lüfter (Anzahl)</label>
                                    <input type="number" @bind="t.ExhaustFanCount" placeholder="1" />
                                </div>
                                <div class="field">
                                    <label>Abluftleistung (m³/h)</label>
                                    <input type="number" @bind="t.ExhaustM3h" placeholder="300" />
                                </div>
                                <div class="field">
                                    <label>Umluft-Lüfter (Anzahl)</label>
                                    <input type="number" @bind="t.CirculationFanCount" placeholder="2" />
                                </div>
                            </div>
                        </div>

                        @* CO₂ *@
                        <div>
                            <div class="section-label" style="font-size:9px; margin-bottom:8px">CO₂</div>
                            <div class="two-col-grid">
                                <div class="field">
                                    <label>CO₂-Anlage</label>
                                    <select @bind="t.Co2Type">
                                        <option value="">Keine</option>
                                        <option value="Flasche">CO₂-Flasche</option>
                                        <option value="Generator">CO₂-Generator</option>
                                    </select>
                                </div>
                                <div class="field">
                                    <label>CO₂-Zielwert (ppm)</label>
                                    <input type="number" @bind="t.Co2TargetPpm" placeholder="1200" />
                                </div>
                            </div>
                        </div>

                        @* HA Entities *@
                        <div>
                            <div class="section-label" style="font-size:9px; margin-bottom:8px">Home Assistant Entities</div>
                            <div class="two-col-grid">
                                <div class="field">
                                    <label>Temperatur Entity</label>
                                    <input @bind="t.TemperatureEntityId" placeholder="sensor.ac_infinity_temperature" />
                                </div>
                                <div class="field">
                                    <label>Luftfeuchte Entity</label>
                                    <input @bind="t.HumidityEntityId" placeholder="sensor.ac_infinity_humidity" />
                                </div>
                                <div class="field">
                                    <label>VPD Entity</label>
                                    <input @bind="t.VpdEntityId" placeholder="sensor.ac_infinity_vpd" />
                                </div>
                                <div class="field">
                                    <label>Licht / Status Entity</label>
                                    <input @bind="t.LightEntityId" placeholder="switch.ac_infinity_light" />
                                </div>
                                <div class="field">
                                    <label>PPFD Entity</label>
                                    <input @bind="t.PpfdEntityId" placeholder="sensor.hauptzelt_ppfd" />
                                </div>
                                <div class="field">
                                    <label>Reservoir pH Entity</label>
                                    <input @bind="t.ReservoirPhEntityId" placeholder="sensor.rdwc_ph" />
                                </div>
                                <div class="field">
                                    <label>Reservoir EC Entity</label>
                                    <input @bind="t.ReservoirEcEntityId" placeholder="sensor.rdwc_ec" />
                                </div>
                                <div class="field">
                                    <label>Wasserstand Entity</label>
                                    <input @bind="t.ReservoirLevelEntityId" placeholder="sensor.rdwc_level_liters" />
                                </div>
                                <div class="field">
                                    <label>Wassertemperatur Entity</label>
                                    <input @bind="t.ReservoirTempEntityId" placeholder="sensor.rdwc_water_temp" />
                                </div>
                                <div class="field">
                                    <label>ORP Sensor</label>
                                    <input @bind="t.OrpEntityId" placeholder="sensor.rdwc_orp" />
                                </div>
                                <div class="field">
                                    <label>Gelöster Sauerstoff</label>
                                    <input @bind="t.DissolvedOxygenEntityId" placeholder="sensor.rdwc_do" />
                                </div>
                                <div class="field">
                                    <label>CO₂ ppm Entity</label>
                                    <input @bind="t.Co2EntityId" placeholder="sensor.hauptzelt_co2" />
                                </div>
                                <div class="field">
                                    <label>Kamera Entity</label>
                                    <input @bind="t.CameraEntityId" placeholder="camera.hauptzelt_overview" />
                                </div>
                            </div>
                        </div>

                        <div class="field">
                            <label>Notizen</label>
                            <textarea rows="3" @bind="t.Notes"></textarea>
                        </div>

                        @if (_tentSaved.Contains(t.Id))
                        {
                            <div style="font-size:12px; color:var(--green); padding:6px 10px; background:oklch(74% 0.19 145 / 0.1); border-radius:6px; border:1px solid oklch(74% 0.19 145 / 0.25)">@t.Name gespeichert.</div>
                        }
                        <button class="btn" style="width:fit-content" @onclick='() => SaveTent(t)'>@t.Name speichern</button>
                    </div>
                </div>
            }
        </div>
```

- [ ] **Step 6: Systeme-Sektion in Razor einfügen**

Vor dem `@* Datenbank *@`-Block (Zeile ~147) folgende neue Sektion einfügen:

```razor
        @* Hydro-Systeme *@
        <div>
            <div class="section-label" style="display:flex; justify-content:space-between; align-items:center">
                <span>Hydro-Systeme</span>
                <button class="btn" style="font-size:11px; padding:4px 10px" @onclick="() => _showNewSystemForm = !_showNewSystemForm">+ Neues System</button>
            </div>

            @if (_showNewSystemForm)
            {
                <div class="panel-card" style="margin-bottom:10px">
                    <div style="padding:14px; display:grid; gap:10px">
                        <div class="two-col-grid">
                            <div class="field">
                                <label>Name</label>
                                <input @bind="_newSystem.Name" placeholder="z. B. RDWC 3-Pot" />
                            </div>
                            <div class="field">
                                <label>Hydro-Typ</label>
                                <select @bind="_newSystem.HydroStyle">
                                    <option value="RDWC">RDWC</option>
                                    <option value="DWC">DWC</option>
                                    <option value="NFT">NFT</option>
                                    <option value="Aero">Aero</option>
                                    <option value="Coco">Coco</option>
                                </select>
                            </div>
                            <div class="field">
                                <label>Pot-Anzahl</label>
                                <input type="number" @bind="_newSystem.PotCount" placeholder="3" />
                            </div>
                            <div class="field">
                                <label>Pot-Größe (L)</label>
                                <input type="number" step="0.5" @bind="_newSystem.PotSizeLiters" placeholder="10" />
                            </div>
                            <div class="field">
                                <label>Reservoir (L)</label>
                                <input type="number" step="0.5" @bind="_newSystem.ReservoirLiters" placeholder="50" />
                            </div>
                            <div class="field">
                                <label>Notizen</label>
                                <input @bind="_newSystem.Notes" placeholder="z. B. Autopot + Airstone" />
                            </div>
                        </div>
                        <div style="display:flex; gap:8px">
                            <button class="btn btn-primary" @onclick="AddSystem">Anlegen</button>
                            <button class="btn" @onclick="() => _showNewSystemForm = false">Abbrechen</button>
                        </div>
                    </div>
                </div>
            }

            @foreach (var sys in _systems)
            {
                var s = sys;
                <div class="panel-card" style="margin-bottom:12px">
                    <div class="panel-card-header">
                        <span class="panel-card-title">@s.Name</span>
                        <div style="display:flex; align-items:center; gap:10px">
                            <span class="panel-card-count">@s.HydroStyle</span>
                            <button class="btn" style="font-size:11px; padding:3px 8px; color:var(--red); border-color:var(--red)" @onclick="() => DeleteSystem(s)">Löschen</button>
                        </div>
                    </div>
                    <div style="padding:16px 14px; display:grid; gap:12px">
                        <div class="two-col-grid">
                            <div class="field">
                                <label>Name</label>
                                <input @bind="s.Name" />
                            </div>
                            <div class="field">
                                <label>Hydro-Typ</label>
                                <select @bind="s.HydroStyle">
                                    <option value="RDWC">RDWC</option>
                                    <option value="DWC">DWC</option>
                                    <option value="NFT">NFT</option>
                                    <option value="Aero">Aero</option>
                                    <option value="Coco">Coco</option>
                                </select>
                            </div>
                            <div class="field">
                                <label>Pot-Anzahl</label>
                                <input type="number" @bind="s.PotCount" />
                            </div>
                            <div class="field">
                                <label>Pot-Größe (L)</label>
                                <input type="number" step="0.5" @bind="s.PotSizeLiters" />
                            </div>
                            <div class="field">
                                <label>Reservoir (L)</label>
                                <input type="number" step="0.5" @bind="s.ReservoirLiters" />
                            </div>
                            <div class="field">
                                <label>Notizen</label>
                                <input @bind="s.Notes" />
                            </div>
                        </div>
                        @if (_systemSaved.Contains(s.Id))
                        {
                            <div style="font-size:12px; color:var(--green); padding:6px 10px; background:oklch(74% 0.19 145 / 0.1); border-radius:6px; border:1px solid oklch(74% 0.19 145 / 0.25)">@s.Name gespeichert.</div>
                        }
                        <button class="btn" style="width:fit-content" @onclick='() => SaveSystem(s)'>@s.Name speichern</button>
                    </div>
                </div>
            }
        </div>
```

- [ ] **Step 7: Build prüfen**

```bash
cd "D:/Grow Operation System new" && dotnet build GrowDiary.slnx -m:1 -v:minimal
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 8: Commit**

```bash
git add GrowDiary.Web/Components/Pages/Einstellungen.razor
git commit -m "feat: Einstellungen — Zelt Create/Delete + physische Felder + Systeme-Sektion"
```

---

### Task 6: GrowForm — System-Dropdown + Auto-Prefill

**Files:**
- Modify: `GrowDiary.Web/ViewModels/GrowFormViewModel.cs`
- Modify: `GrowDiary.Web/Components/Pages/GrowForm.razor`

- [ ] **Step 1: `GrowFormViewModel` — `SystemId` Property ergänzen**

In `GrowFormViewModel.cs` (Zeile 15), nach `public int? TentId { get; set; }` einfügen:

```csharp
    public int? SystemId { get; set; }
```

In `FromGrow(GrowRun grow)` (Zeile ~67), nach `TentId = grow.TentId,` einfügen:

```csharp
            SystemId = grow.SystemId,
```

In `ToGrow()` (Zeile ~121), nach `TentId = TentId,` einfügen:

```csharp
            SystemId = SystemId,
```

- [ ] **Step 2: `GrowForm.razor` — `_systems` laden + `OnSystemChanged` Methode**

Im `@code`-Block (Zeile ~249), nach `private List<GrowTemplate> _templates = new();` einfügen:

```csharp
    private List<GrowSystem> _systems = new();
```

In `OnParametersSetAsync` (Zeile ~263), nach `_tents = GrowRepo.GetTents();` einfügen:

```csharp
        _systems = GrowRepo.GetSystems();
```

Nach der `OnStartMaterialChanged`-Methode (Zeile ~308) folgende Methode hinzufügen:

```csharp
    private void OnSystemChanged(int? systemId)
    {
        _model.SystemId = systemId;
        if (systemId is null) return;
        var sys = _systems.FirstOrDefault(s => s.Id == systemId);
        if (sys is null) return;
        if (!string.IsNullOrEmpty(sys.HydroStyle) && Enum.TryParse<HydroStyle>(sys.HydroStyle, out var hs))
            _model.HydroStyle = hs;
        if (sys.PotCount.HasValue) _model.PlantCount = sys.PotCount;
        if (sys.PotSizeLiters.HasValue) _model.ContainerSize = $"{sys.PotSizeLiters:0.#} L";
        if (sys.ReservoirLiters.HasValue) _model.ReservoirSize = $"{sys.ReservoirLiters:0.#} L";
    }
```

- [ ] **Step 3: `GrowForm.razor` — System-Dropdown ins Razor einfügen**

Im Markup, im "Genetik"-Panel, nach dem bestehenden `<div class="field">` für "Zelt" (Zeile ~62–68), direkt danach einfügen:

```razor
                            <div class="field">
                                <label for="grow-system">System</label>
                                <InputSelect id="grow-system"
                                             TValue="int?"
                                             Value="@_model.SystemId"
                                             ValueChanged="@OnSystemChanged"
                                             ValueExpression="@(() => _model.SystemId)">
                                    <option value="">— kein System —</option>
                                    @foreach (var sys in _systems)
                                    {
                                        <option value="@sys.Id">@sys.Name</option>
                                    }
                                </InputSelect>
                            </div>
```

- [ ] **Step 4: Build prüfen**

```bash
cd "D:/Grow Operation System new" && dotnet build GrowDiary.slnx -m:1 -v:minimal
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 5: Commit**

```bash
git add GrowDiary.Web/ViewModels/GrowFormViewModel.cs GrowDiary.Web/Components/Pages/GrowForm.razor
git commit -m "feat: GrowForm — System-Dropdown mit Auto-Prefill für HydroStyle/Pots/Reservoir"
```
