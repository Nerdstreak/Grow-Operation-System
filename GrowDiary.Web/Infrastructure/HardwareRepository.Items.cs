using System.Globalization;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed partial class HardwareRepository
{
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
                TentId, SetupId, HydroSetupId, GrowId, WearTemplateId, TentSensorId, HaEntityId, SensorMetricType, DeviceKind,
                Manufacturer, Model, SerialNumber,
                InstalledAtUtc, RetiredAtUtc,
                ExpectedLifespanDays, InspectionIntervalDays, CalibrationIntervalDays, Notes,
                CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $name, $category, $status, $criticality,
                $tentId, $setupId, $hydroSetupId, $growId, $wearTemplateId, $tentSensorId, $haEntityId, $sensorMetricType, $deviceKind,
                $manufacturer, $model, $serialNumber,
                $installedAtUtc, $retiredAtUtc,
                $expectedLifespanDays, $inspectionIntervalDays, $calibrationIntervalDays, $notes,
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
                SensorMetricType = $sensorMetricType,
                DeviceKind = $deviceKind,
                Manufacturer = $manufacturer,
                Model = $model,
                SerialNumber = $serialNumber,
                InstalledAtUtc = $installedAtUtc,
                RetiredAtUtc = $retiredAtUtc,
                ExpectedLifespanDays = $expectedLifespanDays,
                InspectionIntervalDays = $inspectionIntervalDays,
                CalibrationIntervalDays = $calibrationIntervalDays,
                Notes = $notes,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddHardwareItemParameters(command, item);
        command.Parameters.AddWithValue("$id", item.Id);
        command.ExecuteNonQuery();
    }


    public void DeleteHardwareItem(int id)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM MaintenanceEvents WHERE HardwareItemId = $id;";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM CalibrationEvents WHERE HardwareItemId = $id;";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "UPDATE RiskEvents SET HardwareItemId = NULL, UpdatedAtUtc = datetime('now') WHERE HardwareItemId = $id;";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM HardwareItems WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
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
            MetricType = HasColumn(reader, "SensorMetricType") && Enum.TryParse<SensorMetricType>(NullString(reader["SensorMetricType"]), out var metricType) ? metricType : null,
            DeviceKind = HasColumn(reader, "DeviceKind") && Enum.TryParse<HardwareDeviceKind>(NullString(reader["DeviceKind"]), out var deviceKind) ? deviceKind : null,
            Manufacturer = NullString(reader["Manufacturer"]),
            Model = NullString(reader["Model"]),
            SerialNumber = NullString(reader["SerialNumber"]),
            InstalledAtUtc = ParseStoredDateTime(reader["InstalledAtUtc"]?.ToString()),
            RetiredAtUtc = ParseStoredDateTime(reader["RetiredAtUtc"]?.ToString()),
            ExpectedLifespanDays = reader["ExpectedLifespanDays"] is DBNull or null ? null : Convert.ToInt32(reader["ExpectedLifespanDays"], CultureInfo.InvariantCulture),
            InspectionIntervalDays = reader["InspectionIntervalDays"] is DBNull or null ? null : Convert.ToInt32(reader["InspectionIntervalDays"], CultureInfo.InvariantCulture),
            CalibrationIntervalDays = reader["CalibrationIntervalDays"] is DBNull or null ? null : Convert.ToInt32(reader["CalibrationIntervalDays"], CultureInfo.InvariantCulture),
            Notes = NullString(reader["Notes"]),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
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
        command.Parameters.AddWithValue("$sensorMetricType", (object?)item.MetricType?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$deviceKind", (object?)item.DeviceKind?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$manufacturer", (object?)NormalizeOptional(item.Manufacturer) ?? DBNull.Value);
        command.Parameters.AddWithValue("$model", (object?)NormalizeOptional(item.Model) ?? DBNull.Value);
        command.Parameters.AddWithValue("$serialNumber", (object?)NormalizeOptional(item.SerialNumber) ?? DBNull.Value);
        command.Parameters.AddWithValue("$installedAtUtc", item.InstalledAtUtc.HasValue ? ToStorageUtc(item.InstalledAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$retiredAtUtc", item.RetiredAtUtc.HasValue ? ToStorageUtc(item.RetiredAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$expectedLifespanDays", (object?)item.ExpectedLifespanDays ?? DBNull.Value);
        command.Parameters.AddWithValue("$inspectionIntervalDays", (object?)item.InspectionIntervalDays ?? DBNull.Value);
        command.Parameters.AddWithValue("$calibrationIntervalDays", (object?)item.CalibrationIntervalDays ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)NormalizeOptional(item.Notes) ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(item.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(item.UpdatedAtUtc));
    }

}
