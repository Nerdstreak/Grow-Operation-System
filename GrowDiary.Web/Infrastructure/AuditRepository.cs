using System.Globalization;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class AuditRepository
{
    private readonly AppPaths _paths;

    public AuditRepository(AppPaths paths)
    {
        _paths = paths;
    }

    public void Add(AuditEntry entry)
    {
        entry.CreatedAtUtc = DateTime.UtcNow;
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO AuditEntries (GrowId, EntityType, EntityId, Action, Summary, CreatedAtUtc) VALUES ($growId, $entityType, $entityId, $action, $summary, $createdAtUtc);";
        command.Parameters.AddWithValue("$growId", entry.GrowId);
        command.Parameters.AddWithValue("$entityType", entry.EntityType);
        command.Parameters.AddWithValue("$entityId", (object?)entry.EntityId ?? DBNull.Value);
        command.Parameters.AddWithValue("$action", entry.Action);
        command.Parameters.AddWithValue("$summary", entry.Summary);
        command.Parameters.AddWithValue("$createdAtUtc", entry.CreatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public List<AuditEntry> GetRecentForGrow(int growId, int limit = 16)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM AuditEntries WHERE GrowId = $growId ORDER BY CreatedAtUtc DESC, Id DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$growId", growId);
        command.Parameters.AddWithValue("$limit", limit);
        var items = new List<AuditEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new AuditEntry
            {
                Id = Convert.ToInt32((long)reader["Id"]),
                GrowId = Convert.ToInt32((long)reader["GrowId"]),
                EntityType = reader["EntityType"]?.ToString() ?? string.Empty,
                EntityId = reader["EntityId"] is DBNull ? null : Convert.ToInt32((long)reader["EntityId"]),
                Action = reader["Action"]?.ToString() ?? string.Empty,
                Summary = reader["Summary"]?.ToString() ?? string.Empty,
                CreatedAtUtc = ParseUtcOrDefault(reader["CreatedAtUtc"])
            });
        }
        return items;
    }

    private static DateTime ParseUtcOrDefault(object raw)
    {
        var text = raw is DBNull ? null : raw?.ToString();
        if (!string.IsNullOrWhiteSpace(text) &&
            DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var parsed))
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
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }
}
