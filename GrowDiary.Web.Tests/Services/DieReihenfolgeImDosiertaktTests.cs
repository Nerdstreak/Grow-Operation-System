using System.Text.RegularExpressions;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Die Reihenfolge, in der der Dosiertakt arbeitet — und wer aussetzt.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> <c>DosingWorker</c> hatte 4,8 % Zeilen-
/// und <b>0 % Zweigabdeckung</b>. Seine <i>Entscheidungen</i> („darf ich
/// überhaupt dosieren?") sind über <c>DosingGuard</c> gut geprüft — 82 Fälle in
/// fünf Dateien. Ungeprüft war die <b>Reihenfolge drumherum</b>, und die stand
/// nur als Kommentar im Takt.</para>
///
/// <para>Ein Kommentar ist keine Zusage. Die beiden Regeln hier sind jetzt
/// eigene Funktionen, damit sie sich prüfen lassen, ohne den Takt zu starten —
/// dasselbe Muster wie <c>AcTest.ZeitplanErlaubt</c> und
/// <c>Kalibrierpunkte.SteilheitProzent</c>.</para>
///
/// <para><b>Warum die Reihenfolge zählt.</b> Dünger verschiebt den pH von
/// selbst. Wer erst Säure gibt und danach Dünger, korrigiert etwas, das sich
/// gleich wieder ändert — und gibt beim nächsten Takt noch einmal Säure. In
/// einem RDWC-Becken ohne Puffer ist das der Weg zu einem pH-Sturz.</para>
///
/// <para><b>Und warum nur eine Dosis je Zelt und Takt.</b> Nach einer Dosis ist
/// der Messwert der übrigen Pumpen desselben Zelts veraltet: die Lösung ist
/// noch nicht durchmischt. Wer auf diesen Wert hin ein zweites Mal dosiert,
/// dosiert auf einen Zustand, den es nicht mehr gibt.</para>
/// </remarks>
public sealed class DieReihenfolgeImDosiertaktTests
{
    /// <summary>Dünger vor pH — in jeder Ausgangsreihenfolge.</summary>
    /// <remarks>
    /// Der Wächter kommt aus der Datenbank in beliebiger Reihenfolge; welche
    /// Zeile zuerst gelesen wird, ist nicht zugesichert. Auf Windows und Linux
    /// ist dieselbe Frage in diesem Projekt schon einmal auseinandergelaufen.
    /// </remarks>
    [Fact]
    public void DuengerKommtVorPh()
    {
        var pumpen = new[]
        {
            Pumpe(1, "Säure", DosingPurpose.PhDown),
            Pumpe(2, "Grow A", DosingPurpose.Nutrient),
            Pumpe(3, "Lauge", DosingPurpose.PhUp),
            Pumpe(4, "CalMag", DosingPurpose.CalMag),
        };

        var reihe = Dosierreihenfolge.Reihenfolge(pumpen);

        // Mengenwaechter: es darf keine Pumpe verschwinden.
        Assert.True(reihe.Count == pumpen.Length,
            $"Aus {pumpen.Length} Pumpen wurden {reihe.Count} — die Reihenfolge verschluckt welche.");

        var ersterPh = reihe.ToList().FindIndex(p => p.Purpose is DosingPurpose.PhDown or DosingPurpose.PhUp);
        var letzterDuenger = reihe.ToList().FindLastIndex(p => p.Purpose is DosingPurpose.Nutrient or DosingPurpose.CalMag);

        Assert.True(letzterDuenger < ersterPh,
            "Eine pH-Pumpe kommt vor einer Duengerpumpe dran: "
            + string.Join(" → ", reihe.Select(p => $"{p.Name} ({p.Purpose})"))
            + ". Duenger verschiebt den pH von selbst — eine Saeuredosis davor korrigiert etwas, "
            + "das sich gleich wieder aendert, und der naechste Takt gibt noch einmal Saeure.");
    }

