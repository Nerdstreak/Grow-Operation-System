using System.Text.Json;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Infrastructure;

public static class HaConfigLoader
{
    public static void Apply(AppPaths paths, GrowRepository repository)
    {
        var configPath = Path.Combine(paths.ContentRootPath, "App_Data", "ha-config.json");
        if (!File.Exists(configPath))
        {
            return;
        }

        using var stream = File.OpenRead(configPath);
        JsonDocument? doc;
        try
        {
            doc = JsonDocument.Parse(stream);
        }
        catch
        {
            // Invalid JSON file: keep app startup resilient.
            return;
        }

        using (doc)
        {
            var root = doc.RootElement;

            if (root.TryGetProperty("homeAssistant", out var ha))
            {
                var url = ha.TryGetProperty("url", out var u) ? u.GetString() : null;
                var token = ha.TryGetProperty("token", out var t) ? t.GetString() : null;

                if (!string.IsNullOrWhiteSpace(url) || !string.IsNullOrWhiteSpace(token))
                {
                    var existing = repository.GetHomeAssistantSettings();
                    repository.SaveHomeAssistantSettings(new HomeAssistantSettings
                    {
                        BaseUrl = !string.IsNullOrWhiteSpace(url) ? url : existing.BaseUrl,
                        AccessToken = !string.IsNullOrWhiteSpace(token) ? token : existing.AccessToken,
                        Enabled = true
                    });
                }
            }

            if (!root.TryGetProperty("tents", out var tentsEl))
            {
                return;
            }

            var tentsByName = repository.GetTents()
                .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var tentEl in tentsEl.EnumerateArray())
            {
                var name = tentEl.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (!tentsByName.TryGetValue(name, out var tent))
                {
                    tent = repository.CreateTent(name);
                    tentsByName[name] = tent;
                }

                // TODO Sprint B1b: TentSensor-Einträge aus ha-config.json laden
                // (entities-Block wird künftig als TentSensor-Liste persistiert)
                if (tentEl.TryGetProperty("entities", out var entities))
                {
                    string? Get(string key) => entities.TryGetProperty(key, out var value) ? value.GetString() : null;
                    tent.CameraEntityId = Get("camera") ?? tent.CameraEntityId;
                }

                repository.UpdateTent(tent);
            }
        }
    }
}
