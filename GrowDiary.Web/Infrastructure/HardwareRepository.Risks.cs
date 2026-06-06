using System.Globalization;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed partial class HardwareRepository
{
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