    /// <summary>Innerhalb einer Gruppe bleibt die Reihenfolge, wie sie kam.</summary>
    /// <remarks>
    /// Sonst wechselt bei jedem Takt, welche von zwei Düngerpumpen zuerst
    /// drankommt — und weil nur <b>eine</b> Dosis je Zelt und Takt fällt, wäre
    /// es Zufall, welche der beiden je zum Zug kommt.
    /// </remarks>
    [Fact]
    public void InnerhalbEinerGruppeBleibtDieReihenfolge()
    {
        var pumpen = new[]
        {
            Pumpe(7, "Grow A", DosingPurpose.Nutrient),
            Pumpe(8, "Grow B", DosingPurpose.Nutrient),
            Pumpe(9, "CalMag", DosingPurpose.CalMag),
        };

        var namen = Dosierreihenfolge.Reihenfolge(pumpen).Select(p => p.Name).ToList();

        Assert.True(namen.SequenceEqual(new[] { "Grow A", "Grow B", "CalMag" }),
            "Die Duengerpumpen wurden untereinander umsortiert: " + string.Join(" → ", namen)
            + ". Weil je Zelt und Takt nur EINE Dosis faellt, entscheidet die Reihenfolge, "
            + "welche Pumpe ueberhaupt je zum Zug kommt.");
    }

    /// <summary>Custom zählt nicht als pH — und wird nicht ans Ende geschoben.</summary>
    /// <remarks>
    /// <c>Custom</c> wird ohnehin nur von Hand ausgelöst. Würde er wie pH
    /// behandelt, änderte das nichts am Ergebnis — aber die Regel hiesse dann
    /// etwas anderes, als sie sagt, und der nächste Leser zöge den falschen
    /// Schluss.
    /// </remarks>
    [Fact]
    public void NurPhZaehltAlsPh()
    {
        var reihe = Dosierreihenfolge.Reihenfolge(new[]
        {
            Pumpe(1, "Säure", DosingPurpose.PhDown),
            Pumpe(2, "Sonstiges", DosingPurpose.Custom),
        });

        Assert.True(reihe[0].Name == "Sonstiges",
            "„Sonstiges\" (Custom) wurde wie eine pH-Pumpe ans Ende geschoben. Die Regel heisst "
            + "„erst Duenger, dann pH\" — Custom ist weder das eine noch das andere.");
    }

    /// <summary>Eine leere Liste bleibt leer.</summary>
    [Fact]
    public void OhnePumpenGibtEsKeineReihenfolge()
    {
        Assert.Empty(Dosierreihenfolge.Reihenfolge([]));
    }

    // ---------------------------------------------------------- Eine je Zelt

    /// <summary>Nach einer Dosis setzt der Rest des Zelts aus.</summary>
    [Fact]
    public void NachEinerDosis_SetztDerRestDesZeltsAus()
    {
        var zeltMitDosis = new HashSet<int> { 1 };

        Assert.False(
            Dosierreihenfolge.DarfDosieren(Pumpe(5, "Säure", DosingPurpose.PhDown, zelt: 1), zeltMitDosis),
            "In Zelt 1 wurde in diesem Takt schon dosiert, und trotzdem darf die naechste Pumpe "
            + "desselben Zelts nachlegen. Ihr Messwert ist von VOR der Dosis — die Loesung ist "
            + "noch nicht durchmischt, und sie dosiert auf einen Zustand, den es nicht mehr gibt.");
    }

    /// <summary>Ein anderes Zelt ist davon nicht betroffen.</summary>
    /// <remarks>
    /// Die Gegenrichtung: würde eine Dosis den ganzen Takt anhalten, käme ein
    /// zweites Zelt bei zwei Bechern nie an die Reihe.
    /// </remarks>
    [Fact]
    public void EinAnderesZelt_DarfWeiter()
    {
        Assert.True(
            Dosierreihenfolge.DarfDosieren(Pumpe(6, "Grow A", DosingPurpose.Nutrient, zelt: 2), new HashSet<int> { 1 }),
            "Eine Dosis in Zelt 1 hat auch Zelt 2 angehalten. Bei zwei Bechern kaeme das zweite "
            + "dann nie an die Reihe.");
    }

    /// <summary>Ohne eingeschaltete Automatik dosiert niemand.</summary>
    [Fact]
    public void OhneAutomatik_DosiertNiemand()
    {
        var pumpe = Pumpe(9, "Säure", DosingPurpose.PhDown, zelt: 3);
        pumpe.AutomationEnabled = false;

        Assert.False(Dosierreihenfolge.DarfDosieren(pumpe, new HashSet<int>()),
            "Eine Pumpe mit ausgeschalteter Automatik hat dosiert. Der Nutzer hat sie ausdruecklich "
            + "auf Handbetrieb gestellt.");
    }

