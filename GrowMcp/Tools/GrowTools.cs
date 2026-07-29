using System.ComponentModel;
using System.Text.Json;
using GrowOsAccess;
using ModelContextProtocol.Server;

namespace GrowMcp.Tools;

/// <summary>
/// Die Griffe, die ein Modell an Grow OS hat.
/// </summary>
/// <remarks>
/// <para>Alle nur lesend. Es gibt bewusst kein Werkzeug zum Dosieren, Schalten
/// oder Ändern: die Sperren dafür sitzen in Grow OS, und was hier nicht gebaut
/// ist, kann auch nicht versehentlich ausgelöst werden. Ein Modell, das eine
/// Pumpe starten will, muss den Menschen fragen.</para>
///
/// <para>Zurückgegeben wird meist das JSON von Grow OS, unverändert. Das ist
/// ehrlicher als eine eigene Zusammenfassung — sie wäre eine zweite Stelle, an der
/// sich Bedeutung verschieben kann.</para>
/// </remarks>
[McpServerToolType]
public sealed class GrowTools(GrowOsReader reader)
{
    /// <summary>Die Messgrössen, die Grow OS kennt — für die Werkzeugbeschreibung.</summary>
    private const string Messgroessen =
        "reservoir-ph, reservoir-ec, reservoir-temp, reservoir-level, reservoir-level-cm, " +
        "orp, dissolved-oxygen, temperature, humidity, vpd, co2, ppfd";

    [McpServerTool(Name = "grows_auflisten")]
    [Description("Listet die laufenden Grows mit Id, Name, Sorte und Phase. Jedes andere Werkzeug braucht eine dieser Ids.")]
    public Task<string> GrowsAuflistenAsync(
        [Description("true, um auch abgeschlossene Grows zu sehen")] bool auchAbgeschlossene = false,
        CancellationToken cancellationToken = default)
        => SicherAsync(() => reader.LesenAsync(
            $"api/grows?archived={(auchAbgeschlossene ? "true" : "false")}", cancellationToken));

