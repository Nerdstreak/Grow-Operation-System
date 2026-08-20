using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests;

/// <summary>
/// Der Kühler-Regler — die Sperren zuerst, dann die Regelung.
///
/// <para><b>Warum das ausführlich geprüft wird.</b> Am Ende dieser Rechnung
/// schaltet ein Kompressor. Zu häufiges Takten zerstört ihn; das ist kein
/// falscher Wert auf einer Kachel, sondern ein Gerät weniger. Und die andere
/// Richtung wiegt genauso: ein Kühler, der zu Unrecht aus bleibt, lässt die
/// Wassertemperatur steigen — und mit ihr fällt der gelöste Sauerstoff, was im
/// RDWC der Weg zur Wurzelfäule ist.</para>
/// </summary>
public sealed class KuehlerServiceTests
{
    private static Tent Zelt(bool an = true, string? steckdose = "switch.kuehler") => new()
    {
        Id = 1,
        Name = "Prüfzelt",
        ChillerControlEnabled = an,
        ChillerSwitchEntityId = steckdose,
        ChillerHysteresisC = 0.4,
        ChillerMinRunMinutes = 5,
        ChillerMinPauseMinutes = 5,
        ChillerMaxReadingAgeMinutes = 10,
    };

    private static readonly DateTime Jetzt = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    private static KuehlerLage Lage(
        double? soll = 19.0,
        double? ist = 19.0,
        int alterMinuten = 1,
        bool? laeuft = false,
        DateTime? letzte = null,
        bool tag = true)
        => new(soll, ist, TimeSpan.FromMinutes(alterMinuten), laeuft, letzte, tag);

    /* ---------------- Die Regelung ---------------- */

    [Fact]
    public void Zu_warm_schaltet_ein()
    {
        // Soll 19,0 + Hysterese 0,4 = ab 19,4 laeuft er an.
        var urteil = KuehlerService.Entscheiden(Lage(ist: 19.5, laeuft: false), Zelt(), Jetzt);

        Assert.Equal(KuehlerSchaltung.Ein, urteil.Schaltung);
        Assert.Contains("19,5", urteil.Grund);
    }

    [Fact]
    public void Kalt_genug_schaltet_aus()
    {
        var urteil = KuehlerService.Entscheiden(Lage(ist: 18.5, laeuft: true), Zelt(), Jetzt);

        Assert.Equal(KuehlerSchaltung.Aus, urteil.Schaltung);
    }

    [Fact]
    public void Im_Totband_passiert_nichts()
    {
        // Genau das ist der Sinn des Totbands: zwischen 18,6 und 19,4 wird
        // nichts angefasst. Ohne das klappert der Kompressor um den Sollwert.
        Assert.Equal(KuehlerSchaltung.Nichts,
            KuehlerService.Entscheiden(Lage(ist: 19.2, laeuft: false), Zelt(), Jetzt).Schaltung);
        Assert.Equal(KuehlerSchaltung.Nichts,
            KuehlerService.Entscheiden(Lage(ist: 18.8, laeuft: true), Zelt(), Jetzt).Schaltung);
    }

    /* ---------------- Der Kompressorschutz ---------------- */

    [Fact]
    public void Die_Mindestpause_haelt_den_Kompressor_zurueck()
    {
        // Vor drei Minuten ausgeschaltet, jetzt waere er wieder dran — darf aber
        // nicht. Das ist die WICHTIGERE der beiden Sperren: ein Kaeltekompressor
        // braucht die Druckangleichung, bevor er wieder anlaufen darf.
        var urteil = KuehlerService.Entscheiden(
            Lage(ist: 20.0, laeuft: false, letzte: Jetzt.AddMinutes(-3)), Zelt(), Jetzt);

        Assert.Equal(KuehlerSchaltung.Nichts, urteil.Schaltung);
        Assert.Contains("Mindestpause", urteil.Grund);
        Assert.Contains("2", urteil.Grund);   // zwei Minuten Rest
    }

    [Fact]
    public void Nach_der_Mindestpause_darf_er_wieder()
    {
        var urteil = KuehlerService.Entscheiden(
            Lage(ist: 20.0, laeuft: false, letzte: Jetzt.AddMinutes(-6)), Zelt(), Jetzt);

        Assert.Equal(KuehlerSchaltung.Ein, urteil.Schaltung);
    }

    [Fact]
    public void Die_Mindestlaufzeit_haelt_ihn_an()
    {
        var urteil = KuehlerService.Entscheiden(
            Lage(ist: 18.0, laeuft: true, letzte: Jetzt.AddMinutes(-2)), Zelt(), Jetzt);

        Assert.Equal(KuehlerSchaltung.Nichts, urteil.Schaltung);
        Assert.Contains("Mindestlaufzeit", urteil.Grund);
    }

    /* ---------------- Die Sperren davor ---------------- */

    [Fact]
    public void Ein_alter_Messwert_reicht_nicht()
    {
        // Auf einen halbstuendigen Wert zu regeln ist etwas anderes, als ihn
        // anzuzeigen. Dieselbe Sicherung wie bei der Dosierung.
        var urteil = KuehlerService.Entscheiden(Lage(ist: 22.0, alterMinuten: 30), Zelt(), Jetzt);

        Assert.Equal(KuehlerSchaltung.Nichts, urteil.Schaltung);
        Assert.Contains("30 Minuten alt", urteil.Grund);
    }