    /// <summary>Im ersten Takt darf jede Pumpe mit Automatik.</summary>
    /// <remarks>
    /// Der Mengenwächter für alles darüber: sagte die Funktion immer nein,
    /// bestünden die drei Fälle oben ebenfalls — und es würde nie dosiert.
    /// </remarks>
    [Fact]
    public void ImErstenTakt_DarfJedeMitAutomatik()
    {
        Assert.True(Dosierreihenfolge.DarfDosieren(Pumpe(1, "Grow A", DosingPurpose.Nutrient, zelt: 1), new HashSet<int>()),
            "Ohne bisherige Dosis darf keine Pumpe — dann dosiert die Automatik nie.");
    }

    /// <summary>
    /// Und die beiden Regeln <b>zusammen</b>: in einem Zelt gewinnt der Dünger.
    /// </summary>
    /// <remarks>
    /// <para>Das ist der Fall, um den es eigentlich geht. Ein Zelt hat eine
    /// Dünger- und eine Säurepumpe, beide wollen dosieren. Die Reihenfolge
    /// stellt den Dünger nach vorn, die Zelt-Sperre nimmt danach die Säure
    /// heraus — <b>obwohl</b> sie für sich genommen dosieren würde.</para>
    ///
    /// <para>Fiele eine der beiden Regeln aus, gäbe es Säure auf einen
    /// EC-Messwert hin, der gleich nicht mehr gilt.</para>
    /// </remarks>
    [Fact]
    public void ImSelbenZelt_GewinntDerDuenger()
    {
        var pumpen = new[]
        {
            Pumpe(1, "Säure", DosingPurpose.PhDown, zelt: 1),
            Pumpe(2, "Grow A", DosingPurpose.Nutrient, zelt: 1),
            Pumpe(3, "Säure Zelt 2", DosingPurpose.PhDown, zelt: 2),
        };

        // Der Takt, so wie DosingWorker ihn faehrt: in Reihenfolge, und wer
        // dosiert, sperrt sein Zelt fuer den Rest des Taktes.
        var dosiert = new List<string>();
        var zelteMitDosis = new HashSet<int>();
        foreach (var pumpe in Dosierreihenfolge.Reihenfolge(pumpen))
        {
            if (!Dosierreihenfolge.DarfDosieren(pumpe, zelteMitDosis)) continue;

            // In echt entscheidet das der DosingGuard; hier wollen alle.
            dosiert.Add(pumpe.Name);
            zelteMitDosis.Add(pumpe.TentId);
        }

        Assert.True(dosiert.SequenceEqual(new[] { "Grow A", "Säure Zelt 2" }),
            "Dosiert wurde: " + string.Join(", ", dosiert)
            + ". Erwartet war „Grow A\" (Duenger vor Saeure im selben Zelt) und „Saeure Zelt 2\" "
            + "(anderes Zelt, nicht gesperrt). Steht „Saeure\" aus Zelt 1 dabei, ging Saeure auf "
            + "einen EC-Wert hin ins Becken, der gleich nicht mehr gilt.");
    }

    // ------------------------------------------------------ Die zweite Haelfte

    /// <summary>Bei stehender Umwälzung bleibt B liegen.</summary>
    /// <remarks>
    /// Konzentriertes B an einer Stelle im Becken steht sonst direkt an den
    /// Wurzeln. Der nächste Takt versucht es wieder — verloren ist nichts.
    /// </remarks>
    [Fact]
    public void BeiStehenderUmwaelzung_BleibtDieZweiteHaelfteLiegen()
    {
        Assert.False(Dosierreihenfolge.ZweiteHaelfteJetzt(simulationsbetrieb: false, umwaelzungLaeuft: false),
            "Die zweite Haelfte ging in ein Becken, dessen Umwaelzpumpe BESTAETIGT steht. "
            + "Konzentriertes B sammelt sich dann an einer Stelle — direkt an den Wurzeln.");
    }

    /// <summary>Bei laufender Umwälzung geht B raus.</summary>
    [Fact]
    public void BeiLaufenderUmwaelzung_GehtDieZweiteHaelfteRaus()
    {
        Assert.True(Dosierreihenfolge.ZweiteHaelfteJetzt(simulationsbetrieb: false, umwaelzungLaeuft: true),
            "Die Umwaelzung laeuft und B bleibt trotzdem liegen — dann steht A ohne B im Becken.");
    }

