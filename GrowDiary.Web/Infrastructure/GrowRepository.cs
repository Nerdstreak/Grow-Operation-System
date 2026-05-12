using System.Globalization;
using System.Text.Json;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge.Schema;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class GrowRepository
{
    private readonly AppPaths _paths;

    public GrowRepository(AppPaths paths)
    {
        _paths = paths;
    }

    public DashboardStats GetDashboardStats()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                COUNT(*) AS TotalGrows,
                COALESCE(SUM(CASE WHEN Status IN ('Planning','Running') THEN 1 ELSE 0 END), 0) AS ActiveGrows,
                COALESCE(SUM(CASE WHEN Status IN ('Completed','Aborted') THEN 1 ELSE 0 END), 0) AS ArchivedGrows,
                COALESCE(SUM(CASE WHEN Status = 'Aborted' THEN 1 ELSE 0 END), 0) AS AbortedGrows,
                (SELECT COUNT(*) FROM Measurements) AS Measurements,
                (SELECT COUNT(*) FROM Photos) AS Photos
            FROM Grows;
        """;
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return new DashboardStats();
        return new DashboardStats
        {
            TotalGrows = Convert.ToInt32(reader["TotalGrows"]),
            ActiveGrows = Convert.ToInt32(reader["ActiveGrows"]),
            ArchivedGrows = Convert.ToInt32(reader["ArchivedGrows"]),
            AbortedGrows = Convert.ToInt32(reader["AbortedGrows"]),
            Measurements = Convert.ToInt32(reader["Measurements"]),
            Photos = Convert.ToInt32(reader["Photos"])
        };
    }

    public List<Tent> GetTents()
    {
        using var connection = OpenConnection();
        using var tentCommand = connection.CreateCommand();
        tentCommand.CommandText = """
            SELECT t.*,
                   (SELECT COUNT(*) FROM Grows g WHERE g.TentId = t.Id AND g.Status IN ('Planning','Running')) AS ActiveGrowCount,
                   (SELECT COUNT(*) FROM Grows g WHERE g.TentId = t.Id AND g.Status IN ('Completed','Aborted')) AS ArchivedGrowCount,
                   (SELECT COUNT(*) FROM Setups s WHERE s.TentId = t.Id AND s.Status IN ('Planning','Active')) AS ActiveSetupCount,
                   (SELECT COUNT(*) FROM Setups s WHERE s.TentId = t.Id AND s.Status = 'Archived') AS ArchivedSetupCount
            FROM Tents t
            ORDER BY t.DisplayOrder, t.Name;
        """;

        var tents = new List<Tent>();
        using (var reader = tentCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                tents.Add(MapTent(reader));
            }
        }

        if (tents.Count == 0) return tents;

        var tentPlaceholders = string.Join(", ", tents.Select((_, i) => $"$t{i}"));
        using var growCommand = connection.CreateCommand();
        growCommand.CommandText = $"""
            SELECT g.*, t.Name AS TentName,
                   (SELECT COUNT(*) FROM Measurements m WHERE m.GrowId = g.Id) AS MeasurementCount,
                   (SELECT RelativePath FROM Photos p WHERE p.GrowId = g.Id ORDER BY p.TakenAtUtc DESC LIMIT 1) AS LatestPhotoPath
            FROM Grows g
            LEFT JOIN Tents t ON t.Id = g.TentId
            WHERE g.TentId IN ({tentPlaceholders}) AND g.Status IN ('Planning','Running')
            ORDER BY g.StartDate DESC, g.Id DESC;
        """;
        for (var i = 0; i < tents.Count; i++)
        {
            growCommand.Parameters.AddWithValue($"$t{i}", tents[i].Id);
        }

        var allGrows = new List<GrowRun>();
        using (var growReader = growCommand.ExecuteReader())
        {
            while (growReader.Read())
            {
                allGrows.Add(MapGrow(growReader));
            }
        }

        if (allGrows.Count > 0)
        {
            var latestMeasurements = GetLatestMeasurementsBatch(connection, allGrows.Select(g => g.Id));
            foreach (var grow in allGrows)
            {
                grow.LatestMeasurement = latestMeasurements.GetValueOrDefault(grow.Id);
            }
        }

        var growsByTentId = allGrows
            .Where(g => g.TentId.HasValue)
            .GroupBy(g => g.TentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
        foreach (var tent in tents)
        {
            tent.ActiveGrows = growsByTentId.TryGetValue(tent.Id, out var grows) ? grows : [];
        }

        // Sensors laden
        if (tents.Count > 0)
        {
            var tentIds = tents.Select(t => t.Id).ToList();
            var sensorsByTentId = LoadSensorsByTentIds(connection, tentIds);
            foreach (var tent in tents)
            {
                tent.Sensors = sensorsByTentId.TryGetValue(tent.Id, out var sensors) ? sensors : new();
            }
        }

        return tents;
    }

    public Tent? GetTent(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.*,
                   (SELECT COUNT(*) FROM Grows g WHERE g.TentId = t.Id AND g.Status IN ('Planning','Running')) AS ActiveGrowCount,
                   (SELECT COUNT(*) FROM Grows g WHERE g.TentId = t.Id AND g.Status IN ('Completed','Aborted')) AS ArchivedGrowCount,
                   (SELECT COUNT(*) FROM Setups s WHERE s.TentId = t.Id AND s.Status IN ('Planning','Active')) AS ActiveSetupCount,
                   (SELECT COUNT(*) FROM Setups s WHERE s.TentId = t.Id AND s.Status = 'Archived') AS ArchivedSetupCount
            FROM Tents t
            WHERE t.Id = $id
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var tent = MapTent(reader);
        tent.ActiveGrows = GetActiveGrowsForTent(tent.Id);
        tent.Sensors = GetTentSensors(id);
        return tent;
    }

    public void UpdateTent(Tent tent)
    {
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
                CameraEntityId = $cameraEntityId
            WHERE Id = $id;
        """;
        AddTentParameters(command, tent);
        command.Parameters.AddWithValue("$id", tent.Id);
        command.ExecuteNonQuery();
    }

    public void DeleteTent(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Tents WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public Tent CreateTent(string name)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Tents (Name, Kind, Notes, DisplayOrder, AccentColor, CreatedAtUtc, UpdatedAtUtc)
            VALUES ($name, 'Grow Tent', NULL, 99, '#69b578', datetime('now'), datetime('now'));
            SELECT last_insert_rowid();
        """;
        command.Parameters.AddWithValue("$name", name);
        var id = Convert.ToInt32((long)(command.ExecuteScalar() ?? 0L));
        return new Tent { Id = id, Name = name };
    }

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

    public void ReplaceTentSensors(int tentId, IReadOnlyCollection<TentSensor> sensors)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM TentSensors WHERE TentId = $tentId;";
            deleteCommand.Parameters.AddWithValue("$tentId", tentId);
            deleteCommand.ExecuteNonQuery();
        }

        foreach (var sensor in sensors)
        {
            var stored = new TentSensor
            {
                TentId = tentId,
                MetricType = sensor.MetricType,
                HaEntityId = sensor.HaEntityId,
                DisplayLabel = sensor.DisplayLabel,
                IsActive = sensor.IsActive,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO TentSensors (TentId, MetricType, HaEntityId, DisplayLabel, IsActive, CreatedAtUtc, UpdatedAtUtc)
                VALUES ($tentId, $metricType, $haEntityId, $displayLabel, $isActive, $createdAtUtc, $updatedAtUtc);
                """;
            AddTentSensorParameters(insertCommand, stored);
            insertCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void DeleteTentSensor(int sensorId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM TentSensors WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", sensorId);
        command.ExecuteNonQuery();
    }

    public TentSensor? GetTentSensor(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM TentSensors WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapTentSensor(reader) : null;
    }

    public HardwareItem CreateHardwareItem(HardwareItem item)
    {
        ValidateHardwareItem(item);
        item.CreatedAtUtc = DateTime.UtcNow;
        item.UpdatedAtUtc = DateTime.UtcNow;
        ApplyRetiredTimestamp(item);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO HardwareItems (
                Name, Category, Status, Criticality,
                TentId, SetupId, GrowId, WearTemplateId, TentSensorId, HaEntityId,
                Manufacturer, Model, SerialNumber,
                InstalledAtUtc, RetiredAtUtc,
                ExpectedLifespanDays, InspectionIntervalDays, Notes,
                CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $name, $category, $status, $criticality,
                $tentId, $setupId, $growId, $wearTemplateId, $tentSensorId, $haEntityId,
                $manufacturer, $model, $serialNumber,
                $installedAtUtc, $retiredAtUtc,
                $expectedLifespanDays, $inspectionIntervalDays, $notes,
                $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddHardwareItemParameters(command, item);
        item.Id = Convert.ToInt32((long)command.ExecuteScalar()!);
        return item;
    }

    public void UpdateHardwareItem(HardwareItem item)
    {
        ValidateHardwareItem(item);
        item.UpdatedAtUtc = DateTime.UtcNow;
        ApplyRetiredTimestamp(item);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE HardwareItems SET
                Name = $name,
                Category = $category,
                Status = $status,
                Criticality = $criticality,
                TentId = $tentId,
                SetupId = $setupId,
                GrowId = $growId,
                WearTemplateId = $wearTemplateId,
                TentSensorId = $tentSensorId,
                HaEntityId = $haEntityId,
                Manufacturer = $manufacturer,
                Model = $model,
                SerialNumber = $serialNumber,
                InstalledAtUtc = $installedAtUtc,
                RetiredAtUtc = $retiredAtUtc,
                ExpectedLifespanDays = $expectedLifespanDays,
                InspectionIntervalDays = $inspectionIntervalDays,
                Notes = $notes,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddHardwareItemParameters(command, item);
        command.Parameters.AddWithValue("$id", item.Id);
        command.ExecuteNonQuery();
    }

    public HardwareItem? GetHardwareItem(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM HardwareItems WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapHardwareItem(reader) : null;
    }

    public List<HardwareItem> GetHardwareItems()
        => GetHardwareItemsByWhere(string.Empty, null);

    public List<HardwareItem> GetHardwareItemsByTent(int tentId)
        => GetHardwareItemsByWhere("WHERE TentId = $value", tentId);

    public List<HardwareItem> GetHardwareItemsByStatus(HardwareItemStatus status)
        => GetHardwareItemsByWhere("WHERE Status = $value", status.ToString());

    public MaintenanceEvent CreateMaintenanceEvent(MaintenanceEvent item)
    {
        var hardware = ValidateMaintenanceEvent(item);
        item.CreatedAtUtc = DateTime.UtcNow;
        item.UpdatedAtUtc = DateTime.UtcNow;
        ApplyMaintenanceDefaults(item, hardware);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        item.GrowTaskId ??= TryCreateMaintenanceReminderTask(connection, transaction, item, hardware);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO MaintenanceEvents (
                HardwareItemId, EventType, Status, Result, Title, Description,
                DueAtUtc, PerformedAtUtc, NextDueAtUtc,
                GrowTaskId, SopInstanceId, Notes,
                CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $hardwareItemId, $eventType, $status, $result, $title, $description,
                $dueAtUtc, $performedAtUtc, $nextDueAtUtc,
                $growTaskId, $sopInstanceId, $notes,
                $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddMaintenanceEventParameters(command, item);
        item.Id = Convert.ToInt32((long)command.ExecuteScalar()!);
        transaction.Commit();
        return item;
    }

    public void UpdateMaintenanceEvent(MaintenanceEvent item)
    {
        var hardware = ValidateMaintenanceEvent(item);
        item.UpdatedAtUtc = DateTime.UtcNow;
        ApplyMaintenanceDefaults(item, hardware);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE MaintenanceEvents SET
                HardwareItemId = $hardwareItemId,
                EventType = $eventType,
                Status = $status,
                Result = $result,
                Title = $title,
                Description = $description,
                DueAtUtc = $dueAtUtc,
                PerformedAtUtc = $performedAtUtc,
                NextDueAtUtc = $nextDueAtUtc,
                GrowTaskId = $growTaskId,
                SopInstanceId = $sopInstanceId,
                Notes = $notes,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddMaintenanceEventParameters(command, item);
        command.Parameters.AddWithValue("$id", item.Id);
        command.ExecuteNonQuery();
    }

    public MaintenanceEvent? GetMaintenanceEvent(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM MaintenanceEvents WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapMaintenanceEvent(reader) : null;
    }

    public List<MaintenanceEvent> GetMaintenanceEvents()
        => GetMaintenanceEventsByWhere(string.Empty, null);

    public List<MaintenanceEvent> GetMaintenanceEventsByHardwareItem(int hardwareItemId)
        => GetMaintenanceEventsByWhere("WHERE HardwareItemId = $value", hardwareItemId);

    public List<MaintenanceEvent> GetOpenMaintenanceEventsByHardwareItem(int hardwareItemId)
        => GetMaintenanceEventsByWhere("WHERE HardwareItemId = $value AND Status = 'Planned'", hardwareItemId);

    public List<MaintenanceEvent> GetDueMaintenanceEvents(DateTime nowUtc)
        => GetMaintenanceEventsByWhere("WHERE DueAtUtc IS NOT NULL AND DueAtUtc <= $value", ToStorageUtc(nowUtc));

    public CalibrationEvent CreateCalibrationEvent(CalibrationEvent item)
    {
        var hardware = ValidateCalibrationEvent(item);
        item.CreatedAtUtc = DateTime.UtcNow;
        item.UpdatedAtUtc = DateTime.UtcNow;
        ApplyCalibrationDefaults(item);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        item.GrowTaskId ??= TryCreateCalibrationReminderTask(connection, transaction, item, hardware);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO CalibrationEvents (
                HardwareItemId, CalibrationType, Status, Result, Title,
                ReferenceSolution, ReferenceValue, BeforeValue, AfterValue, TemperatureC,
                DueAtUtc, PerformedAtUtc, NextDueAtUtc,
                GrowTaskId, Notes,
                CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $hardwareItemId, $calibrationType, $status, $result, $title,
                $referenceSolution, $referenceValue, $beforeValue, $afterValue, $temperatureC,
                $dueAtUtc, $performedAtUtc, $nextDueAtUtc,
                $growTaskId, $notes,
                $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddCalibrationEventParameters(command, item);
        item.Id = Convert.ToInt32((long)command.ExecuteScalar()!);
        transaction.Commit();
        return item;
    }

    public void UpdateCalibrationEvent(CalibrationEvent item)
    {
        ValidateCalibrationEvent(item);
        item.UpdatedAtUtc = DateTime.UtcNow;
        ApplyCalibrationDefaults(item);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE CalibrationEvents SET
                HardwareItemId = $hardwareItemId,
                CalibrationType = $calibrationType,
                Status = $status,
                Result = $result,
                Title = $title,
                ReferenceSolution = $referenceSolution,
                ReferenceValue = $referenceValue,
                BeforeValue = $beforeValue,
                AfterValue = $afterValue,
                TemperatureC = $temperatureC,
                DueAtUtc = $dueAtUtc,
                PerformedAtUtc = $performedAtUtc,
                NextDueAtUtc = $nextDueAtUtc,
                GrowTaskId = $growTaskId,
                Notes = $notes,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddCalibrationEventParameters(command, item);
        command.Parameters.AddWithValue("$id", item.Id);
        command.ExecuteNonQuery();
    }

    public CalibrationEvent? GetCalibrationEvent(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM CalibrationEvents WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapCalibrationEvent(reader) : null;
    }

    public List<CalibrationEvent> GetCalibrationEvents()
        => GetCalibrationEventsByWhere(string.Empty, null);

    public List<CalibrationEvent> GetCalibrationEventsByHardwareItem(int hardwareItemId)
        => GetCalibrationEventsByWhere("WHERE HardwareItemId = $value", hardwareItemId);

    public List<CalibrationEvent> GetOpenCalibrationEventsByHardwareItem(int hardwareItemId)
        => GetCalibrationEventsByWhere("WHERE HardwareItemId = $value AND Status = 'Planned'", hardwareItemId);

    public List<CalibrationEvent> GetDueCalibrationEvents(DateTime nowUtc)
        => GetCalibrationEventsByWhere("WHERE DueAtUtc IS NOT NULL AND DueAtUtc <= $value", ToStorageUtc(nowUtc));

    public CalibrationEvent? GetLatestCompletedCalibrationEvent(int hardwareItemId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM CalibrationEvents
            WHERE HardwareItemId = $hardwareItemId AND Status = 'Completed'
            ORDER BY PerformedAtUtc DESC, Id DESC
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$hardwareItemId", hardwareItemId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapCalibrationEvent(reader) : null;
    }

    public Setup CreateSetup(Setup setup)
    {
        ValidateSetupTentCompatibility(setup.TentId, setup.SetupType);

        setup.CreatedAtUtc = DateTime.UtcNow;
        setup.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Setups (
                TentId, Name, SetupType, Status, Notes,
                CloneCounterTotal, LastCloneCutAt, MotherHealthStatus,
                QuarantineStartedAt, QuarantinePlannedEndAt, QuarantineResult,
                CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $tentId, $name, $setupType, $status, $notes,
                $cloneCounterTotal, $lastCloneCutAt, $motherHealthStatus,
                $quarantineStartedAt, $quarantinePlannedEndAt, $quarantineResult,
                $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddSetupParameters(command, setup);
        setup.Id = Convert.ToInt32((long)command.ExecuteScalar()!);
        return setup;
    }

    public Setup? GetSetup(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Setups WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapSetup(reader) : null;
    }

    public List<Setup> GetSetups()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM Setups
            ORDER BY CASE Status WHEN 'Active' THEN 0 WHEN 'Planning' THEN 1 ELSE 2 END, Name, Id;
        """;

        var list = new List<Setup>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapSetup(reader));
        }
        return list;
    }

    public List<Setup> GetSetupsForTent(int tentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM Setups
            WHERE TentId = $tentId
            ORDER BY CASE Status WHEN 'Active' THEN 0 WHEN 'Planning' THEN 1 ELSE 2 END, Name, Id;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);

        var list = new List<Setup>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapSetup(reader));
        }
        return list;
    }

    public void UpdateSetup(Setup setup)
    {
        ValidateSetupTentCompatibility(setup.TentId, setup.SetupType);

        setup.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Setups SET
                TentId = $tentId,
                Name = $name,
                SetupType = $setupType,
                Status = $status,
                Notes = $notes,
                CloneCounterTotal = $cloneCounterTotal,
                LastCloneCutAt = $lastCloneCutAt,
                MotherHealthStatus = $motherHealthStatus,
                QuarantineStartedAt = $quarantineStartedAt,
                QuarantinePlannedEndAt = $quarantinePlannedEndAt,
                QuarantineResult = $quarantineResult,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddSetupParameters(command, setup);
        command.Parameters.AddWithValue("$id", setup.Id);
        command.ExecuteNonQuery();
    }

    public Strain CreateStrain(Strain strain)
    {
        strain.CreatedAtUtc = DateTime.UtcNow;
        strain.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Strains (
                Name, Breeder, Dominance, FlowerWeeksMin, FlowerWeeksMax, Notes,
                NutrientDemandFactor, StretchFactor, VpdPreferenceShift,
                CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $name, $breeder, $dominance, $flowerWeeksMin, $flowerWeeksMax, $notes,
                $nutrientDemandFactor, $stretchFactor, $vpdPreferenceShift,
                $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddStrainParameters(command, strain);
        strain.Id = Convert.ToInt32((long)command.ExecuteScalar()!);
        return strain;
    }

    public void UpdateStrain(Strain strain)
    {
        strain.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Strains SET
                Name = $name,
                Breeder = $breeder,
                Dominance = $dominance,
                FlowerWeeksMin = $flowerWeeksMin,
                FlowerWeeksMax = $flowerWeeksMax,
                Notes = $notes,
                NutrientDemandFactor = $nutrientDemandFactor,
                StretchFactor = $stretchFactor,
                VpdPreferenceShift = $vpdPreferenceShift,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddStrainParameters(command, strain);
        command.Parameters.AddWithValue("$id", strain.Id);
        command.ExecuteNonQuery();
    }

    public Strain? GetStrain(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Strains WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapStrain(reader) : null;
    }

    public List<Strain> GetStrains()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Strains ORDER BY Name, Breeder, Id;";
        var list = new List<Strain>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapStrain(reader));
        }
        return list;
    }

    public PlantInstance CreatePlant(PlantInstance plant)
    {
        plant.CreatedAtUtc = DateTime.UtcNow;
        plant.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO PlantInstances (
                StrainId, SetupId, GrowId, ParentPlantId, Label, PlantRole, PlantStatus,
                PhenoLabel, StartedAt, EndedAt, Notes, CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $strainId, $setupId, $growId, $parentPlantId, $label, $plantRole, $plantStatus,
                $phenoLabel, $startedAt, $endedAt, $notes, $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddPlantParameters(command, plant);
        plant.Id = Convert.ToInt32((long)command.ExecuteScalar()!);
        return GetPlant(plant.Id) ?? plant;
    }

    public PlantInstance CreateCloneFromMother(PlantInstance clone, int? motherSetupId, DateTime cutAt)
    {
        clone.CreatedAtUtc = DateTime.UtcNow;
        clone.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = """
            INSERT INTO PlantInstances (
                StrainId, SetupId, GrowId, ParentPlantId, Label, PlantRole, PlantStatus,
                PhenoLabel, StartedAt, EndedAt, Notes, CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $strainId, $setupId, $growId, $parentPlantId, $label, $plantRole, $plantStatus,
                $phenoLabel, $startedAt, $endedAt, $notes, $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddPlantParameters(insertCommand, clone);
        clone.Id = Convert.ToInt32((long)insertCommand.ExecuteScalar()!);

        if (motherSetupId.HasValue)
        {
            using var setupCommand = connection.CreateCommand();
            setupCommand.Transaction = transaction;
            setupCommand.CommandText = """
                UPDATE Setups
                SET CloneCounterTotal = COALESCE(CloneCounterTotal, 0) + 1,
                    LastCloneCutAt = $cutAt,
                    UpdatedAtUtc = $updatedAtUtc
                WHERE Id = $setupId AND SetupType = 'Mother';
            """;
            setupCommand.Parameters.AddWithValue("$cutAt", ToStorage(cutAt));
            setupCommand.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(DateTime.UtcNow));
            setupCommand.Parameters.AddWithValue("$setupId", motherSetupId.Value);
            setupCommand.ExecuteNonQuery();
        }

        PlantInstance created;
        using (var getCommand = connection.CreateCommand())
        {
            getCommand.Transaction = transaction;
            getCommand.CommandText = """
                SELECT p.*, s.Name AS StrainName
                FROM PlantInstances p
                LEFT JOIN Strains s ON s.Id = p.StrainId
                WHERE p.Id = $id
                LIMIT 1;
            """;
            getCommand.Parameters.AddWithValue("$id", clone.Id);
            using var reader = getCommand.ExecuteReader();
            created = reader.Read() ? MapPlant(reader) : clone;
        }

        transaction.Commit();
        return created;
    }

    public PlantInstance DecideQuarantinePlant(PlantInstance plant, int quarantineSetupId, string quarantineResult)
    {
        plant.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using var plantCommand = connection.CreateCommand();
        plantCommand.Transaction = transaction;
        plantCommand.CommandText = """
            UPDATE PlantInstances SET
                StrainId = $strainId,
                SetupId = $setupId,
                GrowId = $growId,
                ParentPlantId = $parentPlantId,
                Label = $label,
                PlantRole = $plantRole,
                PlantStatus = $plantStatus,
                PhenoLabel = $phenoLabel,
                StartedAt = $startedAt,
                EndedAt = $endedAt,
                Notes = $notes,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddPlantParameters(plantCommand, plant);
        plantCommand.Parameters.AddWithValue("$id", plant.Id);
        plantCommand.ExecuteNonQuery();

        using var setupCommand = connection.CreateCommand();
        setupCommand.Transaction = transaction;
        setupCommand.CommandText = """
            UPDATE Setups
            SET QuarantineResult = $quarantineResult,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $setupId AND SetupType = 'Quarantine';
        """;
        setupCommand.Parameters.AddWithValue("$quarantineResult", quarantineResult);
        setupCommand.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(DateTime.UtcNow));
        setupCommand.Parameters.AddWithValue("$setupId", quarantineSetupId);
        setupCommand.ExecuteNonQuery();

        PlantInstance updated;
        using (var getCommand = connection.CreateCommand())
        {
            getCommand.Transaction = transaction;
            getCommand.CommandText = """
                SELECT p.*, s.Name AS StrainName
                FROM PlantInstances p
                LEFT JOIN Strains s ON s.Id = p.StrainId
                WHERE p.Id = $id
                LIMIT 1;
            """;
            getCommand.Parameters.AddWithValue("$id", plant.Id);
            using var reader = getCommand.ExecuteReader();
            updated = reader.Read() ? MapPlant(reader) : plant;
        }

        transaction.Commit();
        return updated;
    }

    public void UpdatePlant(PlantInstance plant)
    {
        plant.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE PlantInstances SET
                StrainId = $strainId,
                SetupId = $setupId,
                GrowId = $growId,
                ParentPlantId = $parentPlantId,
                Label = $label,
                PlantRole = $plantRole,
                PlantStatus = $plantStatus,
                PhenoLabel = $phenoLabel,
                StartedAt = $startedAt,
                EndedAt = $endedAt,
                Notes = $notes,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddPlantParameters(command, plant);
        command.Parameters.AddWithValue("$id", plant.Id);
        command.ExecuteNonQuery();
    }

    public PlantInstance? GetPlant(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.*, s.Name AS StrainName
            FROM PlantInstances p
            LEFT JOIN Strains s ON s.Id = p.StrainId
            WHERE p.Id = $id
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapPlant(reader) : null;
    }

    public List<PlantInstance> GetPlants()
        => GetPlantsByWhere(string.Empty, null, null);

    public List<PlantInstance> GetPlantsBySetup(int setupId)
        => GetPlantsByWhere("WHERE p.SetupId = $setupId", setupId, null);

    public List<PlantInstance> GetPlantsByGrow(int growId)
        => GetPlantsByWhere("WHERE p.GrowId = $growId", null, growId);

    public AutoMeasurementConfig CreateAutoMeasurementConfig(AutoMeasurementConfig config)
    {
        config.CreatedAtUtc = DateTime.UtcNow;
        config.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AutoMeasurementConfigs (
                GrowId, TentId, Name, Status, TriggerKind, DelayMinutes, WindowMinutes,
                CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $growId, $tentId, $name, $status, $triggerKind, $delayMinutes, $windowMinutes,
                $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddAutoMeasurementConfigParameters(command, config);
        config.Id = Convert.ToInt32((long)command.ExecuteScalar()!);
        return config;
    }

    public void UpdateAutoMeasurementConfig(AutoMeasurementConfig config)
    {
        config.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE AutoMeasurementConfigs SET
                TentId = $tentId,
                Name = $name,
                Status = $status,
                TriggerKind = $triggerKind,
                DelayMinutes = $delayMinutes,
                WindowMinutes = $windowMinutes,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddAutoMeasurementConfigParameters(command, config);
        command.Parameters.AddWithValue("$id", config.Id);
        command.ExecuteNonQuery();
    }

    public AutoMeasurementConfig? GetAutoMeasurementConfig(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM AutoMeasurementConfigs WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapAutoMeasurementConfig(reader) : null;
    }

    public List<AutoMeasurementConfig> GetAutoMeasurementConfigs()
        => GetAutoMeasurementConfigsByWhere(string.Empty, null);

    public List<AutoMeasurementConfig> GetAutoMeasurementConfigsByGrow(int growId)
        => GetAutoMeasurementConfigsByWhere("WHERE GrowId = $growId", growId);

    public List<AutoMeasurementConfig> GetEnabledAutoMeasurementConfigs()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM AutoMeasurementConfigs
            WHERE Status = 'Enabled'
            ORDER BY Id;
        """;

        var configs = new List<AutoMeasurementConfig>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            configs.Add(MapAutoMeasurementConfig(reader));
        }
        return configs;
    }

    public void ReplaceAutoMeasurementFieldMappings(int configId, IReadOnlyCollection<AutoMeasurementFieldMapping> mappings)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM AutoMeasurementFieldMappings WHERE ConfigId = $configId;";
            deleteCommand.Parameters.AddWithValue("$configId", configId);
            deleteCommand.ExecuteNonQuery();
        }

        foreach (var mapping in mappings)
        {
            var stored = new AutoMeasurementFieldMapping
            {
                ConfigId = configId,
                MeasurementField = mapping.MeasurementField,
                MetricKey = mapping.MetricKey,
                Aggregation = mapping.Aggregation,
                IsRequired = mapping.IsRequired,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO AutoMeasurementFieldMappings (
                    ConfigId, MeasurementField, MetricKey, Aggregation, IsRequired,
                    CreatedAtUtc, UpdatedAtUtc
                )
                VALUES (
                    $configId, $measurementField, $metricKey, $aggregation, $isRequired,
                    $createdAtUtc, $updatedAtUtc
                );
            """;
            AddAutoMeasurementFieldMappingParameters(insertCommand, stored);
            insertCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public List<AutoMeasurementFieldMapping> GetAutoMeasurementFieldMappings(int configId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM AutoMeasurementFieldMappings
            WHERE ConfigId = $configId
            ORDER BY Id;
        """;
        command.Parameters.AddWithValue("$configId", configId);
        var list = new List<AutoMeasurementFieldMapping>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapAutoMeasurementFieldMapping(reader));
        }
        return list;
    }

    public AutoMeasurementRun CreateAutoMeasurementRunIfNotExists(AutoMeasurementRun run)
    {
        run.CreatedAtUtc = DateTime.UtcNow;
        run.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT OR IGNORE INTO AutoMeasurementRuns (
                    ConfigId, GrowId, TriggerKind, ScheduledForUtc, MeasurementId, Status,
                    ErrorMessage, CreatedAtUtc, UpdatedAtUtc
                )
                VALUES (
                    $configId, $growId, $triggerKind, $scheduledForUtc, $measurementId, $status,
                    $errorMessage, $createdAtUtc, $updatedAtUtc
                );
            """;
            AddAutoMeasurementRunParameters(insertCommand, run);
            insertCommand.ExecuteNonQuery();
        }

        AutoMeasurementRun stored;
        using (var selectCommand = connection.CreateCommand())
        {
            selectCommand.Transaction = transaction;
            selectCommand.CommandText = """
                SELECT * FROM AutoMeasurementRuns
                WHERE ConfigId = $configId AND TriggerKind = $triggerKind AND ScheduledForUtc = $scheduledForUtc
                LIMIT 1;
            """;
            selectCommand.Parameters.AddWithValue("$configId", run.ConfigId);
            selectCommand.Parameters.AddWithValue("$triggerKind", run.TriggerKind.ToString());
            selectCommand.Parameters.AddWithValue("$scheduledForUtc", ToStorageUtc(run.ScheduledForUtc));
            using var reader = selectCommand.ExecuteReader();
            stored = reader.Read() ? MapAutoMeasurementRun(reader) : run;
        }

        transaction.Commit();
        return stored;
    }

    public List<AutoMeasurementRun> GetAutoMeasurementRunsByConfig(int configId)
        => GetAutoMeasurementRunsByWhere("WHERE ConfigId = $configId", configId, null);

    public List<AutoMeasurementRun> GetAutoMeasurementRunsByGrow(int growId)
        => GetAutoMeasurementRunsByWhere("WHERE GrowId = $growId", null, growId);

    public AutoMeasurementRun? GetAutoMeasurementRun(int configId, AutoMeasurementTriggerKind triggerKind, DateTime scheduledForUtc)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM AutoMeasurementRuns
            WHERE ConfigId = $configId
              AND TriggerKind = $triggerKind
              AND ScheduledForUtc = $scheduledForUtc
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$configId", configId);
        command.Parameters.AddWithValue("$triggerKind", triggerKind.ToString());
        command.Parameters.AddWithValue("$scheduledForUtc", ToStorageUtc(scheduledForUtc));
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapAutoMeasurementRun(reader) : null;
    }

    public void UpdateAutoMeasurementRun(AutoMeasurementRun run)
    {
        run.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE AutoMeasurementRuns SET
                MeasurementId = $measurementId,
                Status = $status,
                ErrorMessage = $errorMessage,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        command.Parameters.AddWithValue("$measurementId", (object?)run.MeasurementId ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", run.Status.ToString());
        command.Parameters.AddWithValue("$errorMessage", (object?)run.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(run.UpdatedAtUtc));
        command.Parameters.AddWithValue("$id", run.Id);
        command.ExecuteNonQuery();
    }

    public LightSchedule CreateLightSchedule(LightSchedule schedule)
    {
        schedule.CreatedAtUtc = DateTime.UtcNow;
        schedule.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO LightSchedules (
                TentId, Name, IsActive, LightsOnTime, LightsOffTime, TimeZoneId, Source,
                CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $tentId, $name, $isActive, $lightsOnTime, $lightsOffTime, $timeZoneId, $source,
                $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddLightScheduleParameters(command, schedule);
        schedule.Id = Convert.ToInt32((long)command.ExecuteScalar()!);
        return schedule;
    }

    public void UpdateLightSchedule(LightSchedule schedule)
    {
        schedule.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE LightSchedules SET
                TentId = $tentId,
                Name = $name,
                IsActive = $isActive,
                LightsOnTime = $lightsOnTime,
                LightsOffTime = $lightsOffTime,
                TimeZoneId = $timeZoneId,
                Source = $source,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddLightScheduleParameters(command, schedule);
        command.Parameters.AddWithValue("$id", schedule.Id);
        command.ExecuteNonQuery();
    }

    public LightSchedule? GetLightSchedule(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM LightSchedules WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapLightSchedule(reader) : null;
    }

    public List<LightSchedule> GetLightSchedulesByTent(int tentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM LightSchedules
            WHERE TentId = $tentId
            ORDER BY IsActive DESC, Name, Id;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);

        var schedules = new List<LightSchedule>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            schedules.Add(MapLightSchedule(reader));
        }
        return schedules;
    }

    public LightSchedule? GetActiveLightScheduleForTent(int tentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM LightSchedules
            WHERE TentId = $tentId AND IsActive = 1
            ORDER BY UpdatedAtUtc DESC, Id DESC
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapLightSchedule(reader) : null;
    }

    public LightTransitionEvent CreateLightTransitionIfNotDuplicate(LightTransitionEvent transition)
    {
        transition.OccurredAtUtc = transition.OccurredAtUtc.ToUniversalTime();
        transition.CreatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var duplicateCommand = connection.CreateCommand())
        {
            duplicateCommand.Transaction = transaction;
            duplicateCommand.CommandText = """
                SELECT *
                FROM LightTransitionEvents
                WHERE TentId = $tentId
                  AND Kind = $kind
                  AND OccurredAtUtc >= $fromUtc
                  AND OccurredAtUtc <= $toUtc
                ORDER BY OccurredAtUtc DESC, Id DESC
                LIMIT 1;
            """;
            duplicateCommand.Parameters.AddWithValue("$tentId", transition.TentId);
            duplicateCommand.Parameters.AddWithValue("$kind", transition.Kind.ToString());
            duplicateCommand.Parameters.AddWithValue("$fromUtc", ToStorageUtc(transition.OccurredAtUtc.AddMinutes(-2)));
            duplicateCommand.Parameters.AddWithValue("$toUtc", ToStorageUtc(transition.OccurredAtUtc.AddMinutes(2)));
            using var reader = duplicateCommand.ExecuteReader();
            if (reader.Read())
            {
                var existing = MapLightTransitionEvent(reader);
                transaction.Commit();
                return existing;
            }
        }

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO LightTransitionEvents (
                    TentId, Kind, OccurredAtUtc, Source, RawState, CreatedAtUtc
                )
                VALUES (
                    $tentId, $kind, $occurredAtUtc, $source, $rawState, $createdAtUtc
                );
                SELECT last_insert_rowid();
            """;
            AddLightTransitionParameters(insertCommand, transition);
            transition.Id = Convert.ToInt32((long)insertCommand.ExecuteScalar()!);
        }

        transaction.Commit();
        return transition;
    }

    public List<LightTransitionEvent> GetLightTransitionsByTent(int tentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM LightTransitionEvents
            WHERE TentId = $tentId
            ORDER BY OccurredAtUtc DESC, Id DESC;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);

        var transitions = new List<LightTransitionEvent>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            transitions.Add(MapLightTransitionEvent(reader));
        }
        return transitions;
    }

    public List<LightTransitionEvent> GetLightTransitionsByTentAndKindSince(int tentId, LightTransitionKind kind, DateTime sinceUtc)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM LightTransitionEvents
            WHERE TentId = $tentId
              AND Kind = $kind
              AND OccurredAtUtc >= $sinceUtc
            ORDER BY OccurredAtUtc ASC, Id ASC;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);
        command.Parameters.AddWithValue("$kind", kind.ToString());
        command.Parameters.AddWithValue("$sinceUtc", ToStorageUtc(sinceUtc));

        var transitions = new List<LightTransitionEvent>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            transitions.Add(MapLightTransitionEvent(reader));
        }
        return transitions;
    }

    public LightTransitionEvent? GetLatestLightTransitionForTent(int tentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM LightTransitionEvents
            WHERE TentId = $tentId
            ORDER BY OccurredAtUtc DESC, Id DESC
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapLightTransitionEvent(reader) : null;
    }

    public LightTransitionEvent? GetLatestLightTransitionForTentAndKind(int tentId, LightTransitionKind kind)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM LightTransitionEvents
            WHERE TentId = $tentId
              AND Kind = $kind
            ORDER BY OccurredAtUtc DESC, Id DESC
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);
        command.Parameters.AddWithValue("$kind", kind.ToString());
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapLightTransitionEvent(reader) : null;
    }

    public SopInstance StartSopInstance(
        int growId,
        SopDefinition sopDefinition,
        SopStartSource source,
        string? sourceRecommendationKey,
        string? treatmentRecommendationStableKey,
        string? notes)
    {
        var now = DateTime.UtcNow;

        // Scheduling: berechne Fälligkeiten aus SOP-Typ
        var isRecurring = string.Equals(sopDefinition.Type, "Recurring", StringComparison.OrdinalIgnoreCase);
        // E4-Fix: bevorzugt Schedule-Trigger, Root-Level nur als Fallback.
        var recurrenceIntervalDays = sopDefinition.Triggers
            .FirstOrDefault(t => string.Equals(t.Type, "Schedule", StringComparison.OrdinalIgnoreCase))
            ?.IntervalDays ?? sopDefinition.IntervalDays;
        DateTime? instanceDueAt = string.Equals(sopDefinition.Type, "MultiDay", StringComparison.OrdinalIgnoreCase) && sopDefinition.DurationDays.HasValue
            ? now.AddDays(sopDefinition.DurationDays.Value)
            : isRecurring && recurrenceIntervalDays.HasValue
                ? now.AddDays(recurrenceIntervalDays.Value)
                : null;

        // Erster Pass: NextStepDueAtUtc aus Step-Definitionen berechnen
        // Regel: Step mit waitMinutes → DueAtUtc = startTime + waitMinutes
        //        Erster Step ohne waitMinutes → DueAtUtc = startTime; weitere → null
        var orderedStepDefs = sopDefinition.Steps.OrderBy(s => s.Order).ToList();
        DateTime? nextStepDue = null;
        for (var i = 0; i < orderedStepDefs.Count; i++)
        {
            DateTime? stepDue = orderedStepDefs[i].WaitMinutes.HasValue
                ? now.AddMinutes(orderedStepDefs[i].WaitMinutes!.Value)
                : i == 0 ? now : null;
            if (stepDue.HasValue && (nextStepDue is null || stepDue.Value < nextStepDue.Value))
                nextStepDue = stepDue;
        }

        var instance = new SopInstance
        {
            GrowId = growId,
            SopId = sopDefinition.Id,
            SopName = sopDefinition.Name,
            SopType = sopDefinition.Type,
            Status = SopInstanceStatus.Active,
            Source = source,
            SourceRecommendationKey = NormalizeOptional(sourceRecommendationKey),
            TreatmentRecommendationStableKey = NormalizeOptional(treatmentRecommendationStableKey),
            StartedAtUtc = now,
            DueAtUtc = instanceDueAt,
            NextStepDueAtUtc = nextStepDue,
            IsRecurring = isRecurring,
            RecurrenceIntervalDays = recurrenceIntervalDays,
            Notes = NormalizeOptional(notes),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var duplicateCommand = connection.CreateCommand())
        {
            duplicateCommand.Transaction = transaction;
            duplicateCommand.CommandText = """
                SELECT COUNT(*)
                FROM SopInstances
                WHERE GrowId = $growId
                  AND SopId = $sopId
                  AND Status = $status;
            """;
            duplicateCommand.Parameters.AddWithValue("$growId", growId);
            duplicateCommand.Parameters.AddWithValue("$sopId", sopDefinition.Id);
            duplicateCommand.Parameters.AddWithValue("$status", SopInstanceStatus.Active.ToString());
            if (Convert.ToInt32(duplicateCommand.ExecuteScalar(), CultureInfo.InvariantCulture) > 0)
            {
                throw new InvalidOperationException("An active SOP instance already exists for this grow and sopId.");
            }
        }

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO SopInstances (
                    GrowId, SopId, SopName, SopType, Status, Source, SourceRecommendationKey,
                    TreatmentRecommendationStableKey, StartedAtUtc, CompletedAtUtc, CancelledAtUtc,
                    DueAtUtc, NextStepDueAtUtc, RecurrenceIntervalDays, IsRecurring,
                    Notes, CreatedAtUtc, UpdatedAtUtc
                )
                VALUES (
                    $growId, $sopId, $sopName, $sopType, $status, $source, $sourceRecommendationKey,
                    $treatmentRecommendationStableKey, $startedAtUtc, $completedAtUtc, $cancelledAtUtc,
                    $dueAtUtc, $nextStepDueAtUtc, $recurrenceIntervalDays, $isRecurring,
                    $notes, $createdAtUtc, $updatedAtUtc
                );
                SELECT last_insert_rowid();
            """;
            AddSopInstanceParameters(insertCommand, instance);
            instance.Id = Convert.ToInt32((long)insertCommand.ExecuteScalar()!);
        }

        for (var idx = 0; idx < orderedStepDefs.Count; idx++)
        {
            var stepDefinition = orderedStepDefs[idx];

            DateTime? stepDueAt;
            DateTime? stepAvailableAt;
            if (stepDefinition.WaitMinutes.HasValue)
            {
                stepDueAt = now.AddMinutes(stepDefinition.WaitMinutes.Value);
                stepAvailableAt = stepDueAt;
            }
            else
            {
                stepDueAt = idx == 0 ? now : null;
                stepAvailableAt = null;
            }

            var step = new SopStepInstance
            {
                SopInstanceId = instance.Id,
                StepId = stepDefinition.Id,
                Order = stepDefinition.Order,
                Title = stepDefinition.Title,
                Description = NormalizeOptional(stepDefinition.Description),
                StepType = stepDefinition.StepType,
                Status = SopStepInstanceStatus.Pending,
                WaitMinutes = stepDefinition.WaitMinutes,
                SubSopId = NormalizeOptional(stepDefinition.SubSopId),
                ExpectedInputsJson = stepDefinition.ExpectedInputs is { Count: > 0 }
                    ? JsonSerializer.Serialize(stepDefinition.ExpectedInputs)
                    : null,
                PhotoRequired = stepDefinition.PhotoRequired,
                PhotoRecommended = stepDefinition.PhotoRecommended,
                DueAtUtc = stepDueAt,
                AvailableAtUtc = stepAvailableAt,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            using var stepCommand = connection.CreateCommand();
            stepCommand.Transaction = transaction;
            stepCommand.CommandText = """
                INSERT INTO SopStepInstances (
                    SopInstanceId, StepId, "Order", Title, Description, StepType, Status,
                    WaitMinutes, SubSopId, ExpectedInputsJson, PhotoRequired, PhotoRecommended,
                    DueAtUtc, AvailableAtUtc, ReminderTaskId,
                    StartedAtUtc, CompletedAtUtc, SkippedAtUtc, Notes, MeasurementId, JournalEntryId,
                    PhotoAssetId, CreatedAtUtc, UpdatedAtUtc
                )
                VALUES (
                    $sopInstanceId, $stepId, $order, $title, $description, $stepType, $status,
                    $waitMinutes, $subSopId, $expectedInputsJson, $photoRequired, $photoRecommended,
                    $dueAtUtc, $availableAtUtc, $reminderTaskId,
                    $startedAtUtc, $completedAtUtc, $skippedAtUtc, $notes, $measurementId, $journalEntryId,
                    $photoAssetId, $createdAtUtc, $updatedAtUtc
                );
            """;
            AddSopStepInstanceParameters(stepCommand, step);
            stepCommand.ExecuteNonQuery();
        }

        transaction.Commit();
        instance.StepCount = sopDefinition.Steps.Count;
        return instance;
    }

    public SopInstance? GetSopInstance(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT si.*, COUNT(ssi.Id) AS StepCount
            FROM SopInstances si
            LEFT JOIN SopStepInstances ssi ON ssi.SopInstanceId = si.Id
            WHERE si.Id = $id
            GROUP BY si.Id
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapSopInstance(reader) : null;
    }

    public List<SopInstance> GetSopInstancesByGrow(int growId)
        => GetSopInstancesByGrow(growId, activeOnly: false);

    public List<SopInstance> GetActiveSopInstancesByGrow(int growId)
        => GetSopInstancesByGrow(growId, activeOnly: true);

    public List<SopStepInstance> GetSopStepInstances(int sopInstanceId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM SopStepInstances
            WHERE SopInstanceId = $sopInstanceId
            ORDER BY "Order", Id;
        """;
        command.Parameters.AddWithValue("$sopInstanceId", sopInstanceId);

        var steps = new List<SopStepInstance>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            steps.Add(MapSopStepInstance(reader));
        }

        return steps;
    }

    public SopStepInstance? GetSopStepInstance(int stepInstanceId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM SopStepInstances
            WHERE Id = $id
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$id", stepInstanceId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapSopStepInstance(reader) : null;
    }

    public SopStepInstance UpdateSopStepInstance(
        int stepInstanceId,
        SopStepInstanceStatus status,
        string? notes,
        int? measurementId,
        int? journalEntryId,
        int? photoAssetId)
    {
        var now = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        SopStepInstance step;
        SopInstance instance;
        using (var selectCommand = connection.CreateCommand())
        {
            selectCommand.Transaction = transaction;
            selectCommand.CommandText = """
                SELECT ssi.*
                FROM SopStepInstances ssi
                WHERE ssi.Id = $id
                LIMIT 1;
            """;
            selectCommand.Parameters.AddWithValue("$id", stepInstanceId);
            using var reader = selectCommand.ExecuteReader();
            if (!reader.Read())
            {
                throw new KeyNotFoundException($"SOP step instance with id {stepInstanceId} does not exist.");
            }

            step = MapSopStepInstance(reader);
        }

        using (var instanceCommand = connection.CreateCommand())
        {
            instanceCommand.Transaction = transaction;
            instanceCommand.CommandText = """
                SELECT si.*, COUNT(ssi.Id) AS StepCount
                FROM SopInstances si
                LEFT JOIN SopStepInstances ssi ON ssi.SopInstanceId = si.Id
                WHERE si.Id = $id
                GROUP BY si.Id
                LIMIT 1;
            """;
            instanceCommand.Parameters.AddWithValue("$id", step.SopInstanceId);
            using var reader = instanceCommand.ExecuteReader();
            if (!reader.Read())
            {
                throw new KeyNotFoundException($"SOP instance with id {step.SopInstanceId} does not exist.");
            }

            instance = MapSopInstance(reader);
        }

        if (instance.Status != SopInstanceStatus.Active)
        {
            throw new InvalidOperationException("SOP instance is not active.");
        }

        step.Status = status;
        step.Notes = NormalizeOptional(notes);
        step.MeasurementId = measurementId;
        step.JournalEntryId = journalEntryId;
        step.PhotoAssetId = photoAssetId;
        step.UpdatedAtUtc = now;

        switch (status)
        {
            case SopStepInstanceStatus.Pending:
                step.StartedAtUtc = null;
                step.CompletedAtUtc = null;
                step.SkippedAtUtc = null;
                break;
            case SopStepInstanceStatus.InProgress:
                step.StartedAtUtc ??= now;
                step.CompletedAtUtc = null;
                step.SkippedAtUtc = null;
                break;
            case SopStepInstanceStatus.Done:
                step.StartedAtUtc ??= now;
                step.CompletedAtUtc = now;
                step.SkippedAtUtc = null;
                break;
            case SopStepInstanceStatus.Skipped:
                step.CompletedAtUtc = null;
                step.SkippedAtUtc = now;
                break;
        }

        using (var updateCommand = connection.CreateCommand())
        {
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = """
                UPDATE SopStepInstances
                SET Status = $status,
                    StartedAtUtc = $startedAtUtc,
                    CompletedAtUtc = $completedAtUtc,
                    SkippedAtUtc = $skippedAtUtc,
                    Notes = $notes,
                    MeasurementId = $measurementId,
                    JournalEntryId = $journalEntryId,
                    PhotoAssetId = $photoAssetId,
                    UpdatedAtUtc = $updatedAtUtc
                WHERE Id = $id;
            """;
            updateCommand.Parameters.AddWithValue("$id", step.Id);
            updateCommand.Parameters.AddWithValue("$status", step.Status.ToString());
            updateCommand.Parameters.AddWithValue("$startedAtUtc", step.StartedAtUtc.HasValue ? ToStorageUtc(step.StartedAtUtc.Value) : DBNull.Value);
            updateCommand.Parameters.AddWithValue("$completedAtUtc", step.CompletedAtUtc.HasValue ? ToStorageUtc(step.CompletedAtUtc.Value) : DBNull.Value);
            updateCommand.Parameters.AddWithValue("$skippedAtUtc", step.SkippedAtUtc.HasValue ? ToStorageUtc(step.SkippedAtUtc.Value) : DBNull.Value);
            updateCommand.Parameters.AddWithValue("$notes", (object?)step.Notes ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("$measurementId", (object?)step.MeasurementId ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("$journalEntryId", (object?)step.JournalEntryId ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("$photoAssetId", (object?)step.PhotoAssetId ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(step.UpdatedAtUtc));
            updateCommand.ExecuteNonQuery();
        }

        RecalculateSopInstanceStatus(connection, transaction, step.SopInstanceId, now);
        transaction.Commit();

        return GetSopStepInstance(step.Id)!;
    }

    public void RecalculateSopInstanceStatus(int sopInstanceId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        RecalculateSopInstanceStatus(connection, transaction, sopInstanceId, DateTime.UtcNow);
        transaction.Commit();
    }

    public void UpdateSopStepReminderTaskId(int stepId, int taskId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE SopStepInstances
            SET ReminderTaskId = $taskId,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        command.Parameters.AddWithValue("$id", stepId);
        command.Parameters.AddWithValue("$taskId", taskId);
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(DateTime.UtcNow));
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

    public List<GrowSystem> GetSystems()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.*,
                   (SELECT COUNT(*) FROM Grows g WHERE g.SystemId = s.Id AND g.Status IN ('Planning','Running')) AS ActiveGrowCount
            FROM GrowSystems s
            ORDER BY s.DisplayOrder, s.Name;
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
        command.CommandText = """
            SELECT s.*, (SELECT COUNT(*) FROM Grows g WHERE g.SystemId = s.Id AND g.Status IN ('Planning','Running')) AS ActiveGrowCount
            FROM GrowSystems s WHERE s.Id = $id LIMIT 1;
        """;
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

    public List<GrowRun> GetActiveGrows(string? search = null)
        => GetGrows("WHERE g.Status IN ('Planning','Running')" + SearchClause(search), search);

    public List<GrowRun> GetArchivedGrows(string? search = null)
        => GetGrows("WHERE g.Status IN ('Completed','Aborted')" + SearchClause(search), search);

    public List<GrowRun> GetActiveGrowsForTent(int tentId)
        => GetGrows("WHERE g.TentId = $tentId AND g.Status IN ('Planning','Running')", null, tentId);

    public List<GrowRun> GetArchivedGrowsForTent(int tentId)
        => GetGrows("WHERE g.TentId = $tentId AND g.Status IN ('Completed','Aborted')", null, tentId);

    public List<GrowRun> GetAllGrows()
        => GetGrows(string.Empty, null);

    public GrowRun? GetGrow(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT g.*, t.Name AS TentName,
                   (SELECT COUNT(*) FROM Measurements m WHERE m.GrowId = g.Id) AS MeasurementCount,
                   (SELECT RelativePath FROM Photos p WHERE p.GrowId = g.Id ORDER BY p.TakenAtUtc DESC LIMIT 1) AS LatestPhotoPath
            FROM Grows g
            LEFT JOIN Tents t ON t.Id = g.TentId
            WHERE g.Id = $id
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var grow = MapGrow(reader);
        grow.LatestMeasurement = GetLatestMeasurement(id);
        return grow;
    }

    public Tent? GetTentForGrow(int growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.*,
                   (SELECT COUNT(*) FROM Grows g WHERE g.TentId = t.Id AND g.Status IN ('Planning','Running')) AS ActiveGrowCount,
                   (SELECT COUNT(*) FROM Grows g WHERE g.TentId = t.Id AND g.Status IN ('Completed','Aborted')) AS ArchivedGrowCount,
                   (SELECT COUNT(*) FROM Setups s WHERE s.TentId = t.Id AND s.Status IN ('Planning','Active')) AS ActiveSetupCount,
                   (SELECT COUNT(*) FROM Setups s WHERE s.TentId = t.Id AND s.Status = 'Archived') AS ArchivedSetupCount
            FROM Tents t
            INNER JOIN Grows g ON g.TentId = t.Id
            WHERE g.Id = $growId
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$growId", growId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapTent(reader) : null;
    }

    public int CreateGrow(GrowRun grow)
    {
        grow.CreatedAtUtc = DateTime.UtcNow;
        grow.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Grows
            (
                TentId, SystemId, SetupId, Name, Strain, Breeder, Status, MediumType, FeedingStyle, HydroStyle, MediumDetail,
                Environment, Light, ContainerSize, ReservoirSize, IrrigationStyle, IrrigationType, WaterSource,
                SeedType, StartMaterial, GerminationMethod, CloneSource, CloneIsRooted,
                BreederFlowerWeeksMin, BreederFlowerWeeksMax, PlantCount, PhenoNumber,
                PropagationMedium, HasChiller, EntryPoint, DaysAlreadyInPhase,
                AutoflowerDaysSinceGermination, FlipDate, GerminatedAt, RootedAt,
                Nutrients, Notes, StartDate, EndDate, CreatedAtUtc, UpdatedAtUtc
            )
            VALUES
            (
                $tentId, $systemId, $setupId, $name, $strain, $breeder, $status, $mediumType, $feedingStyle, $hydroStyle, $mediumDetail,
                $environment, $light, $containerSize, $reservoirSize, $irrigationStyle, $irrigationType, $waterSource,
                $seedType, $startMaterial, $germinationMethod, $cloneSource, $cloneIsRooted,
                $breederFlowerWeeksMin, $breederFlowerWeeksMax, $plantCount, $phenoNumber,
                $propagationMedium, $hasChiller, $entryPoint, $daysAlreadyInPhase,
                $autoflowerDaysSinceGermination, $flipDate, $germinatedAt, $rootedAt,
                $nutrients, $notes, $startDate, $endDate, $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddGrowParameters(command, grow);
        return Convert.ToInt32((long)command.ExecuteScalar()!);
    }

    public void UpdateGrow(GrowRun grow)
    {
        grow.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Grows
            SET
                TentId = $tentId,
                SystemId = $systemId,
                SetupId = $setupId,
                Name = $name,
                Strain = $strain,
                Breeder = $breeder,
                Status = $status,
                MediumType = $mediumType,
                FeedingStyle = $feedingStyle,
                HydroStyle = $hydroStyle,
                MediumDetail = $mediumDetail,
                Environment = $environment,
                Light = $light,
                ContainerSize = $containerSize,
                ReservoirSize = $reservoirSize,
                IrrigationStyle = $irrigationStyle,
                IrrigationType = $irrigationType,
                WaterSource = $waterSource,
                SeedType = $seedType,
                StartMaterial = $startMaterial,
                GerminationMethod = $germinationMethod,
                CloneSource = $cloneSource,
                CloneIsRooted = $cloneIsRooted,
                BreederFlowerWeeksMin = $breederFlowerWeeksMin,
                BreederFlowerWeeksMax = $breederFlowerWeeksMax,
                PlantCount = $plantCount,
                PhenoNumber = $phenoNumber,
                PropagationMedium = $propagationMedium,
                HasChiller = $hasChiller,
                EntryPoint = $entryPoint,
                DaysAlreadyInPhase = $daysAlreadyInPhase,
                AutoflowerDaysSinceGermination = $autoflowerDaysSinceGermination,
                FlipDate = $flipDate,
                GerminatedAt = $germinatedAt,
                RootedAt = $rootedAt,
                Nutrients = $nutrients,
                Notes = $notes,
                StartDate = $startDate,
                EndDate = $endDate,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddGrowParameters(command, grow);
        command.Parameters.AddWithValue("$id", grow.Id);
        command.ExecuteNonQuery();
    }

    public void DeleteGrow(int id)
    {
        using var connection = OpenConnection();
        using var photoCommand = connection.CreateCommand();
        photoCommand.CommandText = "SELECT RelativePath FROM Photos WHERE GrowId = $id;";
        photoCommand.Parameters.AddWithValue("$id", id);

        var filesToDelete = new List<string>();
        using (var reader = photoCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                var relativePath = reader["RelativePath"]?.ToString();
                if (!string.IsNullOrWhiteSpace(relativePath))
                {
                    filesToDelete.Add(relativePath);
                }
            }
        }

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Grows WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();

        foreach (var relativePath in filesToDelete)
        {
            if (TryResolveUploadPath(relativePath, out var physicalPath) && File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }
        }
    }

    public List<Measurement> GetMeasurementsForGrow(int growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM Measurements
            WHERE GrowId = $growId
            ORDER BY TakenAt DESC, Id DESC;
        """;
        command.Parameters.AddWithValue("$growId", growId);

        var items = new List<Measurement>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(MapMeasurement(reader));
        }

        return items;
    }

    public List<Measurement> GetMeasurementsForTent(int tentId, int limit = 200)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT m.*
            FROM Measurements m
            INNER JOIN Grows g ON g.Id = m.GrowId
            WHERE g.TentId = $tentId
            ORDER BY m.TakenAt DESC, m.Id DESC
            LIMIT $limit;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);
        command.Parameters.AddWithValue("$limit", limit);

        var items = new List<Measurement>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(MapMeasurement(reader));
        }
        return items;
    }

    public Measurement? GetMeasurement(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Measurements WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapMeasurement(reader) : null;
    }

    public Measurement? GetLatestMeasurement(int growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Measurements WHERE GrowId = $growId ORDER BY TakenAt DESC, Id DESC LIMIT 1;";
        command.Parameters.AddWithValue("$growId", growId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapMeasurement(reader) : null;
    }

    public Measurement? GetPreviousMeasurement(int growId, DateTime beforeTakenAt, int currentMeasurementId = 0)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM Measurements
            WHERE GrowId = $growId
              AND TakenAt < $beforeTakenAt
              AND Id <> $currentMeasurementId
            ORDER BY TakenAt DESC, Id DESC
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$growId", growId);
        command.Parameters.AddWithValue("$beforeTakenAt", ToStorage(beforeTakenAt));
        command.Parameters.AddWithValue("$currentMeasurementId", currentMeasurementId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapMeasurement(reader) : null;
    }

    public int CreateMeasurement(Measurement measurement)
    {
        measurement.CreatedAtUtc = DateTime.UtcNow;
        measurement.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Measurements
            (
                GrowId, TakenAt, Stage, Source, Notes,
                AirTemperatureC, HumidityPercent, HeightCm,
                WaterAmountMl, RunoffAmountMl, IrrigationPh, IrrigationEc, DrainPh, DrainEc,
                ReservoirPh, ReservoirEc, ReservoirWaterTempC, ReservoirLevelCm, ReservoirLevelLiters,
                DissolvedOxygenMgL, OrpMv, TopOffLiters, AddbackEc, SolutionChange,
                PpfdMol, Co2Ppm, CreatedAtUtc, UpdatedAtUtc
            )
            VALUES
            (
                $growId, $takenAt, $stage, $source, $notes,
                $airTemperatureC, $humidityPercent, $heightCm,
                $waterAmountMl, $runoffAmountMl, $irrigationPh, $irrigationEc, $drainPh, $drainEc,
                $reservoirPh, $reservoirEc, $reservoirWaterTempC, $reservoirLevelCm, $reservoirLevelLiters,
                $dissolvedOxygenMgL, $orpMv, $topOffLiters, $addbackEc, $solutionChange,
                $ppfdMol, $co2Ppm, $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddMeasurementParameters(command, measurement);
        return Convert.ToInt32((long)command.ExecuteScalar()!);
    }

    public void UpdateMeasurement(Measurement measurement)
    {
        measurement.UpdatedAtUtc = DateTime.UtcNow;
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Measurements SET
                TakenAt = $takenAt,
                Stage = $stage,
                Source = $source,
                Notes = $notes,
                AirTemperatureC = $airTemperatureC,
                HumidityPercent = $humidityPercent,
                HeightCm = $heightCm,
                WaterAmountMl = $waterAmountMl,
                RunoffAmountMl = $runoffAmountMl,
                IrrigationPh = $irrigationPh,
                IrrigationEc = $irrigationEc,
                DrainPh = $drainPh,
                DrainEc = $drainEc,
                ReservoirPh = $reservoirPh,
                ReservoirEc = $reservoirEc,
                ReservoirWaterTempC = $reservoirWaterTempC,
                ReservoirLevelCm = $reservoirLevelCm,
                ReservoirLevelLiters = $reservoirLevelLiters,
                DissolvedOxygenMgL = $dissolvedOxygenMgL,
                OrpMv = $orpMv,
                TopOffLiters = $topOffLiters,
                AddbackEc = $addbackEc,
                SolutionChange = $solutionChange,
                PpfdMol = $ppfdMol,
                Co2Ppm = $co2Ppm,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddMeasurementParameters(command, measurement);
        command.Parameters.AddWithValue("$id", measurement.Id);
        command.ExecuteNonQuery();
    }

    public void DeleteMeasurement(int id)
    {
        using var connection = OpenConnection();
        using var photoCommand = connection.CreateCommand();
        photoCommand.CommandText = "SELECT RelativePath FROM Photos WHERE MeasurementId = $id;";
        photoCommand.Parameters.AddWithValue("$id", id);

        var filesToDelete = new List<string>();
        using (var reader = photoCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                var relativePath = reader["RelativePath"]?.ToString();
                if (!string.IsNullOrWhiteSpace(relativePath))
                {
                    filesToDelete.Add(relativePath);
                }
            }
        }

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Measurements WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();

        foreach (var relativePath in filesToDelete)
        {
            if (TryResolveUploadPath(relativePath, out var physicalPath) && File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }
        }
    }

    public List<PhotoAsset> GetPhotosForGrow(int growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Photos WHERE GrowId = $growId ORDER BY TakenAtUtc DESC, Id DESC;";
        command.Parameters.AddWithValue("$growId", growId);

        var items = new List<PhotoAsset>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(MapPhoto(reader));
        }
        return items;
    }

    public List<PhotoAsset> GetPhotosForMeasurement(int measurementId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Photos WHERE MeasurementId = $measurementId ORDER BY TakenAtUtc DESC, Id DESC;";
        command.Parameters.AddWithValue("$measurementId", measurementId);

        var items = new List<PhotoAsset>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(MapPhoto(reader));
        }
        return items;
    }

    public void AddPhoto(PhotoAsset photo)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Photos (GrowId, MeasurementId, RelativePath, Caption, Tag, Source, IsReferenceShot, TakenAtUtc)
            VALUES ($growId, $measurementId, $relativePath, $caption, $tag, $source, $isReferenceShot, $takenAtUtc);
        """;
        command.Parameters.AddWithValue("$growId", photo.GrowId);
        command.Parameters.AddWithValue("$measurementId", (object?)photo.MeasurementId ?? DBNull.Value);
        command.Parameters.AddWithValue("$relativePath", photo.RelativePath);
        command.Parameters.AddWithValue("$caption", (object?)photo.Caption ?? DBNull.Value);
        command.Parameters.AddWithValue("$tag", photo.Tag.ToString());
        command.Parameters.AddWithValue("$source", photo.Source.ToString());
        command.Parameters.AddWithValue("$isReferenceShot", photo.IsReferenceShot ? 1 : 0);
        command.Parameters.AddWithValue("$takenAtUtc", ToStorageUtc(photo.TakenAtUtc));
        command.ExecuteNonQuery();
    }


    public List<PhotoAsset> GetRecentPhotos(int limit = 18)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Photos ORDER BY TakenAtUtc DESC, Id DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        var items = new List<PhotoAsset>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(MapPhoto(reader));
        }
        return items;
    }

    public HomeAssistantSettings GetHomeAssistantSettings()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Key, Value FROM AppSettings WHERE Key IN ('ha:baseUrl','ha:accessToken','ha:enabled');";
        using var reader = command.ExecuteReader();

        var settings = new HomeAssistantSettings();
        while (reader.Read())
        {
            var key = reader["Key"]?.ToString();
            var value = reader["Value"]?.ToString();
            switch (key)
            {
                case "ha:baseUrl":
                    settings.BaseUrl = value;
                    break;
                case "ha:accessToken":
                    settings.AccessToken = value;
                    break;
                case "ha:enabled":
                    settings.Enabled = string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                    break;
            }
        }
        return settings;
    }

    public void SaveHomeAssistantSettings(HomeAssistantSettings settings)
    {
        using var connection = OpenConnection();
        UpsertSetting(connection, "ha:baseUrl", settings.BaseUrl);
        UpsertSetting(connection, "ha:accessToken", settings.AccessToken);
        UpsertSetting(connection, "ha:enabled", settings.Enabled ? "1" : "0");
    }

    public void AddTentSensorSnapshot(TentSensorSnapshot snapshot, TimeSpan? dedupeWindow = null)
    {
        var window = dedupeWindow ?? TimeSpan.FromMinutes(4);
        using var connection = OpenConnection();
        using var dedupe = connection.CreateCommand();
        dedupe.CommandText = """
            SELECT COUNT(*) FROM TentSensorSnapshots
            WHERE TentId = $tentId AND MetricKey = $metricKey AND CapturedAtUtc >= $threshold;
        """;
        dedupe.Parameters.AddWithValue("$tentId", snapshot.TentId);
        dedupe.Parameters.AddWithValue("$metricKey", snapshot.MetricKey);
        dedupe.Parameters.AddWithValue("$threshold", ToStorageUtc(snapshot.CapturedAtUtc.Subtract(window)));
        var recentCount = Convert.ToInt32(dedupe.ExecuteScalar() ?? 0);
        if (recentCount > 0)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO TentSensorSnapshots (TentId, MetricKey, Value, Unit, CapturedAtUtc)
            VALUES ($tentId, $metricKey, $value, $unit, $capturedAtUtc);
        """;
        command.Parameters.AddWithValue("$tentId", snapshot.TentId);
        command.Parameters.AddWithValue("$metricKey", snapshot.MetricKey);
        command.Parameters.AddWithValue("$value", snapshot.Value);
        command.Parameters.AddWithValue("$unit", (object?)snapshot.Unit ?? DBNull.Value);
        command.Parameters.AddWithValue("$capturedAtUtc", ToStorageUtc(snapshot.CapturedAtUtc));
        command.ExecuteNonQuery();
    }

    public List<TentSensorSnapshot> GetTentSensorSnapshots(int tentId, IEnumerable<string>? metricKeys = null, int limitPerMetric = 48)
    {
        var keys = metricKeys?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                   ?? ["temperature", "humidity", "vpd", "reservoir-ph", "reservoir-ec", "reservoir-level", "reservoir-temp"];
        if (keys.Count == 0)
        {
            return [];
        }

        using var connection = OpenConnection();
        var placeholders = string.Join(", ", keys.Select((_, i) => $"$k{i}"));
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            WITH ranked AS (
                SELECT *, ROW_NUMBER() OVER (PARTITION BY MetricKey ORDER BY CapturedAtUtc DESC) AS rn
                FROM TentSensorSnapshots
                WHERE TentId = $tentId AND MetricKey IN ({placeholders})
            )
            SELECT * FROM ranked WHERE rn <= $limit;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);
        command.Parameters.AddWithValue("$limit", limitPerMetric);
        for (var i = 0; i < keys.Count; i++)
        {
            command.Parameters.AddWithValue($"$k{i}", keys[i]);
        }

        var items = new List<TentSensorSnapshot>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(MapTentSensorSnapshot(reader));
        }
        return items;
    }

    private List<GrowRun> GetGrows(string whereClause, string? search, int? tentId = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT g.*, t.Name AS TentName,
                   (SELECT COUNT(*) FROM Measurements m WHERE m.GrowId = g.Id) AS MeasurementCount,
                   (SELECT RelativePath FROM Photos p WHERE p.GrowId = g.Id ORDER BY p.TakenAtUtc DESC LIMIT 1) AS LatestPhotoPath
            FROM Grows g
            LEFT JOIN Tents t ON t.Id = g.TentId
            {whereClause}
            ORDER BY g.StartDate DESC, g.Id DESC;
        """;

        if (!string.IsNullOrWhiteSpace(search))
        {
            command.Parameters.AddWithValue("$search", $"%{search.Trim()}%");
        }
        if (tentId.HasValue)
        {
            command.Parameters.AddWithValue("$tentId", tentId.Value);
        }

        var items = new List<GrowRun>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                items.Add(MapGrow(reader));
            }
        }

        if (items.Count > 0)
        {
            var latestMeasurements = GetLatestMeasurementsBatch(connection, items.Select(g => g.Id));
            foreach (var grow in items)
            {
                grow.LatestMeasurement = latestMeasurements.GetValueOrDefault(grow.Id);
            }
        }

        return items;
    }

    private List<PlantInstance> GetPlantsByWhere(string whereClause, int? setupId, int? growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT p.*, s.Name AS StrainName
            FROM PlantInstances p
            LEFT JOIN Strains s ON s.Id = p.StrainId
            {whereClause}
            ORDER BY CASE p.PlantStatus WHEN 'Active' THEN 0 WHEN 'Planned' THEN 1 ELSE 2 END, p.Label, p.Id;
        """;

        if (setupId.HasValue)
        {
            command.Parameters.AddWithValue("$setupId", setupId.Value);
        }
        if (growId.HasValue)
        {
            command.Parameters.AddWithValue("$growId", growId.Value);
        }

        var plants = new List<PlantInstance>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            plants.Add(MapPlant(reader));
        }
        return plants;
    }

    private List<AutoMeasurementConfig> GetAutoMeasurementConfigsByWhere(string whereClause, int? growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT *
            FROM AutoMeasurementConfigs
            {whereClause}
            ORDER BY Name, Id;
        """;

        if (growId.HasValue)
        {
            command.Parameters.AddWithValue("$growId", growId.Value);
        }

        var configs = new List<AutoMeasurementConfig>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            configs.Add(MapAutoMeasurementConfig(reader));
        }
        return configs;
    }

    private List<AutoMeasurementRun> GetAutoMeasurementRunsByWhere(string whereClause, int? configId, int? growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT *
            FROM AutoMeasurementRuns
            {whereClause}
            ORDER BY ScheduledForUtc DESC, Id DESC;
        """;

        if (configId.HasValue)
        {
            command.Parameters.AddWithValue("$configId", configId.Value);
        }
        if (growId.HasValue)
        {
            command.Parameters.AddWithValue("$growId", growId.Value);
        }

        var runs = new List<AutoMeasurementRun>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            runs.Add(MapAutoMeasurementRun(reader));
        }
        return runs;
    }

    private List<SopInstance> GetSopInstancesByGrow(int growId, bool activeOnly)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var statusFilter = activeOnly ? "AND si.Status = $status" : string.Empty;
        command.CommandText = $"""
            SELECT si.*, COUNT(ssi.Id) AS StepCount
            FROM SopInstances si
            LEFT JOIN SopStepInstances ssi ON ssi.SopInstanceId = si.Id
            WHERE si.GrowId = $growId {statusFilter}
            GROUP BY si.Id
            ORDER BY si.StartedAtUtc DESC, si.Id DESC;
        """;
        command.Parameters.AddWithValue("$growId", growId);
        if (activeOnly)
        {
            command.Parameters.AddWithValue("$status", SopInstanceStatus.Active.ToString());
        }

        var instances = new List<SopInstance>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            instances.Add(MapSopInstance(reader));
        }
        return instances;
    }

    private static void RecalculateSopInstanceStatus(SqliteConnection connection, SqliteTransaction transaction, int sopInstanceId, DateTime now)
    {
        int totalSteps;
        int openSteps;
        using (var countCommand = connection.CreateCommand())
        {
            countCommand.Transaction = transaction;
            countCommand.CommandText = """
                SELECT
                    COUNT(*) AS TotalSteps,
                    COALESCE(SUM(CASE WHEN Status IN ('Done', 'Skipped') THEN 0 ELSE 1 END), 0) AS OpenSteps
                FROM SopStepInstances
                WHERE SopInstanceId = $sopInstanceId;
            """;
            countCommand.Parameters.AddWithValue("$sopInstanceId", sopInstanceId);
            using var reader = countCommand.ExecuteReader();
            if (!reader.Read())
                return;
            totalSteps = Convert.ToInt32(reader["TotalSteps"], CultureInfo.InvariantCulture);
            openSteps = Convert.ToInt32(reader["OpenSteps"], CultureInfo.InvariantCulture);
        }

        if (totalSteps == 0)
            return;

        if (openSteps == 0)
        {
            // Alle Steps erledigt: SOP abschliessen, NextStepDueAtUtc auf null setzen
            using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = """
                UPDATE SopInstances
                SET Status = $status,
                    CompletedAtUtc = $completedAtUtc,
                    NextStepDueAtUtc = NULL,
                    UpdatedAtUtc = $updatedAtUtc
                WHERE Id = $id
                  AND Status = $activeStatus;
            """;
            updateCommand.Parameters.AddWithValue("$id", sopInstanceId);
            updateCommand.Parameters.AddWithValue("$status", SopInstanceStatus.Completed.ToString());
            updateCommand.Parameters.AddWithValue("$activeStatus", SopInstanceStatus.Active.ToString());
            updateCommand.Parameters.AddWithValue("$completedAtUtc", ToStorageUtc(now));
            updateCommand.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(now));
            updateCommand.ExecuteNonQuery();
        }
        else
        {
            // Offene Steps vorhanden: NextStepDueAtUtc aus frühester Fälligkeit berechnen
            string? minDueRaw;
            using (var dueCommand = connection.CreateCommand())
            {
                dueCommand.Transaction = transaction;
                dueCommand.CommandText = """
                    SELECT MIN(COALESCE(DueAtUtc, AvailableAtUtc)) AS MinDue
                    FROM SopStepInstances
                    WHERE SopInstanceId = $sopInstanceId
                      AND Status NOT IN ('Done', 'Skipped');
                """;
                dueCommand.Parameters.AddWithValue("$sopInstanceId", sopInstanceId);
                var raw = dueCommand.ExecuteScalar();
                minDueRaw = raw is DBNull or null ? null : raw.ToString();
            }

            using var updateNextCommand = connection.CreateCommand();
            updateNextCommand.Transaction = transaction;
            updateNextCommand.CommandText = """
                UPDATE SopInstances
                SET NextStepDueAtUtc = $nextStepDueAtUtc,
                    UpdatedAtUtc = $updatedAtUtc
                WHERE Id = $id;
            """;
            updateNextCommand.Parameters.AddWithValue("$id", sopInstanceId);
            updateNextCommand.Parameters.AddWithValue("$nextStepDueAtUtc", minDueRaw is not null ? (object)minDueRaw : DBNull.Value);
            updateNextCommand.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(now));
            updateNextCommand.ExecuteNonQuery();
        }
    }

    private static Dictionary<int, Measurement> GetLatestMeasurementsBatch(SqliteConnection connection, IEnumerable<int> growIds)
    {
        var ids = growIds.ToList();
        if (ids.Count == 0) return [];

        var placeholders = string.Join(", ", ids.Select((_, i) => $"$p{i}"));
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            WITH ranked AS (
                SELECT *, ROW_NUMBER() OVER (PARTITION BY GrowId ORDER BY TakenAt DESC, Id DESC) AS rn
                FROM Measurements
                WHERE GrowId IN ({placeholders})
            )
            SELECT * FROM ranked WHERE rn = 1;
        """;
        for (var i = 0; i < ids.Count; i++)
        {
            command.Parameters.AddWithValue($"$p{i}", ids[i]);
        }

        var result = new Dictionary<int, Measurement>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var m = MapMeasurement(reader);
            result[m.GrowId] = m;
        }
        return result;
    }

    private static string SearchClause(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return string.Empty;
        }
        return " AND (g.Name LIKE $search OR g.Strain LIKE $search OR g.Breeder LIKE $search OR g.Nutrients LIKE $search OR t.Name LIKE $search)";
    }

    private List<HardwareItem> GetHardwareItemsByWhere(string whereClause, object? value)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT *
            FROM HardwareItems
            {whereClause}
            ORDER BY CASE Status WHEN 'Active' THEN 0 WHEN 'MaintenanceDue' THEN 1 WHEN 'Offline' THEN 2 ELSE 3 END,
                     Name,
                     Id;
        """;
        if (value is not null)
        {
            command.Parameters.AddWithValue("$value", value);
        }

        var list = new List<HardwareItem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapHardwareItem(reader));
        }
        return list;
    }

    private List<MaintenanceEvent> GetMaintenanceEventsByWhere(string whereClause, object? value)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT *
            FROM MaintenanceEvents
            {whereClause}
            ORDER BY COALESCE(DueAtUtc, NextDueAtUtc, PerformedAtUtc, CreatedAtUtc) ASC, Id DESC;
        """;
        if (value is not null)
        {
            command.Parameters.AddWithValue("$value", value);
        }

        var list = new List<MaintenanceEvent>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapMaintenanceEvent(reader));
        }
        return list;
    }

    private List<CalibrationEvent> GetCalibrationEventsByWhere(string whereClause, object? value)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT *
            FROM CalibrationEvents
            {whereClause}
            ORDER BY COALESCE(DueAtUtc, NextDueAtUtc, PerformedAtUtc, CreatedAtUtc) ASC, Id DESC;
        """;
        if (value is not null)
        {
            command.Parameters.AddWithValue("$value", value);
        }

        var list = new List<CalibrationEvent>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapCalibrationEvent(reader));
        }
        return list;
    }

    private static void UpsertSetting(SqliteConnection connection, string key, string? value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AppSettings (Key, Value)
            VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
        """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", (object?)value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private void ValidateSetupTentCompatibility(int tentId, SetupType setupType)
    {
        var tent = GetTent(tentId);
        if (tent is null)
        {
            throw new InvalidOperationException($"Tent with id {tentId} does not exist.");
        }

        if (!SetupTentCompatibilityPolicy.IsCompatible(tent.TentType, setupType))
        {
            throw new InvalidOperationException($"Setup type {setupType} is not supported in tent type {tent.TentType}.");
        }
    }

    private void ValidateHardwareItem(HardwareItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            throw new InvalidOperationException("HardwareItem name must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(item.Category))
        {
            throw new InvalidOperationException("HardwareItem category must not be empty.");
        }

        if (item.TentId.HasValue && GetTent(item.TentId.Value) is null)
        {
            throw new InvalidOperationException($"Tent with id {item.TentId.Value} does not exist.");
        }

        if (item.SetupId.HasValue && GetSetup(item.SetupId.Value) is null)
        {
            throw new InvalidOperationException($"Setup with id {item.SetupId.Value} does not exist.");
        }

        if (item.GrowId.HasValue && GetGrow(item.GrowId.Value) is null)
        {
            throw new InvalidOperationException($"Grow with id {item.GrowId.Value} does not exist.");
        }

        if (item.TentSensorId.HasValue && GetTentSensor(item.TentSensorId.Value) is null)
        {
            throw new InvalidOperationException($"TentSensor with id {item.TentSensorId.Value} does not exist.");
        }

        if (item.InstalledAtUtc.HasValue &&
            item.RetiredAtUtc.HasValue &&
            item.RetiredAtUtc.Value.ToUniversalTime() < item.InstalledAtUtc.Value.ToUniversalTime())
        {
            throw new InvalidOperationException("RetiredAtUtc must not be before InstalledAtUtc.");
        }
    }

    private static void ApplyRetiredTimestamp(HardwareItem item)
    {
        if (item.Status == HardwareItemStatus.Retired && !item.RetiredAtUtc.HasValue)
        {
            item.RetiredAtUtc = DateTime.UtcNow;
        }
    }

    private HardwareItem ValidateMaintenanceEvent(MaintenanceEvent item)
    {
        var hardware = GetHardwareItem(item.HardwareItemId);
        if (hardware is null)
        {
            throw new InvalidOperationException($"HardwareItem with id {item.HardwareItemId} does not exist.");
        }

        if (string.IsNullOrWhiteSpace(item.Title))
        {
            throw new InvalidOperationException("MaintenanceEvent title must not be empty.");
        }

        if (!Enum.IsDefined(item.EventType))
        {
            throw new InvalidOperationException("MaintenanceEvent event type is invalid.");
        }

        if (!Enum.IsDefined(item.Status))
        {
            throw new InvalidOperationException("MaintenanceEvent status is invalid.");
        }

        if (!Enum.IsDefined(item.Result))
        {
            throw new InvalidOperationException("MaintenanceEvent result is invalid.");
        }

        if (item.PerformedAtUtc.HasValue &&
            item.NextDueAtUtc.HasValue &&
            item.NextDueAtUtc.Value.ToUniversalTime() < item.PerformedAtUtc.Value.ToUniversalTime())
        {
            throw new InvalidOperationException("NextDueAtUtc must not be before PerformedAtUtc.");
        }

        return hardware;
    }

    private static void ApplyMaintenanceDefaults(MaintenanceEvent item, HardwareItem hardware)
    {
        if (item.Status == MaintenanceEventStatus.Planned)
        {
            item.PerformedAtUtc = null;
            return;
        }

        if (item.Status == MaintenanceEventStatus.Completed && !item.PerformedAtUtc.HasValue)
        {
            item.PerformedAtUtc = DateTime.UtcNow;
        }

        if (item.Status == MaintenanceEventStatus.Completed &&
            !item.NextDueAtUtc.HasValue &&
            item.PerformedAtUtc.HasValue &&
            hardware.InspectionIntervalDays.HasValue)
        {
            item.NextDueAtUtc = item.PerformedAtUtc.Value.ToUniversalTime().AddDays(hardware.InspectionIntervalDays.Value);
        }
    }

    private static int? TryCreateMaintenanceReminderTask(
        SqliteConnection connection,
        SqliteTransaction transaction,
        MaintenanceEvent item,
        HardwareItem hardware)
    {
        if (item.Status != MaintenanceEventStatus.Planned || !item.DueAtUtc.HasValue || !hardware.GrowId.HasValue)
        {
            return null;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO GrowTasks (GrowId, Title, Notes, DueAtUtc, Priority, Status, CreatedAtUtc, CompletedAtUtc)
            VALUES ($growId, $title, $notes, $dueAtUtc, $priority, 'Open', $createdAtUtc, NULL);
            SELECT last_insert_rowid();
        """;
        command.Parameters.AddWithValue("$growId", hardware.GrowId.Value);
        command.Parameters.AddWithValue("$title", $"Wartung: {hardware.Name} - {item.Title}");
        command.Parameters.AddWithValue("$notes", (object?)item.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$dueAtUtc", ToStorageUtc(item.DueAtUtc.Value));
        command.Parameters.AddWithValue("$priority", ToMaintenanceTaskPriority(hardware.Criticality).ToString());
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(DateTime.UtcNow));
        return Convert.ToInt32((long)command.ExecuteScalar()!);
    }

    private static TaskPriority ToMaintenanceTaskPriority(HardwareItemCriticality criticality)
        => criticality switch
        {
            HardwareItemCriticality.Critical or HardwareItemCriticality.High => TaskPriority.High,
            HardwareItemCriticality.Low => TaskPriority.Low,
            _ => TaskPriority.Normal
        };

    private HardwareItem ValidateCalibrationEvent(CalibrationEvent item)
    {
        var hardware = GetHardwareItem(item.HardwareItemId);
        if (hardware is null)
        {
            throw new InvalidOperationException($"HardwareItem with id {item.HardwareItemId} does not exist.");
        }

        if (string.IsNullOrWhiteSpace(item.Title))
        {
            throw new InvalidOperationException("CalibrationEvent title must not be empty.");
        }

        if (!Enum.IsDefined(item.CalibrationType))
        {
            throw new InvalidOperationException("CalibrationEvent type is invalid.");
        }

        if (!Enum.IsDefined(item.Status))
        {
            throw new InvalidOperationException("CalibrationEvent status is invalid.");
        }

        if (!Enum.IsDefined(item.Result))
        {
            throw new InvalidOperationException("CalibrationEvent result is invalid.");
        }

        if (item.TemperatureC is < -10m or > 60m)
        {
            throw new InvalidOperationException("TemperatureC must be between -10 and 60.");
        }

        if (item.PerformedAtUtc.HasValue &&
            item.NextDueAtUtc.HasValue &&
            item.NextDueAtUtc.Value.ToUniversalTime() < item.PerformedAtUtc.Value.ToUniversalTime())
        {
            throw new InvalidOperationException("NextDueAtUtc must not be before PerformedAtUtc.");
        }

        return hardware;
    }

    private static void ApplyCalibrationDefaults(CalibrationEvent item)
    {
        if (item.Status == CalibrationEventStatus.Planned)
        {
            item.PerformedAtUtc = null;
            return;
        }

        if ((item.Status == CalibrationEventStatus.Completed || item.Status == CalibrationEventStatus.Failed) &&
            !item.PerformedAtUtc.HasValue)
        {
            item.PerformedAtUtc = DateTime.UtcNow;
        }

        if ((item.Status == CalibrationEventStatus.Completed || item.Status == CalibrationEventStatus.Failed) &&
            !item.NextDueAtUtc.HasValue &&
            item.PerformedAtUtc.HasValue)
        {
            var intervalDays = item.CalibrationType switch
            {
                CalibrationEventType.Ph => 14,
                CalibrationEventType.Ec or CalibrationEventType.Orp or CalibrationEventType.Do => 30,
                _ => (int?)null
            };

            if (intervalDays.HasValue)
            {
                item.NextDueAtUtc = item.PerformedAtUtc.Value.ToUniversalTime().AddDays(intervalDays.Value);
            }
        }
    }

    private static int? TryCreateCalibrationReminderTask(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CalibrationEvent item,
        HardwareItem hardware)
    {
        if (item.Status != CalibrationEventStatus.Planned || !item.DueAtUtc.HasValue || !hardware.GrowId.HasValue)
        {
            return null;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO GrowTasks (GrowId, Title, Notes, DueAtUtc, Priority, Status, CreatedAtUtc, CompletedAtUtc)
            VALUES ($growId, $title, $notes, $dueAtUtc, $priority, 'Open', $createdAtUtc, NULL);
            SELECT last_insert_rowid();
        """;
        command.Parameters.AddWithValue("$growId", hardware.GrowId.Value);
        command.Parameters.AddWithValue("$title", $"Kalibrierung: {hardware.Name} - {item.Title}");
        command.Parameters.AddWithValue("$notes", (object?)item.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$dueAtUtc", ToStorageUtc(item.DueAtUtc.Value));
        command.Parameters.AddWithValue("$priority", ToMaintenanceTaskPriority(hardware.Criticality).ToString());
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(DateTime.UtcNow));
        return Convert.ToInt32((long)command.ExecuteScalar()!);
    }

    private SqliteConnection OpenConnection()
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private static GrowRun MapGrow(SqliteDataReader reader)
    {
        return new GrowRun
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            TentId = reader["TentId"] is DBNull ? null : Convert.ToInt32((long)reader["TentId"]),
            SystemId = reader["SystemId"] is DBNull or null ? null : Convert.ToInt32((long)reader["SystemId"]),
            SetupId = reader["SetupId"] is DBNull or null ? null : Convert.ToInt32((long)reader["SetupId"]),
            TentName = NullString(reader["TentName"]),
            Name = reader["Name"]?.ToString() ?? string.Empty,
            Strain = NullString(reader["Strain"]),
            Breeder = NullString(reader["Breeder"]),
            Status = ParseEnum(reader["Status"]?.ToString(), GrowStatus.Planning),
            MediumType = ParseEnum(reader["MediumType"]?.ToString(), MediumType.Hydro),
            FeedingStyle = ParseEnum(reader["FeedingStyle"]?.ToString(), FeedingStyle.None),
            HydroStyle = ParseEnum(reader["HydroStyle"]?.ToString(), HydroStyle.None),
            MediumDetail = NullString(reader["MediumDetail"]),
            Environment = ParseEnum(reader["Environment"]?.ToString(), GrowEnvironment.Indoor),
            Light = NullString(reader["Light"]),
            ContainerSize = NullString(reader["ContainerSize"]),
            ReservoirSize = NullString(reader["ReservoirSize"]),
            IrrigationStyle = NullString(reader["IrrigationStyle"]),
            IrrigationType = ParseEnum(reader["IrrigationType"]?.ToString(), IrrigationType.ActiveHydro),
            WaterSource = ParseEnum(reader["WaterSource"]?.ToString(), WaterSource.Tap),
            SeedType = ParseEnum(reader["SeedType"]?.ToString(), SeedType.Feminized),
            StartMaterial = ParseEnum(reader["StartMaterial"]?.ToString(), StartMaterial.Seed),
            GerminationMethod = reader["GerminationMethod"] is DBNull or null ? null : Enum.TryParse<GerminationMethod>(reader["GerminationMethod"]?.ToString(), out var gm) ? gm : null,
            CloneSource = NullString(reader["CloneSource"]),
            CloneIsRooted = reader["CloneIsRooted"] is long cr && cr == 1,
            BreederFlowerWeeksMin = reader["BreederFlowerWeeksMin"] is DBNull or null ? null : Convert.ToInt32(reader["BreederFlowerWeeksMin"], CultureInfo.InvariantCulture),
            BreederFlowerWeeksMax = reader["BreederFlowerWeeksMax"] is DBNull or null ? null : Convert.ToInt32(reader["BreederFlowerWeeksMax"], CultureInfo.InvariantCulture),
            PlantCount = reader["PlantCount"] is DBNull or null ? null : Convert.ToInt32(reader["PlantCount"], CultureInfo.InvariantCulture),
            PhenoNumber = reader["PhenoNumber"] is DBNull or null ? null : Convert.ToInt32(reader["PhenoNumber"], CultureInfo.InvariantCulture),
            PropagationMedium = reader["PropagationMedium"] is DBNull or null ? null : Enum.TryParse<PropagationMedium>(reader["PropagationMedium"]?.ToString(), out var pm) ? pm : null,
            HasChiller = reader["HasChiller"] is long hc && hc == 1,
            EntryPoint = ParseEnum(reader["EntryPoint"]?.ToString(), GrowEntryPoint.Germination),
            DaysAlreadyInPhase = reader["DaysAlreadyInPhase"] is DBNull or null ? null : Convert.ToInt32(reader["DaysAlreadyInPhase"], CultureInfo.InvariantCulture),
            AutoflowerDaysSinceGermination = reader["AutoflowerDaysSinceGermination"] is DBNull or null ? null : Convert.ToInt32(reader["AutoflowerDaysSinceGermination"], CultureInfo.InvariantCulture),
            FlipDate = ParseStoredDate(reader["FlipDate"]?.ToString()),
            GerminatedAt = reader["GerminatedAt"] is DBNull or null ? null : (DateTime.TryParse(reader["GerminatedAt"]?.ToString(), out var ga) ? ga : (DateTime?)null),
            RootedAt = reader["RootedAt"] is DBNull or null ? null : (DateTime.TryParse(reader["RootedAt"]?.ToString(), out var ra) ? ra : (DateTime?)null),
            Nutrients = NullString(reader["Nutrients"]),
            Notes = NullString(reader["Notes"]),
            StartDate = ParseStoredDate(reader["StartDate"]?.ToString()) ?? DateTime.Today,
            EndDate = ParseStoredDate(reader["EndDate"]?.ToString()),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            MeasurementCount = reader["MeasurementCount"] is DBNull ? 0 : Convert.ToInt32(reader["MeasurementCount"], CultureInfo.InvariantCulture),
            LatestPhotoPath = NullString(reader["LatestPhotoPath"])
        };
    }

    private static Setup MapSetup(SqliteDataReader reader)
    {
        return new Setup
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            TentId = Convert.ToInt32((long)reader["TentId"]),
            Name = reader["Name"]?.ToString() ?? string.Empty,
            SetupType = ParseEnum(reader["SetupType"]?.ToString(), SetupType.Production),
            Status = ParseEnum(reader["Status"]?.ToString(), SetupStatus.Planning),
            Notes = NullString(reader["Notes"]),
            CloneCounterTotal = reader["CloneCounterTotal"] is DBNull or null ? null : Convert.ToInt32(reader["CloneCounterTotal"], CultureInfo.InvariantCulture),
            LastCloneCutAt = ParseStoredDateTime(reader["LastCloneCutAt"]?.ToString()),
            MotherHealthStatus = NullString(reader["MotherHealthStatus"]),
            QuarantineStartedAt = ParseStoredDateTime(reader["QuarantineStartedAt"]?.ToString()),
            QuarantinePlannedEndAt = ParseStoredDateTime(reader["QuarantinePlannedEndAt"]?.ToString()),
            QuarantineResult = NullString(reader["QuarantineResult"]),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static Strain MapStrain(SqliteDataReader reader)
    {
        return new Strain
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            Name = reader["Name"]?.ToString() ?? string.Empty,
            Breeder = NullString(reader["Breeder"]),
            Dominance = ParseEnum(reader["Dominance"]?.ToString(), StrainDominance.Unknown),
            FlowerWeeksMin = reader["FlowerWeeksMin"] is DBNull or null ? null : Convert.ToInt32(reader["FlowerWeeksMin"], CultureInfo.InvariantCulture),
            FlowerWeeksMax = reader["FlowerWeeksMax"] is DBNull or null ? null : Convert.ToInt32(reader["FlowerWeeksMax"], CultureInfo.InvariantCulture),
            Notes = NullString(reader["Notes"]),
            NutrientDemandFactor = NullableDouble(reader["NutrientDemandFactor"]),
            StretchFactor = NullableDouble(reader["StretchFactor"]),
            VpdPreferenceShift = NullableDouble(reader["VpdPreferenceShift"]),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static PlantInstance MapPlant(SqliteDataReader reader)
    {
        return new PlantInstance
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            StrainId = reader["StrainId"] is DBNull or null ? null : Convert.ToInt32(reader["StrainId"], CultureInfo.InvariantCulture),
            SetupId = reader["SetupId"] is DBNull or null ? null : Convert.ToInt32(reader["SetupId"], CultureInfo.InvariantCulture),
            GrowId = reader["GrowId"] is DBNull or null ? null : Convert.ToInt32(reader["GrowId"], CultureInfo.InvariantCulture),
            ParentPlantId = reader["ParentPlantId"] is DBNull or null ? null : Convert.ToInt32(reader["ParentPlantId"], CultureInfo.InvariantCulture),
            Label = reader["Label"]?.ToString() ?? string.Empty,
            PlantRole = ParseEnum(reader["PlantRole"]?.ToString(), PlantRole.Production),
            PlantStatus = ParseEnum(reader["PlantStatus"]?.ToString(), PlantStatus.Planned),
            PhenoLabel = NullString(reader["PhenoLabel"]),
            StartedAt = ParseStoredDateTime(reader["StartedAt"]?.ToString()),
            EndedAt = ParseStoredDateTime(reader["EndedAt"]?.ToString()),
            Notes = NullString(reader["Notes"]),
            StrainName = NullString(reader["StrainName"]),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static AutoMeasurementConfig MapAutoMeasurementConfig(SqliteDataReader reader)
    {
        return new AutoMeasurementConfig
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            GrowId = Convert.ToInt32(reader["GrowId"], CultureInfo.InvariantCulture),
            TentId = reader["TentId"] is DBNull or null ? null : Convert.ToInt32(reader["TentId"], CultureInfo.InvariantCulture),
            Name = reader["Name"]?.ToString() ?? string.Empty,
            Status = ParseEnum(reader["Status"]?.ToString(), AutoMeasurementStatus.Enabled),
            TriggerKind = ParseEnum(reader["TriggerKind"]?.ToString(), AutoMeasurementTriggerKind.Manual),
            DelayMinutes = reader["DelayMinutes"] is DBNull or null ? null : Convert.ToInt32(reader["DelayMinutes"], CultureInfo.InvariantCulture),
            WindowMinutes = Convert.ToInt32(reader["WindowMinutes"], CultureInfo.InvariantCulture),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static AutoMeasurementFieldMapping MapAutoMeasurementFieldMapping(SqliteDataReader reader)
    {
        return new AutoMeasurementFieldMapping
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            ConfigId = Convert.ToInt32(reader["ConfigId"], CultureInfo.InvariantCulture),
            MeasurementField = ParseEnum(reader["MeasurementField"]?.ToString(), AutoMeasurementField.AirTemperatureC),
            MetricKey = reader["MetricKey"]?.ToString() ?? string.Empty,
            Aggregation = ParseEnum(reader["Aggregation"]?.ToString(), AutoMeasurementAggregation.Latest),
            IsRequired = reader["IsRequired"] is not DBNull and not null && Convert.ToInt32(reader["IsRequired"], CultureInfo.InvariantCulture) == 1,
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static AutoMeasurementRun MapAutoMeasurementRun(SqliteDataReader reader)
    {
        return new AutoMeasurementRun
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            ConfigId = Convert.ToInt32(reader["ConfigId"], CultureInfo.InvariantCulture),
            GrowId = Convert.ToInt32(reader["GrowId"], CultureInfo.InvariantCulture),
            TriggerKind = ParseEnum(reader["TriggerKind"]?.ToString(), AutoMeasurementTriggerKind.Manual),
            ScheduledForUtc = ParseStoredDateTime(reader["ScheduledForUtc"]?.ToString()) ?? DateTime.UtcNow,
            MeasurementId = reader["MeasurementId"] is DBNull or null ? null : Convert.ToInt32(reader["MeasurementId"], CultureInfo.InvariantCulture),
            Status = ParseEnum(reader["Status"]?.ToString(), AutoMeasurementRunStatus.Pending),
            ErrorMessage = NullString(reader["ErrorMessage"]),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static LightSchedule MapLightSchedule(SqliteDataReader reader)
    {
        return new LightSchedule
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            TentId = Convert.ToInt32(reader["TentId"], CultureInfo.InvariantCulture),
            Name = reader["Name"]?.ToString() ?? string.Empty,
            IsActive = reader["IsActive"] is not DBNull and not null && Convert.ToInt32(reader["IsActive"], CultureInfo.InvariantCulture) == 1,
            LightsOnTime = reader["LightsOnTime"]?.ToString() ?? string.Empty,
            LightsOffTime = reader["LightsOffTime"]?.ToString() ?? string.Empty,
            TimeZoneId = NullString(reader["TimeZoneId"]),
            Source = ParseEnum(reader["Source"]?.ToString(), LightSource.Manual),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static LightTransitionEvent MapLightTransitionEvent(SqliteDataReader reader)
    {
        return new LightTransitionEvent
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            TentId = Convert.ToInt32(reader["TentId"], CultureInfo.InvariantCulture),
            Kind = ParseEnum(reader["Kind"]?.ToString(), LightTransitionKind.LightOn),
            OccurredAtUtc = ParseStoredDateTime(reader["OccurredAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            Source = ParseEnum(reader["Source"]?.ToString(), LightSource.HomeAssistant),
            RawState = NullString(reader["RawState"]),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static SopInstance MapSopInstance(SqliteDataReader reader)
    {
        return new SopInstance
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            GrowId = Convert.ToInt32(reader["GrowId"], CultureInfo.InvariantCulture),
            SopId = reader["SopId"]?.ToString() ?? string.Empty,
            SopName = reader["SopName"]?.ToString() ?? string.Empty,
            SopType = reader["SopType"]?.ToString() ?? string.Empty,
            Status = ParseEnum(reader["Status"]?.ToString(), SopInstanceStatus.Active),
            Source = ParseEnum(reader["Source"]?.ToString(), SopStartSource.Manual),
            SourceRecommendationKey = NullString(reader["SourceRecommendationKey"]),
            TreatmentRecommendationStableKey = NullString(reader["TreatmentRecommendationStableKey"]),
            StartedAtUtc = ParseStoredDateTime(reader["StartedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            CompletedAtUtc = ParseStoredDateTime(reader["CompletedAtUtc"]?.ToString()),
            CancelledAtUtc = ParseStoredDateTime(reader["CancelledAtUtc"]?.ToString()),
            DueAtUtc = ParseStoredDateTimeIfColumn(reader, "DueAtUtc"),
            NextStepDueAtUtc = ParseStoredDateTimeIfColumn(reader, "NextStepDueAtUtc"),
            RecurrenceIntervalDays = HasColumn(reader, "RecurrenceIntervalDays") && reader["RecurrenceIntervalDays"] is not DBNull
                ? Convert.ToInt32(reader["RecurrenceIntervalDays"], CultureInfo.InvariantCulture)
                : null,
            IsRecurring = HasColumn(reader, "IsRecurring") && reader["IsRecurring"] is not DBNull
                && Convert.ToInt32(reader["IsRecurring"], CultureInfo.InvariantCulture) == 1,
            Notes = NullString(reader["Notes"]),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            StepCount = HasColumn(reader, "StepCount") && reader["StepCount"] is not DBNull
                ? Convert.ToInt32(reader["StepCount"], CultureInfo.InvariantCulture)
                : 0
        };
    }

    private static SopStepInstance MapSopStepInstance(SqliteDataReader reader)
    {
        return new SopStepInstance
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            SopInstanceId = Convert.ToInt32(reader["SopInstanceId"], CultureInfo.InvariantCulture),
            StepId = reader["StepId"]?.ToString() ?? string.Empty,
            Order = Convert.ToInt32(reader["Order"], CultureInfo.InvariantCulture),
            Title = reader["Title"]?.ToString() ?? string.Empty,
            Description = NullString(reader["Description"]),
            StepType = reader["StepType"]?.ToString() ?? string.Empty,
            Status = ParseEnum(reader["Status"]?.ToString(), SopStepInstanceStatus.Pending),
            WaitMinutes = reader["WaitMinutes"] is DBNull or null ? null : Convert.ToInt32(reader["WaitMinutes"], CultureInfo.InvariantCulture),
            SubSopId = NullString(reader["SubSopId"]),
            ExpectedInputsJson = NullString(reader["ExpectedInputsJson"]),
            PhotoRequired = reader["PhotoRequired"] is not DBNull and not null && Convert.ToInt32(reader["PhotoRequired"], CultureInfo.InvariantCulture) == 1,
            PhotoRecommended = reader["PhotoRecommended"] is not DBNull and not null && Convert.ToInt32(reader["PhotoRecommended"], CultureInfo.InvariantCulture) == 1,
            DueAtUtc = ParseStoredDateTimeIfColumn(reader, "DueAtUtc"),
            AvailableAtUtc = ParseStoredDateTimeIfColumn(reader, "AvailableAtUtc"),
            ReminderTaskId = HasColumn(reader, "ReminderTaskId") && reader["ReminderTaskId"] is not DBNull
                ? Convert.ToInt32(reader["ReminderTaskId"], CultureInfo.InvariantCulture)
                : null,
            StartedAtUtc = ParseStoredDateTime(reader["StartedAtUtc"]?.ToString()),
            CompletedAtUtc = ParseStoredDateTime(reader["CompletedAtUtc"]?.ToString()),
            SkippedAtUtc = ParseStoredDateTime(reader["SkippedAtUtc"]?.ToString()),
            Notes = NullString(reader["Notes"]),
            MeasurementId = reader["MeasurementId"] is DBNull or null ? null : Convert.ToInt32(reader["MeasurementId"], CultureInfo.InvariantCulture),
            JournalEntryId = reader["JournalEntryId"] is DBNull or null ? null : Convert.ToInt32(reader["JournalEntryId"], CultureInfo.InvariantCulture),
            PhotoAssetId = reader["PhotoAssetId"] is DBNull or null ? null : Convert.ToInt32(reader["PhotoAssetId"], CultureInfo.InvariantCulture),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static Tent MapTent(SqliteDataReader reader)
    {
        return new Tent
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            Name = reader["Name"]?.ToString() ?? string.Empty,
            Kind = reader["Kind"]?.ToString() ?? "Grow Tent",
            TentType = ParseEnum(NullString(reader["TentType"]), TentType.MultiPurpose),
            Notes = NullString(reader["Notes"]),
            DisplayOrder = Convert.ToInt32(reader["DisplayOrder"], CultureInfo.InvariantCulture),
            AccentColor = reader["AccentColor"]?.ToString() ?? "#69b578",
            WidthCm             = reader["WidthCm"] is DBNull or null ? null : Convert.ToInt32(reader["WidthCm"], CultureInfo.InvariantCulture),
            DepthCm             = reader["DepthCm"] is DBNull or null ? null : Convert.ToInt32(reader["DepthCm"], CultureInfo.InvariantCulture),
            TentHeightCm        = reader["TentHeightCm"] is DBNull or null ? null : Convert.ToInt32(reader["TentHeightCm"], CultureInfo.InvariantCulture),
            LightType           = NullString(reader["LightType"]),
            LightWatt           = reader["LightWatt"] is DBNull or null ? null : Convert.ToInt32(reader["LightWatt"], CultureInfo.InvariantCulture),
            LightController     = Enum.TryParse<LightControllerType>(NullString(reader["LightController"]), out var lc) ? lc : (LightControllerType?)null,
            LightControllerEntityId = NullString(reader["LightControllerEntityId"]),
            ExhaustFanCount     = reader["ExhaustFanCount"] is DBNull or null ? null : Convert.ToInt32(reader["ExhaustFanCount"], CultureInfo.InvariantCulture),
            ExhaustM3h          = reader["ExhaustM3h"] is DBNull or null ? null : Convert.ToInt32(reader["ExhaustM3h"], CultureInfo.InvariantCulture),
            CirculationFanCount = reader["CirculationFanCount"] is DBNull or null ? null : Convert.ToInt32(reader["CirculationFanCount"], CultureInfo.InvariantCulture),
            HvacController      = Enum.TryParse<HvacControllerType>(NullString(reader["HvacController"]), out var hc) ? hc : (HvacControllerType?)null,
            HvacControllerEntityId = NullString(reader["HvacControllerEntityId"]),
            Co2Available        = reader["Co2Available"] is not DBNull and not null && Convert.ToInt32(reader["Co2Available"], CultureInfo.InvariantCulture) == 1,
            CameraEntityId      = NullString(reader["CameraEntityId"]),
            ActiveGrowCount = reader["ActiveGrowCount"] is DBNull ? 0 : Convert.ToInt32(reader["ActiveGrowCount"], CultureInfo.InvariantCulture),
            ArchivedGrowCount = reader["ArchivedGrowCount"] is DBNull ? 0 : Convert.ToInt32(reader["ArchivedGrowCount"], CultureInfo.InvariantCulture),
            ActiveSetupCount = reader["ActiveSetupCount"] is DBNull ? 0 : Convert.ToInt32(reader["ActiveSetupCount"], CultureInfo.InvariantCulture),
            ArchivedSetupCount = reader["ArchivedSetupCount"] is DBNull ? 0 : Convert.ToInt32(reader["ArchivedSetupCount"], CultureInfo.InvariantCulture)
        };
    }

    private static Measurement MapMeasurement(SqliteDataReader reader)
    {
        return new Measurement
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            GrowId = Convert.ToInt32((long)reader["GrowId"]),
            TakenAt = ParseStoredDateTime(reader["TakenAt"]?.ToString()) ?? DateTime.Now,
            Stage = ParseEnum(reader["Stage"]?.ToString(), GrowStage.Veg),
            Source = ParseEnum(reader["Source"]?.ToString(), ValueOrigin.Manual),
            Notes = NullString(reader["Notes"]),
            AirTemperatureC = NullableDouble(reader["AirTemperatureC"]),
            HumidityPercent = NullableDouble(reader["HumidityPercent"]),
            HeightCm = NullableDouble(reader["HeightCm"]),
            WaterAmountMl = NullableDouble(reader["WaterAmountMl"]),
            RunoffAmountMl = NullableDouble(reader["RunoffAmountMl"]),
            IrrigationPh = NullableDouble(reader["IrrigationPh"]),
            IrrigationEc = NullableDouble(reader["IrrigationEc"]),
            DrainPh = NullableDouble(reader["DrainPh"]),
            DrainEc = NullableDouble(reader["DrainEc"]),
            ReservoirPh = NullableDouble(reader["ReservoirPh"]),
            ReservoirEc = NullableDouble(reader["ReservoirEc"]),
            ReservoirWaterTempC = NullableDouble(reader["ReservoirWaterTempC"]),
            ReservoirLevelCm = NullableDouble(reader["ReservoirLevelCm"]),
            ReservoirLevelLiters = NullableDouble(reader["ReservoirLevelLiters"]),
            DissolvedOxygenMgL = NullableDouble(reader["DissolvedOxygenMgL"]),
            OrpMv = NullableDouble(reader["OrpMv"]),
            TopOffLiters = NullableDouble(reader["TopOffLiters"]),
            AddbackEc = NullableDouble(reader["AddbackEc"]),
            SolutionChange = reader["SolutionChange"] is not DBNull && Convert.ToInt32(reader["SolutionChange"], CultureInfo.InvariantCulture) == 1,
            PpfdMol = NullableDouble(reader["PpfdMol"]),
            Co2Ppm = NullableDouble(reader["Co2Ppm"]),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static PhotoAsset MapPhoto(SqliteDataReader reader)
    {
        return new PhotoAsset
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            GrowId = Convert.ToInt32((long)reader["GrowId"]),
            MeasurementId = reader["MeasurementId"] is DBNull ? null : Convert.ToInt32((long)reader["MeasurementId"]),
            RelativePath = reader["RelativePath"]?.ToString() ?? string.Empty,
            Caption = NullString(reader["Caption"]),
            Tag = ParseEnum(reader["Tag"]?.ToString(), PhotoTag.Overview),
            Source = ParseEnum(reader["Source"]?.ToString(), ValueOrigin.Manual),
            IsReferenceShot = reader["IsReferenceShot"] is not DBNull && Convert.ToInt32(reader["IsReferenceShot"], CultureInfo.InvariantCulture) == 1,
            TakenAtUtc = ParseStoredDateTime(reader["TakenAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static TentSensorSnapshot MapTentSensorSnapshot(SqliteDataReader reader)
    {
        return new TentSensorSnapshot
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            TentId = Convert.ToInt32((long)reader["TentId"]),
            MetricKey = reader["MetricKey"]?.ToString() ?? string.Empty,
            Value = Convert.ToDouble(reader["Value"], CultureInfo.InvariantCulture),
            Unit = NullString(reader["Unit"]),
            CapturedAtUtc = ParseStoredDateTime(reader["CapturedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

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

    private static TentSensor MapTentSensor(SqliteDataReader reader)
    {
        return new TentSensor
        {
            Id           = Convert.ToInt32((long)reader["Id"]),
            TentId       = Convert.ToInt32((long)reader["TentId"]),
            MetricType   = ParseEnum(reader["MetricType"]?.ToString(), SensorMetricType.AirTemperature),
            HaEntityId   = reader["HaEntityId"]?.ToString() ?? string.Empty,
            DisplayLabel = NullString(reader["DisplayLabel"]),
            IsActive     = reader["IsActive"] is not DBNull and not null && Convert.ToInt32(reader["IsActive"], CultureInfo.InvariantCulture) == 1,
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static HardwareItem MapHardwareItem(SqliteDataReader reader)
    {
        return new HardwareItem
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            Name = reader["Name"]?.ToString() ?? string.Empty,
            Category = reader["Category"]?.ToString() ?? string.Empty,
            Status = ParseEnum(reader["Status"]?.ToString(), HardwareItemStatus.Active),
            Criticality = ParseEnum(reader["Criticality"]?.ToString(), HardwareItemCriticality.Medium),
            TentId = reader["TentId"] is DBNull or null ? null : Convert.ToInt32(reader["TentId"], CultureInfo.InvariantCulture),
            SetupId = reader["SetupId"] is DBNull or null ? null : Convert.ToInt32(reader["SetupId"], CultureInfo.InvariantCulture),
            GrowId = reader["GrowId"] is DBNull or null ? null : Convert.ToInt32(reader["GrowId"], CultureInfo.InvariantCulture),
            WearTemplateId = NullString(reader["WearTemplateId"]),
            TentSensorId = reader["TentSensorId"] is DBNull or null ? null : Convert.ToInt32(reader["TentSensorId"], CultureInfo.InvariantCulture),
            HaEntityId = NullString(reader["HaEntityId"]),
            Manufacturer = NullString(reader["Manufacturer"]),
            Model = NullString(reader["Model"]),
            SerialNumber = NullString(reader["SerialNumber"]),
            InstalledAtUtc = ParseStoredDateTime(reader["InstalledAtUtc"]?.ToString()),
            RetiredAtUtc = ParseStoredDateTime(reader["RetiredAtUtc"]?.ToString()),
            ExpectedLifespanDays = reader["ExpectedLifespanDays"] is DBNull or null ? null : Convert.ToInt32(reader["ExpectedLifespanDays"], CultureInfo.InvariantCulture),
            InspectionIntervalDays = reader["InspectionIntervalDays"] is DBNull or null ? null : Convert.ToInt32(reader["InspectionIntervalDays"], CultureInfo.InvariantCulture),
            Notes = NullString(reader["Notes"]),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static MaintenanceEvent MapMaintenanceEvent(SqliteDataReader reader)
    {
        return new MaintenanceEvent
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            HardwareItemId = Convert.ToInt32(reader["HardwareItemId"], CultureInfo.InvariantCulture),
            EventType = ParseEnum(reader["EventType"]?.ToString(), MaintenanceEventType.Inspection),
            Status = ParseEnum(reader["Status"]?.ToString(), MaintenanceEventStatus.Planned),
            Result = ParseEnum(reader["Result"]?.ToString(), MaintenanceResult.Unknown),
            Title = reader["Title"]?.ToString() ?? string.Empty,
            Description = NullString(reader["Description"]),
            DueAtUtc = ParseStoredUtcDateTime(reader["DueAtUtc"]?.ToString()),
            PerformedAtUtc = ParseStoredUtcDateTime(reader["PerformedAtUtc"]?.ToString()),
            NextDueAtUtc = ParseStoredUtcDateTime(reader["NextDueAtUtc"]?.ToString()),
            GrowTaskId = reader["GrowTaskId"] is DBNull or null ? null : Convert.ToInt32(reader["GrowTaskId"], CultureInfo.InvariantCulture),
            SopInstanceId = reader["SopInstanceId"] is DBNull or null ? null : Convert.ToInt32(reader["SopInstanceId"], CultureInfo.InvariantCulture),
            Notes = NullString(reader["Notes"]),
            CreatedAtUtc = ParseStoredUtcDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredUtcDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static CalibrationEvent MapCalibrationEvent(SqliteDataReader reader)
    {
        return new CalibrationEvent
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            HardwareItemId = Convert.ToInt32(reader["HardwareItemId"], CultureInfo.InvariantCulture),
            CalibrationType = ParseEnum(reader["CalibrationType"]?.ToString(), CalibrationEventType.Ph),
            Status = ParseEnum(reader["Status"]?.ToString(), CalibrationEventStatus.Planned),
            Result = ParseEnum(reader["Result"]?.ToString(), CalibrationResult.Unknown),
            Title = reader["Title"]?.ToString() ?? string.Empty,
            ReferenceSolution = NullString(reader["ReferenceSolution"]),
            ReferenceValue = reader["ReferenceValue"] is DBNull or null ? null : Convert.ToDecimal(reader["ReferenceValue"], CultureInfo.InvariantCulture),
            BeforeValue = reader["BeforeValue"] is DBNull or null ? null : Convert.ToDecimal(reader["BeforeValue"], CultureInfo.InvariantCulture),
            AfterValue = reader["AfterValue"] is DBNull or null ? null : Convert.ToDecimal(reader["AfterValue"], CultureInfo.InvariantCulture),
            TemperatureC = reader["TemperatureC"] is DBNull or null ? null : Convert.ToDecimal(reader["TemperatureC"], CultureInfo.InvariantCulture),
            DueAtUtc = ParseStoredUtcDateTime(reader["DueAtUtc"]?.ToString()),
            PerformedAtUtc = ParseStoredUtcDateTime(reader["PerformedAtUtc"]?.ToString()),
            NextDueAtUtc = ParseStoredUtcDateTime(reader["NextDueAtUtc"]?.ToString()),
            GrowTaskId = reader["GrowTaskId"] is DBNull or null ? null : Convert.ToInt32(reader["GrowTaskId"], CultureInfo.InvariantCulture),
            Notes = NullString(reader["Notes"]),
            CreatedAtUtc = ParseStoredUtcDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredUtcDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
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

    private static void AddHardwareItemParameters(SqliteCommand command, HardwareItem item)
    {
        command.Parameters.AddWithValue("$name", item.Name.Trim());
        command.Parameters.AddWithValue("$category", item.Category.Trim());
        command.Parameters.AddWithValue("$status", item.Status.ToString());
        command.Parameters.AddWithValue("$criticality", item.Criticality.ToString());
        command.Parameters.AddWithValue("$tentId", (object?)item.TentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$setupId", (object?)item.SetupId ?? DBNull.Value);
        command.Parameters.AddWithValue("$growId", (object?)item.GrowId ?? DBNull.Value);
        command.Parameters.AddWithValue("$wearTemplateId", (object?)NormalizeOptional(item.WearTemplateId) ?? DBNull.Value);
        command.Parameters.AddWithValue("$tentSensorId", (object?)item.TentSensorId ?? DBNull.Value);
        command.Parameters.AddWithValue("$haEntityId", (object?)NormalizeOptional(item.HaEntityId) ?? DBNull.Value);
        command.Parameters.AddWithValue("$manufacturer", (object?)NormalizeOptional(item.Manufacturer) ?? DBNull.Value);
        command.Parameters.AddWithValue("$model", (object?)NormalizeOptional(item.Model) ?? DBNull.Value);
        command.Parameters.AddWithValue("$serialNumber", (object?)NormalizeOptional(item.SerialNumber) ?? DBNull.Value);
        command.Parameters.AddWithValue("$installedAtUtc", item.InstalledAtUtc.HasValue ? ToStorageUtc(item.InstalledAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$retiredAtUtc", item.RetiredAtUtc.HasValue ? ToStorageUtc(item.RetiredAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$expectedLifespanDays", (object?)item.ExpectedLifespanDays ?? DBNull.Value);
        command.Parameters.AddWithValue("$inspectionIntervalDays", (object?)item.InspectionIntervalDays ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)NormalizeOptional(item.Notes) ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(item.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(item.UpdatedAtUtc));
    }

    private static void AddMaintenanceEventParameters(SqliteCommand command, MaintenanceEvent item)
    {
        command.Parameters.AddWithValue("$hardwareItemId", item.HardwareItemId);
        command.Parameters.AddWithValue("$eventType", item.EventType.ToString());
        command.Parameters.AddWithValue("$status", item.Status.ToString());
        command.Parameters.AddWithValue("$result", item.Result.ToString());
        command.Parameters.AddWithValue("$title", item.Title.Trim());
        command.Parameters.AddWithValue("$description", (object?)NormalizeOptional(item.Description) ?? DBNull.Value);
        command.Parameters.AddWithValue("$dueAtUtc", item.DueAtUtc.HasValue ? ToStorageUtc(item.DueAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$performedAtUtc", item.PerformedAtUtc.HasValue ? ToStorageUtc(item.PerformedAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$nextDueAtUtc", item.NextDueAtUtc.HasValue ? ToStorageUtc(item.NextDueAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$growTaskId", (object?)item.GrowTaskId ?? DBNull.Value);
        command.Parameters.AddWithValue("$sopInstanceId", (object?)item.SopInstanceId ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)NormalizeOptional(item.Notes) ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(item.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(item.UpdatedAtUtc));
    }

    private static void AddCalibrationEventParameters(SqliteCommand command, CalibrationEvent item)
    {
        command.Parameters.AddWithValue("$hardwareItemId", item.HardwareItemId);
        command.Parameters.AddWithValue("$calibrationType", item.CalibrationType.ToString());
        command.Parameters.AddWithValue("$status", item.Status.ToString());
        command.Parameters.AddWithValue("$result", item.Result.ToString());
        command.Parameters.AddWithValue("$title", item.Title.Trim());
        command.Parameters.AddWithValue("$referenceSolution", (object?)NormalizeOptional(item.ReferenceSolution) ?? DBNull.Value);
        command.Parameters.AddWithValue("$referenceValue", (object?)item.ReferenceValue ?? DBNull.Value);
        command.Parameters.AddWithValue("$beforeValue", (object?)item.BeforeValue ?? DBNull.Value);
        command.Parameters.AddWithValue("$afterValue", (object?)item.AfterValue ?? DBNull.Value);
        command.Parameters.AddWithValue("$temperatureC", (object?)item.TemperatureC ?? DBNull.Value);
        command.Parameters.AddWithValue("$dueAtUtc", item.DueAtUtc.HasValue ? ToStorageUtc(item.DueAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$performedAtUtc", item.PerformedAtUtc.HasValue ? ToStorageUtc(item.PerformedAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$nextDueAtUtc", item.NextDueAtUtc.HasValue ? ToStorageUtc(item.NextDueAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$growTaskId", (object?)item.GrowTaskId ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)NormalizeOptional(item.Notes) ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(item.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(item.UpdatedAtUtc));
    }

    private static void AddGrowParameters(SqliteCommand command, GrowRun grow)
    {
        command.Parameters.AddWithValue("$tentId", (object?)grow.TentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$systemId", (object?)grow.SystemId ?? DBNull.Value);
        command.Parameters.AddWithValue("$setupId", (object?)grow.SetupId ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", grow.Name);
        command.Parameters.AddWithValue("$strain", (object?)grow.Strain ?? DBNull.Value);
        command.Parameters.AddWithValue("$breeder", (object?)grow.Breeder ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", grow.Status.ToString());
        command.Parameters.AddWithValue("$mediumType", grow.MediumType.ToString());
        command.Parameters.AddWithValue("$feedingStyle", grow.FeedingStyle.ToString());
        command.Parameters.AddWithValue("$hydroStyle", grow.HydroStyle.ToString());
        command.Parameters.AddWithValue("$mediumDetail", (object?)grow.MediumDetail ?? DBNull.Value);
        command.Parameters.AddWithValue("$environment", grow.Environment.ToString());
        command.Parameters.AddWithValue("$light", (object?)grow.Light ?? DBNull.Value);
        command.Parameters.AddWithValue("$containerSize", (object?)grow.ContainerSize ?? DBNull.Value);
        command.Parameters.AddWithValue("$reservoirSize", (object?)grow.ReservoirSize ?? DBNull.Value);
        command.Parameters.AddWithValue("$irrigationStyle", (object?)grow.IrrigationStyle ?? DBNull.Value);
        command.Parameters.AddWithValue("$irrigationType", grow.IrrigationType.ToString());
        command.Parameters.AddWithValue("$waterSource", grow.WaterSource.ToString());
        command.Parameters.AddWithValue("$seedType", grow.SeedType.ToString());
        command.Parameters.AddWithValue("$startMaterial", grow.StartMaterial.ToString());
        command.Parameters.AddWithValue("$germinationMethod", (object?)grow.GerminationMethod?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$cloneSource", (object?)grow.CloneSource ?? DBNull.Value);
        command.Parameters.AddWithValue("$cloneIsRooted", grow.CloneIsRooted ? 1 : 0);
        command.Parameters.AddWithValue("$breederFlowerWeeksMin", (object?)grow.BreederFlowerWeeksMin ?? DBNull.Value);
        command.Parameters.AddWithValue("$breederFlowerWeeksMax", (object?)grow.BreederFlowerWeeksMax ?? DBNull.Value);
        command.Parameters.AddWithValue("$plantCount", (object?)grow.PlantCount ?? DBNull.Value);
        command.Parameters.AddWithValue("$phenoNumber", (object?)grow.PhenoNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$propagationMedium", (object?)grow.PropagationMedium?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$hasChiller", grow.HasChiller ? 1 : 0);
        command.Parameters.AddWithValue("$entryPoint", grow.EntryPoint.ToString());
        command.Parameters.AddWithValue("$daysAlreadyInPhase", (object?)grow.DaysAlreadyInPhase ?? DBNull.Value);
        command.Parameters.AddWithValue("$autoflowerDaysSinceGermination", (object?)grow.AutoflowerDaysSinceGermination ?? DBNull.Value);
        command.Parameters.AddWithValue("$flipDate", grow.FlipDate.HasValue ? ToStorage(grow.FlipDate.Value.Date) : DBNull.Value);
        command.Parameters.AddWithValue("$germinatedAt", grow.GerminatedAt.HasValue ? ToStorageUtc(grow.GerminatedAt.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$rootedAt", grow.RootedAt.HasValue ? ToStorageUtc(grow.RootedAt.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$nutrients", (object?)grow.Nutrients ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)grow.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$startDate", ToStorage(grow.StartDate.Date));
        command.Parameters.AddWithValue("$endDate", grow.EndDate.HasValue ? ToStorage(grow.EndDate.Value.Date) : DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(grow.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(grow.UpdatedAtUtc));
    }

    private static void AddSetupParameters(SqliteCommand command, Setup setup)
    {
        command.Parameters.AddWithValue("$tentId", setup.TentId);
        command.Parameters.AddWithValue("$name", setup.Name);
        command.Parameters.AddWithValue("$setupType", setup.SetupType.ToString());
        command.Parameters.AddWithValue("$status", setup.Status.ToString());
        command.Parameters.AddWithValue("$notes", (object?)setup.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$cloneCounterTotal", (object?)setup.CloneCounterTotal ?? DBNull.Value);
        command.Parameters.AddWithValue("$lastCloneCutAt", setup.LastCloneCutAt.HasValue ? ToStorage(setup.LastCloneCutAt.Value) : (object)DBNull.Value);
        command.Parameters.AddWithValue("$motherHealthStatus", (object?)setup.MotherHealthStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("$quarantineStartedAt", setup.QuarantineStartedAt.HasValue ? ToStorage(setup.QuarantineStartedAt.Value) : (object)DBNull.Value);
        command.Parameters.AddWithValue("$quarantinePlannedEndAt", setup.QuarantinePlannedEndAt.HasValue ? ToStorage(setup.QuarantinePlannedEndAt.Value) : (object)DBNull.Value);
        command.Parameters.AddWithValue("$quarantineResult", (object?)setup.QuarantineResult ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(setup.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(setup.UpdatedAtUtc));
    }

    private static void AddStrainParameters(SqliteCommand command, Strain strain)
    {
        command.Parameters.AddWithValue("$name", strain.Name);
        command.Parameters.AddWithValue("$breeder", (object?)strain.Breeder ?? DBNull.Value);
        command.Parameters.AddWithValue("$dominance", strain.Dominance.ToString());
        command.Parameters.AddWithValue("$flowerWeeksMin", (object?)strain.FlowerWeeksMin ?? DBNull.Value);
        command.Parameters.AddWithValue("$flowerWeeksMax", (object?)strain.FlowerWeeksMax ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)strain.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$nutrientDemandFactor", (object?)strain.NutrientDemandFactor ?? DBNull.Value);
        command.Parameters.AddWithValue("$stretchFactor", (object?)strain.StretchFactor ?? DBNull.Value);
        command.Parameters.AddWithValue("$vpdPreferenceShift", (object?)strain.VpdPreferenceShift ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(strain.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(strain.UpdatedAtUtc));
    }

    private static void AddPlantParameters(SqliteCommand command, PlantInstance plant)
    {
        command.Parameters.AddWithValue("$strainId", (object?)plant.StrainId ?? DBNull.Value);
        command.Parameters.AddWithValue("$setupId", (object?)plant.SetupId ?? DBNull.Value);
        command.Parameters.AddWithValue("$growId", (object?)plant.GrowId ?? DBNull.Value);
        command.Parameters.AddWithValue("$parentPlantId", (object?)plant.ParentPlantId ?? DBNull.Value);
        command.Parameters.AddWithValue("$label", plant.Label);
        command.Parameters.AddWithValue("$plantRole", plant.PlantRole.ToString());
        command.Parameters.AddWithValue("$plantStatus", plant.PlantStatus.ToString());
        command.Parameters.AddWithValue("$phenoLabel", (object?)plant.PhenoLabel ?? DBNull.Value);
        command.Parameters.AddWithValue("$startedAt", plant.StartedAt.HasValue ? ToStorage(plant.StartedAt.Value) : (object)DBNull.Value);
        command.Parameters.AddWithValue("$endedAt", plant.EndedAt.HasValue ? ToStorage(plant.EndedAt.Value) : (object)DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)plant.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(plant.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(plant.UpdatedAtUtc));
    }

    private static void AddAutoMeasurementConfigParameters(SqliteCommand command, AutoMeasurementConfig config)
    {
        command.Parameters.AddWithValue("$growId", config.GrowId);
        command.Parameters.AddWithValue("$tentId", (object?)config.TentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", config.Name);
        command.Parameters.AddWithValue("$status", config.Status.ToString());
        command.Parameters.AddWithValue("$triggerKind", config.TriggerKind.ToString());
        command.Parameters.AddWithValue("$delayMinutes", (object?)config.DelayMinutes ?? DBNull.Value);
        command.Parameters.AddWithValue("$windowMinutes", config.WindowMinutes);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(config.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(config.UpdatedAtUtc));
    }

    private static void AddAutoMeasurementFieldMappingParameters(SqliteCommand command, AutoMeasurementFieldMapping mapping)
    {
        command.Parameters.AddWithValue("$configId", mapping.ConfigId);
        command.Parameters.AddWithValue("$measurementField", mapping.MeasurementField.ToString());
        command.Parameters.AddWithValue("$metricKey", mapping.MetricKey);
        command.Parameters.AddWithValue("$aggregation", mapping.Aggregation.ToString());
        command.Parameters.AddWithValue("$isRequired", mapping.IsRequired ? 1 : 0);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(mapping.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(mapping.UpdatedAtUtc));
    }

    private static void AddAutoMeasurementRunParameters(SqliteCommand command, AutoMeasurementRun run)
    {
        command.Parameters.AddWithValue("$configId", run.ConfigId);
        command.Parameters.AddWithValue("$growId", run.GrowId);
        command.Parameters.AddWithValue("$triggerKind", run.TriggerKind.ToString());
        command.Parameters.AddWithValue("$scheduledForUtc", ToStorageUtc(run.ScheduledForUtc));
        command.Parameters.AddWithValue("$measurementId", (object?)run.MeasurementId ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", run.Status.ToString());
        command.Parameters.AddWithValue("$errorMessage", (object?)run.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(run.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(run.UpdatedAtUtc));
    }

    private static void AddLightScheduleParameters(SqliteCommand command, LightSchedule schedule)
    {
        command.Parameters.AddWithValue("$tentId", schedule.TentId);
        command.Parameters.AddWithValue("$name", schedule.Name);
        command.Parameters.AddWithValue("$isActive", schedule.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$lightsOnTime", schedule.LightsOnTime);
        command.Parameters.AddWithValue("$lightsOffTime", schedule.LightsOffTime);
        command.Parameters.AddWithValue("$timeZoneId", (object?)schedule.TimeZoneId ?? DBNull.Value);
        command.Parameters.AddWithValue("$source", schedule.Source.ToString());
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(schedule.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(schedule.UpdatedAtUtc));
    }

    private static void AddLightTransitionParameters(SqliteCommand command, LightTransitionEvent transition)
    {
        command.Parameters.AddWithValue("$tentId", transition.TentId);
        command.Parameters.AddWithValue("$kind", transition.Kind.ToString());
        command.Parameters.AddWithValue("$occurredAtUtc", ToStorageUtc(transition.OccurredAtUtc));
        command.Parameters.AddWithValue("$source", transition.Source.ToString());
        command.Parameters.AddWithValue("$rawState", (object?)transition.RawState ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(transition.CreatedAtUtc));
    }

    private static void AddSopInstanceParameters(SqliteCommand command, SopInstance instance)
    {
        command.Parameters.AddWithValue("$growId", instance.GrowId);
        command.Parameters.AddWithValue("$sopId", instance.SopId);
        command.Parameters.AddWithValue("$sopName", instance.SopName);
        command.Parameters.AddWithValue("$sopType", instance.SopType);
        command.Parameters.AddWithValue("$status", instance.Status.ToString());
        command.Parameters.AddWithValue("$source", instance.Source.ToString());
        command.Parameters.AddWithValue("$sourceRecommendationKey", (object?)instance.SourceRecommendationKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$treatmentRecommendationStableKey", (object?)instance.TreatmentRecommendationStableKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$startedAtUtc", ToStorageUtc(instance.StartedAtUtc));
        command.Parameters.AddWithValue("$completedAtUtc", instance.CompletedAtUtc.HasValue ? ToStorageUtc(instance.CompletedAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$cancelledAtUtc", instance.CancelledAtUtc.HasValue ? ToStorageUtc(instance.CancelledAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$dueAtUtc", instance.DueAtUtc.HasValue ? ToStorageUtc(instance.DueAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$nextStepDueAtUtc", instance.NextStepDueAtUtc.HasValue ? ToStorageUtc(instance.NextStepDueAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$recurrenceIntervalDays", (object?)instance.RecurrenceIntervalDays ?? DBNull.Value);
        command.Parameters.AddWithValue("$isRecurring", instance.IsRecurring ? 1 : 0);
        command.Parameters.AddWithValue("$notes", (object?)instance.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(instance.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(instance.UpdatedAtUtc));
    }

    private static void AddSopStepInstanceParameters(SqliteCommand command, SopStepInstance step)
    {
        command.Parameters.AddWithValue("$sopInstanceId", step.SopInstanceId);
        command.Parameters.AddWithValue("$stepId", step.StepId);
        command.Parameters.AddWithValue("$order", step.Order);
        command.Parameters.AddWithValue("$title", step.Title);
        command.Parameters.AddWithValue("$description", (object?)step.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$stepType", step.StepType);
        command.Parameters.AddWithValue("$status", step.Status.ToString());
        command.Parameters.AddWithValue("$waitMinutes", (object?)step.WaitMinutes ?? DBNull.Value);
        command.Parameters.AddWithValue("$subSopId", (object?)step.SubSopId ?? DBNull.Value);
        command.Parameters.AddWithValue("$expectedInputsJson", (object?)step.ExpectedInputsJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$photoRequired", step.PhotoRequired ? 1 : 0);
        command.Parameters.AddWithValue("$photoRecommended", step.PhotoRecommended ? 1 : 0);
        command.Parameters.AddWithValue("$dueAtUtc", step.DueAtUtc.HasValue ? ToStorageUtc(step.DueAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$availableAtUtc", step.AvailableAtUtc.HasValue ? ToStorageUtc(step.AvailableAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$reminderTaskId", (object?)step.ReminderTaskId ?? DBNull.Value);
        command.Parameters.AddWithValue("$startedAtUtc", step.StartedAtUtc.HasValue ? ToStorageUtc(step.StartedAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$completedAtUtc", step.CompletedAtUtc.HasValue ? ToStorageUtc(step.CompletedAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$skippedAtUtc", step.SkippedAtUtc.HasValue ? ToStorageUtc(step.SkippedAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)step.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$measurementId", (object?)step.MeasurementId ?? DBNull.Value);
        command.Parameters.AddWithValue("$journalEntryId", (object?)step.JournalEntryId ?? DBNull.Value);
        command.Parameters.AddWithValue("$photoAssetId", (object?)step.PhotoAssetId ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(step.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(step.UpdatedAtUtc));
    }

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
    }

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
            CreatedAtUtc    = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            ActiveGrowCount = reader["ActiveGrowCount"] is DBNull ? 0 : Convert.ToInt32(reader["ActiveGrowCount"], CultureInfo.InvariantCulture)
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

    private static void AddMeasurementParameters(SqliteCommand command, Measurement measurement)
    {
        command.Parameters.AddWithValue("$growId", measurement.GrowId);
        command.Parameters.AddWithValue("$takenAt", ToStorage(measurement.TakenAt));
        command.Parameters.AddWithValue("$stage", measurement.Stage.ToString());
        command.Parameters.AddWithValue("$source", measurement.Source.ToString());
        command.Parameters.AddWithValue("$notes", (object?)measurement.Notes ?? DBNull.Value);
        AddNullable(command, "$airTemperatureC", measurement.AirTemperatureC);
        AddNullable(command, "$humidityPercent", measurement.HumidityPercent);
        AddNullable(command, "$heightCm", measurement.HeightCm);
        AddNullable(command, "$waterAmountMl", measurement.WaterAmountMl);
        AddNullable(command, "$runoffAmountMl", measurement.RunoffAmountMl);
        AddNullable(command, "$irrigationPh", measurement.IrrigationPh);
        AddNullable(command, "$irrigationEc", measurement.IrrigationEc);
        AddNullable(command, "$drainPh", measurement.DrainPh);
        AddNullable(command, "$drainEc", measurement.DrainEc);
        AddNullable(command, "$reservoirPh", measurement.ReservoirPh);
        AddNullable(command, "$reservoirEc", measurement.ReservoirEc);
        AddNullable(command, "$reservoirWaterTempC", measurement.ReservoirWaterTempC);
        AddNullable(command, "$reservoirLevelCm", measurement.ReservoirLevelCm);
        AddNullable(command, "$reservoirLevelLiters", measurement.ReservoirLevelLiters);
        AddNullable(command, "$dissolvedOxygenMgL", measurement.DissolvedOxygenMgL);
        AddNullable(command, "$orpMv", measurement.OrpMv);
        AddNullable(command, "$topOffLiters", measurement.TopOffLiters);
        AddNullable(command, "$addbackEc", measurement.AddbackEc);
        command.Parameters.AddWithValue("$solutionChange", measurement.SolutionChange ? 1 : 0);
        AddNullable(command, "$ppfdMol", measurement.PpfdMol);
        AddNullable(command, "$co2Ppm", measurement.Co2Ppm);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(measurement.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(measurement.UpdatedAtUtc));
    }

    private bool TryResolveUploadPath(string relativePath, out string physicalPath)
    {
        physicalPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var normalized = relativePath.Replace('\\', '/').Trim();
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized;
        }
        if (!normalized.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var uploadsRoot = Path.GetFullPath(Path.Combine(_paths.ContentRootPath, "wwwroot", "uploads"));
        var candidatePath = Path.GetFullPath(Path.Combine(_paths.ContentRootPath, "wwwroot", normalized.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (!candidatePath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        physicalPath = candidatePath;
        return true;
    }

    private static void AddNullable(SqliteCommand command, string name, double? value)
        => command.Parameters.AddWithValue(name, value.HasValue ? value.Value : DBNull.Value);

    private static string? NullString(object value)
        => value is DBNull ? null : value?.ToString();

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static double? NullableDouble(object value)
        => value is DBNull ? null : Convert.ToDouble(value, CultureInfo.InvariantCulture);

    private static TEnum ParseEnum<TEnum>(string? raw, TEnum fallback) where TEnum : struct
        => Enum.TryParse<TEnum>(raw, out var parsed) ? parsed : fallback;

    private static bool HasColumn(SqliteDataReader reader, string name)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static DateTime? ParseStoredDateTime(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out var result) ? result : null;

    private static DateTime? ParseStoredUtcDateTime(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces, out var result) ? result : null;

    private static DateTime? ParseStoredDateTimeIfColumn(SqliteDataReader reader, string columnName)
    {
        if (!HasColumn(reader, columnName) || reader[columnName] is DBNull)
            return null;
        var text = reader[columnName]?.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return null;
        return DateTime.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result)
            ? result
            : null;
    }

    private static DateTime? ParseStoredDate(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var result) ? result.Date : null;

    private static string ToStorage(DateTime value)
        => value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    private static string ToStorageUtc(DateTime value)
        => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