    [Fact]
    public void Ohne_Sollwert_bleibt_der_Kuehler_wie_er_ist()
    {
        // <b>Der wichtigste Fall.</b> Ohne Flip gibt es keine Bluetewoche und
        // damit keinen Nachtwert — die Nachtabsenkung schreibt aus demselben
        // Grund bewusst nichts. Daraus „dann eben aus" zu machen, waere bei
        // einem laufenden Kuehler ein steigendes Becken. Autoflower haben NIE
        // einen Flip.
        var urteil = KuehlerService.Entscheiden(Lage(soll: null, laeuft: true), Zelt(), Jetzt);

        Assert.Equal(KuehlerSchaltung.Nichts, urteil.Schaltung);
        Assert.Contains("bleibt, wie er ist", urteil.Grund);
    }

    [Fact]
    public void Unter_der_harten_Untergrenze_wird_nicht_gekuehlt()
    {
        var urteil = KuehlerService.Entscheiden(Lage(soll: 10.0, ist: 20.0), Zelt(), Jetzt);

        Assert.Equal(KuehlerSchaltung.Nichts, urteil.Schaltung);
        Assert.Contains("Untergrenze", urteil.Grund);
    }

    [Fact]
    public void Ohne_bekannten_Zustand_wird_nicht_geschaltet()
    {
        // Ohne den Zustand der Steckdose laesst sich weder Mindestlauf noch
        // Mindestpause beurteilen — und blind zu schalten hiesse, den
        // Kompressor genau dann zu treffen, wenn er gerade angelaufen ist.
        var urteil = KuehlerService.Entscheiden(Lage(ist: 22.0, laeuft: null), Zelt(), Jetzt);

        Assert.Equal(KuehlerSchaltung.Nichts, urteil.Schaltung);
        Assert.Contains("unbekannt", urteil.Grund);
    }

    [Fact]
    public void Ohne_Steckdose_und_ohne_Freigabe_passiert_nichts()
    {
        Assert.Equal(KuehlerSchaltung.Nichts,
            KuehlerService.Entscheiden(Lage(ist: 25.0), Zelt(an: false), Jetzt).Schaltung);
        Assert.Equal(KuehlerSchaltung.Nichts,
            KuehlerService.Entscheiden(Lage(ist: 25.0), Zelt(steckdose: null), Jetzt).Schaltung);
    }

    /* ---------------- Tag und Nacht ---------------- */

    [Fact]
    public void Am_Tag_gilt_der_Tagwert_und_nachts_der_Nachtwert()
    {
        // Es sind ZWEI Sollwerte, nicht einer. Ein Regler, der nur den
        // Nachtwert kennt, kuehlt tagsueber falsch — und zwar den ganzen Tag.
        var plan = new Absenkplan(
            Wochen: [],
            HeuteTagC: 20.0,
            HeuteNachtC: 17.5,
            AktuelleWoche: 3,
            Herkunft: "Prüfung",
            Luecke: null);

        Assert.Equal(20.0, KuehlerService.SollJetzt(plan, lichtAn: true));
        Assert.Equal(17.5, KuehlerService.SollJetzt(plan, lichtAn: false));
    }

    [Fact]
    public void Der_Grund_nennt_Tag_oder_Nacht()
    {
        // Wer auf die Kachel sieht, muss erkennen, GEGEN WELCHEN Wert gerade
        // geregelt wird — sonst sieht ein Nachtwert bei Tag wie ein Fehler aus.
        var tags = KuehlerService.Entscheiden(Lage(ist: 20.0, tag: true), Zelt(), Jetzt);
        var nachts = KuehlerService.Entscheiden(Lage(ist: 20.0, tag: false), Zelt(), Jetzt);

        Assert.Contains("Tagwert", tags.Grund);
        Assert.Contains("Nachtwert", nachts.Grund);
    }

    /* ---------------- Dass die Pruefung beisst ---------------- */

    [Fact]
    public void Ohne_Hysterese_wuerde_er_klappern()
    {
        // <b>Der Beweis, dass das Totband traegt.</b> Bei Hysterese 0 liegt
        // jeder Wert oberhalb des Solls im Ein- und jeder darunter im
        // Ausschaltbereich: der Regler wechselt bei jedem Zehntelgrad. Genau
        // deshalb steht der Standard NICHT auf 0.
        var ohne = Zelt();
        ohne.ChillerHysteresisC = 0.0001;

        Assert.Equal(KuehlerSchaltung.Ein,
            KuehlerService.Entscheiden(Lage(ist: 19.05, laeuft: false), ohne, Jetzt).Schaltung);
        Assert.Equal(KuehlerSchaltung.Aus,
            KuehlerService.Entscheiden(Lage(ist: 18.95, laeuft: true), ohne, Jetzt).Schaltung);

        // Mit dem Standard-Totband bleibt bei denselben Werten beides ruhig.
        Assert.Equal(KuehlerSchaltung.Nichts,
            KuehlerService.Entscheiden(Lage(ist: 19.05, laeuft: false), Zelt(), Jetzt).Schaltung);
        Assert.Equal(KuehlerSchaltung.Nichts,
            KuehlerService.Entscheiden(Lage(ist: 18.95, laeuft: true), Zelt(), Jetzt).Schaltung);
    }
}
