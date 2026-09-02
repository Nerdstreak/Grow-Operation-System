using System.Text.RegularExpressions;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Jede Schwere, die eine Karte tragen kann, kommt bei der Ampel an.
/// </summary>
/// <remarks>
/// <para><b>Warum das zählt.</b> Die Zustandsampel ist das <b>Einzige</b>, was
/// von den Empfehlungen beim Nutzer ankommt: <c>/api/live/tents/{id}</c>
/// liefert <c>stateTone</c> und <c>stateLabel</c>, sonst nichts — an der
/// laufenden App nachgesehen. Titel und Text der 55 Karten werden gerechnet
/// und weggeworfen.</para>
///
/// <para><b>Der Anlass (02.09.2026).</b> Die Schwere war eine freie
/// Zeichenkette, an neun Stellen roh hingeschrieben, und die Ampel verglich
/// gegen <c>"danger"</c> und <c>"warning"</c>. Alles andere fiel als „gesund"
/// durch. Ein Tippfehler oder ein neuer Wert hätte aus einem kritischen Befund
/// still ein „stabil" gemacht — grün, weil niemand hinsieht.</para>
///
/// <para>Das ist dasselbe Muster wie <c>RisikoTypenVollstaendigTests</c>: kein
/// Erzeuger schreibt einen Wert, den der Verbraucher nicht kennt.</para>
/// </remarks>
public sealed class JedeKartenschwereErreichtDieAmpelTests
{
    /// <summary>Jede der vier Schweren führt zu einer bestimmten Ampel.</summary>
    /// <remarks>
    /// Ausgeschrieben, damit die Zuordnung nachlesbar ist und nicht aus dem
    /// Code erschlossen werden muss.
    /// </remarks>
    [Theory]
    [InlineData(Kartenschwere.Gefahr, "critical")]
    [InlineData(Kartenschwere.Warnung, "attention")]
    [InlineData(Kartenschwere.Hinweis, "healthy")]
    [InlineData(Kartenschwere.Gut, "healthy")]
    public void JedeSchwereFuehrtZuEinerAmpel(string schwere, string erwartet)
    {
        var ampel = GrowAlertService.ResolveStateTone(
            [new RecommendationCard { Severity = schwere, Title = "x", Message = "y" }],
            homeAssistantConfigured: true);

        Assert.True(ampel == erwartet,
            $"Eine Karte der Schwere „{schwere}\" ergibt die Ampel „{ampel}\", erwartet war "
            + $"„{erwartet}\". Die Ampel ist das Einzige, was von den Empfehlungen beim Nutzer "
            + "ankommt.");
    }

    /// <summary>Die schwerste Karte gewinnt.</summary>
    /// <remarks>
    /// Sonst hinge es an der Reihenfolge, in der die Karten entstehen — und
    /// eine Gefahr könnte hinter drei Hinweisen verschwinden.
    /// </remarks>
    [Fact]
    public void DieSchwersteKarteGewinnt()
    {
        var karten = new[]
        {
            new RecommendationCard { Severity = Kartenschwere.Hinweis },
            new RecommendationCard { Severity = Kartenschwere.Gefahr },
            new RecommendationCard { Severity = Kartenschwere.Warnung },
            new RecommendationCard { Severity = Kartenschwere.Gut },
        };

        Assert.True(GrowAlertService.ResolveStateTone(karten, true) == "critical",
            "Eine Gefahr ist hinter Hinweisen verschwunden. Dann steht die Ampel auf gruen, "
            + "waehrend etwas im Becken schieflaeuft.");
    }

    /// <summary>Ohne Home Assistant steht die Ampel auf „neutral", nicht auf „stabil".</summary>
    /// <remarks>
    /// Der Unterschied ist der ganze Punkt: „stabil" heisst „ich habe
    /// nachgesehen", „neutral" heisst „ich kann nichts sehen". Wer keine
    /// Sensoren angeschlossen hat, darf kein grünes Licht bekommen.
    /// </remarks>
    [Fact]
    public void OhneHomeAssistantIstDieAmpelNeutral()
    {
        Assert.True(GrowAlertService.ResolveStateTone([], homeAssistantConfigured: false) == "neutral",
            "Ohne angeschlossene Sensoren steht die Ampel auf „stabil\" — das behauptet, jemand "
            + "haette nachgesehen.");
    }