    /// <summary>
    /// <b>Unbekannt</b> lässt durch — und das ist Absicht.
    /// </summary>
    /// <remarks>
    /// Die meisten Anlagen haben keinen Umwälz-Sensor. Würde <c>null</c> wie
    /// <c>false</c> behandelt, bliebe B bei jedem solchen Aufbau <b>für immer</b>
    /// liegen: im Becken stünde dauerhaft A ohne B. Das ist die schlechtere
    /// Hälfte des Risikos, und deshalb steht dieser Fall hier ausdrücklich.
    /// </remarks>
    [Fact]
    public void UnbekannteUmwaelzung_LaesstDurch()
    {
        Assert.True(Dosierreihenfolge.ZweiteHaelfteJetzt(simulationsbetrieb: false, umwaelzungLaeuft: null),
            "Ohne Umwaelz-Sensor bleibt B liegen — bei den meisten Anlagen also FUER IMMER. "
            + "Im Becken stuende dann dauerhaft A ohne B.");
    }

    /// <summary>Im Simulationsbetrieb wird gar nichts geprüft.</summary>
    /// <remarks>
    /// Dort schaltet nichts Echtes; eine Umwälzung gibt es nicht zu prüfen.
    /// Auch bei bestätigt stehender Umwälzung läuft die Simulation weiter,
    /// sonst könnte man den Ablauf nie ohne Anlage durchspielen.
    /// </remarks>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void ImSimulationsbetrieb_GehtBImmer(bool? umwaelzung)
    {
        Assert.True(Dosierreihenfolge.ZweiteHaelfteJetzt(simulationsbetrieb: true, umwaelzung),
            $"Im Simulationsbetrieb wurde B bei Umwaelzung={umwaelzung?.ToString() ?? "unbekannt"} "
            + "zurueckgehalten — dann laesst sich der Ablauf ohne Anlage nie durchspielen.");
    }

    // ------------------------------------------------- Niemand schreibt sie ab

    /// <summary>
    /// Die beiden Regeln stehen nur an <b>einer</b> Stelle.
    /// </summary>
    /// <remarks>
    /// <para><b>Warum diese Zählung.</b> Die Prüfungen oben hängen an
    /// <see cref="Dosierreihenfolge"/>. Schreibt jemand die Sortierung oder die
    /// Zelt-Sperre daneben noch einmal hin — im Takt, in einem Endpunkt, in
    /// einem Vorschau-Dienst —, bleiben sie <i>grün</i> und prüfen eine Kopie,
    /// die niemand benutzt.</para>
    ///
    /// <para>Genau so herum ist es in diesem Projekt schon schiefgegangen:
    /// dieselbe Zahl an zwei Stellen läuft auseinander (<c>CLAUDE.md</c>:
    /// EINE WAHRHEIT JE ZAHL).</para>
    /// </remarks>
    [Fact]
    public void DieRegelnStehenNurAnEinerStelle()
    {
        var wurzel = Path.Combine(ProjektWurzel(), "GrowDiary.Web");
        var eigene = Path.Combine(wurzel, "Services", "Dosierreihenfolge.cs");

        var abschriften = new List<string>();
        var gesehen = 0;

        foreach (var datei in Directory.EnumerateFiles(wurzel, "*.cs", SearchOption.AllDirectories))
        {
            gesehen += 1;
            if (string.Equals(datei, eigene, StringComparison.OrdinalIgnoreCase)) continue;

            var zeilen = File.ReadAllLines(datei);
            for (var i = 0; i < zeilen.Length; i += 1)
            {
                // Kommentare zaehlen nicht: eine Erwaehnung ist keine Verwendung.
                var ohneKommentar = zeilen[i].Split("//")[0];
                if (SORTIERT_SELBST.IsMatch(ohneKommentar) || SPERRT_SELBST.IsMatch(ohneKommentar))
                {
                    abschriften.Add($"{Path.GetFileName(datei)}:{i + 1}  {zeilen[i].Trim()}");
                }
            }
        }

        // Mengenwaechter: sieht die Zaehlung ihre Grundmenge ueberhaupt?
        Assert.True(gesehen >= 200,
            $"Nur {gesehen} Quelldateien gefunden — die Zaehlung sieht ihre Grundmenge nicht.");

        Assert.True(abschriften.Count == 0,
            "Hier steht eine der beiden Dosier-Regeln noch einmal:\n  "
            + string.Join("\n  ", abschriften)
            + "\n\nSie gehoert nach Dosierreihenfolge — sonst pruefen die neun Faelle "
            + "darueber eine Kopie, die niemand benutzt, und die beiden Fassungen laufen "
            + "auseinander.");
    }

