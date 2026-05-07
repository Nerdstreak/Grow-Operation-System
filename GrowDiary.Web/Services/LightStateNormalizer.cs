using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

public static class LightStateNormalizer
{
    public static LightState Normalize(string? rawState)
    {
        var normalized = rawState?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return LightState.Unknown;
        }

        return normalized.ToLowerInvariant() switch
        {
            "on" or "true" or "1" or "open" => LightState.On,
            "off" or "false" or "0" or "closed" => LightState.Off,
            _ => LightState.Unknown
        };
    }
}
