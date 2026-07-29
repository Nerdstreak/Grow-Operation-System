using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace GrowAgent.Services;

/// <summary>Was der Berater über die Anlage weiß.</summary>
/// <param name="Lagebericht">Der Stand des gewählten Grows.</param>
/// <param name="Wissen">Abläufe, Behandlungen, Symptome, Regeln, Sollwerte.</param>
/// <param name="Anweisung">Die Systemanweisung aus Grow OS.</param>
public sealed record BeraterWissen(string Lagebericht, string Wissen, string Anweisung);

/// <summary>Ein Grow, wie ihn Grow OS auflistet.</summary>
public sealed record GrowKurz(int Id, string Name);

/// <summary>
/// Der Draht zu Grow OS.
/// </summary>
/// <remarks>
/// <para>Es wird genau das geholt, was die Berater-Mappe auch als ZIP ausgibt —
/// dieselbe Quelle, damit Mappe und Add-on nie auseinanderlaufen. Der Unterschied
/// ist nur, dass hier niemand etwas herunterladen und anhängen muss.</para>
///
/// <para>Grow OS lässt lesende Anfragen aus dem internen Add-on-Netz zu. Ein
/// Schlüssel ist deshalb nicht nötig, und es gibt auch keinen: schreiben kann
/// dieser Weg nicht.</para>
/// </remarks>
public sealed class GrowOsClient
{
    private readonly HttpClient _http;
    private readonly ILogger<GrowOsClient> _logger;

    public GrowOsClient(HttpClient http, ILogger<GrowOsClient> logger)
    {
        _http = http;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>Antwortet Grow OS unter dieser Adresse?</summary>
    public async Task<bool> ErreichbarAsync(string baseUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var antwort = await _http.GetAsync($"{baseUrl}/api/agent-export/mappe", cancellationToken);
            return antwort.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Grow OS unter {BaseUrl} nicht erreichbar.", baseUrl);
            return false;
        }
    }

    /// <summary>Die laufenden Grows.</summary>
    public async Task<IReadOnlyList<GrowKurz>> GrowsAsync(string baseUrl, CancellationToken cancellationToken)
    {
        using var antwort = await _http.GetAsync($"{baseUrl}/api/grows?archived=false", cancellationToken);
        antwort.EnsureSuccessStatusCode();

        using var strom = await antwort.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(strom, cancellationToken: cancellationToken);

        return json.RootElement.EnumerateArray()
            .Select(eintrag => new GrowKurz(
                eintrag.GetProperty("id").GetInt32(),
                eintrag.TryGetProperty("name", out var name) ? name.GetString() ?? "Grow" : "Grow"))
            .ToList();
    }

    /// <summary>
    /// Die Mappe holen und in ihre Teile zerlegen.
    /// </summary>
    /// <remarks>
    /// Das Wissen wird zu einem Text zusammengefasst; die Trennung in Dateien ist
    /// nur für Menschen gedacht, die sie irgendwo anhängen.
    /// </remarks>
    public async Task<BeraterWissen> WissenAsync(string baseUrl, int growId, CancellationToken cancellationToken)
    {
        using var antwort = await _http.GetAsync(
            $"{baseUrl}/api/agent-export/grows/{growId}/paket", cancellationToken);
        antwort.EnsureSuccessStatusCode();

        var bytes = await antwort.Content.ReadAsByteArrayAsync(cancellationToken);
        using var speicher = new MemoryStream(bytes);
        using var archiv = new ZipArchive(speicher, ZipArchiveMode.Read);

        var lagebericht = string.Empty;
        var anweisung = string.Empty;
        var wissen = new StringBuilder();

        // Nach Namen sortiert, damit die Reihenfolge im Text stabil bleibt —
        // die Nummern im Dateinamen sind genau dafür da.
        foreach (var eintrag in archiv.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal))
        {
            using var strom = eintrag.Open();
            using var leser = new StreamReader(strom, Encoding.UTF8);
            var inhalt = await leser.ReadToEndAsync(cancellationToken);

            if (eintrag.FullName.StartsWith("10-", StringComparison.Ordinal)) lagebericht = inhalt;
            else if (eintrag.FullName.StartsWith("00-", StringComparison.Ordinal)) anweisung = inhalt;
            else if (eintrag.FullName.StartsWith("2", StringComparison.Ordinal))
            {
                wissen.AppendLine(inhalt);
                wissen.AppendLine();
            }
        }

        return new BeraterWissen(lagebericht, wissen.ToString(), anweisung);
    }
}
