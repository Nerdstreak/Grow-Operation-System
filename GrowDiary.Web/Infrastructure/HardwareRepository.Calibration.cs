using System.Globalization;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed partial class HardwareRepository
{
    public CalibrationEvent CreateCalibrationEvent(CalibrationEvent item)
    {
        var hardware = ValidateCalibrationEvent(item);
        item.CreatedAtUtc = DateTime.UtcNow;
        item.UpdatedAtUtc = DateTime.UtcNow;
        ApplyCalibrationDefaults(item, hardware.CalibrationIntervalDays);

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
        var hardware = ValidateCalibrationEvent(item);
        item.UpdatedAtUtc = DateTime.UtcNow;
        ApplyCalibrationDefaults(item, hardware.CalibrationIntervalDays);

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


    private static void ApplyCalibrationDefaults(CalibrationEvent item, int? calibrationIntervalDays)
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
            // A per-item calibration interval (set by the user) wins; otherwise fall
            // back to a sensible default per calibration type.
            var intervalDays = calibrationIntervalDays ?? item.CalibrationType switch
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

}
