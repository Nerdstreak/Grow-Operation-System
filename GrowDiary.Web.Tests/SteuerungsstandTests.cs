using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests;

/// <summary>
/// „Läuft Crop Steering gerade?" — und wenn nicht, woran es liegt.
///
/// <para><b>Der Anlass.</b> Rückmeldung des Testers: auf der Crop-Steering-Seite
/// „steht nicht, wann es aktiv ist". Der Plan war da, die Tabelle war da, die
/// Einstellungen waren da — nur die Antwort auf die eine Frage nicht. Sie hängt
/// an einer Kette von vier Bedingungen je Zweig; fehlt eine, passiert nichts,
/// und die Seite sah genauso aus wie im laufenden Betrieb.</para>
/// </summary>
public sealed class SteuerungsstandTests
{
    private static GrowRun Grow(bool rampeAn = true) => new()
    {
        Id = 1,
        Name = "Prüflauf",
        StartDate = DateTime.Today.AddDays(-60),
        FlipDate = DateTime.Today.AddDays(-30),
        NightRampEnabled = rampeAn,
    };

    private static Tent Zelt(
        string? ziel = "climate.chiller",
        bool kuehlerAn = true,
        string? steckdose = "switch.kuehler") => new()
    {
        Id = 1,
        Name = "Prüfzelt",
        WaterTargetEntityId = ziel,
        ChillerControlEnabled = kuehlerAn,
        ChillerSwitchEntityId = steckdose,
    };

    private static Absenkplan Plan(string? luecke = null) => luecke is null
        ? new Absenkplan(
            Wochen: [new AbsenkWoche(1, 20, 18, false), new AbsenkWoche(2, 20, 17, false)],
            HeuteTagC: 20, HeuteNachtC: 17, AktuelleWoche: 2,
            Herkunft: "Prüfung", Luecke: null)
        : new Absenkplan([], null, null, null, "Prüfung", luecke);

    private static Steuerungsstand Bauen(
        GrowRun? grow = null, Tent? zelt = null, Absenkplan? plan = null,
        bool haVerbunden = true, bool testbetrieb = false,
        DateTime? sollwert = null, DateTime? schaltung = null)
        => SteuerungsstandBauer.Bauen(
            grow ?? Grow(), zelt ?? Zelt(), plan ?? Plan(),
            haVerbunden, testbetrieb, sollwert, schaltung);

    /* ---------------- Der gute Fall ---------------- */

    [Fact]
    public void Alles_eingerichtet_heisst_es_laeuft()
    {
        var stand = Bauen();

        Assert.True(stand.RampeSchreibt);
        Assert.True(stand.KuehlerSchaltet);
        Assert.StartsWith("Aktiv.", stand.Kurzfassung);
        Assert.All(stand.Rampe, s => Assert.True(s.Erfuellt));
        Assert.All(stand.Kuehler, s => Assert.True(s.Erfuellt));
    }

    /* ---------------- Jedes Glied der Kette einzeln ---------------- */

    [Fact]
    public void Ohne_Schalter_laeuft_die_Rampe_nicht()
    {
        var stand = Bauen(grow: Grow(rampeAn: false));

        Assert.False(stand.RampeSchreibt);
        Assert.Contains("eingeschaltet", stand.Kurzfassung);
    }

    [Fact]
    public void Ohne_Zielgeraet_wird_nur_geplant()
    {
        var stand = Bauen(zelt: Zelt(ziel: null));

        Assert.False(stand.RampeSchreibt);
        var schritt = stand.Rampe.Single(s => s.Titel.Contains("Zielgerät"));
        Assert.False(schritt.Erfuellt);
        // Der Text sagt, was ZU TUN ist — daran hing die Beschwerde.
        Assert.Contains("Trag", schritt.Text);
    }

    [Fact]
    public void Ohne_Home_Assistant_laeuft_gar_nichts()
    {
        var stand = Bauen(haVerbunden: false);

        Assert.False(stand.RampeSchreibt);
        Assert.False(stand.KuehlerSchaltet);
    }

    [Fact]
    public void Ohne_Zelt_faellt_beides_aus()
    {
        // Ein Grow ohne Zelt: weder Zielgerät noch Steckdose können existieren.
        //
        // NICHT über den Helfer `Bauen`: dessen `zelt ?? Zelt()` macht aus einer
        // übergebenen null wieder ein Zelt — der Fall hätte nie geprüft, was er
        // soll. Genau die Sorte Helfer, die eine Prüfung still entwertet.
        var stand = SteuerungsstandBauer.Bauen(
            Grow(), zelt: null, Plan(), haVerbunden: true, testbetrieb: false,
            letzterSollwertUtc: null, letzteSchaltungUtc: null);

        Assert.False(stand.RampeSchreibt);
        Assert.False(stand.KuehlerSchaltet);
    }

    [Fact]
    public void Der_Grund_des_Plans_wird_uebernommen_und_nicht_nachgebaut()
    {
        // <b>Eine Wahrheit je Satz.</b> Was der Rampe fehlt, weiss der Plan —
        // eine zweite Formulierung hier würde von der ersten abdriften.
        const string luecke = "Noch keine Blüte — die Rampe beginnt mit dem Flip.";
        var stand = Bauen(plan: Plan(luecke));

        Assert.Contains(stand.Rampe, s => s.Text == luecke);
    }

