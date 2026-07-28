using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Sollwert-Profile je Anbaustil.
/// </summary>
/// <remarks>
/// Vorher gab es genau eine Datei, und DWC entstand daraus über einen
/// EC-Multiplikator im Code. Ein zweites Profil danebenzulegen hätte nichts
/// bewirkt — niemand hätte es gelesen. Jetzt wählt der Anbaustil die Datei.
/// </remarks>
public sealed class TargetProfileTests
{
    private static TargetValueService Service() => TestKnowledgeBase.TargetValues();

    [Fact]
    public void BothShippedProfiles_AreLoaded()
    {
        var ids = Service().ProfileIds;

        Assert.Contains("rdwc-default", ids);
        Assert.Contains("dwc-default", ids);
    }

    [Fact]
    public void DwcHasItsOwnProfile()
    {
        Assert.Equal("dwc-default", TargetValueService.ProfileIdFor(HydroStyle.DWC));
        Assert.Equal("rdwc-default", TargetValueService.ProfileIdFor(HydroStyle.RDWC));
    }

    [Theory]
    [InlineData(HydroStyle.NFT)]
    [InlineData(HydroStyle.Aeroponic)]
    [InlineData(HydroStyle.Other)]
    [InlineData(HydroStyle.None)]
    public void StylesWithoutAProfile_FallBackToRdwc(HydroStyle style)
    {
        // Eine Annahme, keine Messung — aber an einer Stelle festgehalten.
        Assert.Equal("rdwc-default", TargetValueService.ProfileIdFor(style));
    }

    [Theory]
    [InlineData(GrowStage.Seedling)]
    [InlineData(GrowStage.Clone)]
    [InlineData(GrowStage.Veg)]
    [InlineData(GrowStage.Transition)]
    [InlineData(GrowStage.Flower)]
    [InlineData(GrowStage.Finish)]
    public void DwcEcIsHigherThanRdwc_InEveryPhase(GrowStage stage)
    {
        var service = Service();
        var rdwc = service.GetTargets(HydroStyle.RDWC, stage)!;
        var dwc = service.GetTargets(HydroStyle.DWC, stage)!;

        Assert.True(dwc.EcMin > rdwc.EcMin, $"{stage}: EC-Untergrenze muss höher sein.");
        Assert.True(dwc.EcMax > rdwc.EcMax, $"{stage}: EC-Obergrenze muss höher sein.");
    }

    [Theory]
    [InlineData(GrowStage.Seedling)]
    [InlineData(GrowStage.Veg)]
    [InlineData(GrowStage.Flower)]
    public void DwcEcIsExactlyTheAgreedFactor(GrowStage stage)
    {
        var service = Service();
        var rdwc = service.GetTargets(HydroStyle.RDWC, stage)!;
        var dwc = service.GetTargets(HydroStyle.DWC, stage)!;

        Assert.Equal(Math.Round(rdwc.EcMin * 1.3, 2), dwc.EcMin, 2);
        Assert.Equal(Math.Round(rdwc.EcMax * 1.3, 2), dwc.EcMax, 2);
    }

    [Fact]
    public void TheFactorIsNotAppliedTwice()
    {
        // Die Falle beim Umbau: Profil MIT eingerechnetem Aufschlag plus der
        // alte Multiplikator im Code haetten das 1,69-fache ergeben.
        var service = Service();
        var rdwc = service.GetTargets(HydroStyle.RDWC, GrowStage.Flower)!;
        var dwc = service.GetTargets(HydroStyle.DWC, GrowStage.Flower)!;

        Assert.True(dwc.EcMax < rdwc.EcMax * 1.5, $"EC {dwc.EcMax} sieht nach doppeltem Aufschlag aus.");
    }

    [Theory]
    [InlineData(GrowStage.Seedling)]
    [InlineData(GrowStage.Veg)]
    [InlineData(GrowStage.Flower)]
    public void OnlyEcDiffers_TheRestIsUnchanged(GrowStage stage)
    {
        // Der Aufschlag betrifft das Puffervolumen, nicht das Klima. Waeren pH
        // oder VPD mitgewandert, waere das ein Tippfehler in der Datei.
        var service = Service();
        var rdwc = service.GetTargets(HydroStyle.RDWC, stage)!;
        var dwc = service.GetTargets(HydroStyle.DWC, stage)!;

        Assert.Equal(rdwc.PhMin, dwc.PhMin);
        Assert.Equal(rdwc.PhMax, dwc.PhMax);
        Assert.Equal(rdwc.VpdMin, dwc.VpdMin);
        Assert.Equal(rdwc.OrpMin, dwc.OrpMin);
        Assert.Equal(rdwc.WaterTempDayC, dwc.WaterTempDayC);
        Assert.Equal(rdwc.PpfdMax, dwc.PpfdMax);
        Assert.Equal(rdwc.Co2Max, dwc.Co2Max);
    }

    [Fact]
    public void AnUnknownProfileFallsBack_RatherThanGoingBlank()
    {
        // Ein spaeter geloeschtes eigenes Profil darf den Bildschirm nicht
        // leerraeumen.
        Assert.NotNull(Service().GetTargets("gibt-es-nicht", GrowStage.Veg));
    }

    [Fact]
    public void StagesWithoutSetpoints_HaveNoTargets()
    {
        // Trocknen und Curen haben keine Naehrloesung.
        Assert.Null(Service().GetTargets(HydroStyle.RDWC, GrowStage.Dry));
    }
}
