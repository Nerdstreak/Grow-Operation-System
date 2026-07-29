using System.Net;

namespace GrowOsAccess;

/// <summary>Grow OS antwortet nicht so, wie es soll.</summary>
/// <remarks>
/// Eigene Ausnahme, damit ein Werkzeug den Fehler als lesbaren Satz weitergeben
/// kann statt als Stapelverfolgung. Ein Modell, das „404" liest, rät weiter; eines,
/// das „Grow mit Id 7 existiert nicht" liest, fragt nach.
/// </remarks>
public sealed class GrowOsException(string message) : Exception(message);

/// <summary>
/// Liest bei Grow OS — mehr nicht.
/// </summary>
/// <remarks>
/// Bewusst nur <c>GET</c>: Grow OS lässt aus dem internen Add-on-Netz auch nur
/// Lesezugriffe zu, und was hier nicht vorgesehen ist, kann auch nicht
/// versehentlich gebaut werden. Dosieren und Schalten bleiben in Grow OS hinter
/// seinen Sperren.
/// </remarks>
public sealed class GrowOsReader(HttpClient http, GrowOsDiscovery discovery)
{
    /// <summary>Einen Pfad abrufen und den rohen JSON-Text zurückgeben.</summary>
    /// <param name="pfad">Etwa <c>api/grows?archived=false</c>, ohne führenden Schrägstrich.</param>
    public async Task<string> LesenAsync(string pfad, CancellationToken cancellationToken)
    {
        var verbindung = await discovery.FindenAsync(cancellationToken);
        if (!verbindung.Erreichbar || verbindung.Basis is null)
        {
            throw new GrowOsException(verbindung.Meldung);
        }

        using var antwort = await http.GetAsync($"{verbindung.Basis}/{pfad.TrimStart('/')}", cancellationToken);

        if (antwort.StatusCode == HttpStatusCode.NotFound)
        {
            throw new GrowOsException($"Grow OS kennt das nicht: {pfad}");
        }

        if (!antwort.IsSuccessStatusCode)
        {
            throw new GrowOsException($"Grow OS antwortete mit {(int)antwort.StatusCode} auf {pfad}.");
        }

        return await antwort.Content.ReadAsStringAsync(cancellationToken);
    }
}
