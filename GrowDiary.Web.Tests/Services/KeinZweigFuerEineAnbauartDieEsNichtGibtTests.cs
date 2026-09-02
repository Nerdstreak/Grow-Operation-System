using System.Reflection;
using System.Text.RegularExpressions;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Kein Zweig verspricht eine Anbauart, die es in dieser App nicht gibt.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> <c>MeasurementSanityService</c>
/// verzweigte auf <c>profile.IsHydro</c> — und <c>GrowthProfile.IsHydro</c> ist
/// <c>=&gt; true</c>, eine Konstante. Der <c>else</c>-Zweig
/// (<c>CheckSubstrate</c>, rund 140 Zeilen) war damit <b>unerreichbar</b>, und
/// darin standen acht pH- und EC-Zahlen für Erde: 4,8 / 5,6 / 6,0 / 6,8 / 7,2
/// / 8,0 sowie 3,0 / 4,0.</para>
///
/// <para><b>Warum das schlimmer ist als toter Code.</b> Diese Zahlen sehen aus
/// wie fachliche Wahrheiten. Wer sie im Quelltext liest, hält sie für die
/// Position der App zu Erde — dabei sagt die App zu Erde <i>nichts</i>. In
/// einem Projekt, dessen Regel „Faustregeln nur mit Etikett, Empfehlungen nur
/// mit Quelle" lautet, ist eine Zahl ohne Wirkung eine Behauptung ohne
/// Deckung.</para>
///
/// <para><b>Was diese Zählung hält.</b> Sie prüft die drei Konstanten, an denen
/// die Einwertigkeit hängt. Kommt Erde eines Tages zurück, wird hier zuerst
/// etwas rot — und derjenige sieht nach, welche Zweige dann wieder gebraucht
/// werden, statt eine Attrappe vorzufinden.</para>
/// </remarks>
public sealed class KeinZweigFuerEineAnbauartDieEsNichtGibtTests
{
    /// <summary>Die App kennt genau eine Bewässerungsart.</summary>
    [Fact]
    public void EsGibtGenauEineBewaesserungsart()
    {
        var werte = Enum.GetValues<IrrigationType>();

        Assert.True(werte.Length == 1,
            $"IrrigationType hat jetzt {werte.Length} Werte ({string.Join(", ", werte)}). "
            + "Damit sind die Zweige, die auf IsHydro pruefen, wieder echte Verzweigungen — "
            + "und der Substrat-Zweig, der am 02.09.2026 als unerreichbar geloescht wurde, "
            + "wird wieder gebraucht. Siehe den Commit dazu.");
    }

    /// <summary>Und genau eine Anbauart.</summary>
    [Theory]
    [InlineData(nameof(GrowthProfile.IsHydro), true)]
    [InlineData(nameof(GrowthProfile.IsSoilOrganic), false)]
    public void DieAnbauartIstFest(string name, bool erwartet)
    {
        var profil = new GrowRun { HydroStyle = HydroStyle.RDWC }.Profile;
        var wert = (bool)typeof(GrowthProfile).GetProperty(name)!.GetValue(profil)!;

        Assert.True(wert == erwartet,
            $"GrowthProfile.{name} ist jetzt {wert}. Solange es fest war, konnte der "
            + "Substrat-Zweig geloescht werden — jetzt muss jemand nachsehen, welche "
            + "Pruefungen fuer die neue Anbauart gelten.");
    }

