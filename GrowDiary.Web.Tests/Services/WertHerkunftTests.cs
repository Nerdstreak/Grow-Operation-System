using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Jeder Wert trägt seine Herkunft — live, von Hand, oder gerechnet.
/// </summary>
/// <remarks>
/// <para>Viele Betreiber haben nur Handmessgeräte. Deren Werte standen zwar schon
/// auf den Kacheln (der Fallback existierte), aber ohne Alter: ein pH von vor
/// fünf Tagen sah aus wie einer von eben. Schlimmer noch: die Verbund-Messung
/// verschmilzt Werte MEHRERER Messungen und trug nur den jüngsten Zeitstempel —
/// genau die Lüge, die diese Tests verhindern.</para>
///
/// <para>Der Belüftungs-Check rechnet, statt zu messen, und wird genau so
/// beschriftet. Die Bänder sind eine Faustregel; die Tests rechnen mit den
/// echten Geräten des Betreibers nach, damit die Stufen zur Beratung passen,
/// die er von Hand bekommen hat.</para>
/// </remarks>
public sealed class WertHerkunftTests
{
    private static readonly GrowDashboardComposer Composer = new(null!, null!, null!, null!, null!);

    private static Tent Zelt() => new() { Id = 1, Name = "Zelt", ActiveGrows = new() };

    [Fact]
    public void AHandMeasurementCarriesItsOwnAgePerMetric()
    {
        // Zwei Messungen: pH vor fuenf Tagen, Temperatur von heute. Die Kachel
        // muss fuer pH das ECHTE Alter zeigen — nicht das der juengsten Messung.
        var messungen = new List<Measurement>
        {
            new() { Id = 2, TakenAt = DateTime.Now.AddHours(-1), AirTemperatureC = 24.0 },
            new() { Id = 1, TakenAt = DateTime.Now.AddDays(-5), ReservoirPh = 6.1 },
        };

        var karten = Composer.BuildTentMetrics(Zelt(), new Dictionary<string, HomeAssistantState>(), messungen);

        var ph = Assert.Single(karten, karte => karte.Key == "reservoir-ph");
        Assert.Equal("hand", ph.ValueSource);
        Assert.NotNull(ph.MeasuredAgeMinutes);
        Assert.InRange(ph.MeasuredAgeMinutes!.Value, 5 * 24 * 60 - 5, 5 * 24 * 60 + 5);

        var temp = Assert.Single(karten, karte => karte.Key == "temperature");
        Assert.InRange(temp.MeasuredAgeMinutes!.Value, 55, 65);
    }

    [Fact]
    public void ALiveValueIsMarkedLiveAndCarriesNoAge()
    {
        var states = new Dictionary<string, HomeAssistantState>
        {
            ["temperature"] = new() { State = "21.7", NumericValue = 21.7 },
        };

        var karten = Composer.BuildTentMetrics(Zelt(), states, new List<Measurement>());

        var temp = Assert.Single(karten, karte => karte.Key == "temperature");
        Assert.Equal("live", temp.ValueSource);
        Assert.Null(temp.MeasuredAgeMinutes);
    }

    [Fact]
    public void WithoutADoMeterTheTileShowsThePhysicalCeiling()
    {
        // Kein DO-Wert, aber Wassertemperatur da: die Kachel nennt die
        // Saettigungsgrenze — als Rechnung gekennzeichnet, nie als Messwert.
        // Die DO-Kachel erscheint ohne Sensor nur bei einem aktiven Hydro-Grow,
        // und der zieht die Sollwert-Aufloesung mit — daher der echte Dienst.
        var states = new Dictionary<string, HomeAssistantState>
        {
            ["reservoir-temp"] = new() { State = "24.0", NumericValue = 24.0 },
        };
        var zelt = new Tent
        {
            Id = 1,
            Name = "Zelt",
            ActiveGrows = [new GrowRun { Id = 1, Name = "Lauf", HydroStyle = HydroStyle.DWC, StartDate = DateTime.Today.AddDays(-10) }],
        };

        var mitZielen = new GrowDashboardComposer(null!, null!, null!, TestKnowledgeBase.TargetValues(), null!);
        var karten = mitZielen.BuildTentMetrics(zelt, states, new List<Measurement>());

        var doKarte = Assert.Single(karten, karte => karte.Key == "dissolved-oxygen");
        Assert.Null(doKarte.NumericValue);
        Assert.Contains("berechnet", doKarte.Hint);
        Assert.Contains("8,4", doKarte.Hint); // USGS: 8,42 mg/L bei 24 °C
    }

