using System.Text.Json;

namespace GrowDiary.Web.Tests;

/// <summary>
/// Wissen, das niemand findet, ist keins.
///
/// <para><b>Der Anlass.</b> Der Ablauf „emergency-power-recovery" lag
/// vollständig in der Wissensbasis und war auf keinem Weg erreichbar: der
/// Empfehler, der ihn vorschlagen sollte, verzweigte auf Ereignistypen, die
/// kein Erzeuger je gesetzt hat. Gemessen bekamen 0 von 21 Risiko-Ereignissen
/// eine Empfehlung. Der Text war fertig, geprüft, mit Quellen — und tot.</para>
///
/// <para><b>Warum über das Verzeichnis.</b> Die Grundmenge sind die Dateien,
/// nicht eine Liste im Code. Eine Liste könnte dieselbe Datei vergessen, die
/// niemand verlinkt hat — sie hätte denselben blinden Fleck wie das, was sie
/// prüfen soll.</para>
///
/// <para><b>Was dieser Test NICHT behauptet.</b> Alle elf Abläufe stehen im
/// Katalog auf <c>/sops</c> (<c>GET /api/knowledge/sops</c>) und lassen sich
/// dort von Hand starten — nachgeprüft an der laufenden App. Unerreichbar ist
/// also keiner. Geprüft wird das Schärfere: <b>wird er von selbst
/// vorgeschlagen</b>, wenn die Lage danach ist? Genau das fehlte beim
/// Notfall-Ablauf: er stand im Katalog und wurde in einem Stromausfall trotzdem
/// nie angeboten.</para>
///
/// <para>Für planbare Routinen ist „nur im Katalog" richtig — man schlägt sie
/// auf, man wird nicht auf sie gestossen. Die stehen unten mit Grund.</para>
/// </summary>
public sealed class WissenErreichbarkeitTests
{
    private static string Wissensbasis()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var pfad = Path.Combine(dir, "GrowDiary.Web", "wwwroot", "knowledge-defaults");
            if (Directory.Exists(pfad)) return pfad;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Wissensbasis nicht gefunden.");
    }

    private static string[] Dateien(string mappe)
        => Directory.GetFiles(Path.Combine(Wissensbasis(), mappe), "*.json");

    private static string Kennung(string datei)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(datei));
        return json.RootElement.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : Path.GetFileNameWithoutExtension(datei);
    }

    /// <summary>
    /// Alles, was auf eine Behandlung oder einen Ablauf zeigen kann.
    /// </summary>
    /// <remarks>
    /// Die Symptome (aus denen der Wegweiser und die Diagnose Vorschläge bauen)
    /// und der gesamte Quelltext — ein Ablauf darf auch aus dem Code heraus
    /// gestartet werden, etwa die tägliche Messroutine über den Fälligkeits-
    /// Wächter.
    /// </remarks>
    private static string Verweistext()
    {
        var teile = new List<string>();

        foreach (var mappe in new[] { "symptoms", "pathogens", "sops", "treatments" })
        {
            foreach (var datei in Dateien(mappe)) teile.Add(File.ReadAllText(datei));
        }

        var wurzel = Directory.GetParent(Wissensbasis())!.Parent!.FullName;
        foreach (var datei in Directory.GetFiles(wurzel, "*.cs", SearchOption.AllDirectories))
        {
            teile.Add(File.ReadAllText(datei));
        }

        return string.Join("\n", teile);
    }

    /// <summary>Bewusst ohne Verweis — jeweils mit Grund.</summary>
    /// <remarks>
    /// Die Kennungen stammen aus den Dateien, nicht aus dem Kopf. Ein erster
    /// Anlauf trug hier erfundene Namen ein („addback-routine" statt
    /// „nutrient-addback") — die Ausnahmen griffen dadurch nicht, und der Test
    /// meldete drei Abläufe, die längst mit Grund ausgenommen sein sollten.
    /// </remarks>
    private static readonly Dictionary<string, string> GewollteAusnahmen = new(StringComparer.OrdinalIgnoreCase)
    {
        // Diese drei plant man, man wird nicht auf sie gestossen. Erreichbar
        // sind sie ueber den Katalog auf /sops.
        ["nutrient-addback"] = "Planbare Routine — man mischt nach, wenn man nachmischt; kein Befund führt darauf",
        ["cuttings-quarantine"] = "Vorsorge beim Einbringen neuer Stecklinge, kein Symptom",
        ["harvest-preparation-flush"] = "Gehört zur Erntevorbereitung und wird geplant, nicht ausgelöst",
    };

    [Fact]
    public void Der_Test_sieht_die_Wissensbasis()
    {
        // Sonst prüft er nichts und ist trotzdem grün.
        Assert.True(Dateien("treatments").Length >= 25, $"Nur {Dateien("treatments").Length} Behandlungen gefunden.");
        Assert.True(Dateien("sops").Length >= 8, $"Nur {Dateien("sops").Length} Abläufe gefunden.");
        Assert.True(Verweistext().Length > 100_000, "Der Verweistext ist zu klein — die Suche greift ins Leere.");
    }

    public static IEnumerable<object[]> Ablaeufe()
        => Directory.GetFiles(Path.Combine(WissensbasisStatisch(), "sops"), "*.json").Select(d => new object[] { Path.GetFileName(d) });

    private static string WissensbasisStatisch() => Wissensbasis();

    [Theory]
    [MemberData(nameof(Ablaeufe))]
    public void Jeder_Ablauf_wird_von_etwas_vorgeschlagen_oder_hat_einen_Grund(string dateiname)
    {
        var datei = Path.Combine(Wissensbasis(), "sops", dateiname);
        var kennung = Kennung(datei);
        if (GewollteAusnahmen.ContainsKey(kennung)) return;

        var text = Verweistext();

        // Der eigene Dateiinhalt zählt nicht: dass ein Ablauf seine eigene
        // Kennung trägt, macht ihn nicht auffindbar.
        var eigen = File.ReadAllText(datei);
        var treffer = ZaehleOhne(text, eigen, kennung);

        Assert.True(treffer > 0,
            $"Der Ablauf {kennung} wird von nichts vorgeschlagen: kein Symptom führt darauf, "
            + "und kein Codepfad startet ihn. Im Katalog auf /sops steht er, aber wer die Lage hat, "
            + "für die er gedacht ist, bekommt ihn nicht angeboten — genau der Fehler des "
            + "Notfall-Ablaufs. Entweder an ein Symptom oder einen Auslöser hängen, oder mit Grund "
            + "in GewollteAusnahmen eintragen.");
    }

    private static int ZaehleOhne(string gesamt, string eigen, string kennung)
    {
        var alle = Vorkommen(gesamt, kennung);
        var selbst = Vorkommen(eigen, kennung);
        return alle - selbst;
    }

    private static int Vorkommen(string text, string wort)
    {
        if (string.IsNullOrEmpty(wort)) return 0;
        var n = 0;
        var i = 0;
        while ((i = text.IndexOf(wort, i, StringComparison.OrdinalIgnoreCase)) >= 0) { n++; i += wort.Length; }
        return n;
    }
}
