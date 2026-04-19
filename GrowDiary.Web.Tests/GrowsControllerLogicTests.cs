using GrowDiary.Web.Controllers;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests;

public sealed class GrowsControllerLogicTests
{
    [Fact]
    public void WaitingForGermination_GibtSeedling()
    {
        var weekInfo = new GrowWeekInfo(GrowCounterState.WaitingForGermination, null, null, null, 1, null, "Keimt");

        var result = GrowsController.DetermineStageFromWeekInfo(weekInfo);

        Assert.Equal(GrowStage.Seedling, result);
    }

    [Fact]
    public void WaitingForRooting_GibtClone()
    {
        var weekInfo = new GrowWeekInfo(GrowCounterState.WaitingForRooting, null, null, null, null, 3, "Bewurzelung");

        var result = GrowsController.DetermineStageFromWeekInfo(weekInfo);

        Assert.Equal(GrowStage.Clone, result);
    }

    [Fact]
    public void Vegetating_GibtVeg()
    {
        var weekInfo = new GrowWeekInfo(GrowCounterState.Vegetating, 2, null, null, null, null, "Veg W2");

        var result = GrowsController.DetermineStageFromWeekInfo(weekInfo);

        Assert.Equal(GrowStage.Veg, result);
    }

    [Fact]
    public void Flowering_GibtFlower()
    {
        var weekInfo = new GrowWeekInfo(GrowCounterState.Flowering, null, 4, null, null, null, "Blüte W4");

        var result = GrowsController.DetermineStageFromWeekInfo(weekInfo);

        Assert.Equal(GrowStage.Flower, result);
    }

    [Theory]
    [InlineData(1,  GrowStage.Veg)]
    [InlineData(4,  GrowStage.Veg)]
    [InlineData(5,  GrowStage.Flower)]
    [InlineData(10, GrowStage.Flower)]
    public void Autoflowering_WocheEntscheidetVegOderFlower(int autoflowerWeek, GrowStage expected)
    {
        var weekInfo = new GrowWeekInfo(GrowCounterState.Autoflowering, null, null, autoflowerWeek, null, null, $"Auto W{autoflowerWeek}");

        var result = GrowsController.DetermineStageFromWeekInfo(weekInfo);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Flowering_NachWoche4_StageSollteFlowerSein()
    {
        var weekInfo = new GrowWeekInfo(GrowCounterState.Autoflowering, null, null, 5, null, null, "Auto W5");

        var result = GrowsController.DetermineStageFromWeekInfo(weekInfo);

        Assert.Equal(GrowStage.Flower, result);
    }

    [Fact]
    public void Flowering_Woche3_StageSollteVegSein()
    {
        var weekInfo = new GrowWeekInfo(GrowCounterState.Autoflowering, null, null, 3, null, null, "Auto W3");

        var result = GrowsController.DetermineStageFromWeekInfo(weekInfo);

        Assert.Equal(GrowStage.Veg, result);
    }

    [Fact]
    public void NoData_StageSollteVegSein()
    {
        var weekInfo = new GrowWeekInfo(GrowCounterState.NoData, null, null, null, null, null, "");

        var result = GrowsController.DetermineStageFromWeekInfo(weekInfo);

        Assert.Equal(GrowStage.Veg, result);
    }
}
