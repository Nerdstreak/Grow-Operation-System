using System.Globalization;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed partial class HardwareRepository
{
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

}
