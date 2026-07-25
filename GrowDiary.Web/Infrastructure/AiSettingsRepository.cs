using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class AiSettingsRepository : RepositoryBase
{
    public AiSettingsRepository(AppPaths paths) : base(paths)
    {
    }

    private const string BaseUrlKey = "ai:baseUrl";
    private const string ApiKeyKey = "ai:apiKey";
    private const string ModelKey = "ai:model";
    private const string EnabledKey = "ai:enabled";
    private const string AllowPhotosKey = "ai:allowPhotos";
    private const string ProviderKey = "ai:provider";

    public AiSettings GetAiSettings()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Key, Value FROM AppSettings WHERE Key LIKE 'ai:%';";
        using var reader = command.ExecuteReader();

        var settings = new AiSettings();
        while (reader.Read())
        {
            var key = reader["Key"]?.ToString();
            var value = reader["Value"]?.ToString();
            switch (key)
            {
                case BaseUrlKey: settings.BaseUrl = Blank(value); break;
                case ApiKeyKey: settings.ApiKey = Blank(value); break;
                case ModelKey: settings.Model = Blank(value); break;
                case EnabledKey: settings.Enabled = ParseBool(value); break;
                case AllowPhotosKey: settings.AllowPhotos = ParseBool(value); break;
                case ProviderKey:
                    settings.Provider = Enum.TryParse<AiProvider>(value, ignoreCase: true, out var provider)
                        ? provider
                        : AiProvider.OpenAiCompatible;
                    break;
            }
        }

        return settings;
    }

    /// <summary>
    /// Saves the connection. A null <paramref name="apiKey"/> means "leave the stored key
    /// alone" — the UI never receives the key back, so it cannot send it in again, and
    /// saving an unrelated change must not wipe it.
    /// </summary>
    public void SaveAiSettings(AiSettings settings, bool replaceApiKey)
    {
        using var connection = OpenConnection();
        Upsert(connection, BaseUrlKey, Trim(settings.BaseUrl));
        Upsert(connection, ModelKey, Trim(settings.Model));
        Upsert(connection, EnabledKey, settings.Enabled ? "1" : "0");
        Upsert(connection, AllowPhotosKey, settings.AllowPhotos ? "1" : "0");
        Upsert(connection, ProviderKey, settings.Provider.ToString());

        if (replaceApiKey)
        {
            Upsert(connection, ApiKeyKey, Trim(settings.ApiKey));
        }
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool ParseBool(string? value) => value is "1" or "true" or "True";

    private static void Upsert(SqliteConnection connection, string key, string? value)
    {
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
