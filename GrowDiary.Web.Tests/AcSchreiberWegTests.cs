using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests;

/// <summary>
/// Der Weg durch <see cref="AcSchreiber"/> — mit einer Wolke, die sich benimmt
/// wie die echte.
/// </summary>
/// <remarks>
/// <para><b>Warum das die eigentliche Prüfung ist.</b> Die Klasse existiert für
/// genau einen Fall: Home Assistant meldet „gesendet", der Controller übernimmt
/// aber nichts. Vorher liess sich nur die Vergleichsfunktion prüfen — also die
/// Zutat, nicht das Gericht. Ein grüner Testlauf hätte nichts darüber gesagt,
/// ob überhaupt nachgelesen, wiederholt oder abgebrochen wird.</para>
///
/// <para>Der Funk hier ist keine Kulisse: er hält Zustände, nimmt Werte an oder
/// verwirft sie, und zählt mit, was ihn wann erreicht hat.</para>
/// </remarks>
public sealed class AcSchreiberWegTests
{
    private static readonly HomeAssistantSettings Egal = new();

    /// <summary>Eine Wolke, die man einstellen kann.</summary>
    private sealed class Wolke : IAcFunk
    {
        private readonly Dictionary<string, string> _stand = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Diese Entitäten nehmen nichts an — die Wolke verwirft still.</summary>
        public HashSet<string> Verwirft { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Diese Entitäten lassen schon das Senden scheitern.</summary>
        public HashSet<string> Sendefehler { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Ab dem wievielten Versuch eine Entität doch annimmt.</summary>
        public Dictionary<string, int> AbVersuch { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Was gesendet wurde, in der Reihenfolge des Sendens.</summary>
        public List<string> Gesendet { get; } = new();

        public void Setzen(string entityId, string zustand) => _stand[entityId] = zustand;

        public Task<HomeAssistantState?> ZustandAsync(
            HomeAssistantSettings einstellungen, string entityId, CancellationToken ct)
            => Task.FromResult(_stand.TryGetValue(entityId, out var z)
                ? new HomeAssistantState { EntityId = entityId, State = z }
                : null);

        public Task<bool> SchickenAsync(
            HomeAssistantSettings einstellungen, string domain, string dienst, string entityId,
            IReadOnlyDictionary<string, object> daten, CancellationToken ct)
        {
            Gesendet.Add(entityId);
            if (Sendefehler.Contains(entityId)) return Task.FromResult(false);

            var wievielter = Gesendet.Count(e => string.Equals(e, entityId, StringComparison.OrdinalIgnoreCase));
            var nimmtAn = !Verwirft.Contains(entityId)
                && (!AbVersuch.TryGetValue(entityId, out var ab) || wievielter >= ab);

            if (nimmtAn)
            {
                var wert = daten.TryGetValue("value", out var w) ? w
                    : daten.TryGetValue("time", out var t) ? t
                    : daten.TryGetValue("option", out var o) ? o
                    : "on";
                _stand[entityId] = Convert.ToString(wert, System.Globalization.CultureInfo.InvariantCulture) ?? "";
            }

            return Task.FromResult(true);
        }
    }

    private static AcSchreiber Schreiber(Wolke wolke)
        => new(wolke, NullLogger<AcSchreiber>.Instance);

    /// <summary>
    /// Eine Uhr, die nicht wartet — aber mitschreibt, worauf gewartet wurde.
    /// </summary>
    /// <remarks>
    /// <para><b>Vorher warf der Testdoppel die Wartezeit weg.</b> Damit liess
    /// sich die Zeile <c>if (!erster) await warten(Pause, ct)</c> ersatzlos
    /// streichen — 21 Tests blieben gruen, und das Backend meldete 1381 von
    /// 1381. Die Pause ist der Grund, aus dem es diese Klasse ueberhaupt gibt:
    /// die AC-Infinity-Cloud verwirft parallele Auftraege. Eine Vorsichts-
    /// massnahme, die kein Test kennt, ist keine.</para>
    /// </remarks>
    private sealed class Uhr
    {
        /// <summary>Jede Wartezeit in der Reihenfolge, in der gewartet wurde.</summary>
        public List<TimeSpan> Wartezeiten { get; } = new();

        public Task Warten(TimeSpan dauer, CancellationToken _)
        {
            Wartezeiten.Add(dauer);
            return Task.CompletedTask;
        }

        /// <summary>Wie oft die Pause zwischen zwei Schritten eingelegt wurde.</summary>
        public int Pausen => Wartezeiten.Count(d => d == AcSchreiber.Pause);
    }

    /// <summary>Im Test wird nicht gewartet — sonst dauert ein Lauf Minuten.</summary>
    private static Task Sofort(TimeSpan _, CancellationToken __) => Task.CompletedTask;

    private static AcSchreibschritt Zahl(string entityId, string wert)
        => new(entityId, "number", "set_value",
            new Dictionary<string, object> { ["value"] = wert }, wert);

    private static AcSchreibschritt Zeit(string entityId, string wert)
        => new(entityId, "time", "set_value",
            new Dictionary<string, object> { ["time"] = wert }, wert);

    private static AcSchreibschritt Auswahl(string entityId, string wert)
        => new(entityId, "select", "select_option",
            new Dictionary<string, object> { ["option"] = wert }, wert);

    [Fact]
    public async Task Eine_still_verworfene_Aenderung_gilt_NICHT_als_gestellt()
    {
        var wolke = new Wolke();
        wolke.Setzen("number.licht", "3");
        wolke.Verwirft.Add("number.licht");

        var ergebnis = await Schreiber(wolke).SchreibenAsync(
            Egal, [Zahl("number.licht", "7")], Sofort);

        var schritt = Assert.Single(ergebnis);
        Assert.False(schritt.Bestaetigt);
        Assert.Equal(AcSchreiber.Versuche, schritt.Versuche);
        Assert.Equal("3", schritt.Ist);
        Assert.Contains("verwirft", schritt.Fehler);
    }

    [Fact]
    public async Task Beim_zweiten_Versuch_angekommen_zaehlt_als_gestellt()
    {
        var wolke = new Wolke();
        wolke.Setzen("number.licht", "3");
        wolke.AbVersuch["number.licht"] = 2;

        var ergebnis = await Schreiber(wolke).SchreibenAsync(
            Egal, [Zahl("number.licht", "7")], Sofort);

        var schritt = Assert.Single(ergebnis);
        Assert.True(schritt.Bestaetigt);
        Assert.Equal(2, schritt.Versuche);
    }

    [Fact]
    public async Task Was_schon_stimmt_wird_nicht_geschrieben()
    {
        var wolke = new Wolke();
        wolke.Setzen("number.licht", "7");

        var ergebnis = await Schreiber(wolke).SchreibenAsync(
            Egal, [Zahl("number.licht", "7")], Sofort);

        Assert.True(ergebnis[0].Uebersprungen);
        Assert.True(ergebnis[0].Bestaetigt);
        Assert.Empty(wolke.Gesendet);
    }

    [Fact]
    public async Task Nach_einem_Fehlschlag_bleiben_die_folgenden_Schritte_liegen()
    {
        // Der Grund steht in AcSchreiber: ein Geraet im Zeitplan-Modus mit alten
        // Zeiten schaltet nach dem ALTEN Plan. Halb gestellt ist schlimmer als
        // gar nicht gestellt.
        var wolke = new Wolke();
        wolke.Setzen("time.ein", "06:00");
        wolke.Setzen("time.aus", "00:00");
        wolke.Setzen("select.modus", "Auto");
        wolke.Verwirft.Add("time.ein");

        var ergebnis = await Schreiber(wolke).SchreibenAsync(Egal,
        [
            Zeit("time.ein", "08:00"),
            Zeit("time.aus", "20:00"),
            Auswahl("select.modus", "Schedule"),
        ], Sofort);

        Assert.Single(ergebnis);
        Assert.False(ergebnis[0].Bestaetigt);
        Assert.DoesNotContain("select.modus", wolke.Gesendet);
        Assert.Equal("Auto", (await wolke.ZustandAsync(Egal, "select.modus", default))!.State);
    }

    [Fact]
    public async Task Der_Modus_kommt_erst_nach_den_Zeiten()
    {
        var wolke = new Wolke();
        wolke.Setzen("time.ein", "06:00");
        wolke.Setzen("time.aus", "00:00");
        wolke.Setzen("select.modus", "Auto");

        await Schreiber(wolke).SchreibenAsync(Egal,
        [
            Zeit("time.ein", "08:00"),
            Zeit("time.aus", "20:00"),
            Auswahl("select.modus", "Schedule"),
        ], Sofort);

        Assert.Equal(new[] { "time.ein", "time.aus", "select.modus" }, wolke.Gesendet);
    }

    [Fact]
    public async Task Ein_Sendefehler_wird_nicht_wiederholt()
    {
        // Wenn schon Home Assistant den Aufruf ablehnt, liegt es nicht an der
        // Wolke — dann hilft Wiederholen nicht, sondern verzoegert nur.
        var wolke = new Wolke();
        wolke.Setzen("number.licht", "3");
        wolke.Sendefehler.Add("number.licht");

        var ergebnis = await Schreiber(wolke).SchreibenAsync(
            Egal, [Zahl("number.licht", "7")], Sofort);

        Assert.False(ergebnis[0].Bestaetigt);
        Assert.Equal(1, ergebnis[0].Versuche);
        Assert.Single(wolke.Gesendet);
    }

    [Fact]
    public async Task Zwischen_zwei_Schritten_liegt_eine_Pause()
    {
        // Der Kern der Klasse: die Wolke verwirft parallele Auftraege.
        var wolke = new Wolke();
        wolke.Setzen("time.ein", "06:00");
        wolke.Setzen("time.aus", "00:00");
        wolke.Setzen("select.modus", "Auto");
        var uhr = new Uhr();

        await Schreiber(wolke).SchreibenAsync(Egal,
        [
            Zeit("time.ein", "08:00"),
            Zeit("time.aus", "20:00"),
            Auswahl("select.modus", "Schedule"),
        ], uhr.Warten);

        // Drei Schritte, also zwei Pausen dazwischen — vor dem ersten keine.
        Assert.Equal(2, uhr.Pausen);
    }

    [Fact]
    public async Task Vor_dem_ersten_Schritt_wird_nicht_gewartet()
    {
        // Sonst kostete jeder einzelne Klick zwei Sekunden ohne Grund.
        var wolke = new Wolke();
        wolke.Setzen("number.licht", "3");
        var uhr = new Uhr();

        await Schreiber(wolke).SchreibenAsync(Egal, [Zahl("number.licht", "7")], uhr.Warten);

        Assert.Equal(0, uhr.Pausen);
        Assert.NotEmpty(uhr.Wartezeiten);   // nachgefragt wurde trotzdem
    }

    [Fact]
    public async Task Ein_uebersprungener_Schritt_kostet_keine_Pause()
    {
        // Steht der Wert schon, geht nichts raus — dann gibt es auch nichts,
        // wovon die naechste Sendung Abstand halten muesste.
        var wolke = new Wolke();
        wolke.Setzen("time.ein", "08:00");     // steht schon richtig
        wolke.Setzen("time.aus", "00:00");
        var uhr = new Uhr();

        await Schreiber(wolke).SchreibenAsync(Egal,
        [
            Zeit("time.ein", "08:00"),
            Zeit("time.aus", "20:00"),
        ], uhr.Warten);

        Assert.Equal(0, uhr.Pausen);
    }

    [Fact]
    public async Task Eine_Entitaet_die_es_nicht_gibt_gilt_nicht_als_gestellt()
    {
        // Sie meldet null — und null ist nie eine Bestaetigung.
        var wolke = new Wolke();
        wolke.Verwirft.Add("number.gibtsnicht");

        var ergebnis = await Schreiber(wolke).SchreibenAsync(
            Egal, [Zahl("number.gibtsnicht", "7")], Sofort);

        Assert.False(ergebnis[0].Bestaetigt);
        Assert.Null(ergebnis[0].Ist);
    }
}
