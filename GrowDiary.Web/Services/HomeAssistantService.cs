using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

public sealed class HomeAssistantService
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan BackoffWindow = TimeSpan.FromSeconds(20);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HomeAssistantService> _logger;
    private long _circuitOpenUntilTicks;

    public HomeAssistantService(IHttpClientFactory httpClientFactory, ILogger<HomeAssistantService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<Dictionary<string, HomeAssistantState>> GetStatesAsync(HomeAssistantSettings settings, Tent tent, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, HomeAssistantState>(StringComparer.OrdinalIgnoreCase);
        if (!settings.IsConfigured) return result;
        if (IsCircuitOpen()) return result;

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

        var activeMappings = mappings
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .ToList();
        if (activeMappings.Count == 0)
        {
            return result;
        }

        var client = _httpClientFactory.CreateClient(nameof(HomeAssistantService));
        client.BaseAddress = new Uri(NormalizeBaseUrl(settings.BaseUrl!));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
        client.Timeout = RequestTimeout;

        var tasks = activeMappings
            .Select(kvp => FetchStateAsync(client, kvp.Key, kvp.Value!, cancellationToken));

        var hadTransportFailure = false;
        foreach (var (key, state, transportFailure) in await Task.WhenAll(tasks))
        {
            hadTransportFailure |= transportFailure;
            if (state is not null) result[key] = state;
        }

        if (result.Count > 0)
        {
            ResetCircuit();
        }
        else if (hadTransportFailure)
        {
            if (TryOpenCircuit())
            {
                _logger.LogWarning(
                    "Home Assistant ist derzeit nicht erreichbar. Weitere Abfragen sind für {BackoffSeconds} Sekunden pausiert (Zelt {TentId}).",
                    (int)BackoffWindow.TotalSeconds,
                    tent.Id);
            }
        }

        return result;
    }

    private async Task<(string Key, HomeAssistantState? State, bool TransportFailure)> FetchStateAsync(
        HttpClient client,
        string key,
        string entityId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync($"/api/states/{entityId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "Home Assistant state {EntityId} fuer Metrik {MetricKey} konnte nicht geladen werden: HTTP {StatusCode}.",
                    entityId,
                    key,
                    (int)response.StatusCode);
                return (key, null, false);
            }

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

            return (key, state, false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return (key, null, false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Home Assistant state {EntityId} fuer Metrik {MetricKey} konnte nicht geladen werden.", entityId, key);
            return (key, null, true);
        }
    }



    public async Task<(byte[] Bytes, string ContentType)?> GetCameraSnapshotAsync(HomeAssistantSettings settings, string entityId, CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured || string.IsNullOrWhiteSpace(entityId))
        {
            return null;
        }
        if (IsCircuitOpen())
        {
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(HomeAssistantService));
            client.BaseAddress = new Uri(NormalizeBaseUrl(settings.BaseUrl!));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
            client.Timeout = RequestTimeout;

            using var response = await client.GetAsync($"/api/camera_proxy/{entityId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "Home Assistant Kamera {EntityId} konnte nicht geladen werden: HTTP {StatusCode}.",
                    entityId,
                    (int)response.StatusCode);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            ResetCircuit();
            return (bytes, contentType);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            if (TryOpenCircuit())
            {
                _logger.LogWarning(
                    ex,
                    "Home Assistant Kamera {EntityId} konnte nicht geladen werden. Weitere Abfragen sind für {BackoffSeconds} Sekunden pausiert.",
                    entityId,
                    (int)BackoffWindow.TotalSeconds);
            }
            else
            {
                _logger.LogDebug(ex, "Home Assistant Kamera {EntityId} konnte nicht geladen werden.", entityId);
            }

            return null;
        }
    }

    private bool IsCircuitOpen()
        => Interlocked.Read(ref _circuitOpenUntilTicks) > DateTime.UtcNow.Ticks;

    private bool TryOpenCircuit()
    {
        var nowTicks = DateTime.UtcNow.Ticks;
        var openUntilTicks = DateTime.UtcNow.Add(BackoffWindow).Ticks;

        while (true)
        {
            var current = Interlocked.Read(ref _circuitOpenUntilTicks);
            if (current > nowTicks)
            {
                return false;
            }

            var observed = Interlocked.CompareExchange(ref _circuitOpenUntilTicks, openUntilTicks, current);
            if (observed == current)
            {
                return true;
            }
        }
    }

    private void ResetCircuit()
        => Interlocked.Exchange(ref _circuitOpenUntilTicks, 0);

    private static string NormalizeBaseUrl(string value)
        => value.Trim().TrimEnd('/');
}
