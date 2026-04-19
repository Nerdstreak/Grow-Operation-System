using System.Net.Http.Headers;
using System.Text.Json;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

public sealed class HomeAssistantService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HomeAssistantService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<Dictionary<string, HomeAssistantState>> GetStatesAsync(HomeAssistantSettings settings, Tent tent, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, HomeAssistantState>(StringComparer.OrdinalIgnoreCase);
        if (!settings.IsConfigured) return result;

        var mappings = new Dictionary<string, string?>
        {
            ["temperature"] = tent.TemperatureEntityId,
            ["humidity"] = tent.HumidityEntityId,
            ["vpd"] = tent.VpdEntityId,
            ["reservoir-ph"] = tent.ReservoirPhEntityId,
            ["reservoir-ec"] = tent.ReservoirEcEntityId,
            ["reservoir-level"] = tent.ReservoirLevelEntityId,
            ["reservoir-temp"] = tent.ReservoirTempEntityId,
            ["light"] = tent.LightEntityId,
            ["ppfd"]              = tent.PpfdEntityId,
            ["orp"]              = tent.OrpEntityId,
            ["dissolved-oxygen"] = tent.DissolvedOxygenEntityId,
            ["co2"]              = tent.Co2EntityId
        };

        var client = _httpClientFactory.CreateClient(nameof(HomeAssistantService));
        client.BaseAddress = new Uri(NormalizeBaseUrl(settings.BaseUrl!));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);

        var tasks = mappings
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .Select(kvp => FetchStateAsync(client, kvp.Key, kvp.Value!, cancellationToken));

        foreach (var (key, state) in await Task.WhenAll(tasks))
        {
            if (state is not null) result[key] = state;
        }

        return result;
    }

    private static async Task<(string Key, HomeAssistantState? State)> FetchStateAsync(HttpClient client, string key, string entityId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync($"/api/states/{entityId}", cancellationToken);
            if (!response.IsSuccessStatusCode) return (key, null);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            var state = new HomeAssistantState
            {
                EntityId = root.TryGetProperty("entity_id", out var eid) ? eid.GetString() ?? entityId : entityId,
                State = root.TryGetProperty("state", out var stateEl) ? stateEl.GetString() ?? string.Empty : string.Empty,
                LastChanged = root.TryGetProperty("last_changed", out var changedEl) && DateTime.TryParse(changedEl.GetString(), out var changed)
                    ? changed.ToUniversalTime()
                    : null
            };

            if (root.TryGetProperty("attributes", out var attrs))
            {
                if (attrs.TryGetProperty("friendly_name", out var friendly)) state.FriendlyName = friendly.GetString();
                if (attrs.TryGetProperty("unit_of_measurement", out var unit)) state.UnitOfMeasurement = unit.GetString();
            }

            if (double.TryParse(state.State, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var numeric))
            {
                state.NumericValue = numeric;
            }

            return (key, state);
        }
        catch
        {
            return (key, null);
        }
    }



    public async Task<(byte[] Bytes, string ContentType)?> GetCameraSnapshotAsync(HomeAssistantSettings settings, string entityId, CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured || string.IsNullOrWhiteSpace(entityId))
        {
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(HomeAssistantService));
            client.BaseAddress = new Uri(NormalizeBaseUrl(settings.BaseUrl!));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);

            using var response = await client.GetAsync($"/api/camera_proxy/{entityId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            return (bytes, contentType);
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeBaseUrl(string value)
        => value.Trim().TrimEnd('/');
}
