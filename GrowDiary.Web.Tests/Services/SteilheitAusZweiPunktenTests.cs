using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Aus zwei Kalibrierpunkten wird die Steilheit — die Zahl, die zählt.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (01.09.2026).</b> Der Nutzer: „beim ph messer gibt es
/// mehr messpunkte also beispiel 4 und 7 oder auch andere." Das Modell trug
/// <b>einen</b> Punkt.</para>
///
/// <para><b>Warum das mehr ist als ein zweites Feld.</b> Ein einzelner Abgleich
/// gegen pH 7,00 sagt über die Sonde <i>nichts</i>: eine tote Sonde lässt sich
/// auf 7,00 genauso einstellen wie eine frische. Erst der Abstand zwischen zwei
/// Punkten verrät, ob sie noch spreizt.</para>
///
/// <para><b>Gerechnet wird vor dem Abgleich.</b> Danach steht die Sonde per
/// Definition auf den Sollwerten — die Steilheit wäre immer 100 %.</para>
/// </remarks>
public sealed class SteilheitAusZweiPunktenTests
{
    /// <summary>Der gerechnete Fall: 6,82 bei Puffer 7, 4,15 bei Puffer 4.</summary>
    /// <remarks>
    /// (6,82 − 4,15) / (7,00 − 4,01) = 2,67 / 2,99 = 89,3 %.
    /// </remarks>
    [Fact]
    public void ZweiPunkte_ErgebenDieSteilheit()
    {
        var steil = Kalibrierpunkte.SteilheitProzent(
        [
            new Kalibrierpunkt("pH 4,01", 4.01, 4.15, 4.01),
            new Kalibrierpunkt("pH 7,00", 7.00, 6.82, 7.00),
        ]);

        Assert.True(steil is not null, "Aus zwei Punkten kam keine Steilheit heraus.");
        Assert.True(Math.Abs(steil!.Value - 89.3) < 0.15,
            $"Erwartet waren rund 89,3 %, gerechnet wurden {steil:0.#} %. "
            + "Die Rechnung ist (vorher_oben - vorher_unten) / (soll_oben - soll_unten).");
    }

    /// <summary>Eine frische Sonde liegt bei rund hundert Prozent.</summary>
    [Fact]
    public void EineFrischeSonde_LiegtBeiHundert()
    {
        var steil = Kalibrierpunkte.SteilheitProzent(
        [
            new Kalibrierpunkt("pH 4,01", 4.01, 4.00, 4.01),
            new Kalibrierpunkt("pH 7,00", 7.00, 6.99, 7.00),
        ]);

        Assert.True(steil is not null && Math.Abs(steil.Value - 100) < 2,
            $"Eine Sonde, die fast richtig anzeigt, kommt auf {steil:0.#} % statt rund 100 %.");
    }

    /// <summary>Ein einzelner Punkt ergibt keine Steilheit — und behauptet auch keine.</summary>
    /// <remarks>
    /// <b>Wichtiger als es aussieht.</b> Eine erfundene Zahl wäre schlimmer als
    /// gar keine: der Nutzer würde einer Sonde vertrauen, über die niemand
    /// etwas weiss.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void WenigerAlsZweiPunkte_ErgebenNichts(int anzahl)
    {
        var punkte = Enumerable.Range(0, anzahl)
            .Select(i => new Kalibrierpunkt("pH 7,00", 7.00 + i, 6.9 + i, 7.00 + i))
            .ToList();

        Assert.True(Kalibrierpunkte.SteilheitProzent(punkte) is null,
            $"Aus {anzahl} Punkt(en) kam eine Steilheit heraus. Ueber eine Sonde, die nur "
            + "gegen EINE Loesung abgeglichen wurde, laesst sich nichts sagen — eine tote "
            + "stellt sich auf 7,00 genauso ein wie eine frische.");
    }

    /// <summary>Zwei Punkte auf derselben Lösung ergeben nichts.</summary>
    /// <remarks>Ohne Abstand gibt es keine Spanne — und eine Division durch null.</remarks>
    [Fact]
    public void ZweiPunkteAufDerselbenLoesung_ErgebenNichts()
    {
        var steil = Kalibrierpunkte.SteilheitProzent(
        [
            new Kalibrierpunkt("pH 7,00", 7.00, 6.9, 7.00),
            new Kalibrierpunkt("pH 7,00", 7.00, 7.1, 7.00),
        ]);

        Assert.True(steil is null, $"Zweimal dieselbe Loesung ergab {steil:0.#} %.");
    }

