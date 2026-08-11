using GrowDiary.Web.Services.Knowledge;

namespace GrowDiary.Web.Services;

/// <summary>Ein Posten der Einkaufsliste — und wofür er gebraucht wird.</summary>
/// <param name="Wofuer">Die Abläufe, die ihn verlangen. Mehrere heisst: lohnt sich doppelt.</param>
public sealed record Einkaufsposten(string Material, IReadOnlyList<string> Wofuer);

/// <summary>Eine Gruppe der Liste — Messgeräte, Chemie, Verbrauch, Werkzeug.</summary>
public sealed record Einkaufsgruppe(string Titel, IReadOnlyList<Einkaufsposten> Posten);

/// <summary>
/// Was man da haben muss, damit die Abläufe durchführbar sind.
/// </summary>
/// <remarks>
/// <para>Jeder Ablauf trägt seit jeher eine Materialliste — 65 Posten über elf
/// Abläufe. Gelesen wurden sie nur beim Drucken der Mappe. Wer im Laden steht,
/// hatte davon nichts.</para>
///
/// <para><b>Zusammengefasst statt aufgezählt:</b> „pH-Messgerät" steht in fünf
/// Abläufen; fünfmal dieselbe Zeile ist keine Liste, sondern Lärm. Jeder Posten
/// erscheint einmal und nennt, wofür er gebraucht wird — das ist zugleich die
/// Begründung, warum er auf der Liste steht.</para>
///
/// <para><b>Grob sortiert, nicht fein:</b> die Gruppen entstehen aus dem Text
/// des Postens. Eine saubere Warenkunde wäre eine eigene Datenpflege; für
/// „womit gehe ich einkaufen" reicht Messen · Chemie · Verbrauch · Werkzeug.</para>
/// </remarks>
public sealed class EinkaufslisteService
{
    private readonly KnowledgeBaseLoader _wissen;

    public EinkaufslisteService(KnowledgeBaseLoader wissen)
    {
        _wissen = wissen;
    }

    public IReadOnlyList<Einkaufsgruppe> Bauen() => Bauen(
        _wissen.Sops.Select(sop => (sop.Name, (IEnumerable<string>)(sop.RequiredMaterials ?? []))));

    /// <summary>Die reine Zusammenführung — ohne Wissensbasis, damit sie prüfbar ist.</summary>
    public static IReadOnlyList<Einkaufsgruppe> Bauen(IEnumerable<(string Ablauf, IEnumerable<string> Material)> quellen)
    {
        var zusammen = new Dictionary<string, (string Anzeige, SortedSet<string> Wofuer)>(StringComparer.OrdinalIgnoreCase);

        foreach (var (ablauf, materialien) in quellen)
        {
            foreach (var eintragRoh in materialien)
            foreach (var roh in Zerlegen(eintragRoh))
            {
                var material = roh?.Trim();
                if (string.IsNullOrWhiteSpace(material)) continue;

                var schluessel = Schluessel(material);
                if (!zusammen.TryGetValue(schluessel, out var eintrag))
                {
                    eintrag = (material, new SortedSet<string>(StringComparer.OrdinalIgnoreCase));
                    zusammen[schluessel] = eintrag;
                }
                eintrag.Wofuer.Add(ablauf);
            }
        }

        return zusammen.Values
            .Select(eintrag => (Gruppe: Gruppe(eintrag.Anzeige), Posten: new Einkaufsposten(eintrag.Anzeige, eintrag.Wofuer.ToList())))
            .GroupBy(x => x.Gruppe)
            .OrderBy(gruppe => Reihenfolge(gruppe.Key))
            .Select(gruppe => new Einkaufsgruppe(
                gruppe.Key,
                gruppe.Select(x => x.Posten)
                    // Was in mehreren Abläufen vorkommt, zuerst: das braucht man sicher.
                    .OrderByDescending(posten => posten.Wofuer.Count)
                    .ThenBy(posten => posten.Material, StringComparer.CurrentCulture)
                    .ToList()))
            .ToList();
    }

