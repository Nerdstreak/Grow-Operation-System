using System.Text.Json;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Infrastructure;

public static class HaConfigLoader
{
    private const string DemoSeedEnvironmentVariable = "GROWDIARY_SEED_DEMO_DATA";

    public static void Apply(AppPaths paths, GrowRepository repository)
    {
        if (!IsDemoSeedEnabled())
        {
            return;
        }

        var configPath = Path.Combine(paths.ContentRootPath, "App_Data", "ha-config.json");
        if (!File.Exists(configPath)) return;

        using var stream = File.OpenRead(configPath);
        JsonDocument? doc;
        try { doc = JsonDocument.Parse(stream); }
        catch { return; }

        using (doc)
        {
            var root = doc.RootElement;

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

            if (!root.TryGetProperty("tents", out var tentsEl)) return;

            var tentsByName = repository.GetTents()
                .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var tentEl in tentsEl.EnumerateArray())
            {
                var name = tentEl.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (!tentsByName.TryGetValue(name, out var tent))
                {
                    tent = repository.CreateTent(name);
                    tentsByName[name] = tent;
                }

                if (tentEl.TryGetProperty("tentType", out var ttEl) &&
                    Enum.TryParse<TentType>(ttEl.GetString(), out var tentType))
                {
                    tent.TentType = tentType;
                }

                if (tentEl.TryGetProperty("cameraEntityId", out var camEl))
                    tent.CameraEntityId = camEl.GetString();

                repository.UpdateTent(tent);

                if (!tentEl.TryGetProperty("sensors", out var sensorsEl)) continue;

                var existingSensors = repository.GetTentSensors(tent.Id)
                    .ToDictionary(s => s.MetricType);

                foreach (var sensorEl in sensorsEl.EnumerateArray())
                {
                    var metricRaw  = sensorEl.TryGetProperty("metricType",   out var m) ? m.GetString() : null;
                    var haEntityId = sensorEl.TryGetProperty("haEntityId",   out var e) ? e.GetString() : null;
                    var label      = sensorEl.TryGetProperty("displayLabel", out var l) ? l.GetString() : null;

                    if (string.IsNullOrWhiteSpace(metricRaw) || string.IsNullOrWhiteSpace(haEntityId)) continue;
                    if (!Enum.TryParse<SensorMetricType>(metricRaw, out var metricType)) continue;

                    if (existingSensors.TryGetValue(metricType, out var existing))
                    {
                        existing.HaEntityId   = haEntityId;
                        existing.DisplayLabel  = label;
                        existing.IsActive      = true;
                        repository.UpdateTentSensor(existing);
                    }
                    else
                    {
                        repository.AddTentSensor(new TentSensor
                        {
                            TentId       = tent.Id,
                            MetricType   = metricType,
                            HaEntityId   = haEntityId,
                            DisplayLabel = label,
                            IsActive     = true
                        });
                    }
                }
            }
        }
    }

    private static bool IsDemoSeedEnabled()
    {
        var raw = Environment.GetEnvironmentVariable(DemoSeedEnvironmentVariable);
        return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
