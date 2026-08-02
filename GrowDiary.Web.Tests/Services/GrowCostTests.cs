using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Was ein Grow gekostet hat — berechnet, mit Herkunft, ohne Scheingenauigkeit.
/// </summary>
/// <remarks>
/// Der Strom ist eine Untergrenze aus Licht-Watt × Stunden × Preis; der Dünger
/// kommt aus dem Dosier-Protokoll. Beides muss seinen Rechenweg nennen — eine
/// nackte Euro-Zahl ohne Herkunft wäre dieselbe Sorte Lüge wie ein erfundener
/// DO-Wert.
/// </remarks>
public sealed class GrowCostTests
{
    private static readonly DateTime Start = new(2026, 6, 1);
    private static readonly DateTime Ende = new(2026, 6, 30);

    private static DoseEvent Dose(int pumpId, double ml) => new()
    {
        PumpId = pumpId,
        DosedMl = ml,
        Outcome = DoseOutcome.Done,
        OccurredAtUtc = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc),
    };

    private static DosingPump Pumpe(int id, string name, double? preis) => new()
    {
        Id = id, Name = name, CostPerLiterEur = preis,
    };

    [Fact]
    public void PowerCostFollowsWattHoursDaysAndPrice()
    {
        // 400 W × 18 h × 30 Tage = 216 kWh; bei 30 ct = 64,80 €.
        var kosten = GrowCostService.Berechnen(
            Start, Ende, flip: null,
            lampenWatt: 400, planStundenProTag: 18, strompreisCent: 30,
            dosen: [], pumpen: [], trockenGramm: null);

        Assert.Equal(64.80, kosten.StromEur!.Value, precision: 2);
        Assert.Contains("berechnet", kosten.StromHerkunft);
        Assert.Contains("Nebenverbraucher nicht enthalten", kosten.StromHerkunft);
    }

    [Fact]
    public void WithoutASchedultTheFlipSplitsInto18And12Hours()
    {
        // 10 Tage bis zum Flip (18 h), 20 Tage danach (12 h):
        // 400 W × (10×18 + 20×12) h = 168 kWh; bei 25 ct = 42,00 €.
        var kosten = GrowCostService.Berechnen(
            Start, Ende, flip: Start.AddDays(10),
            lampenWatt: 400, planStundenProTag: null, strompreisCent: 25,
            dosen: [], pumpen: [], trockenGramm: null);

        Assert.Equal(42.00, kosten.StromEur!.Value, precision: 2);
        Assert.Contains("18/12", kosten.StromHerkunft);
    }

    [Fact]
    public void NutrientCostComesFromTheDoseLogTimesThePrice()
    {
        // 500 ml pH-Minus bei 12 €/L = 6,00 €; die zweite Pumpe ohne Preis
        // fehlt in der Summe UND wird beim Namen genannt.
        var kosten = GrowCostService.Berechnen(
            Start, Ende, flip: null,
            lampenWatt: null, planStundenProTag: null, strompreisCent: null,
            dosen: [Dose(1, 200), Dose(1, 300), Dose(2, 100)],
            pumpen: [Pumpe(1, "pH-Minus", 12), Pumpe(2, "CalMag", null)],
            trockenGramm: null);

        Assert.Equal(6.00, kosten.DuengerEur!.Value, precision: 2);
        Assert.Contains("CalMag", kosten.PumpenOhnePreis);
        Assert.Contains("Handzugaben", kosten.DuengerHerkunft);
        Assert.Null(kosten.StromEur);
    }

    [Fact]
    public void EuroPerGramOnlyExistsWithAHarvest()
    {
        var mitErnte = GrowCostService.Berechnen(
            Start, Ende, null, 400, 18, 30, [], [], trockenGramm: 216);
        // 64,80 € auf 216 g = 0,30 €/g.
        Assert.Equal(0.30, mitErnte.EurProGramm!.Value, precision: 2);

        var ohneErnte = GrowCostService.Berechnen(
            Start, Ende, null, 400, 18, 30, [], [], trockenGramm: null);
        Assert.Null(ohneErnte.EurProGramm);
    }

    [Fact]
    public void WithoutAnyPricesThereIsNoInventedNumber()
    {
        var kosten = GrowCostService.Berechnen(
            Start, Ende, null,
            lampenWatt: 400, planStundenProTag: 18, strompreisCent: null,
            dosen: [Dose(1, 100)], pumpen: [Pumpe(1, "pH-Minus", null)],
            trockenGramm: 100);

        Assert.Null(kosten.StromEur);
        Assert.Null(kosten.DuengerEur);
        Assert.Null(kosten.SummeEur);
        Assert.Null(kosten.EurProGramm);
        // Aber der Weg dahin wird gewiesen.
        Assert.Contains("eintragen", kosten.DuengerHerkunft);
    }
}
