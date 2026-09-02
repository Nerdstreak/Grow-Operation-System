using System.Text.RegularExpressions;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Es gibt <b>zwei</b> Zustandsampeln für dasselbe Zelt — und der Nutzer sieht
/// die andere.
/// </summary>
/// <remarks>
/// <para><b>Der Befund (02.09.2026).</b> Nachgesehen an der laufenden App, nicht
/// gerechnet:</para>
///
/// <list type="table">
///   <item><term><c>GET /api/live/tents/1</c></term>
///   <description><c>stateTone: "attention"</c>, <c>stateLabel: "beobachten"</c></description></item>
///   <item><term>Was auf <c>/zelte/1</c> steht</term>
///   <description><b>„Stabil / 100 %"</b></description></item>
/// </list>
///
/// <para><b>Warum.</b> Die angezeigte Ampel rechnet der Browser selbst
/// (<c>live-model.ts</c>) aus den Kacheln: wie viele Werte liegen in ihrem
/// Zielband, mit eigenen Schwellen (55 / 82) und eigenen Wörtern. Die Ampel
/// aus dem Backend (<c>GrowAlertService.ResolveStateTone</c> über
/// <c>RecommendationEngine</c>) wird geliefert und <b>von keiner Seite
/// gelesen</b>.</para>
///
/// <para><b>Was das bedeutet.</b> <c>RecommendationEngine</c> — 852 Zeilen mit
/// über fünfzig Empfehlungstexten — hat heute <b>keine sichtbare Wirkung</b>.
/// Seine Texte werden gerechnet und weggeworfen, und seine Schwere landet in
/// einem Feld, das niemand anzeigt.</para>
///
/// <para><b>Was diese Prüfung tut.</b> Sie entscheidet nicht, welche Ampel
/// gewinnen soll — das ist eine Gestaltungsfrage. Sie hält den Zustand fest,
/// damit er nicht wieder in Vergessenheit gerät: <c>stateTone</c> hat keinen
/// Leser, und wenn jemand einen baut, wird hier zuerst etwas rot. Dann gehört
/// entschieden, welche der beiden Rechnungen gilt — zwei Ampeln für dasselbe
/// Zelt sind eine zu viel (<c>CLAUDE.md</c>: EINE WAHRHEIT JE ZAHL).</para>
/// </remarks>
public sealed class ZweiAmpelnFuerDasselbeZeltTests
{
    /// <summary>Heute liest keine Seite <c>stateTone</c>.</summary>
    /// <remarks>
    /// Wird das eines Tages anders, ist das <b>gut</b> — aber dann muss jemand
    /// entscheiden, was mit der Rechnung im Browser passiert. Diese Prüfung
    /// wird dann rot und verlangt genau das.
    /// </remarks>
    [Fact]
    public void HeuteLiestKeineSeiteDieAmpelAusDemBackend()
    {
        var leser = SucheInDerOberflaeche(@"\b(stateTone|stateLabel)\b");

        // Die Typdeklaration zaehlt nicht: sie beschreibt das Feld, sie liest es
        // nicht. Eine Erwaehnung ist keine Verwendung.
        var echteLeser = leser.Where(z => !z.Datei.EndsWith("automation.ts", StringComparison.Ordinal)).ToList();

        Assert.True(echteLeser.Count == 0,
            "Jemand liest jetzt stateTone/stateLabel:\n  "
            + string.Join("\n  ", echteLeser.Select(z => $"{z.Datei}:{z.Zeile}  {z.Text}"))
            + "\n\nDas ist gut — aber dann gibt es ZWEI Rechnungen fuer dieselbe Ampel: diese "
            + "hier und die im Browser (live-model.ts, Schwellen 55/82). Zwei Ampeln fuer "
            + "dasselbe Zelt sind eine zu viel. Bitte entscheiden, welche gilt, und die andere "
            + "entfernen.");
    }

    /// <summary>Und der Browser rechnet seine eigene.</summary>
    /// <remarks>
    /// Der Gegenbeleg zur Prüfung darüber. Verschwände die Rechnung im Browser,
    /// wäre die Aussage „der Nutzer sieht die andere" nicht mehr wahr — dann
    /// wäre der Zustand ein anderer und dieser Test gehört angesehen.
    /// </remarks>
    [Fact]
    public void DerBrowserRechnetSeineEigeneAmpel()
    {
        var datei = Path.Combine(
            ProjektWurzel(), "GrowDiary.React", "src", "features", "live", "live-model.ts");
        Assert.True(File.Exists(datei), $"live-model.ts liegt nicht unter „{datei}\".");

        var quelle = File.ReadAllText(datei);

        Assert.Contains("'Stabil'", quelle, StringComparison.Ordinal);
        Assert.Contains("'Beobachten'", quelle, StringComparison.Ordinal);
        Assert.True(Regex.IsMatch(quelle, @"value\s*<\s*55"),
            "Die Schwelle 55 steht nicht mehr in live-model.ts. Rechnet der Browser die Ampel "
            + "nicht mehr selbst? Dann stimmt die Aussage dieser Datei nicht mehr.");
    }

    // ------------------------------------------------------------------ Hilfe

    private static List<(string Datei, int Zeile, string Text)> SucheInDerOberflaeche(string muster)
    {
        var wurzel = Path.Combine(ProjektWurzel(), "GrowDiary.React", "src");
        var regex = new Regex(muster, RegexOptions.Compiled);
        var raus = new List<(string, int, string)>();
        var gesehen = 0;

        foreach (var datei in Directory.EnumerateFiles(wurzel, "*.*", SearchOption.AllDirectories))
        {
            if (Path.GetExtension(datei) is not (".ts" or ".tsx")) continue;
            gesehen += 1;

            var zeilen = File.ReadAllLines(datei);
            for (var i = 0; i < zeilen.Length; i += 1)
            {
                // Kommentare zaehlen nicht.
                var ohneKommentar = zeilen[i].Split("//")[0];
                if (regex.IsMatch(ohneKommentar))
                {
                    raus.Add((Path.GetFileName(datei), i + 1, zeilen[i].Trim()));
                }
            }
        }

        // Mengenwaechter: sieht die Suche die Oberflaeche ueberhaupt?
        Assert.True(gesehen >= 100,
            $"Nur {gesehen} Dateien der Oberflaeche gelesen — dann findet diese Pruefung nichts "
            + "und ist auch dann gruen, wenn jemand die Ampel liest.");

        return raus;
    }

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
