using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

public sealed class GrowStageResolverTests
{
    private static readonly DateTime Heute = new(2026, 7, 27);

    private static GrowRun Grow(Action<GrowRun>? anpassen = null)
    {
        var grow = new GrowRun
        {
            Name = "Test",
            StartDate = Heute.AddDays(-7),
            SeedType = SeedType.Feminized,
            StartMaterial = StartMaterial.Seed,
            EntryPoint = GrowEntryPoint.Germination,
            GerminatedAt = Heute.AddDays(-7),
        };
        anpassen?.Invoke(grow);
        return grow;
    }

    [Fact]
    public void AGrowWithoutAnyMeasurement_StillHasAStage()
    {
        // Der Fall aus dem Alltag: Grow läuft seit einer Woche, Sensoren liefern,
        // von Hand gemessen wurde noch nie. Vorher gab es hier gar keine Phase
        // und damit auf dem ganzen Bildschirm keinen einzigen Zielbereich.
        Assert.Equal(GrowStage.Seedling, GrowStageResolver.Resolve(Grow(), Heute));
    }

    [Fact]
    public void TheFirstTwoWeeksAfterGermination_AreSeedling()
    {
        var grow = Grow(g => { g.StartDate = Heute.AddDays(-3); g.GerminatedAt = Heute.AddDays(-3); });

        Assert.Equal(GrowStage.Seedling, GrowStageResolver.Resolve(grow, Heute));
    }

    [Fact]
    public void AfterTheSeedlingWeeks_ItIsVeg()
    {
        var grow = Grow(g => { g.StartDate = Heute.AddDays(-30); g.GerminatedAt = Heute.AddDays(-30); });

        Assert.Equal(GrowStage.Veg, GrowStageResolver.Resolve(grow, Heute));
    }

    [Fact]
    public void ARecordedFlip_BeatsEveryCalculation()
    {
        var grow = Grow(g => g.FlipDate = Heute.AddDays(-30));

        Assert.Equal(GrowStage.Flower, GrowStageResolver.Resolve(grow, Heute));
    }

    [Fact]
    public void TheFirstDaysAfterTheFlip_AreTransition()
    {
        var grow = Grow(g => g.FlipDate = Heute.AddDays(-3));

        Assert.Equal(GrowStage.Transition, GrowStageResolver.Resolve(grow, Heute));
    }

    [Fact]
    public void TheLastTwoWeeksBeforeHarvest_AreFinish()
    {
        // Neun Wochen Blüte, geflippt vor 58 Tagen ⇒ Ernte in 5 Tagen.
        var grow = Grow(g =>
        {
            g.FlipDate = Heute.AddDays(-58);
            g.BreederFlowerWeeksMax = 9;
        });

        Assert.Equal(GrowStage.Finish, GrowStageResolver.Resolve(grow, Heute));
    }

    [Fact]
    public void WithoutBreederWeeks_FinishIsNotGuessed()
    {
        var grow = Grow(g => g.FlipDate = Heute.AddDays(-58));

        Assert.Equal(GrowStage.Flower, GrowStageResolver.Resolve(grow, Heute));
    }

    [Fact]
    public void AFlipInTheFuture_IsStillVeg()
    {
        var grow = Grow(g =>
        {
            g.StartDate = Heute.AddDays(-30);
            g.GerminatedAt = Heute.AddDays(-30);
            g.FlipDate = Heute.AddDays(5);
        });

        Assert.Equal(GrowStage.Veg, GrowStageResolver.Resolve(grow, Heute));
    }

    [Fact]
    public void AnUnrootedClone_IsAClone()
    {
        var grow = Grow(g =>
        {
            g.StartMaterial = StartMaterial.Clone;
            g.GerminatedAt = null;
            g.CloneIsRooted = false;
            g.RootedAt = null;
        });

        Assert.Equal(GrowStage.Clone, GrowStageResolver.Resolve(grow, Heute));
    }

    [Fact]
    public void EnteringMidGrow_TrustsTheEntryPoint()
    {
        // Wer mitten im Lauf einsteigt, hat kein Keimdatum — und keimt auch nicht.
        var grow = Grow(g => { g.GerminatedAt = null; g.EntryPoint = GrowEntryPoint.Flower; });

        Assert.Equal(GrowStage.Flower, GrowStageResolver.Resolve(grow, Heute));
    }

    [Fact]
    public void Autoflower_GoesToFlowerByDaysInsteadOfAFlip()
    {
        var grow = Grow(g =>
        {
            g.SeedType = SeedType.Autoflower;
            g.StartDate = Heute.AddDays(-40);
            g.GerminatedAt = Heute.AddDays(-40);
        });

        Assert.Equal(GrowStage.Flower, GrowStageResolver.Resolve(grow, Heute));
    }

    [Fact]
    public void YoungAutoflower_IsStillVeg()
    {
        var grow = Grow(g =>
        {
            g.SeedType = SeedType.Autoflower;
            g.StartDate = Heute.AddDays(-20);
            g.GerminatedAt = Heute.AddDays(-20);
        });

        Assert.Equal(GrowStage.Veg, GrowStageResolver.Resolve(grow, Heute));
    }
}