    [Fact]
    public void Im_Testbetrieb_gibt_es_keinen_gruenen_Haken()
    {
        // Ein gruener Haken waere formal richtig (die Einstellungen sind gesetzt)
        // und trotzdem irrefuehrend: bei GROW_OS_DEMO=1 sendet die App nichts.
        var stand = Bauen(testbetrieb: true);

        Assert.False(stand.RampeSchreibt);
        Assert.False(stand.KuehlerSchaltet);
        Assert.Contains(stand.Rampe, v => v.Text.Contains("Testbetrieb"));
    }

    /* ---------------- Die Kurzfassung nennt das ERSTE fehlende Glied ---------------- */

    [Fact]
    public void Die_Kurzfassung_nennt_ein_Glied_und_nicht_alle()
    {
        // Wer eine Kette repariert, fängt vorne an. Alle fehlenden Glieder
        // stehen ohnehin in der Liste darunter.
        var stand = Bauen(grow: Grow(rampeAn: false), zelt: Zelt(ziel: null, kuehlerAn: false));

        Assert.False(stand.RampeSchreibt);
        Assert.Equal("Nicht aktiv. Es fehlt die eingeschaltete Absenkung.", stand.Kurzfassung);
    }

    [Fact]
    public void Ein_Zweig_kann_laufen_waehrend_der_andere_steht()
    {
        var stand = Bauen(zelt: Zelt(kuehlerAn: false));

        Assert.True(stand.RampeSchreibt);
        Assert.False(stand.KuehlerSchaltet);
        Assert.Contains("Der Sollwert wird geschrieben", stand.Kurzfassung);
        Assert.Contains("Kühler wird nicht geregelt", stand.Kurzfassung);
    }

    /* ---------------- Der Beleg, dass es wirklich lief ---------------- */

    [Fact]
    public void Der_letzte_Schreibvorgang_wird_durchgereicht()
    {
        // Alle Haken gesetzt heisst „müsste laufen". Eine Zeile aus dem
        // Protokoll heisst „hat gelaufen" — die Rampe schreibt nur zweimal am
        // Tag, ohne diesen Zeitpunkt bleibt offen, ob je etwas ankam.
        var zeitpunkt = new DateTime(2026, 8, 23, 6, 0, 0, DateTimeKind.Utc);
        var stand = Bauen(sollwert: zeitpunkt);

        Assert.Equal(zeitpunkt, stand.LetzterSollwertUtc);
    }

    /* ---------------- Dass die Pruefung beisst ---------------- */

    [Fact]
    public void Jedes_fehlende_Glied_faellt_einzeln_auf()
    {
        // <b>Der Bissnachweis.</b> Nacheinander wird je EINE Voraussetzung
        // weggenommen; jedes Mal muss der Stand kippen. Bliebe eine davon
        // wirkungslos, prüfte die Kette an dieser Stelle nichts.
        var faelle = new (string Name, Steuerungsstand Stand)[]
        {
            ("Schalter aus", Bauen(grow: Grow(rampeAn: false))),
            ("kein Zielgerät", Bauen(zelt: Zelt(ziel: null))),
            ("kein Plan", Bauen(plan: Plan("Kein Flip."))),
            ("kein Home Assistant", Bauen(haVerbunden: false)),
        };

        foreach (var (name, stand) in faelle)
        {
            Assert.False(stand.RampeSchreibt, $"Fall {name}: die Rampe haette stoppen muessen.");
        }

        // Und der gute Fall bleibt gut — sonst meldet die Kette immer „läuft nicht".
        Assert.True(Bauen().RampeSchreibt);
    }

    /// <summary>Jede Voraussetzung kann in einem deutschen Satz genannt werden.</summary>
    /// <remarks>
    /// <para><b>Der Anlass.</b> Die Kurzfassung hat den Listen-Titel mit
    /// <c>ToLowerInvariant()</c> in den Satz gezwungen, und heraus kam „Es
    /// fehlt: zielgerät zugeordnet." — ein kleingeschriebenes deutsches
    /// Hauptwort. Dieselbe Sorte Fehler wie der englische Dezimalpunkt in
    /// beta.50.</para>
    ///
    /// <para>Die Zaehlung geht ueber ALLE Voraussetzungen beider Ketten, nicht
    /// ueber die eine, die gerade fehlt.</para>
    /// </remarks>
    [Fact]
    public void Jede_Voraussetzung_passt_in_einen_deutschen_Satz()
    {
        var stand = Bauen();

        var alle = stand.Rampe.Concat(stand.Kuehler).ToList();

        // Mengenwaechter: ohne Glieder prueft die Schleife nichts.
        Assert.True(alle.Count >= 6, $"Nur {alle.Count} Voraussetzungen — die Zaehlung sieht ihre Grundmenge nicht.");

        foreach (var glied in alle)
        {
            Assert.False(string.IsNullOrWhiteSpace(glied.Fehlt),
                $"Die Voraussetzung {glied.Titel} hat keinen Satzteil fuer den Fehlt-Satz.");

            // Der Satz lautet „Es fehlt <x>." — da gehoert kein Grossbuchstabe
            // an den Anfang, ausser bei einem Eigennamen. Geprueft wird nur,
            // dass ueberhaupt jemand darueber nachgedacht hat: ein Satzteil,
            // der mit dem Titel identisch ist, ist keiner.
            Assert.NotEqual(glied.Titel, glied.Fehlt);
            Assert.DoesNotContain(glied.Fehlt, new[] { glied.Titel.ToLowerInvariant() });
        }
    }
}