    /// <summary>Drei Punkte nehmen die äussersten — die grösste belegte Spanne.</summary>
    [Fact]
    public void DreiPunkte_NehmenDieAeussersten()
    {
        var steil = Kalibrierpunkte.SteilheitProzent(
        [
            new Kalibrierpunkt("pH 7,00", 7.00, 6.90, 7.00),
            new Kalibrierpunkt("pH 4,01", 4.01, 4.10, 4.01),
            new Kalibrierpunkt("pH 10,01", 10.01, 9.70, 10.01),
        ]);

        // (9,70 - 4,10) / (10,01 - 4,01) = 5,60 / 6,00 = 93,3 %
        Assert.True(steil is not null && Math.Abs(steil.Value - 93.3) < 0.15,
            $"Bei drei Punkten kamen {steil:0.#} % heraus, erwartet waren rund 93,3 % "
            + "(die Spanne zwischen 4,01 und 10,01).");
    }

    /// <summary>Der Satz nennt die Faustregel und ihre Herkunft.</summary>
    /// <remarks>
    /// Projektregel: „Faustregeln nur mit Etikett." Eine Zahl ohne Quelle ist
    /// schlechter als „zu wenig Daten".
    /// </remarks>
    [Fact]
    public void DerSatzNenntDieFaustregelUndIhreHerkunft()
    {
        var satz = Kalibrierpunkte.SteilheitSatz(72);

        Assert.True(satz is not null, "Zu 72 % gibt es gar keinen Satz.");
        Assert.Contains("Faustregel", satz!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fällig", satz, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Die Stufen sagen, was sie meinen — 89 % ist nicht „im üblichen Bereich".
    /// </summary>
    /// <remarks>
    /// Eine erste Fassung nannte alles über 85 % „im üblichen Bereich", auch
    /// 89 %. Am laufenden Stand gesehen: unter dem Formular stand
    /// „Steilheit 89,3 % — im üblichen Bereich (Faustregel: 95–105 % gut)" —
    /// zwei Aussagen in einem Satz, die einander widersprechen.
    /// </remarks>
    [Theory]
    [InlineData(72, "fällig")]
    [InlineData(89.3, "unter dem üblichen Bereich")]
    [InlineData(99, "im üblichen Bereich")]
    [InlineData(118, "ungewöhnlich hoch")]
    public void DerSatzStuftEhrlich(double prozent, string erwartet)
    {
        var satz = Kalibrierpunkte.SteilheitSatz(prozent);

        Assert.True(satz is not null, $"Zu {prozent} % gibt es keinen Satz.");
        Assert.True(satz!.Contains(erwartet, StringComparison.OrdinalIgnoreCase),
            $"Bei {prozent} % steht da: „{satz}\" — erwartet war „{erwartet}\".");
    }

    /// <summary>Und der Rundweg durch das JSON verliert nichts.</summary>
    [Fact]
    public void SchreibenUndLesen_VerliertKeinenPunkt()
    {
        var punkte = new List<Kalibrierpunkt>
        {
            new("pH 4,01", 4.01, 4.15, 4.01),
            new("pH 7,00", 7.00, 6.82, 7.00),
        };

        var gelesen = Kalibrierpunkte.Lesen(Kalibrierpunkte.Schreiben(punkte));

        Assert.True(gelesen.Count == 2, $"Nach dem Rundweg sind {gelesen.Count} von 2 Punkten da.");
        Assert.True(gelesen[1].Vorher is not null && Math.Abs(gelesen[1].Vorher!.Value - 6.82) < 0.001,
            $"Der Vorher-Wert kam als {gelesen[1].Vorher} zurueck statt als 6,82.");
        Assert.True(gelesen[0].Loesung == "pH 4,01",
            $"Die Bezeichnung kam als „{gelesen[0].Loesung}\" zurueck.");
    }

    /// <summary>Eine unlesbare Zeile macht die Geräteseite nicht kaputt.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("kein json")]
    [InlineData("{\"nicht\":\"eine liste\"}")]
    public void EineUnlesbareZeile_ErgibtEineLeereListe(string json)
    {
        Assert.True(Kalibrierpunkte.Lesen(json).Count == 0,
            $"„{json}\" ergab Punkte statt einer leeren Liste — oder hat geworfen.");
    }

    /// <summary>Leere Punkte werden gar nicht erst geschrieben.</summary>
    [Fact]
    public void LeerePunkte_ErgebenNull()
    {
        Assert.True(Kalibrierpunkte.Schreiben([]) is null, "Eine leere Liste ergab eine JSON-Zeile.");
        Assert.True(Kalibrierpunkte.Schreiben([new Kalibrierpunkt("pH 7,00", null, null, null)]) is null,
            "Ein Punkt ganz ohne Zahlen ergab eine JSON-Zeile.");
    }
}