    /// <summary>
    /// Zerlegt einen Eintrag, der in Wahrheit mehrere Dinge nennt.
    /// </summary>
    /// <remarks>
    /// Manche Abläufe listen „pH-Messgerät, EC-Messgerät, ORP-Messgerät" als
    /// EINEN Posten. Auf einem Einkaufszettel sind das drei Zeilen. Getrennt
    /// wird nur an Kommas AUSSERHALB von Klammern — „Silikat (z. B. Athena
    /// Silica, Rhino Skin)" ist ein Posten mit einem Beispiel darin, kein zwei.
    /// </remarks>
    private static IEnumerable<string> Zerlegen(string? eintrag)
    {
        if (string.IsNullOrWhiteSpace(eintrag)) yield break;

        var tiefe = 0;
        var anfang = 0;
        for (var i = 0; i < eintrag.Length; i++)
        {
            if (eintrag[i] == '(') tiefe++;
            else if (eintrag[i] == ')') tiefe = Math.Max(0, tiefe - 1);
            // Nicht am Dezimalkomma trennen: „Addback-Behälter 18,9 L" ist ein
            // Behälter, kein „Addback-Behälter 18" und ein „9 L".
            else if (eintrag[i] == ',' && tiefe == 0 && !ZwischenZiffern(eintrag, i))
            {
                yield return eintrag[anfang..i];
                anfang = i + 1;
            }
        }
        yield return eintrag[anfang..];
    }

    private static bool ZwischenZiffern(string text, int i)
        => i > 0 && i + 1 < text.Length && char.IsDigit(text[i - 1]) && char.IsDigit(text[i + 1]);

    /// <summary>Füllwörter, die dasselbe Ding anders nennen.</summary>
    /// <remarks>
    /// „Frisches RO-Wasser" und „RO-Wasser" sind dieselbe Zeile auf dem Zettel.
    /// Bewusst eine kurze, sichtbare Liste statt kluger Wortverwandtschaft: was
    /// hier nicht steht, bleibt getrennt — lieber eine Zeile zu viel als zwei
    /// verschiedene Dinge zusammengeworfen.
    /// </remarks>
    private static readonly string[] Fuellwoerter =
        ["frisches ", "frischer ", "sauberer ", "saubere ", "sauberes ", "steriler ", "sterile ", "steriles "];

    /// <summary>
    /// Der Vergleichsschlüssel: Klammerzusätze und Feinheiten fallen weg.
    /// </summary>
    /// <remarks>
    /// „pH-Messgerät (kalibriert)" und „pH-Messgerät" sind dasselbe Gerät. Ohne
    /// diesen Schnitt stünde es zweimal auf der Liste, und der Nutzer kauft
    /// zwei.
    /// </remarks>
    private static string Schluessel(string material)
    {
        var klammer = material.IndexOf('(');
        var kern = (klammer > 0 ? material[..klammer] : material).Trim();
        kern = kern.TrimEnd(',', ';', '.', '–', '-').Trim();

        foreach (var fuellwort in Fuellwoerter)
        {
            if (kern.StartsWith(fuellwort, StringComparison.OrdinalIgnoreCase))
            {
                kern = kern[fuellwort.Length..].Trim();
                break;
            }
        }

        return kern;
    }

    private static string Gruppe(string material)
    {
        var text = material.ToLowerInvariant();

        if (text.Contains("messgerät") || text.Contains("meter") || text.Contains("sonde")
            || text.Contains("mikroskop") || text.Contains("lupe") || text.Contains("thermometer"))
        {
            return "Messen";
        }

        if (text.Contains("hocl") || text.Contains("h2o2") || text.Contains("peroxid")
            || text.Contains("kalibrier") || text.Contains("ph-") || text.Contains("silikat")
            || text.Contains("calmag") || text.Contains("dünger") || text.Contains("nährstoff")
            || text.Contains("ipm") || text.Contains("stimulator")
            || text.Contains("pk 13") || text.Contains("hauptkomponente") || text.Contains("komponente")
            || text.Contains("a + b") || text.Contains("bloom") || text.Contains("grow a"))
        {
            return "Chemie & Dünger";
        }

        if (text.Contains("wasser") || text.Contains("handschuh") || text.Contains("tuch")
            || text.Contains("schwamm") || text.Contains("filter"))
        {
            return "Verbrauch";
        }

        return "Werkzeug & Behälter";
    }

    private static int Reihenfolge(string gruppe) => gruppe switch
    {
        "Messen" => 0,
        "Chemie & Dünger" => 1,
        "Verbrauch" => 2,
        _ => 3,
    };
}