    [Theory]
    [InlineData(20, 9.09)]
    [InlineData(24, 8.42)]
    [InlineData(30, 7.56)]
    public void TheSaturationTableMatchesThePublishedValues(double temp, double erwartet)
    {
        Assert.Equal(erwartet, AerationCheck.SaettigungMgL(temp), precision: 2);
    }

    [Fact]
    public void TheAerationBandsReproduceTheAdviceGivenByHand()
    {
        // Dieselben Faelle, die im Gespraech von Hand durchgerechnet wurden —
        // die App muss zum gleichen Schluss kommen wie die Beratung:
        // V-20 (1200 L/h) voll auf einen 19-L-Eimer: Whirlpool → drosseln.
        Assert.Equal("sehr_hoch", AerationCheck.Beurteilen(1200, 19)!.Stufe);
        // V-20 auf den kuenftigen 36-L-Eimer: reichlich, im gruenen Bereich.
        Assert.Equal("gut", AerationCheck.Beurteilen(1200, 36)!.Stufe);
        // V-10 (600 L/h) auf ein 250-L-RDWC: zu wenig.
        Assert.Equal("zu_wenig", AerationCheck.Beurteilen(600, 250)!.Stufe);
        // V-10 auf 200 L liegt genau auf der Kante — ein ehrliches "knapp".
        Assert.Equal("knapp", AerationCheck.Beurteilen(600, 200)!.Stufe);
    }

    /// <summary>
    /// Zwei Faustregeln, die auseinandergehen — und keine wird verworfen.
    /// </summary>
    /// <remarks>
    /// SKX nennt 0,5 L/min je Liter als Optimum und alles darüber als
    /// schädlich; die DWC-Literatur setzt die untere Kante bei etwa 0,1. Die
    /// App nennt beides: knapp über dem Optimum bleibt es grün (eine Faustregel
    /// ist ein Ziel, keine Klippe), deutlich darüber kommt der Satz mit der
    /// Begründung — aber ohne Aufforderung, eine laufende Anlage umzubauen.
    /// </remarks>
    [Fact]
    public void AboveTheOptimumTheAppExplainsInsteadOfScolding()
    {
        // 1200 L/h auf 36 L = 0,56 je Liter: 11 % ueber dem Optimum. Gruen.
        Assert.Equal("gut", AerationCheck.Beurteilen(1200, 36)!.Stufe);

        // 1200 L/h auf 20 L = 1,0 je Liter: das ist der Whirlpool-Fall.
        Assert.Equal("sehr_hoch", AerationCheck.Beurteilen(1200, 20)!.Stufe);

        // 1200 L/h auf 25 L = 0,8 je Liter: deutlich ueber dem Optimum, aber
        // kein Whirlpool — hier steht der erklaerende Satz.
        var reichlich = AerationCheck.Beurteilen(1200, 25)!;
        Assert.Equal("mehr_als_noetig", reichlich.Stufe);
        Assert.Contains("Optimum", reichlich.Satz);
        Assert.Contains("kein Grund umzubauen", reichlich.Satz);
    }

    [Fact]
    public void WithoutPumpOrVolumeThereIsNoVerdictInsteadOfAGuess()
    {
        Assert.Null(AerationCheck.Beurteilen(null, 100));
        Assert.Null(AerationCheck.Beurteilen(1200, null));
        Assert.Null(AerationCheck.Beurteilen(0, 100));
    }
}
