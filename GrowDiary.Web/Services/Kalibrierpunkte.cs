using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrowDiary.Web.Services;

/// <summary>Ein einzelner Abgleich gegen eine Pufferlösung.</summary>
/// <param name="Loesung">Wogegen abgeglichen wurde, etwa „pH 7,00".</param>
/// <param name="Sollwert">Der Wert der Lösung — bei pH 7,00 also 7,00.</param>
/// <param name="Vorher">Was die Sonde <b>vor</b> dem Abgleich anzeigte.</param>
/// <param name="Nachher">Was sie danach anzeigt.</param>
public sealed record Kalibrierpunkt(
    string? Loesung,
    double? Sollwert,
    double? Vorher,
    double? Nachher);

/// <summary>
/// Die Punkte einer Kalibrierung — und was sich daraus ablesen lässt.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (01.09.2026).</b> Der Nutzer: „beim ph messer gibt es
/// mehr messpunkte also beispiel 4 und 7 oder auch andere." Das Modell trug
/// <b>einen</b> Punkt — ein Zweipunkt-Abgleich passte nicht hinein, und wer
/// beide festhalten wollte, musste zwei Ereignisse anlegen, die nichts
/// voneinander wissen.</para>
///
/// <para><b>Warum das mehr ist als ein zweites Feld.</b> Erst aus zwei Punkten
/// ergibt sich die <b>Steilheit</b> — die Zahl, die sagt, ob die Sonde noch
/// taugt. Ein einzelner Abgleich gegen pH 7,00 verrät darüber gar nichts: eine
/// tote Sonde lässt sich auf 7,00 genauso einstellen wie eine frische.</para>
///
/// <para>Gespeichert wird wie bei den Erntegewichten je Pflanze: die Punkte als
/// JSON <b>neben</b> den Einzelfeldern, die die Zusammenfassung tragen. Ältere
/// Kalibrierungen ohne Punkte bleiben damit lesbar.</para>
/// </remarks>
public static class Kalibrierpunkte
{
    private static readonly JsonSerializerOptions Optionen = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Liest die Punkte — eine unlesbare Zeile ergibt eine leere Liste.</summary>
    /// <remarks>
    /// Bewusst still: eine kaputte JSON-Zeile darf die Geräteseite nicht
    /// unbenutzbar machen. Die Einzelfelder daneben tragen die Zusammenfassung.
    /// </remarks>
    public static IReadOnlyList<Kalibrierpunkt> Lesen(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<Kalibrierpunkt>>(json, Optionen) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Schreibt die Punkte — leer ergibt <c>null</c>, nicht „[]".</summary>
    public static string? Schreiben(IEnumerable<Kalibrierpunkt>? punkte)
    {
        var liste = punkte?
            .Where(p => p.Sollwert is not null || p.Vorher is not null || p.Nachher is not null)
            .ToList();

        return liste is { Count: > 0 } ? JsonSerializer.Serialize(liste, Optionen) : null;
    }

    /// <summary>
    /// Die Steilheit in Prozent — oder <c>null</c>, solange zwei taugliche
    /// Punkte fehlen.
    /// </summary>
    /// <remarks>
    /// <para><b>Was gerechnet wird.</b> Die Spanne, die die Sonde <i>vor</i> dem
    /// Abgleich zwischen zwei Pufferlösungen gezeigt hat, geteilt durch die
    /// Spanne, die zwischen ihnen liegt:</para>
    ///
    /// <code>
    /// (vorher₇ − vorher₄) / (7,00 − 4,01)
    /// </code>
    ///
    /// <para>Zeigt die Sonde bei Puffer 7 noch 6,82 und bei Puffer 4 schon
    /// 4,15, sind das 2,67 von 2,99 — also <b>89 %</b>.</para>
    ///
    /// <para><b>Vor</b> dem Abgleich, nicht danach: nach der Kalibrierung steht
    /// die Sonde per Definition auf den Sollwerten, und die Steilheit wäre
    /// immer 100 %. Was sie taugt, zeigt sich an dem, was sie ungetrimmt
    /// anzeigt.</para>
    ///
    /// <para><b>Faustregel, keine Messgröße dieser App:</b> 95–105 % gelten als
    /// gut, unter 85 % ist die Sonde fällig. Die Zahlen stehen so in den
    /// Handbüchern gängiger Sonden (Bluelab, Hanna, Milwaukee); Grow OS gibt
    /// sie weiter und erfindet keine eigene Schwelle.</para>
    /// </remarks>
    public static double? SteilheitProzent(IReadOnlyList<Kalibrierpunkt> punkte)
    {
        var taugliche = punkte
            .Where(p => p.Sollwert is not null && p.Vorher is not null)
            .OrderBy(p => p.Sollwert!.Value)
            .ToList();

        if (taugliche.Count < 2) return null;

        var unten = taugliche[0];
        var oben = taugliche[^1];

        var erwartet = oben.Sollwert!.Value - unten.Sollwert!.Value;
        if (Math.Abs(erwartet) < 0.001) return null;

        var gemessen = oben.Vorher!.Value - unten.Vorher!.Value;
        return Math.Round(gemessen / erwartet * 100, 1);
    }

    /// <summary>Untere Grenze der Faustregel: darunter ist die Sonde fällig.</summary>
    /// <remarks>
    /// Aus den Handbüchern gängiger Sonden, nicht aus dieser App. Eine Zahl,
    /// die niemand nachprüfen kann, wäre schlechter als „zu wenig Daten".
    /// </remarks>
    public const double SteilheitFaelligUnter = 85;

    /// <summary>Untergrenze des üblichen Bereichs — darunter lässt die Sonde nach.</summary>
    public const double SteilheitGutAb = 95;

    /// <summary>Obergrenze — darüber stimmt eher der Puffer nicht.</summary>
    public const double SteilheitGutBis = 105;

    /// <summary>
    /// Der Satz zur Steilheit — oder <c>null</c>, wenn es keine gibt.
    /// </summary>
    /// <remarks>
    /// <b>Drei Stufen, nicht zwei.</b> Eine erste Fassung nannte alles über
    /// 85 % „im üblichen Bereich" — auch 89 %, das nun einmal <i>nicht</i> im
    /// Bereich 95–105 liegt. Eine Sonde, die nachlässt, aber noch taugt, ist
    /// genau der Fall, den der Nutzer früh sehen will.
    /// </remarks>
    public static string? SteilheitSatz(double? prozent)
    {
        if (prozent is not { } wert) return null;

        var kopf = $"Steilheit {wert:0.#} % — ";
        var regel = $" (Faustregel aus den Sonden-Handbüchern: {SteilheitGutAb:0}–{SteilheitGutBis:0} % gut, "
                    + $"unter {SteilheitFaelligUnter:0} % fällig.)";

        if (wert < SteilheitFaelligUnter)
            return kopf + "die Sonde ist fällig." + regel;
        if (wert < SteilheitGutAb)
            return kopf + "brauchbar, aber unter dem üblichen Bereich; im Auge behalten." + regel;
        if (wert > SteilheitGutBis)
            return kopf + "ungewöhnlich hoch. Stimmen die Pufferlösungen und ihre Sollwerte?" + regel;

        return kopf + "im üblichen Bereich." + regel;
    }
}
