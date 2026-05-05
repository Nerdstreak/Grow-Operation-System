using System.Globalization;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class TaskRepository
{
    private readonly AppPaths _paths;

    public TaskRepository(AppPaths paths)
    {
        _paths = paths;
    }

    public List<GrowTask> GetOpenForGrow(int growId) => GetForGrow(growId, GrowTaskStatus.Open);

    public List<GrowTask> GetForGrow(int growId, GrowTaskStatus? status = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var filter = status.HasValue ? "AND gt.Status = $status" : string.Empty;
        // Open tasks sort by priority; all tasks sort by recency
        var order = status == GrowTaskStatus.Open
            ? "ORDER BY CASE gt.Priority WHEN 'Critical' THEN 0 WHEN 'High' THEN 1 WHEN 'Normal' THEN 2 ELSE 3 END, gt.DueAtUtc, gt.Id DESC"
            : "ORDER BY COALESCE(gt.CompletedAtUtc, gt.DueAtUtc, gt.CreatedAtUtc) DESC, gt.Id DESC";
        command.CommandText = $"""
            SELECT gt.*, g.Name AS GrowName
            FROM GrowTasks gt
            LEFT JOIN Grows g ON g.Id = gt.GrowId
            WHERE gt.GrowId = $growId {filter}
            {order};
        """;
        command.Parameters.AddWithValue("$growId", growId);
        if (status.HasValue)
            command.Parameters.AddWithValue("$status", status.Value.ToString());
        return ReadTasks(command);
    }

    public List<GrowTask> GetDueSoon(int limit = 12)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT gt.*, g.Name AS GrowName FROM GrowTasks gt LEFT JOIN Grows g ON g.Id = gt.GrowId WHERE gt.Status = 'Open' ORDER BY COALESCE(gt.DueAtUtc, gt.CreatedAtUtc) ASC, gt.Id DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);
        return ReadTasks(command);
    }

    public int Create(GrowTask task)
    {
        task.CreatedAtUtc = DateTime.UtcNow;
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO GrowTasks (GrowId, Title, Notes, DueAtUtc, Priority, Status, CreatedAtUtc, CompletedAtUtc)
            VALUES ($growId, $title, $notes, $dueAtUtc, $priority, $status, $createdAtUtc, $completedAtUtc);
            SELECT last_insert_rowid();
        """;
        command.Parameters.AddWithValue("$growId", task.GrowId);
        command.Parameters.AddWithValue("$title", task.Title);
        command.Parameters.AddWithValue("$notes", (object?)task.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$dueAtUtc", task.DueAtUtc.HasValue ? task.DueAtUtc.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);
        command.Parameters.AddWithValue("$priority", task.Priority.ToString());
        command.Parameters.AddWithValue("$status", task.Status.ToString());
        command.Parameters.AddWithValue("$createdAtUtc", task.CreatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$completedAtUtc", task.CompletedAtUtc.HasValue ? task.CompletedAtUtc.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);
        return Convert.ToInt32((long)command.ExecuteScalar()!);
    }

    public GrowTask? Get(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT gt.*, g.Name AS GrowName FROM GrowTasks gt LEFT JOIN Grows g ON g.Id = gt.GrowId WHERE gt.Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public void SetStatus(int id, GrowTaskStatus status)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE GrowTasks SET Status = $status, CompletedAtUtc = $completedAtUtc WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$status", status.ToString());
        command.Parameters.AddWithValue("$completedAtUtc", status == GrowTaskStatus.Open ? DBNull.Value : DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM GrowTasks WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private List<GrowTask> ReadTasks(SqliteCommand command)
    {
        var items = new List<GrowTask>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(Map(reader));
        }
        return items;
    }

    private static GrowTask Map(SqliteDataReader reader)
        => new()
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            GrowId = Convert.ToInt32((long)reader["GrowId"]),
            GrowName = reader["GrowName"] is DBNull ? null : reader["GrowName"]?.ToString(),
            Title = reader["Title"]?.ToString() ?? string.Empty,
            Notes = reader["Notes"] is DBNull ? null : reader["Notes"]?.ToString(),
            DueAtUtc = ParseUtcOrNull(reader["DueAtUtc"]),
            Priority = Enum.TryParse<TaskPriority>(reader["Priority"]?.ToString(), out var priority) ? priority : TaskPriority.Normal,
            Status = Enum.TryParse<GrowTaskStatus>(reader["Status"]?.ToString(), out var status) ? status : GrowTaskStatus.Open,
            CreatedAtUtc = ParseUtcOrDefault(reader["CreatedAtUtc"]),
            CompletedAtUtc = ParseUtcOrNull(reader["CompletedAtUtc"])
        };

    private static DateTime ParseUtcOrDefault(object raw)
    {
        var parsed = ParseUtcOrNull(raw);
        return parsed ?? DateTime.UtcNow;
    }

    private static DateTime? ParseUtcOrNull(object raw)
    {
        if (raw is DBNull)
        {
            return null;
        }

        var text = raw?.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
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
