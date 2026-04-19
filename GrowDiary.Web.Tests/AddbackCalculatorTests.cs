using GrowDiary.Web.Services;
using Xunit;

namespace GrowDiary.Web.Tests;

public sealed class AddbackCalculatorTests
{
    [Fact]
    public void KeinAddbackNoetig_WennEcIstGroesserGleichZiel()
    {
        var result = AddbackCalculator.Calculate(
            reservoirLiters: 60,
            ecIst: 1.2,
            ecZiel: 1.0,
            ecStock: 3.0);

        Assert.False(result.NeedsAddback);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void AddbackBerechnet_Korrekt()
    {
        // V_add = 60 * (1.2 - 0.8) / (3.0 - 1.2) = 60 * 0.4 / 1.8 = 13.33
        var result = AddbackCalculator.Calculate(
            reservoirLiters: 60,
            ecIst: 0.8,
            ecZiel: 1.2,
            ecStock: 3.0);

        Assert.True(result.NeedsAddback);
        Assert.Equal(Math.Round(60 * 0.4 / 1.8, 2), result.LitersToAdd);
    }

    [Fact]
    public void AddbackStockZuNiedrig_GibtFehler()
    {
        var result = AddbackCalculator.Calculate(
            reservoirLiters: 60,
            ecIst: 1.0,
            ecZiel: 2.0,
            ecStock: 2.0);

        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void ReservoirVolumenNull_GibtFehler()
    {
        var result = AddbackCalculator.Calculate(
            reservoirLiters: 0,
            ecIst: 0.8,
            ecZiel: 1.2,
            ecStock: 3.0);

        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void EcIstGleichZiel_KeinAddback()
    {
        var result = AddbackCalculator.Calculate(
            reservoirLiters: 60,
            ecIst: 1.2,
            ecZiel: 1.2,
            ecStock: 3.0);

        Assert.False(result.NeedsAddback);
        Assert.Equal(0, result.LitersToAdd);
    }

    [Fact]
    public void GroessesReservoir_ErgebnisKorrektGerundet()
    {
        // V_add = 200 * (1.4 - 0.6) / (4.0 - 1.4) = 200 * 0.8 / 2.6 = 61.538... → 61.54
        var result = AddbackCalculator.Calculate(
            reservoirLiters: 200,
            ecIst: 0.6,
            ecZiel: 1.4,
            ecStock: 4.0);

        Assert.True(result.NeedsAddback);
        Assert.Equal(61.54, result.LitersToAdd);
    }
}
