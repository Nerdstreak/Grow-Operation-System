using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Der Übergang Sämling → Veg wird beobachtet, nicht gerechnet.
/// </summary>
/// <remarks>
/// Die Regel kommt vom Nutzer und deckt sich mit der Praxis: gewechselt wird,
/// sobald die Pflanze keine Keimblätter mehr trägt und sichtbar wächst — echte
/// gezackte Blätter, dickerer Stängel, regelmäßig neue Blattpaare, Seitentriebe
/// an den Knoten, spürbar mehr Wasserverbrauch. Typisch ein bis drei Wochen nach
/// der Keimung, aber eben typisch.
///
/// Vorher hing es allein an 14 Tagen, und das widersprach der Anzeige: der
/// Balken sagte „Veg Tag 8", die Kacheln zeigten Sämlings-Ziele.
/// </remarks>
public sealed class GrowStageVegStartedTests
{
    private static readonly DateTime Start = new(2026, 7, 21);

    private static GrowRun Grow(DateTime? vegAb = null, DateTime? flip = null, DateTime? gekeimt = null)
        => new()
        {
            Id = 1,
            Name = "Test",
            StartDate = Start,
            EntryPoint = GrowEntryPoint.Germination,
            StartMaterial = StartMaterial.Seed,
            SeedType = SeedType.Feminized,
            GerminatedAt = gekeimt,
            VegStartedAt = vegAb,
            FlipDate = flip,
        };

    [Fact]
    public void WithoutAnEntry_TheDaysDecide()
    {
        // Tag 8 von 14: die Schaetzung sagt Saemling.
        Assert.Equal(GrowStage.Seedling, GrowStageResolver.Resolve(Grow(), Start.AddDays(7)));
        Assert.Equal(GrowStage.Veg, GrowStageResolver.Resolve(Grow(), Start.AddDays(20)));
    }

    [Fact]
    public void ARecordedTransition_BeatsTheCalculation()
    {
        // Am Tag 6 gesehen: echte gezackte Blaetter. Ab da ist Veg, egal was die
        // 14-Tage-Regel meint.
        var grow = Grow(vegAb: Start.AddDays(5));

        Assert.Equal(GrowStage.Veg, GrowStageResolver.Resolve(grow, Start.AddDays(7)));
    }

    [Fact]
    public void BeforeTheRecordedDay_ItIsStillASeedling()
    {
        // Wer den Uebergang nachtraegt, verschiebt ihn nicht rueckwirkend auf
        // den ganzen Lauf.
        var grow = Grow(vegAb: Start.AddDays(10));

        Assert.Equal(GrowStage.Seedling, GrowStageResolver.Resolve(grow, Start.AddDays(7)));
        Assert.Equal(GrowStage.Veg, GrowStageResolver.Resolve(grow, Start.AddDays(10)));
    }

    [Fact]
    public void AnEarlyTransition_IsAllowed()
    {
        // Unter guten Bedingungen geht es schneller. Die Rechnung haette hier
        // noch neun Tage Saemling behauptet.
        var grow = Grow(vegAb: Start.AddDays(4));

        Assert.Equal(GrowStage.Veg, GrowStageResolver.Resolve(grow, Start.AddDays(5)));
    }

    [Fact]
    public void ALateTransition_IsAlsoAllowed()
    {
        // Drei Wochen sind auch noch normal. Ohne den Eintrag waere die Pflanze
        // ab Tag 15 rechnerisch in der Veg gewesen — mit deutlich schaerferen
        // EC-Zielen, als ein Saemling vertraegt.
        var grow = Grow(vegAb: Start.AddDays(20));

        Assert.Equal(GrowStage.Seedling, GrowStageResolver.Resolve(grow, Start.AddDays(16)));
        Assert.Equal(GrowStage.Veg, GrowStageResolver.Resolve(grow, Start.AddDays(21)));
    }

    [Fact]
    public void TheFlipStillWins()
    {
        // Geflippt ist geflippt — der Veg-Eintrag darf die Bluete nicht
        // zurueckdrehen.
        var grow = Grow(vegAb: Start.AddDays(5), flip: Start.AddDays(30));

        Assert.Equal(GrowStage.Veg, GrowStageResolver.Resolve(grow, Start.AddDays(20)));
        Assert.NotEqual(GrowStage.Veg, GrowStageResolver.Resolve(grow, Start.AddDays(35)));
    }

    [Fact]
    public void ABloomingAutoflower_IsNotDraggedBackIntoVeg()
    {
        // Beim Live-Durchspielen aufgefallen: die Regel stand zuerst ganz oben in
        // Resolve und schlug damit ALLES — auch eine Autoflower, die laengst
        // blueht. Autoflower kennt keinen Flip, der Riegel gegen FlipDate lief
        // also ins Leere, und ein Grow in Woche 10 sprang zurueck auf Veg-Ziele.
        var auto = new GrowRun
        {
            Id = 2,
            Name = "Auto",
            StartDate = Start,
            EntryPoint = GrowEntryPoint.Germination,
            StartMaterial = StartMaterial.Seed,
            SeedType = SeedType.Autoflower,
            VegStartedAt = Start.AddDays(5),
        };

        Assert.Equal(GrowStage.Veg, GrowStageResolver.Resolve(auto, Start.AddDays(10)));
        Assert.NotEqual(GrowStage.Veg, GrowStageResolver.Resolve(auto, Start.AddDays(40)));
        Assert.NotEqual(GrowStage.Seedling, GrowStageResolver.Resolve(auto, Start.AddDays(40)));
    }

    [Fact]
    public void ASeedGrowWithoutAGerminationDate_DoesNotStaySeedlingForever()
    {
        // Der Normalfall: Keimdatum nie eingetragen. Vorher gab der Auflöser
        // dann fuer immer „Saemling" zurueck — nach drei Monaten haette eine
        // ausgewachsene Pflanze noch Saemlings-EC bekommen.
        var grow = Grow();

        Assert.Equal(GrowStage.Seedling, GrowStageResolver.Resolve(grow, Start.AddDays(7)));
        Assert.Equal(GrowStage.Veg, GrowStageResolver.Resolve(grow, Start.AddDays(90)));
    }

    [Fact]
    public void GerminationDateShiftsTheEstimate()
    {
        // Ohne Keimdatum zaehlt der Start, mit Keimdatum die Keimung — der
        // Saemling beginnt schliesslich erst dort.
        var spaetGekeimt = Grow(gekeimt: Start.AddDays(6));

        Assert.Equal(GrowStage.Seedling, GrowStageResolver.Resolve(spaetGekeimt, Start.AddDays(16)));
    }
}