    /// <summary>
    /// Die Zählung: kein Erzeuger schreibt eine Schwere von Hand.
    /// </summary>
    /// <remarks>
    /// <para>Die Grundmenge ist der Quelltext des Backends. Gesucht wird die
    /// Form <c>Severity = "…"</c> auf einer <c>RecommendationCard</c> — genau
    /// die neun Stellen, die es am 02.09.2026 gab.</para>
    ///
    /// <para>Solange jeder Erzeuger <see cref="Kartenschwere"/> benutzt, kann
    /// die Ampel keinen Wert verpassen: der Satz ist geschlossen, und die
    /// Theorie oben geht ihn ganz durch.</para>
    /// </remarks>
    [Fact]
    public void KeinErzeugerSchreibtEineSchwereVonHand()
    {
        var wurzel = Path.Combine(ProjektWurzel(), "GrowDiary.Web");
        var treffer = new List<string>();
        var gesehen = 0;

        foreach (var datei in Directory.EnumerateFiles(wurzel, "*.cs", SearchOption.AllDirectories))
        {
            gesehen += 1;

            /* Nur wo Karten gebaut werden. `SystemAuditEvent.Severity` traegt
               denselben Namen und meint etwas anderes — die Schwere eines
               Protokolleintrags, die niemand fuer eine Entscheidung liest.
               Ohne diese Einschraenkung meldete die Zaehlung sie mit, und wer
               dem folgte, zoege zwei Begriffe zusammen, die nichts miteinander
               zu tun haben. */
            var inhalt = File.ReadAllText(datei);
            if (!inhalt.Contains("RecommendationCard", StringComparison.Ordinal)) continue;

            var zeilen = inhalt.Split('\n');
            for (var i = 0; i < zeilen.Length; i += 1)
            {
                // Kommentare zaehlen nicht: eine Erwaehnung ist keine Verwendung.
                var ohneKommentar = zeilen[i].Split("//")[0];
                if (!SCHREIBT_ROH.IsMatch(ohneKommentar)) continue;

                treffer.Add($"{Path.GetFileName(datei)}:{i + 1}  {zeilen[i].Trim()}");
            }
        }

        // Mengenwaechter: sieht die Zaehlung ihre Grundmenge ueberhaupt?
        Assert.True(gesehen >= 200,
            $"Nur {gesehen} Quelldateien gefunden — die Zaehlung sieht ihre Grundmenge nicht.");

        Assert.True(treffer.Count == 0,
            "Hier wird eine Kartenschwere von Hand geschrieben:\n  " + string.Join("\n  ", treffer)
            + "\n\nSie gehoert nach Kartenschwere. Ein Tippfehler oder ein neuer Wert faellt "
            + "in GrowAlertService.ResolveStateTone sonst als „gesund\" durch — die Ampel steht "
            + "auf gruen, waehrend etwas schieflaeuft.");
    }

    /// <summary>Der Selbsttest: trifft das Muster die alte Schreibweise?</summary>
    /// <remarks>
    /// Eine Zählung mit kaputtem Muster läuft null Mal durch und ist grün. Am
    /// 02.09.2026 ist genau das an anderer Stelle passiert.
    /// </remarks>
    [Theory]
    [InlineData("        => new() { Severity = \"info\", Title = title };", true)]
    [InlineData("                Severity = \"success\",", true)]
    [InlineData("            DeviationSeverity.Critical => \"danger\",", false)]
    [InlineData("        => new() { Severity = Kartenschwere.Hinweis };", false)]
    [InlineData("    public string Severity { get; set; } = \"info\";", true)]
    public void DasMusterTrifftDieAlteSchreibweise(string zeile, bool erwartet)
    {
        Assert.True(SCHREIBT_ROH.IsMatch(zeile) == erwartet,
            $"Das Muster sagt zu <{zeile}> das Gegenteil von dem, was es soll.");
    }

    /// <summary>Schreibt hier jemand eine Kartenschwere als Zeichenkette hin?</summary>
    private static readonly Regex SCHREIBT_ROH = new(
        @"(?<!Deviation|RiskEvent|Trend)Severity\s*(\{[^}]*\})?\s*=\s*""",
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
