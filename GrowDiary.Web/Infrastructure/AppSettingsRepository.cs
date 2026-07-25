using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

/// <summary>
/// Plain key/value access to the AppSettings table, for state that has no shape of its own.
///
/// Used by the trend guard to remember what it already reported. In-memory would have been
/// less code, but a restart would then re-announce every finding — and a restart while
/// nobody is home is exactly the moment not to send a burst of stale warnings.
/// </summary>
public sealed class AppSettingsRepository : RepositoryBase
{
    public AppSettingsRepository(AppPaths paths) : base(paths)
    {
    }

    public string? GetValue(string key)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM AppSettings WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    public void SetValue(string key, string? value)
    {
        using var connection = OpenConnection();
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
}