    /// <summary>
    /// Und der Takt <b>benutzt</b> die drei Regeln auch.
    /// </summary>
    /// <remarks>
    /// <para><b>Die Lücke, die der Prüfer fand (02.09.2026).</b> Die Zählung
    /// darunter fängt <i>Abschreiben</i> — jemand schreibt die Regel daneben
    /// noch einmal hin. Sie fängt aber nicht <i>Weglassen</i>: wer
    /// <c>Dosierreihenfolge.DarfDosieren(…)</c> im Takt ersatzlos streicht,
    /// bleibt grün. Und dann dosieren Dünger und Säure im selben Takt, auf
    /// Messwerte von vor der ersten Dosis.</para>
    ///
    /// <para>Die 21 Fälle darüber prüfen die Regeln — keiner fasst
    /// <c>DosingWorker</c> an. Diese Prüfung schliesst die Lücke: sie verlangt,
    /// dass der Takt alle drei Griffe wirklich benutzt.</para>
    /// </remarks>
    [Theory]
    [InlineData("Dosierreihenfolge.Reihenfolge(")]
    [InlineData("Dosierreihenfolge.DarfDosieren(")]
    [InlineData("Dosierreihenfolge.ZweiteHaelfteJetzt(")]
    public void DerTaktBenutztDieRegel(string griff)
    {
        var quelle = File.ReadAllText(Path.Combine(
            ProjektWurzel(), "GrowDiary.Web", "Services", "DosingWorker.cs"));

        // Kommentare zaehlen nicht: eine Erwaehnung ist keine Verwendung.
        var ohneKommentare = Regex.Replace(
            Regex.Replace(quelle, @"/\*.*?\*/", " ", RegexOptions.Singleline), @"//[^\n]*", string.Empty);

        // Mengenwaechter: wird die Datei ueberhaupt gelesen?
        Assert.True(ohneKommentare.Length > 3000,
            $"Nur {ohneKommentare.Length} Zeichen von DosingWorker gelesen — die Pruefung sieht "
            + "die Datei nicht und waere auch dann gruen, wenn der Griff fehlt.");

        Assert.Contains(griff, ohneKommentare, StringComparison.Ordinal);
    }

    /// <summary>Der Selbsttest: treffen die Muster die abgeschriebene Form?</summary>
    /// <remarks>
    /// Eine Zählung mit kaputtem Muster läuft null Mal durch und ist grün.
    /// Am 02.09.2026 ist genau das passiert, weil eine Wortgrenze beim
    /// Schreiben zu einem Steuerzeichen wurde.
    /// </remarks>
    [Theory]
    [InlineData(".OrderBy(pump => pump.Purpose is DosingPurpose.PhDown ? 1 : 0)", true)]
    [InlineData("if (pump.AutomationEnabled && !dosedTents.Contains(pump.TentId))", true)]
    [InlineData("var pumps = Dosierreihenfolge.Reihenfolge(dosing.GetPumps());", false)]
    [InlineData("if (Dosierreihenfolge.DarfDosieren(pump, dosedTents))", false)]
    [InlineData("pumpen.OrderBy(p => p.Name).ToList();", false)]
    public void DieMusterTreffenDieAbgeschriebeneForm(string zeile, bool erwartet)
    {
        var trifft = SORTIERT_SELBST.IsMatch(zeile) || SPERRT_SELBST.IsMatch(zeile);
        Assert.True(trifft == erwartet,
            $"Das Muster sagt zu <{zeile}> das Gegenteil von dem, was es soll. Eine Zaehlung "
            + "mit kaputtem Muster laeuft null Mal durch und ist gruen.");
    }

    /// <summary>Sortiert hier jemand selbst nach dem Pumpenzweck?</summary>
    private static readonly Regex SORTIERT_SELBST = new(
        @"OrderBy\s*\([^)]*Purpose\s+is\s+DosingPurpose");

    /// <summary>Sperrt hier jemand selbst ein Zelt für den Rest des Taktes?</summary>
    private static readonly Regex SPERRT_SELBST = new(
        @"AutomationEnabled\s*&&\s*!\w+\.Contains\(\w+\.TentId\)");

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

    // ------------------------------------------------------------------ Hilfe

    private static DosingPump Pumpe(int id, string name, DosingPurpose zweck, int zelt = 1)
        => new()
        {
            Id = id,
            Name = name,
            Purpose = zweck,
            TentId = zelt,
            AutomationEnabled = true,
        };
}
