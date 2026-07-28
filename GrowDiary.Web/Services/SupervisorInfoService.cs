using System.Text.Json;
using GrowDiary.Web.Infrastructure;

namespace GrowDiary.Web.Services;

/// <summary>
/// Fragt den Supervisor, wie dieses Add-on heisst.
/// </summary>
/// <remarks>
/// Wofür: Grow OS wird über den Ingress ausgeliefert, und dieser Pfad
/// (<c>/api/hassio_ingress/&lt;token&gt;/</c>) trägt ein Token, das pro Anfrage
/// wechselt — ein Lesezeichen darauf ist am nächsten Tag tot. Stabil ist nur der
/// Panel-Pfad <c>/hassio/ingress/&lt;slug&gt;</c>, und dafür braucht es den Slug.
///
/// Raten geht nicht: der Slug ist je nach Installationsweg <c>local_grow_os</c>
/// oder <c>&lt;repo-hash&gt;_grow_os</c>. Der Supervisor weiss es, also wird er
/// gefragt.
///
/// Das Ergebnis wird gemerkt — der Slug ändert sich zur Laufzeit nie, und die
/// Seite, die ihn braucht, wird oft geöffnet.
/// </remarks>
public sealed class SupervisorInfoService
{
    private const string SelfInfoUrl = "http://supervisor/addons/self/info";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SupervisorInfoService> _logger;

    private string? _cachedSlug;

    public SupervisorInfoService(IHttpClientFactory httpClientFactory, ILogger<SupervisorInfoService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>Der Add-on-Slug, oder null wenn Grow OS nicht als Add-on läuft.</summary>
    public async Task<string?> GetAddonSlugAsync(CancellationToken cancellationToken = default)
    {
        // Testdatenmodus: der Entwicklungsrechner hat keinen Supervisor. Ein
        // plausibler Slug, damit sich die Seite lokal ansehen laesst.
        if (DemoData.IsEnabled)
        {
            return "local_grow_os";
        }

        if (_cachedSlug is not null)
        {
            return _cachedSlug;
        }

        var token = HomeAssistantAddon.SupervisorToken;
        if (token is null)
        {
            return null;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient(nameof(SupervisorInfoService));
            client.Timeout = RequestTimeout;
            using var request = new HttpRequestMessage(HttpMethod.Get, SelfInfoUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Supervisor antwortete auf addons/self/info mit {Status}.", (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            _cachedSlug = ReadSlug(await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken));
            return _cachedSlug;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Supervisor nach dem eigenen Slug gefragt — keine brauchbare Antwort.");
            return null;
        }
    }

    /// <summary>
    /// Die Antwort des Supervisors ist immer <c>{ "result": "ok", "data": { … } }</c>.
    /// </summary>
    public static string? ReadSlug(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("slug", out var slug)
            || slug.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = slug.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>Der stabile Panel-Pfad zu diesem Add-on, oder null ohne Slug.</summary>
    public static string? PanelPath(string? slug)
        => string.IsNullOrWhiteSpace(slug) ? null : $"/hassio/ingress/{slug}";
}
