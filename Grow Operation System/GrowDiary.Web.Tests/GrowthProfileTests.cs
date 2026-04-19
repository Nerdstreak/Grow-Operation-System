using GrowDiary.Web.Models;
using Xunit;

namespace GrowDiary.Web.Tests;

public sealed class GrowthProfileTests
{
    [Fact]
    public void Hydro_UsesReservoirFelder()
    {
        var profile = new GrowthProfile(HydroStyle.RDWC);

        Assert.True(profile.UsesReservoirPh);
        Assert.False(profile.UsesWaterAmount);
    }

    [Fact]
    public void Hydro_UsesAddbackUndDO()
    {
        var profile = new GrowthProfile(HydroStyle.RDWC);

        Assert.True(profile.UsesAddbackEc);
        Assert.True(profile.UsesReservoirDissolvedOxygen);
        Assert.True(profile.UsesReservoirOrp);
    }

    [Fact]
    public void Hydro_LabelEnthaeltHydroStyle()
    {
        var profile = new GrowthProfile(HydroStyle.RDWC);

        Assert.Contains("RDWC", profile.Label);
        Assert.Contains("Hydro", profile.Label);
    }

    [Fact]
    public void Hydro_IstImmerHydro()
    {
        var profile = new GrowthProfile(HydroStyle.DWC);

        Assert.True(profile.IsHydro);
        Assert.False(profile.IsAutopot);
        Assert.False(profile.IsSoilOrganic);
        Assert.False(profile.IsCoco);
    }
}
