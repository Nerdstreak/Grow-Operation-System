using System.Text.Json;

namespace GrowAgent.Services;

/// <summary>
/// Der Draht zum Home-Assistant-Supervisor.
/// </summary>
/// <remarks>
/// <para>Gebraucht wird er für genau eine Frage: wie heisse ich selbst? Aus dem
/// eigenen Namen folgt der von Grow OS, denn beide kommen aus demselben
/// Repository und tragen denselben Hash-Vorsatz.</para>
///
/// <para><c>/addons/self/info</c> ist eine Info-Abfrage und damit von der
/// Standardrolle gedeckt — <c>hassio_api: true</c> allein genügt. Nach der
/// vollständigen Add-on-Liste wird bewusst nicht gefragt: die verlangte
/// <c>hassio_role: manager</c>, und damit dürfte dieses Add-on jedes andere
/// starten, stoppen und deinstallieren.</para>
/// </remarks>
public sealed class SupervisorClient
{
    private readonly HttpClient _http;
    private readonly ILogger<SupervisorClient> _logger;

    public SupervisorClient(HttpClient http, ILogger<SupervisorClient> logger)
    {
        _http = http;
        _logger = logger;

        var token = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        }

        _http.BaseAddress = new Uri("http://supervisor/");
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>Läuft dieses Programm überhaupt als Add-on?</summary>
    public static bool ImAddon => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN"));

    /// <summary>Der eigene Slug — oder <c>null</c>, wenn nichts zu holen ist.</summary>
    public async Task<string?> EigenerSlugAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var antwort = await _http.GetAsync("addons/self/info", cancellationToken);
            antwort.EnsureSuccessStatusCode();

            using var strom = await antwort.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(strom, cancellationToken: cancellationToken);

            if (!json.RootElement.TryGetProperty("data", out var daten))
            {
                _logger.LogWarning("Die Antwort des Supervisors enthielt keine Angaben zu diesem Add-on.");
                return null;
            }

            var slug = Text(daten, "slug");
            return string.IsNullOrWhiteSpace(slug) ? null : slug;
        }
        catch (Exception ex)
        {
            // Kein Beinbruch: ohne Auskunft werden die festen Namen probiert,
            // und zur Not traegt der Betreiber die Adresse selbst ein.
            _logger.LogWarning(ex, "Der Supervisor war nicht erreichbar.");
            return null;
        }
    }

    private static string Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var wert) ? wert.GetString() ?? string.Empty : string.Empty;
}
