using System.Net;

namespace GrowOsAccess;

/// <summary>Grow OS antwortet nicht so, wie es soll.</summary>
/// <remarks>
/// Eigene Ausnahme, damit ein Werkzeug den Fehler als lesbaren Satz weitergeben
/// kann statt als Stapelverfolgung. Ein Modell, das „404" liest, rät weiter; eines,
/// das „Grow mit Id 7 existiert nicht" liest, fragt nach.
/// </remarks>
public sealed class GrowOsException(string message, bool nichtGefunden = false) : Exception(message)
{
    /// <summary>
    /// Grow OS lief, kannte den Weg aber nicht.
    /// </summary>
    /// <remarks>
    /// Der Unterschied entscheidet, ob ein Werkzeug weitermachen darf. „Zu
    /// diesem Grow gibt es keinen Pheno Hunt" ist eine Antwort und soll die
    /// Pflanzenliste daneben nicht mitreissen. „Grow OS ist nicht erreichbar"
    /// dagegen muss durchschlagen — sonst sieht ein halb leeres Ergebnis aus wie
    /// ein vollstaendiges.
    /// </remarks>
    public bool NichtGefunden { get; } = nichtGefunden;
}

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
            throw new GrowOsException($"Grow OS kennt das nicht: {pfad}", nichtGefunden: true);
        }

        if (!antwort.IsSuccessStatusCode)
        {
            throw new GrowOsException($"Grow OS antwortete mit {(int)antwort.StatusCode} auf {pfad}.");
        }

        return await antwort.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>Eine Datei abrufen — Bytes samt Medientyp.</summary>
    /// <remarks>
    /// <para>Für Fotos: die JSON-Wege liefern nur den Pfad, das Bild selbst
    /// liegt unter <c>/uploads/…</c>. Ein Modell, das die Pflanze wirklich
    /// ansehen soll, braucht die Bytes, nicht den Dateinamen.</para>
    ///
    /// <para><paramref name="maxBytes"/> ist eine harte Grenze: ein Foto aus
    /// einer modernen Kamera kann zweistellige Megabyte haben, und base64
    /// bläht das nochmal um ein Drittel auf. Was zu groß ist, wird abgelehnt
    /// statt die Antwort des Modells zu sprengen.</para>
    /// </remarks>
    public async Task<(byte[] Bytes, string MedienTyp)> DateiLesenAsync(
        string pfad,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        var verbindung = await discovery.FindenAsync(cancellationToken);
        if (!verbindung.Erreichbar || verbindung.Basis is null)
        {
            throw new GrowOsException(verbindung.Meldung);
        }

        using var antwort = await http.GetAsync($"{verbindung.Basis}/{pfad.TrimStart('/')}", cancellationToken);

        if (antwort.StatusCode == HttpStatusCode.NotFound)
        {
            throw new GrowOsException($"Grow OS kennt das nicht: {pfad}", nichtGefunden: true);
        }

        if (!antwort.IsSuccessStatusCode)
        {
            throw new GrowOsException($"Grow OS antwortete mit {(int)antwort.StatusCode} auf {pfad}.");
        }

        // Erst die angekuendigte Groesse pruefen, dann erst laden: sonst liegt
        // das zu grosse Bild schon im Speicher, wenn die Grenze greift.
        if (antwort.Content.Headers.ContentLength is { } laenge && laenge > maxBytes)
        {
            throw new GrowOsException(
                $"Das Bild ist {laenge / 1024 / 1024} MB gross, die Grenze liegt bei {maxBytes / 1024 / 1024} MB.");
        }

        var bytes = await antwort.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.LongLength > maxBytes)
        {
            throw new GrowOsException(
                $"Das Bild ist {bytes.LongLength / 1024 / 1024} MB gross, die Grenze liegt bei {maxBytes / 1024 / 1024} MB.");
        }

        var typ = antwort.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(typ) || !typ.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            // Aus der Endung ableiten: der statische Dateidienst setzt den Typ
            // zwar, aber ein falscher Typ waere fuer das Modell schlimmer als
            // eine ehrliche Ablehnung.
            typ = Path.GetExtension(pfad).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => throw new GrowOsException($"Das ist kein Bild, das ich weitergeben kann: {pfad}"),
            };
        }

        return (bytes, typ);
    }
}
