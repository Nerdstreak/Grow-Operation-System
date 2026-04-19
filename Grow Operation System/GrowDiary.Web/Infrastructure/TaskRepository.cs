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

    public List<GrowTask> GetOpenForGrow(int growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT gt.*, g.Name AS GrowName FROM GrowTasks gt LEFT JOIN Grows g ON g.Id = gt.GrowId WHERE gt.GrowId = $growId AND gt.Status = 'Open' ORDER BY CASE gt.Priority WHEN 'Critical' THEN 0 WHEN 'High' THEN 1 WHEN 'Normal' THEN 2 ELSE 3 END, gt.DueAtUtc, gt.Id DESC;";
        command.Parameters.AddWithValue("$growId", growId);
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
            DueAtUtc = reader["DueAtUtc"] is DBNull ? null : DateTime.Parse(reader["DueAtUtc"]?.ToString() ?? string.Empty, CultureInfo.InvariantCulture).ToUniversalTime(),
            Priority = Enum.TryParse<TaskPriority>(reader["Priority"]?.ToString(), out var priority) ? priority : TaskPriority.Normal,
            Status = Enum.TryParse<GrowTaskStatus>(reader["Status"]?.ToString(), out var status) ? status : GrowTaskStatus.Open,
            CreatedAtUtc = DateTime.Parse(reader["CreatedAtUtc"]?.ToString() ?? string.Empty, CultureInfo.InvariantCulture).ToUniversalTime(),
            CompletedAtUtc = reader["CompletedAtUtc"] is DBNull ? null : DateTime.Parse(reader["CompletedAtUtc"]?.ToString() ?? string.Empty, CultureInfo.InvariantCulture).ToUniversalTime()
        };

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
