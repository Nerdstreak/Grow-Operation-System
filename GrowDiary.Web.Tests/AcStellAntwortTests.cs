using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests;

/// <summary>
/// Die drei Ausgänge eines Stellbefehls — und warum es drei sind, nicht zwei.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass, wörtlich vom Tester (25.08.2026):</b> „manchmal kommt
/// das 502 — aber das schalten funktioniert." Beides stimmte. Das Gerät hatte
/// geschaltet; die AC-Infinity-Integration meldete den neuen Wert nur später
/// zurück, als die Nachkontrolle wartete. Die erste Fassung kannte aber nur
/// bestätigt oder Fehler — und log damit in die andere Richtung als vorher:
/// erst meldete sie Erfolg ohne Beleg, jetzt Fehlschlag ohne Fehlschlag.</para>
///
/// <para>Ein echter Fehler ist NUR die abgelehnte Sendung: dann wurde nichts
/// geschaltet. Alles andere ist bestätigt (grün) oder in der Schwebe (gelb).</para>
/// </remarks>
public sealed class AcStellAntwortTests
{
    private static AcSchrittErgebnis Bestaetigt(string entity = "number.licht")
        => new(entity, Uebersprungen: false, Bestaetigt: true, Versuche: 1, Ist: "7", Fehler: null);

    private static AcSchrittErgebnis Uebersprungen(string entity = "number.licht")
        => new(entity, Uebersprungen: true, Bestaetigt: true, Versuche: 0, Ist: "7", Fehler: null);

    private static AcSchrittErgebnis Schwebend(string entity = "number.licht")
        => new(entity, Uebersprungen: false, Bestaetigt: false, Versuche: 3, Ist: "2",
            Fehler: "Der Controller meldet weiterhin 2 statt 7.");

    private static AcSchrittErgebnis Abgelehnt(string entity = "number.licht")
        => new(entity, Uebersprungen: false, Bestaetigt: false, Versuche: 1, Ist: null,
            Fehler: "Home Assistant hat den Aufruf nicht angenommen.", Angenommen: false);

    [Fact]
    public void Alles_bestaetigt_ist_ok_ohne_Meldungen()
    {
        var antwort = AcStellAntwort.Bauen([Bestaetigt(), Uebersprungen("time.ein")]);

        Assert.True(antwort.Ok);
        Assert.Empty(antwort.Meldungen);
    }

    [Fact]
    public void Gesendet_aber_nicht_zurueckgemeldet_ist_NICHT_ok_aber_auch_kein_Fehler()
    {
        var ergebnisse = new[] { Bestaetigt("time.ein"), Schwebend("number.licht") };

        var antwort = AcStellAntwort.Bauen(ergebnisse);

        Assert.False(antwort.Ok);
        var meldung = Assert.Single(antwort.Meldungen);

        // Die Meldung nennt Entitaet und Zahlen — "Fehler 502" hilft niemandem.
        Assert.Contains("number.licht", meldung);
        Assert.Contains("2 statt 7", meldung);

        // Und es ist KEINE abgelehnte Sendung: das Geraet hat vermutlich
        // laengst geschaltet. Genau diese Unterscheidung fehlte.
        Assert.False(AcStellAntwort.SendungAbgelehnt(ergebnisse));
    }

    [Fact]
    public void Nur_die_abgelehnte_Sendung_ist_ein_echter_Fehler()
    {
        Assert.True(AcStellAntwort.SendungAbgelehnt([Abgelehnt()]));
        Assert.True(AcStellAntwort.SendungAbgelehnt([Bestaetigt(), Abgelehnt("time.aus")]));
        Assert.False(AcStellAntwort.SendungAbgelehnt([Schwebend()]));
        Assert.False(AcStellAntwort.SendungAbgelehnt([Bestaetigt()]));
    }

    [Fact]
    public void Die_Protokollzeile_nennt_jeden_Schritt_beim_Namen()
    {
        var zeile = AcStellAntwort.ProtokollZeile("Zelt 1", "LED Top", "Stufe 7",
            [Uebersprungen("time.ein"), Bestaetigt("number.licht"), Schwebend("select.modus")]);

        Assert.Contains("Zelt 1", zeile);
        Assert.Contains("LED Top", zeile);
        Assert.Contains("time.ein stand schon", zeile);
        Assert.Contains("number.licht ok nach 1", zeile);
        Assert.Contains("select.modus NICHT bestaetigt", zeile);
    }

    [Fact]
    public void Der_Schwebezustand_beisst()
    {
        // Waere Bauen immer ok, verschwaende der gelbe Hinweis und die Seite
        // meldete wieder Erfolg ohne Beleg — der Fehler vor beta.55.
        Assert.False(AcStellAntwort.Bauen([Schwebend()]).Ok);
        Assert.NotEmpty(AcStellAntwort.Bauen([Schwebend()]).Meldungen);
    }
}
