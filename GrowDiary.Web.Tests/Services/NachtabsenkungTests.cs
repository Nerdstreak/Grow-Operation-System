using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Die Nachtabsenkung — Crop Steering über die Wassertemperatur.
/// </summary>
/// <remarks>
/// <para>Diese Rechnung endet in einem Sollwert, der an einen echten Chiller
/// geht. Ein Vorzeichenfehler kühlt ein Reservoir auf Kühlschranktemperatur;
/// eine fehlende Untergrenze tut dasselbe langsam. Deshalb prüfen diese Tests
/// nicht nur, dass die Rampe rechnet, sondern vor allem, wo sie aufhört.</para>
/// </remarks>
public sealed class NachtabsenkungTests
{
    private static readonly DateTime Heute = new(2026, 8, 4);

    /// <summary>Sollwerte wie im ausgelieferten rdwc-default: Blüte 20/18, Finish 18/16.</summary>
    private static HydroTargetValues Ziele(double tag, double nacht) => new(
        PhMin: 5.9, PhMax: 6.0, EcMin: 1.0, EcMax: 1.2, OrpMin: 300, OrpMax: 400,
        WaterTempDayC: tag, WaterTempNightC: nacht, VpdMin: 1.0, VpdMax: 1.2,
        PpfdMin: 800, PpfdMax: 1000, Co2Min: 400, Co2Max: 800);

    private static GrowRun Grow(int flipVorTagen = 0, bool an = true, double? boden = null) => new()
    {
        Id = 1,
        StartDate = Heute.AddDays(-60),
        FlipDate = Heute.AddDays(-flipVorTagen),
        NightRampEnabled = an,
        NightRampFloorC = boden,
    };

    [Fact]
    public void TheRampDropsOneDegreePerFloweringWeekAndThenStops()
    {
        var plan = NachtabsenkungService.Rechnen(
            Grow(flipVorTagen: 0), Ziele(20, 18), Ziele(18, 16), null, Heute);

        // Start beim Bluete-Nachtwert, dann je Woche ein Grad tiefer — bis zum
        // Finish-Nachtwert des Profils. Danach bleibt die Rampe stehen.
        Assert.Equal(18, plan.Wochen[0].NachtC);
        Assert.Equal(17, plan.Wochen[1].NachtC);
        Assert.Equal(16, plan.Wochen[2].NachtC);
        Assert.Equal(16, plan.Wochen[3].NachtC);
        Assert.True(plan.Wochen[2].Erreicht);

        // Der Tagwert wird NICHT angefasst — abgesenkt wird die Nacht.
        Assert.All(plan.Wochen, w => Assert.Equal(20, w.TagC));
    }

    [Fact]
    public void TodaysValueFollowsTheFloweringWeek()
    {
        // Flip vor 15 Tagen = Bluetewoche 3 = der dritte Rampenwert.
        var plan = NachtabsenkungService.Rechnen(
            Grow(flipVorTagen: 15), Ziele(20, 18), Ziele(18, 16), null, Heute);

        Assert.Equal(3, plan.AktuelleWoche);
        Assert.Equal(16, plan.HeuteNachtC);
        Assert.Equal(20, plan.HeuteTagC);
        Assert.Null(plan.Luecke);
    }

    [Fact]
    public void BeforeTheFlipNothingIsSet()
    {
        // Kein Flip = keine Bluetewoche = kein Sollwert. Ein geratener Wert
        // wuerde eine echte Kuehlung verstellen.
        var grow = Grow();
        grow.FlipDate = null;

        var plan = NachtabsenkungService.Rechnen(grow, Ziele(20, 18), Ziele(18, 16), null, Heute);

        Assert.Null(plan.HeuteNachtC);
        Assert.Null(plan.HeuteTagC);
        Assert.Null(plan.AktuelleWoche);
        Assert.Contains("beginnt mit dem Flip", plan.Luecke);
    }

    [Fact]
    public void SwitchedOffMeansNoPlanAtAll()
    {
        var plan = NachtabsenkungService.Rechnen(
            Grow(an: false), Ziele(20, 18), Ziele(18, 16), null, Heute);

        Assert.Empty(plan.Wochen);
        Assert.Null(plan.HeuteNachtC);
        Assert.Contains("nicht eingeschaltet", plan.Luecke);
    }

    [Fact]
    public void AnOwnFloorIsRespectedButNeverBelowTheHardLimit()
    {
        // Eigene Untergrenze 14 °C: die Rampe laeuft weiter als bis zum
        // Finish-Wert, hoert aber bei 14 auf.
        var tiefer = NachtabsenkungService.Rechnen(
            Grow(), Ziele(20, 18), Ziele(18, 16), untergrenzeC: 14, Heute);
        Assert.Equal(14, tiefer.Wochen[^1].NachtC);

        // Und ein Vertipper (4 °C) faellt auf die harte Kante zurueck.
        var vertippt = NachtabsenkungService.Rechnen(
            Grow(), Ziele(20, 18), Ziele(18, 16), untergrenzeC: 4, Heute);
        Assert.Equal(NachtabsenkungService.AbsoluteUntergrenzeC, vertippt.Wochen[^1].NachtC);
        Assert.All(vertippt.Wochen, w => Assert.True(w.NachtC >= NachtabsenkungService.AbsoluteUntergrenzeC));
    }

    [Fact]
    public void AFloorAboveTheStartIsRefusedInsteadOfWarmingTheReservoir()
    {
        // Untergrenze 20 bei Startwert 18: das waere keine Absenkung, sondern
        // eine Erwaermung. Lieber gar nichts als das Falsche.
        var plan = NachtabsenkungService.Rechnen(
            Grow(), Ziele(20, 18), Ziele(18, 16), untergrenzeC: 20, Heute);

        Assert.Empty(plan.Wochen);
        Assert.Contains("so kann nichts absinken", plan.Luecke);
    }

    [Fact]
    public void WeeksBeyondThePlanHoldTheLastValue()
    {
        // Woche 12 einer langen Bluete: die Rampe steht laengst auf dem Boden
        // und bleibt dort, statt weiter zu fallen.
        var plan = NachtabsenkungService.Rechnen(
            Grow(flipVorTagen: 80), Ziele(20, 18), Ziele(18, 16), null, Heute);

        Assert.Equal(12, plan.AktuelleWoche);
        Assert.Equal(16, plan.HeuteNachtC);
    }

    [Fact]
    public void TheSourceIsNamedWithEveryPlan()
    {
        var plan = NachtabsenkungService.Rechnen(
            Grow(flipVorTagen: 7), Ziele(20, 18), Ziele(18, 16), null, Heute);

        Assert.Contains("Cold Morning Routine", plan.Herkunft);
        Assert.Contains("SKX", plan.Herkunft);
        // Und dass die Zahlen aus SEINEM Profil stammen, nicht aus unserer Fantasie.
        Assert.Contains("Sollwert-Profil", plan.Herkunft);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(13, 2)]
    [InlineData(14, 3)]
    public void TheFloweringWeekCountsFromTheFlip(int tageSeitFlip, int erwarteteWoche)
    {
        Assert.Equal(erwarteteWoche, NachtabsenkungService.Bluetewoche(Grow(flipVorTagen: tageSeitFlip), Heute));
    }
}
