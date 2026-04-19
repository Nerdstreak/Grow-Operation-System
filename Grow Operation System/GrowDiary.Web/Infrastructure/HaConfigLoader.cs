using System.Text.Json;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Infrastructure;

public static class HaConfigLoader
{
    public static void Apply(AppPaths paths, GrowRepository repository)
    {
        var configPath = Path.Combine(paths.ContentRootPath, "App_Data", "ha-config.json");
        if (!File.Exists(configPath)) return;

        using var stream = File.OpenRead(configPath);
        JsonDocument doc;
        try { doc = JsonDocument.Parse(stream); }
        catch { return; } // ungültige JSON-Datei → still ignorieren

        var root = doc.RootElement;

        // ── HA-Verbindung ─────────────────────────────────────────────
        if (root.TryGetProperty("homeAssistant", out var ha))
        {
            var url   = ha.TryGetProperty("url",   out var u) ? u.GetString() : null;
            var token = ha.TryGetProperty("token", out var t) ? t.GetString() : null;

            if (!string.IsNullOrWhiteSpace(url) || !string.IsNullOrWhiteSpace(token))
            {
                var existing = repository.GetHomeAssistantSettings();
                repository.SaveHomeAssistantSettings(new HomeAssistantSettings
                {
                    BaseUrl     = !string.IsNullOrWhiteSpace(url)   ? url   : existing.BaseUrl,
                    AccessToken = !string.IsNullOrWhiteSpace(token) ? token : existing.AccessToken,
                    Enabled     = true
                });
            }
        }

        // ── Zelte ────────────────────────────────────────────────────
        if (!root.TryGetProperty("tents", out var tentsEl)) return;

        var allTents = repository.GetTents();

        foreach (var tentEl in tentsEl.EnumerateArray())
        {
            var name = tentEl.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(name)) continue;

            var tent = allTents.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                       ?? repository.CreateTent(name);

            if (!tentEl.TryGetProperty("entities", out var entities)) continue;

            string? Get(string key) => entities.TryGetProperty(key, out var v) ? v.GetString() : null;

            tent.TemperatureEntityId      = Get("temperature")      ?? tent.TemperatureEntityId;
            tent.HumidityEntityId         = Get("humidity")         ?? tent.HumidityEntityId;
            tent.VpdEntityId              = Get("vpd")              ?? tent.VpdEntityId;
            tent.LightEntityId            = Get("light")            ?? tent.LightEntityId;
            tent.CameraEntityId           = Get("camera")           ?? tent.CameraEntityId;
            tent.ReservoirPhEntityId      = Get("reservoirPh")      ?? tent.ReservoirPhEntityId;
            tent.ReservoirEcEntityId      = Get("reservoirEc")      ?? tent.ReservoirEcEntityId;
            tent.ReservoirLevelEntityId   = Get("reservoirLevel")   ?? tent.ReservoirLevelEntityId;
            tent.ReservoirTempEntityId    = Get("reservoirTemp")    ?? tent.ReservoirTempEntityId;
            tent.PpfdEntityId             = Get("ppfd")             ?? tent.PpfdEntityId;
            tent.OrpEntityId              = Get("orp")              ?? tent.OrpEntityId;
            tent.DissolvedOxygenEntityId  = Get("dissolvedOxygen")  ?? tent.DissolvedOxygenEntityId;
            tent.Co2EntityId              = Get("co2")              ?? tent.Co2EntityId;

            repository.UpdateTent(tent);
        }
    }
}
