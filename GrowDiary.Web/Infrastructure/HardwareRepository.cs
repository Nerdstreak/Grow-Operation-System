using System.Globalization;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class HardwareRepository : RepositoryBase
{
    public HardwareRepository(AppPaths paths) : base(paths)
    {
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
                TentId, SetupId, HydroSetupId, GrowId, WearTemplateId, TentSensorId, HaEntityId,
                Manufacturer, Model, SerialNumber,
                InstalledAtUtc, RetiredAtUtc,
                ExpectedLifespanDays, InspectionIntervalDays, Notes,
                CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $name, $category, $status, $criticality,
                $tentId, $setupId, $hydroSetupId, $growId, $wearTemplateId, $tentSensorId, $haEntityId,
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
                HydroSetupId = $hydroSetupId,
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

    public List<HardwareItem> GetHardwareItemsByHydroSetup(int hydroSetupId)
        => GetHardwareItemsByWhere("WHERE HydroSetupId = $value", hydroSetupId);

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

    public RiskEvent CreateRiskEvent(RiskEvent item)
    {
        ValidateRiskEvent(item);
        item.CreatedAtUtc = DateTime.UtcNow;
        item.UpdatedAtUtc = DateTime.UtcNow;
        ApplyRiskEventDefaults(item);

        var dedupeKey = NormalizeOptional(item.DedupeKey);
        if (dedupeKey is not null)
        {
            var existing = FindOpenRiskEventByDedupeKey(dedupeKey);
            if (existing is not null)
            {
                var candidateLastSeen = item.LastSeenAtUtc ?? DateTime.UtcNow;
                existing.LastSeenAtUtc = candidateLastSeen.ToUniversalTime() < existing.StartedAtUtc.ToUniversalTime()
                    ? existing.StartedAtUtc
                    : candidateLastSeen.ToUniversalTime();
                existing.UpdatedAtUtc = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(item.RawValue))
                {
                    existing.RawValue = item.RawValue;
                }
                UpdateRiskEvent(existing);
                return GetRiskEvent(existing.Id)!;
            }
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO RiskEvents (
                EventType, Severity, Status, Source, Title, Description,
                HardwareItemId, TentId, GrowId, TentSensorId, HaEntityId,
                SopInstanceId, GrowTaskId,
                StartedAtUtc, LastSeenAtUtc, ResolvedAtUtc, AcknowledgedAtUtc,
                DedupeKey, RawValue, Notes,
                CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $eventType, $severity, $status, $source, $title, $description,
                $hardwareItemId, $tentId, $growId, $tentSensorId, $haEntityId,
                $sopInstanceId, $growTaskId,
                $startedAtUtc, $lastSeenAtUtc, $resolvedAtUtc, $acknowledgedAtUtc,
                $dedupeKey, $rawValue, $notes,
                $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddRiskEventParameters(command, item);
        item.Id = Convert.ToInt32((long)command.ExecuteScalar()!);
        return item;
    }

    public void UpdateRiskEvent(RiskEvent item)
    {
        ValidateRiskEvent(item);
        item.UpdatedAtUtc = DateTime.UtcNow;
        ApplyRiskEventDefaults(item);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE RiskEvents SET
                EventType = $eventType,
                Severity = $severity,
                Status = $status,
                Source = $source,
                Title = $title,
                Description = $description,
                HardwareItemId = $hardwareItemId,
                TentId = $tentId,
                GrowId = $growId,
                TentSensorId = $tentSensorId,
                HaEntityId = $haEntityId,
                SopInstanceId = $sopInstanceId,
                GrowTaskId = $growTaskId,
                StartedAtUtc = $startedAtUtc,
                LastSeenAtUtc = $lastSeenAtUtc,
                ResolvedAtUtc = $resolvedAtUtc,
                AcknowledgedAtUtc = $acknowledgedAtUtc,
                DedupeKey = $dedupeKey,
                RawValue = $rawValue,
                Notes = $notes,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddRiskEventParameters(command, item);
        command.Parameters.AddWithValue("$id", item.Id);
        command.ExecuteNonQuery();
    }

    public RiskEvent? GetRiskEvent(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM RiskEvents WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapRiskEvent(reader) : null;
    }

    public List<RiskEvent> GetRiskEvents()
        => GetRiskEventsByWhere(string.Empty, null);

    public List<RiskEvent> GetOpenRiskEvents()
        => GetRiskEventsByWhere("WHERE Status IN ('Open', 'Acknowledged')", null);

    public List<RiskEvent> GetRiskEventsByHardwareItem(int hardwareItemId)
        => GetRiskEventsByWhere("WHERE HardwareItemId = $value", hardwareItemId);

    public List<RiskEvent> GetRiskEventsByTent(int tentId)
        => GetRiskEventsByWhere("WHERE TentId = $value", tentId);

    public List<RiskEvent> GetRiskEventsByGrow(int growId)
        => GetRiskEventsByWhere("WHERE GrowId = $value", growId);

    public List<RiskEvent> GetRiskEventsByStatus(RiskEventStatus status)
        => GetRiskEventsByWhere("WHERE Status = $value", status.ToString());

    public RiskEvent? FindOpenRiskEventByDedupeKey(string dedupeKey)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM RiskEvents
            WHERE DedupeKey = $dedupeKey AND Status IN ('Open', 'Acknowledged')
            ORDER BY StartedAtUtc DESC, Id DESC
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$dedupeKey", dedupeKey.Trim());
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapRiskEvent(reader) : null;
    }

    public RiskEvent ResolveRiskEvent(int id, DateTime resolvedAtUtc, string? notes)
    {
        var item = GetRiskEvent(id) ?? throw new InvalidOperationException($"RiskEvent with id {id} does not exist.");
        item.Status = RiskEventStatus.Resolved;
        item.ResolvedAtUtc = resolvedAtUtc.ToUniversalTime();
        item.Notes = AppendNotes(item.Notes, notes);
        UpdateRiskEvent(item);
        return GetRiskEvent(id)!;
    }

    public RiskEvent AcknowledgeRiskEvent(int id, DateTime acknowledgedAtUtc, string? notes)
    {
        var item = GetRiskEvent(id) ?? throw new InvalidOperationException($"RiskEvent with id {id} does not exist.");
        item.Status = RiskEventStatus.Acknowledged;
        item.AcknowledgedAtUtc = acknowledgedAtUtc.ToUniversalTime();
        item.Notes = AppendNotes(item.Notes, notes);
        UpdateRiskEvent(item);
        return GetRiskEvent(id)!;
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

    private List<RiskEvent> GetRiskEventsByWhere(string whereClause, object? value)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT *
            FROM RiskEvents
            {whereClause}
            ORDER BY StartedAtUtc DESC, Id DESC;
        """;
        if (value is not null)
        {
            command.Parameters.AddWithValue("$value", value);
        }

        var list = new List<RiskEvent>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapRiskEvent(reader));
        }
        return list;
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

        if (item.TentId.HasValue && !RowExists("Tents", item.TentId.Value))
        {
            throw new InvalidOperationException($"Tent with id {item.TentId.Value} does not exist.");
        }

        if (item.SetupId.HasValue && !RowExists("Setups", item.SetupId.Value))
        {
            throw new InvalidOperationException($"Setup with id {item.SetupId.Value} does not exist.");
        }

        var hydroSetupTentId = item.HydroSetupId.HasValue
            ? GetHydroSetupTentId(item.HydroSetupId.Value)
            : (exists: false, tentId: (int?)null);
        if (item.HydroSetupId.HasValue && !hydroSetupTentId.exists)
        {
            throw new InvalidOperationException($"HydroSetup with id {item.HydroSetupId.Value} does not exist.");
        }

        if (hydroSetupTentId.exists && item.TentId.HasValue && hydroSetupTentId.tentId.HasValue && hydroSetupTentId.tentId.Value != item.TentId.Value)
        {
            throw new InvalidOperationException("HydroSetup must belong to the selected tent.");
        }

        if (item.GrowId.HasValue && !RowExists("Grows", item.GrowId.Value))
        {
            throw new InvalidOperationException($"Grow with id {item.GrowId.Value} does not exist.");
        }

        if (item.TentSensorId.HasValue && !RowExists("TentSensors", item.TentSensorId.Value))
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

    private void ValidateRiskEvent(RiskEvent item)
    {
        if (string.IsNullOrWhiteSpace(item.Title))
        {
            throw new InvalidOperationException("RiskEvent title must not be empty.");
        }

        if (!Enum.IsDefined(item.EventType))
        {
            throw new InvalidOperationException("RiskEvent type is invalid.");
        }

        if (!Enum.IsDefined(item.Severity))
        {
            throw new InvalidOperationException("RiskEvent severity is invalid.");
        }

        if (!Enum.IsDefined(item.Status))
        {
            throw new InvalidOperationException("RiskEvent status is invalid.");
        }

        if (!Enum.IsDefined(item.Source))
        {
            throw new InvalidOperationException("RiskEvent source is invalid.");
        }

        if (item.HardwareItemId.HasValue && GetHardwareItem(item.HardwareItemId.Value) is null)
        {
            throw new InvalidOperationException($"HardwareItem with id {item.HardwareItemId.Value} does not exist.");
        }

        if (item.TentId.HasValue && !RowExists("Tents", item.TentId.Value))
        {
            throw new InvalidOperationException($"Tent with id {item.TentId.Value} does not exist.");
        }

        if (item.GrowId.HasValue && !RowExists("Grows", item.GrowId.Value))
        {
            throw new InvalidOperationException($"Grow with id {item.GrowId.Value} does not exist.");
        }

        if (item.TentSensorId.HasValue && !RowExists("TentSensors", item.TentSensorId.Value))
        {
            throw new InvalidOperationException($"TentSensor with id {item.TentSensorId.Value} does not exist.");
        }

        var startedAtUtc = NormalizeStartedAt(item.StartedAtUtc);
        if (item.ResolvedAtUtc.HasValue && item.ResolvedAtUtc.Value.ToUniversalTime() < startedAtUtc)
        {
            throw new InvalidOperationException("ResolvedAtUtc must not be before StartedAtUtc.");
        }

        if (item.AcknowledgedAtUtc.HasValue && item.AcknowledgedAtUtc.Value.ToUniversalTime() < startedAtUtc)
        {
            throw new InvalidOperationException("AcknowledgedAtUtc must not be before StartedAtUtc.");
        }

        if (item.LastSeenAtUtc.HasValue && item.LastSeenAtUtc.Value.ToUniversalTime() < startedAtUtc)
        {
            throw new InvalidOperationException("LastSeenAtUtc must not be before StartedAtUtc.");
        }
    }

    private static void ApplyRiskEventDefaults(RiskEvent item)
    {
        if (item.StartedAtUtc == default)
        {
            item.StartedAtUtc = DateTime.UtcNow;
        }
        else
        {
            item.StartedAtUtc = item.StartedAtUtc.ToUniversalTime();
        }

        if (item.Status == RiskEventStatus.Resolved && !item.ResolvedAtUtc.HasValue)
        {
            item.ResolvedAtUtc = DateTime.UtcNow;
        }

        if (item.Status == RiskEventStatus.Acknowledged && !item.AcknowledgedAtUtc.HasValue)
        {
            item.AcknowledgedAtUtc = DateTime.UtcNow;
        }
    }

    private bool RowExists(string tableName, int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName} WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return Convert.ToInt64(command.ExecuteScalar() ?? 0L, CultureInfo.InvariantCulture) > 0;
    }

    private (bool exists, int? tentId) GetHydroSetupTentId(int hydroSetupId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT TentId FROM GrowSystems WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", hydroSetupId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return (false, null);
        }

        return (true, reader["TentId"] is DBNull or null ? null : Convert.ToInt32(reader["TentId"], CultureInfo.InvariantCulture));
    }

    private static DateTime NormalizeStartedAt(DateTime startedAtUtc)
        => startedAtUtc == default ? DateTime.UtcNow : startedAtUtc.ToUniversalTime();

    private static string? AppendNotes(string? existing, string? addition)
    {
        var normalizedAddition = NormalizeOptional(addition);
        if (normalizedAddition is null)
        {
            return NormalizeOptional(existing);
        }

        var normalizedExisting = NormalizeOptional(existing);
        return normalizedExisting is null ? normalizedAddition : $"{normalizedExisting}\n{normalizedAddition}";
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
            HydroSetupId = reader["HydroSetupId"] is DBNull or null ? null : Convert.ToInt32(reader["HydroSetupId"], CultureInfo.InvariantCulture),
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

    private static RiskEvent MapRiskEvent(SqliteDataReader reader)
    {
        return new RiskEvent
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            EventType = ParseEnum(reader["EventType"]?.ToString(), RiskEventType.Other),
            Severity = ParseEnum(reader["Severity"]?.ToString(), RiskEventSeverity.Warning),
            Status = ParseEnum(reader["Status"]?.ToString(), RiskEventStatus.Open),
            Source = ParseEnum(reader["Source"]?.ToString(), RiskEventSource.Manual),
            Title = reader["Title"]?.ToString() ?? string.Empty,
            Description = NullString(reader["Description"]),
            HardwareItemId = reader["HardwareItemId"] is DBNull or null ? null : Convert.ToInt32(reader["HardwareItemId"], CultureInfo.InvariantCulture),
            TentId = reader["TentId"] is DBNull or null ? null : Convert.ToInt32(reader["TentId"], CultureInfo.InvariantCulture),
            GrowId = reader["GrowId"] is DBNull or null ? null : Convert.ToInt32(reader["GrowId"], CultureInfo.InvariantCulture),
            TentSensorId = reader["TentSensorId"] is DBNull or null ? null : Convert.ToInt32(reader["TentSensorId"], CultureInfo.InvariantCulture),
            HaEntityId = NullString(reader["HaEntityId"]),
            SopInstanceId = reader["SopInstanceId"] is DBNull or null ? null : Convert.ToInt32(reader["SopInstanceId"], CultureInfo.InvariantCulture),
            GrowTaskId = reader["GrowTaskId"] is DBNull or null ? null : Convert.ToInt32(reader["GrowTaskId"], CultureInfo.InvariantCulture),
            StartedAtUtc = ParseStoredUtcDateTime(reader["StartedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            LastSeenAtUtc = ParseStoredUtcDateTime(reader["LastSeenAtUtc"]?.ToString()),
            ResolvedAtUtc = ParseStoredUtcDateTime(reader["ResolvedAtUtc"]?.ToString()),
            AcknowledgedAtUtc = ParseStoredUtcDateTime(reader["AcknowledgedAtUtc"]?.ToString()),
            DedupeKey = NullString(reader["DedupeKey"]),
            RawValue = NullString(reader["RawValue"]),
            Notes = NullString(reader["Notes"]),
            CreatedAtUtc = ParseStoredUtcDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredUtcDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static void AddHardwareItemParameters(SqliteCommand command, HardwareItem item)
    {
        command.Parameters.AddWithValue("$name", item.Name.Trim());
        command.Parameters.AddWithValue("$category", item.Category.Trim());
        command.Parameters.AddWithValue("$status", item.Status.ToString());
        command.Parameters.AddWithValue("$criticality", item.Criticality.ToString());
        command.Parameters.AddWithValue("$tentId", (object?)item.TentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$setupId", (object?)item.SetupId ?? DBNull.Value);
        command.Parameters.AddWithValue("$hydroSetupId", (object?)item.HydroSetupId ?? DBNull.Value);
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

    private static void AddRiskEventParameters(SqliteCommand command, RiskEvent item)
    {
        command.Parameters.AddWithValue("$eventType", item.EventType.ToString());
        command.Parameters.AddWithValue("$severity", item.Severity.ToString());
        command.Parameters.AddWithValue("$status", item.Status.ToString());
        command.Parameters.AddWithValue("$source", item.Source.ToString());
        command.Parameters.AddWithValue("$title", item.Title.Trim());
        command.Parameters.AddWithValue("$description", (object?)NormalizeOptional(item.Description) ?? DBNull.Value);
        command.Parameters.AddWithValue("$hardwareItemId", (object?)item.HardwareItemId ?? DBNull.Value);
        command.Parameters.AddWithValue("$tentId", (object?)item.TentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$growId", (object?)item.GrowId ?? DBNull.Value);
        command.Parameters.AddWithValue("$tentSensorId", (object?)item.TentSensorId ?? DBNull.Value);
        command.Parameters.AddWithValue("$haEntityId", (object?)NormalizeOptional(item.HaEntityId) ?? DBNull.Value);
        command.Parameters.AddWithValue("$sopInstanceId", (object?)item.SopInstanceId ?? DBNull.Value);
        command.Parameters.AddWithValue("$growTaskId", (object?)item.GrowTaskId ?? DBNull.Value);
        command.Parameters.AddWithValue("$startedAtUtc", ToStorageUtc(item.StartedAtUtc));
        command.Parameters.AddWithValue("$lastSeenAtUtc", item.LastSeenAtUtc.HasValue ? ToStorageUtc(item.LastSeenAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$resolvedAtUtc", item.ResolvedAtUtc.HasValue ? ToStorageUtc(item.ResolvedAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$acknowledgedAtUtc", item.AcknowledgedAtUtc.HasValue ? ToStorageUtc(item.AcknowledgedAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$dedupeKey", (object?)NormalizeOptional(item.DedupeKey) ?? DBNull.Value);
        command.Parameters.AddWithValue("$rawValue", (object?)NormalizeOptional(item.RawValue) ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)NormalizeOptional(item.Notes) ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(item.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(item.UpdatedAtUtc));
    }
}
