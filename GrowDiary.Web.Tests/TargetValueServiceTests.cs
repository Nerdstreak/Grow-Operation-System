using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Xunit;

namespace GrowDiary.Web.Tests;

public sealed class TargetValueServiceTests
{
    [Fact]
    public void RDWC_Seedling_KorrekteSollwerte()
    {
        var result = TargetValueService.GetTargets(HydroStyle.RDWC, GrowStage.Seedling);

        Assert.NotNull(result);
        Assert.Equal(6.0, result.PhMin);
        Assert.Equal(6.2, result.PhMax);
        Assert.Equal(0.2, result.EcMin);
        Assert.Equal(0.4, result.EcMax);
    }

    [Fact]
    public void DWC_Seedling_ECHoeherAlsRDWC()
    {
        var result = TargetValueService.GetTargets(HydroStyle.DWC, GrowStage.Seedling);

        Assert.NotNull(result);
        Assert.True(result.EcMin > 0.2);
        Assert.Equal(Math.Round(0.2 * TargetValueService.DwcEcMultiplier, 2), result.EcMin);
        Assert.Equal(Math.Round(0.4 * TargetValueService.DwcEcMultiplier, 2), result.EcMax);
    }

    [Fact]
    public void DWC_AndereWerteGleichWieRDWC()
    {
        var rdwc = TargetValueService.GetTargets(HydroStyle.RDWC, GrowStage.Flower)!;
        var dwc  = TargetValueService.GetTargets(HydroStyle.DWC,  GrowStage.Flower)!;

        Assert.Equal(rdwc.PhMin,  dwc.PhMin);
        Assert.Equal(rdwc.PhMax,  dwc.PhMax);
        Assert.Equal(rdwc.VpdMin, dwc.VpdMin);
        Assert.Equal(rdwc.VpdMax, dwc.VpdMax);
        Assert.NotEqual(rdwc.EcMin, dwc.EcMin);
    }

    [Fact]
    public void Dry_Cure_GibtNull()
    {
        Assert.Null(TargetValueService.GetTargets(HydroStyle.RDWC, GrowStage.Dry));
        Assert.Null(TargetValueService.GetTargets(HydroStyle.RDWC, GrowStage.Cure));
    }

    [Fact]
    public void Flower_PhNiedrigerAlsVeg()
    {
        var veg    = TargetValueService.GetTargets(HydroStyle.RDWC, GrowStage.Veg)!;
        var flower = TargetValueService.GetTargets(HydroStyle.RDWC, GrowStage.Flower)!;

        Assert.True(flower.PhMax < veg.PhMax);
    }

    [Fact]
    public void Flower_ECHoeherAlsSeedling()
    {
        var seedling = TargetValueService.GetTargets(HydroStyle.RDWC, GrowStage.Seedling)!;
        var flower   = TargetValueService.GetTargets(HydroStyle.RDWC, GrowStage.Flower)!;

        Assert.True(flower.EcMin > seedling.EcMax);
    }

    [Fact]
    public void RDWC_Flower_PhNiedrigerAlsVeg()
    {
        var veg    = TargetValueService.GetTargets(HydroStyle.RDWC, GrowStage.Veg)!;
        var flower = TargetValueService.GetTargets(HydroStyle.RDWC, GrowStage.Flower)!;

        Assert.True(flower.PhMax <= veg.PhMax);
    }

    [Fact]
    public void RDWC_Finish_ECMaxGroesserGleichFlowerECMax()
    {
        var flower = TargetValueService.GetTargets(HydroStyle.RDWC, GrowStage.Flower)!;
        var finish = TargetValueService.GetTargets(HydroStyle.RDWC, GrowStage.Finish)!;

        Assert.True(finish.EcMax >= flower.EcMax);
    }

    [Fact]
    public void DWC_EcMultiplikatorKorrektFuerAlleStages()
    {
        var stages = new[] { GrowStage.Seedling, GrowStage.Veg, GrowStage.Flower, GrowStage.Finish };

        foreach (var stage in stages)
        {
            var rdwc = TargetValueService.GetTargets(HydroStyle.RDWC, stage)!;
            var dwc  = TargetValueService.GetTargets(HydroStyle.DWC,  stage)!;

            Assert.Equal(Math.Round(rdwc.EcMin * TargetValueService.DwcEcMultiplier, 2), dwc.EcMin);
            Assert.Equal(Math.Round(rdwc.EcMax * TargetValueService.DwcEcMultiplier, 2), dwc.EcMax);
        }
    }
}