    /// <summary>
    /// <b>Nirgends</b> hängt eine Entscheidung an der Anbauart.
    /// </summary>
    /// <remarks>
    /// <para>Eine Verzweigung, deren Bedingung immer gleich ausgeht, liest sich
    /// wie eine Wahl und ist keine. Am 02.09.2026 standen sechs davon im
    /// Backend: ein früher Ausstieg in der Diagnose, der nie greifen konnte;
    /// zwei konstante Glieder in der Alarm-Bedingung; und vier im Empfehler,
    /// darunter ein <c>? :</c>, dessen zweiter Zweig
    /// (<c>current.IrrigationEc</c>) nie gelesen wurde.</para>
    ///
    /// <para>Wer so einen Wächter sieht, hält den Code für
    /// anbauart-abhängig — und er ist es nicht. Das ist kein Schönheitsfehler:
    /// es ist eine Zusage, die niemand einlöst.</para>
    /// </remarks>
    [Fact]
    public void NirgendsHaengtEineEntscheidungAnDerAnbauart()
    {
        var wurzel = Path.Combine(ProjektWurzel(), "GrowDiary.Web");
        var treffer = new List<string>();
        var gesehen = 0;

        foreach (var datei in Directory.EnumerateFiles(wurzel, "*.cs", SearchOption.AllDirectories))
        {
            gesehen += 1;
            var zeilen = File.ReadAllLines(datei);
            for (var i = 0; i < zeilen.Length; i += 1)
            {
                // Kommentare zaehlen nicht: eine Erwaehnung ist keine Verwendung.
                var ohneKommentar = zeilen[i].Split("//")[0];
                if (!ENTSCHEIDET_NACH_ANBAUART.IsMatch(ohneKommentar)) continue;

                treffer.Add($"{Path.GetFileName(datei)}:{i + 1}  {zeilen[i].Trim()}");
            }
        }

        // Mengenwaechter: sieht die Zaehlung ihre Grundmenge ueberhaupt?
        Assert.True(gesehen >= 200,
            $"Nur {gesehen} Quelldateien gefunden — die Zaehlung sieht ihre Grundmenge nicht.");

        Assert.True(treffer.Count == 0,
            "Hier haengt eine Entscheidung an der Anbauart:\n  " + string.Join("\n  ", treffer)
            + "\n\nGrowthProfile.IsHydro ist „=> true\" und IrrigationType hat genau einen Wert. "
            + "Diese Bedingung geht immer gleich aus: sie liest sich wie eine Wahl und ist keine, "
            + "und der andere Zweig ist unerreichbar. Kommt eine zweite Anbauart, wird zuerst die "
            + "Pruefung darueber rot — dann gehoert der Waechter zurueck.");
    }

    /// <summary>Der Selbsttest: trifft das Muster die belegten Formen?</summary>
    /// <remarks>
    /// Alle sechs wörtlich aus dem Stand vom 02.09.2026, dazu vier Zeilen, die
    /// in Ruhe bleiben müssen. Eine Zählung mit kaputtem Muster läuft null Mal
    /// durch und ist grün.
    /// </remarks>
    [Theory]
    [InlineData("        if (grow.IrrigationType != IrrigationType.ActiveHydro || !grow.Profile.IsHydro)", true)]
    [InlineData("        if (latest is not null && grow.IrrigationType == IrrigationType.ActiveHydro && grow.Profile.IsHydro)", true)]
    [InlineData("                var measuredEc = grow.Profile.IsHydro ? current.ReservoirEc : current.IrrigationEc;", true)]
    [InlineData("                if (grow.Profile.IsHydro && current.OrpMv is { } athenaOrp)", true)]
    [InlineData("        if (profile.IsHydro)", true)]
    [InlineData("        else if (!profile.IsSoilOrganic)", true)]
    // Und was in Ruhe bleibt: schreiben, abbilden, vorbelegen.
    [InlineData("        grow.IrrigationType = IrrigationType.ActiveHydro;", false)]
    [InlineData("        IrrigationType: grow.IrrigationType,", false)]
    [InlineData("    public IrrigationType IrrigationType { get; set; } = IrrigationType.ActiveHydro;", false)]
    [InlineData("            IrrigationType = source.IrrigationType,", false)]
    public void DasMusterTrifftDieBelegtenWaechter(string zeile, bool erwartet)
    {
        Assert.True(ENTSCHEIDET_NACH_ANBAUART.IsMatch(zeile) == erwartet,
            $"Das Muster sagt zu <{zeile}> das Gegenteil von dem, was es soll.");
    }

    /// <summary>Entscheidet diese Zeile nach der Anbauart?</summary>
    /// <remarks>
    /// Zwei Formen: eine Bedingung (<c>if</c>, <c>&amp;&amp;</c>, <c>||</c>)
    /// oder ein <c>? :</c>. Eine Zuweisung oder eine Abbildung ist keine
    /// Entscheidung und bleibt in Ruhe.
    /// </remarks>
    private static readonly Regex ENTSCHEIDET_NACH_ANBAUART = new(
        @"(if\s*\(|&&|\|\|)[^;]*\b(IsHydro|IsSoilOrganic|IrrigationType\s*[!=]=)"
        + @"|\b(IsHydro|IsSoilOrganic)\b[^;]*\?",
        RegexOptions.Compiled);

    private static string ProjektWurzel()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "GrowDiary.Web"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Projektwurzel nicht gefunden.");
    }
}
