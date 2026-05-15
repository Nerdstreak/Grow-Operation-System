using System.Globalization;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class SystemAuditRepository
{
    private readonly AppPaths _paths;

    public SystemAuditRepository(AppPaths paths)
    {
        _paths = paths;
    }

    public void Add(SystemAuditEvent entry)
    {
        entry.CreatedAtUtc = DateTime.UtcNow;
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO SystemAuditEvents (EventType, Action, Summary, Severity, Source, RemoteAddress, RelatedGrowId, RelatedFileName, Success, CreatedAtUtc)
            VALUES ($eventType, $action, $summary, $severity, $source, $remoteAddress, $relatedGrowId, $relatedFileName, $success, $createdAtUtc);";
        command.Parameters.AddWithValue("$eventType", entry.EventType);
        command.Parameters.AddWithValue("$action", entry.Action);
        command.Parameters.AddWithValue("$summary", entry.Summary);
        command.Parameters.AddWithValue("$severity", string.IsNullOrWhiteSpace(entry.Severity) ? "info" : entry.Severity);
        command.Parameters.AddWithValue("$source", string.IsNullOrWhiteSpace(entry.Source) ? "backend" : entry.Source);
        command.Parameters.AddWithValue("$remoteAddress", (object?)entry.RemoteAddress ?? DBNull.Value);
        command.Parameters.AddWithValue("$relatedGrowId", (object?)entry.RelatedGrowId ?? DBNull.Value);
        command.Parameters.AddWithValue("$relatedFileName", (object?)entry.RelatedFileName ?? DBNull.Value);
        command.Parameters.AddWithValue("$success", entry.Success ? 1 : 0);
        command.Parameters.AddWithValue("$createdAtUtc", entry.CreatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public List<SystemAuditEvent> GetRecent(int limit = 100, string? eventType = null)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        if (string.IsNullOrWhiteSpace(eventType))
        {
            command.CommandText = "SELECT * FROM SystemAuditEvents ORDER BY CreatedAtUtc DESC, Id DESC LIMIT $limit;";
        }
        else
        {
            command.CommandText = "SELECT * FROM SystemAuditEvents WHERE EventType = $eventType ORDER BY CreatedAtUtc DESC, Id DESC LIMIT $limit;";
            command.Parameters.AddWithValue("$eventType", eventType.Trim());
        }

        command.Parameters.AddWithValue("$limit", safeLimit);
        var items = new List<SystemAuditEvent>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(Map(reader));
        }

        return items;
    }

    private static SystemAuditEvent Map(SqliteDataReader reader)
        => new()
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            EventType = reader["EventType"]?.ToString() ?? string.Empty,
            Action = reader["Action"]?.ToString() ?? string.Empty,
            Summary = reader["Summary"]?.ToString() ?? string.Empty,
            Severity = reader["Severity"]?.ToString() ?? "info",
            Source = reader["Source"]?.ToString() ?? "backend",
            RemoteAddress = reader["RemoteAddress"] is DBNull ? null : reader["RemoteAddress"]?.ToString(),
            RelatedGrowId = reader["RelatedGrowId"] is DBNull ? null : Convert.ToInt32((long)reader["RelatedGrowId"]),
            RelatedFileName = reader["RelatedFileName"] is DBNull ? null : reader["RelatedFileName"]?.ToString(),
            Success = Convert.ToInt32((long)reader["Success"]) == 1,
            CreatedAtUtc = ParseUtcOrDefault(reader["CreatedAtUtc"])
        };

    private static DateTime ParseUtcOrDefault(object raw)
    {
        var text = raw is DBNull ? null : raw?.ToString();
        if (!string.IsNullOrWhiteSpace(text)
            && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        return DateTime.UtcNow;
    }

    private SqliteConnection OpenConnection()
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }
}
