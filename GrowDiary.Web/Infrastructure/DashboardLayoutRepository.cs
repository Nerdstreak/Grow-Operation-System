using System.Text.Json;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Infrastructure;

/// <summary>
/// Stores each tent's dashboard arrangement as JSON in the settings table — it is user
/// preference, not domain data, so it doesn't warrant its own schema.
/// </summary>
public sealed class DashboardLayoutRepository : RepositoryBase
{
    private const string KeyPrefix = "dashboard:tent:";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public DashboardLayoutRepository(AppPaths paths) : base(paths)
    {
    }

    /// <summary>The saved layout, or the built-in default when the tent has none.</summary>
    public DashboardLayout Get(int tentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM AppSettings WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", KeyPrefix + tentId);
        var raw = command.ExecuteScalar() as string;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return DashboardLayout.Default(tentId);
        }

        try
        {
            var layout = JsonSerializer.Deserialize<DashboardLayout>(raw, Json);
            if (layout is null || layout.IsEmpty)
            {
                return DashboardLayout.Default(tentId);
            }

            layout.TentId = tentId;
            return layout;
        }
        catch (JsonException)
        {
            // A corrupt preference must never take the dashboard down.
            return DashboardLayout.Default(tentId);
        }
    }

    public void Save(DashboardLayout layout)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AppSettings (Key, Value) VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        command.Parameters.AddWithValue("$key", KeyPrefix + layout.TentId);
        command.Parameters.AddWithValue("$value", JsonSerializer.Serialize(layout, Json));
        command.ExecuteNonQuery();
    }

    /// <summary>Drops the customisation so the tent falls back to the built-in layout.</summary>
    public void Reset(int tentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM AppSettings WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", KeyPrefix + tentId);
        command.ExecuteNonQuery();
    }
}