    [McpServerTool(Name = "lagebericht")]
    [Description("Der vollständige Stand eines Grows als Text: Phase, aktuelle Werte mit Zielbereich, offene Risiken, Journal und Dosierungen. Der beste erste Griff bei jeder Frage zu einem konkreten Grow.")]
    public Task<string> LageberichtAsync(
        [Description("Die Id des Grows")] int growId,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            var roh = await reader.LesenAsync($"api/agent-export/grows/{growId}", cancellationToken);
            using var json = JsonDocument.Parse(roh);
            return Text(json.RootElement, "markdown") ?? roh;
        });

    [McpServerTool(Name = "messwert_verlauf")]
    [Description("Der zeitliche Verlauf einzelner Messgrössen — das, was ein Momentwert nicht zeigt. Bis 2 Tage kommen Einzelmessungen, darüber Tageswerte mit Min, Mittel und Max.")]
    public Task<string> MesswertVerlaufAsync(
        [Description("Die Id des Grows")] int growId,
        [Description($"Eine oder mehrere Messgrössen, mit Komma getrennt. Möglich: {Messgroessen}")] string messgroessen,
        [Description("Wie viele Tage zurück, 1 bis 365")] int tage = 14,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            // Der Verlauf haengt am Zelt, nicht am Grow. Das Modell soll davon
            // nichts wissen muessen — die Zuordnung steht im Grow selbst.
            var grow = await DetailAsync(growId, cancellationToken);
            if (Zahl(grow, "tentId") is not { } zeltId)
            {
                return $"Der Grow {growId} hängt an keinem Zelt, deshalb gibt es keine Messreihen.";
            }

            var gefragt = Uri.EscapeDataString(messgroessen.Trim());
            return await reader.LesenAsync(
                $"api/tents/{zeltId}/history?metrics={gefragt}&days={tage}", cancellationToken);
        });

    [McpServerTool(Name = "trends")]
    [Description("Was Grow OS selbst an Bewegung erkannt hat: Befunde je Messgrösse und eine Einschätzung, wie stabil der Grow läuft.")]
    public Task<string> TrendsAsync(
        [Description("Die Id des Grows")] int growId,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            var befunde = await reader.LesenAsync($"api/trends/{growId}", cancellationToken);
            var stabilitaet = await reader.LesenAsync($"api/trends/{growId}/stability", cancellationToken);
            return $"{{\"befunde\":{befunde},\"stabilitaet\":{stabilitaet}}}";
        });

    [McpServerTool(Name = "abweichungen")]
    [Description("Wo der Grow von seinen Sollwerten abweicht, und welche Behandlungen Grow OS dazu vorschlägt.")]
    public Task<string> AbweichungenAsync(
        [Description("Die Id des Grows")] int growId,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            var abweichungen = await reader.LesenAsync($"api/grows/{growId}/deviations", cancellationToken);
            var vorschlaege = await reader.LesenAsync(
                $"api/grows/{growId}/treatment-recommendations", cancellationToken);
            return $"{{\"abweichungen\":{abweichungen},\"vorschlaege\":{vorschlaege}}}";
        });

    [McpServerTool(Name = "anlage")]
    [Description("Die Technik hinter dem Grow: Volumen, Pumpen, Kühler, UV-Klärer, Topfzahl. Wichtig für alles, was mit Mengen zu tun hat.")]
    public Task<string> AnlageAsync(
        [Description("Die Id des Grows")] int growId,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            var grow = await DetailAsync(growId, cancellationToken);
            return Zahl(grow, "systemId") is { } systemId
                ? await reader.LesenAsync($"api/hydro-setups/{systemId}", cancellationToken)
                : $"Dem Grow {growId} ist keine Anlage zugeordnet.";
        });

    [McpServerTool(Name = "sorte")]
    [Description("Die hinterlegte Sorte: Blütewochen, Stretch, Düngerbedarf und eigene Notizen aus früheren Läufen.")]
    public Task<string> SorteAsync(
        [Description("Die Id des Grows")] int growId,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            var grow = await DetailAsync(growId, cancellationToken);
            return Zahl(grow, "strainId") is { } sortenId
                ? await reader.LesenAsync($"api/strains/{sortenId}", cancellationToken)
                : $"Dem Grow {growId} ist keine Sorte aus der Sortenliste zugeordnet.";
        });

    [McpServerTool(Name = "journal")]
    [Description("Die Einträge, die der Betreiber selbst geschrieben hat. Was er getan und beobachtet hat, in seinen Worten.")]
    public Task<string> JournalAsync(
        [Description("Die Id des Grows")] int growId,
        CancellationToken cancellationToken = default)
        => SicherAsync(() => reader.LesenAsync($"api/grows/{growId}/journal", cancellationToken));

    [McpServerTool(Name = "wissen_liste")]
    [Description("Das Fachwissen in Grow OS als Übersicht — die Kürzel, mit denen sich dann einzelne Einträge nachschlagen lassen.")]
    public Task<string> WissenListeAsync(
        [Description("Eine von: sops, treatments, symptoms, pathogens, setpoints")] string art,
        CancellationToken cancellationToken = default)
        => SicherAsync(() => Bereich(art) is { } pfad
            ? reader.LesenAsync($"api/knowledge/{pfad}", cancellationToken)
            : Task.FromResult($"„{art}\" gibt es nicht. Möglich sind: sops, treatments, symptoms, pathogens, setpoints."));

    [McpServerTool(Name = "wissen_nachschlagen")]
    [Description("Einen einzelnen Eintrag im Fachwissen vollständig lesen, etwa den Ablauf 'root-rot-treatment'. Kürzel vorher mit wissen_liste oder suchen finden.")]
    public Task<string> WissenNachschlagenAsync(
        [Description("Eine von: sops, treatments")] string art,
        [Description("Das Kürzel des Eintrags, etwa root-rot-treatment")] string kuerzel,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            var pfad = Bereich(art);
            if (pfad is not ("sops" or "treatments"))
            {
                return $"Einzeln nachschlagen geht bei sops und treatments. Für „{art}\" nimm wissen_liste.";
            }

            try
            {
                return await reader.LesenAsync(
                    $"api/knowledge/{pfad}/{Uri.EscapeDataString(kuerzel)}", cancellationToken);
            }
            catch (GrowOsException)
            {
                // „Kennt Grow OS nicht" hilft niemandem weiter. Abläufe und
                // Behandlungen sehen von aussen gleich aus — root-rot-treatment
                // klingt wie eine Behandlung und ist ein Ablauf. Also nachsehen,
                // ob es in der anderen Schublade liegt, und dorthin verweisen.
                var andere = pfad == "sops" ? "treatments" : "sops";
                var liste = await reader.LesenAsync($"api/knowledge/{andere}", cancellationToken);

                return EnthaeltKuerzel(liste, kuerzel)
                    ? $"„{kuerzel}\" steht nicht unter {pfad}, sondern unter {andere}. "
                      + $"Ruf wissen_nachschlagen noch einmal mit art={andere} auf."
                    : $"„{kuerzel}\" gibt es weder unter sops noch unter treatments. "
                      + "Mit wissen_liste die vorhandenen Kürzel ansehen oder mit suchen danach fahnden.";
            }
        });

    [McpServerTool(Name = "suchen")]
    [Description("Volltextsuche über Grows, Journal und Fachwissen. Nützlich, wenn das passende Kürzel oder die Grow-Id noch fehlt.")]
    public Task<string> SuchenAsync(
        [Description("Der Suchbegriff, mindestens zwei Zeichen")] string begriff,
        CancellationToken cancellationToken = default)
        => SicherAsync(() => reader.LesenAsync(
            $"api/search?q={Uri.EscapeDataString(begriff.Trim())}", cancellationToken));

    /// <summary>Den Grow holen, um Zelt, Anlage und Sorte daraus zu lesen.</summary>
    private async Task<JsonElement> DetailAsync(int growId, CancellationToken cancellationToken)
    {
        var roh = await reader.LesenAsync($"api/grows/{growId}", cancellationToken);
        using var json = JsonDocument.Parse(roh);
        return json.RootElement.Clone();
    }

    /// <summary>
    /// Erwartbare Fehler als Satz zurückgeben statt als Absturz.
    /// </summary>
    /// <remarks>
    /// „Grow OS ist nicht erreichbar" ist eine Antwort, keine Panne — das Modell
    /// kann sie weitergeben und der Mensch weiss, was zu tun ist. Alles andere
    /// fliegt weiter und wird zum Werkzeugfehler; Stillschweigen waere schlimmer.
    /// </remarks>
    private static async Task<string> SicherAsync(Func<Task<string>> arbeit)
    {
        try
        {
            return await arbeit();
        }
        catch (GrowOsException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Steht dieses Kürzel in der übergebenen Liste?</summary>
    private static bool EnthaeltKuerzel(string listeAlsJson, string kuerzel)
    {
        using var json = JsonDocument.Parse(listeAlsJson);
        if (json.RootElement.ValueKind != JsonValueKind.Array) return false;

        return json.RootElement.EnumerateArray()
            .Any(eintrag => string.Equals(Text(eintrag, "id"), kuerzel, StringComparison.OrdinalIgnoreCase));
    }

    private static string? Bereich(string art) => art.Trim().ToLowerInvariant() switch
    {
        "sops" or "sop" or "ablauf" or "ablaeufe" => "sops",
        "treatments" or "treatment" or "behandlungen" => "treatments",
        "symptoms" or "symptome" => "symptoms",
        "pathogens" or "erreger" => "pathogens",
        "setpoints" or "sollwerte" => "setpoints",
        _ => null,
    };

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var wert) && wert.ValueKind == JsonValueKind.String
            ? wert.GetString()
            : null;

    private static int? Zahl(JsonElement element, string name)
        => element.TryGetProperty(name, out var wert) && wert.ValueKind == JsonValueKind.Number
            ? wert.GetInt32()
            : null;
}
