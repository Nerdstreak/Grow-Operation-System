using System.Text.Json;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Holds the one tap-water profile.
/// </summary>
/// <remarks>
/// Stored as JSON in the AppSettings key/value table instead of an own table:
/// it is a single row that is read whole and written whole, never queried by
/// column. A table would buy nothing except one more migration to carry. If
/// profiles ever multiply (several taps, a well), that is the moment to move.
/// </remarks>
public sealed class WaterProfileStore
{
    private const string Key = "water-profile";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly AppSettingsRepository _settings;

    public WaterProfileStore(AppSettingsRepository settings)
    {
        _settings = settings;
    }

    /// <summary>The stored profile — or <c>null</c> if none was ever saved.</summary>
    public WaterProfile? Get()
    {
        var raw = _settings.GetValue(Key);
        if (string.IsNullOrWhiteSpace(raw)) return null;

        try
        {
            return JsonSerializer.Deserialize<WaterProfile>(raw, Json);
        }
        catch (JsonException)
        {
            // Ein kaputter Eintrag darf die Seite nicht mitreissen; er zaehlt
            // als "kein Profil" und wird beim naechsten Speichern ersetzt.
            return null;
        }
    }

    public void Save(WaterProfile profile)
    {
        profile.UpdatedAtUtc = DateTime.UtcNow;
        _settings.SetValue(Key, JsonSerializer.Serialize(profile, Json));
    }
}
