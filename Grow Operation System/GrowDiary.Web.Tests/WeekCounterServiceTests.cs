using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Xunit;

namespace GrowDiary.Web.Tests;

public sealed class WeekCounterServiceTests
{
    private static GrowRun CreateGrow() => new()
    {
        Name = "Test",
        MediumType = MediumType.Hydro,
        IrrigationType = IrrigationType.ActiveHydro,
        HydroStyle = HydroStyle.RDWC,
        StartDate = DateTime.Today,
        StartMaterial = StartMaterial.Seed,
        SeedType = SeedType.Feminized
    };

    private static readonly WeekCounterService Svc = new();

    [Fact]
    public void Samen_OhneKeimung_WaitingForGermination()
    {
        var grow = CreateGrow();
        grow.StartDate = DateTime.Today.AddDays(-3);
        grow.GerminatedAt = null;

        var result = Svc.Calculate(grow);

        Assert.Equal(GrowCounterState.WaitingForGermination, result.State);
        Assert.Equal(3, result.DaysGerminating);
        Assert.Contains("3", result.Label);
    }

    [Fact]
    public void Samen_OhneKeimung_Nach7Tagen_Label()
    {
        var grow = CreateGrow();
        grow.StartDate = DateTime.Today.AddDays(-7);
        grow.GerminatedAt = null;

        var result = Svc.Calculate(grow);

        Assert.Equal(7, result.DaysGerminating);
    }

    [Fact]
    public void Samen_Gekeimt_KeinFlip_Vegetating()
    {
        var grow = CreateGrow();
        grow.GerminatedAt = DateTime.Today.AddDays(-14);
        grow.FlipDate = null;
        grow.SeedType = SeedType.Feminized;

        var result = Svc.Calculate(grow);

        Assert.Equal(GrowCounterState.Vegetating, result.State);
        Assert.Equal(3, result.VegWeek);
        Assert.Contains("Veg Woche 3", result.Label);
    }

    [Fact]
    public void Samen_MitFlip_Flowering()
    {
        var grow = CreateGrow();
        grow.GerminatedAt = DateTime.Today.AddDays(-28);
        grow.FlipDate = DateTime.Today.AddDays(-14);

        var result = Svc.Calculate(grow);

        Assert.Equal(GrowCounterState.Flowering, result.State);
        Assert.Equal(3, result.FlowerWeek);
        Assert.Equal(3, result.VegWeek);
        Assert.Contains("Blüte Woche 3", result.Label);
    }

    [Fact]
    public void Autoflower_LaeuftDurch()
    {
        var grow = CreateGrow();
        grow.SeedType = SeedType.Autoflower;
        grow.GerminatedAt = DateTime.Today.AddDays(-21);

        var result = Svc.Calculate(grow);

        Assert.Equal(GrowCounterState.Autoflowering, result.State);
        Assert.Equal(4, result.AutoflowerWeek);
        Assert.Contains("Woche 4", result.Label);
    }

    [Fact]
    public void Steckling_NichtBewurzelt_WaitingForRooting()
    {
        var grow = CreateGrow();
        grow.StartMaterial = StartMaterial.Clone;
        grow.CloneIsRooted = false;
        grow.RootedAt = null;
        grow.StartDate = DateTime.Today.AddDays(-5);

        var result = Svc.Calculate(grow);

        Assert.Equal(GrowCounterState.WaitingForRooting, result.State);
        Assert.Equal(5, result.DaysRooting);
    }

    [Fact]
    public void Steckling_BereitsBewurzelt_Vegetating()
    {
        var grow = CreateGrow();
        grow.StartMaterial = StartMaterial.Clone;
        grow.CloneIsRooted = true;
        grow.StartDate = DateTime.Today.AddDays(-10);

        var result = Svc.Calculate(grow);

        Assert.Equal(GrowCounterState.Vegetating, result.State);
        Assert.Equal(2, result.VegWeek);
    }

    [Fact]
    public void Autoflower_FlipDateIgnoriert()
    {
        var grow = CreateGrow();
        grow.SeedType = SeedType.Autoflower;
        grow.GerminatedAt = DateTime.Today.AddDays(-14);
        grow.FlipDate = DateTime.Today.AddDays(-7);

        var result = Svc.Calculate(grow);

        Assert.Equal(GrowCounterState.Autoflowering, result.State);
    }

    [Fact]
    public void Steckling_CloneIsRooted_RootedAt_Gesetzt_IstVegetating()
    {
        var svc = new WeekCounterService();
        var grow = new GrowRun
        {
            StartMaterial = StartMaterial.Clone,
            CloneIsRooted = true,
            RootedAt = DateTime.Today.AddDays(-10),
            StartDate = DateTime.Today.AddDays(-10),
            SeedType = SeedType.Feminized
        };
        var result = svc.Calculate(grow);
        Assert.Equal(GrowCounterState.Vegetating, result.State);
        Assert.NotNull(result.VegWeek);
        Assert.True(result.VegWeek >= 1);
    }
}
