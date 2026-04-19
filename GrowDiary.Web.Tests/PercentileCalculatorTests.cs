using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Xunit;

namespace GrowDiary.Web.Tests;

public sealed class PercentileCalculatorTests
{
    [Fact]
    public void EinWert_GibtDenWertZurueck()
    {
        var result = PercentileCalculator.Calculate(new[] { 42.0 }, 50);

        Assert.Equal(42.0, result);
    }

    [Fact]
    public void ZweiWerte_Median_IstDurchschnitt()
    {
        // P50 von [10, 20]: index = 0.5 → 10 + 0.5*(20-10) = 15
        var result = PercentileCalculator.Calculate(new[] { 10.0, 20.0 }, 50);

        Assert.Equal(15.0, result);
    }

    [Fact]
    public void FuenfWerte_P50_IstMittelwert()
    {
        // [1,2,3,4,5]: index = 0.5*4 = 2 → Wert an Index 2 = 3.0
        var result = PercentileCalculator.Calculate(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 }, 50);

        Assert.Equal(3.0, result);
    }

    [Fact]
    public void SymmetrischeWerte_P5_P95_KorrektBerechnet()
    {
        // 10 Werte: [10,20,30,40,50,60,70,80,90,100]
        // P5:  index = 0.05 * 9 = 0.45 → 10 + 0.45*(20-10) = 14.5
        // P95: index = 0.95 * 9 = 8.55 → 90 + 0.55*(100-90) = 95.5
        var values = new[] { 10.0, 20.0, 30.0, 40.0, 50.0, 60.0, 70.0, 80.0, 90.0, 100.0 };

        var p5  = PercentileCalculator.Calculate(values, 5);
        var p95 = PercentileCalculator.Calculate(values, 95);

        Assert.Equal(14.5, p5,  precision: 10);
        Assert.Equal(95.5, p95, precision: 10);
    }

    [Fact]
    public void ComputeStats_AlleFelder_KorrektGesetzt()
    {
        var values = new[] { 10.0, 20.0, 30.0, 40.0, 50.0 };
        var date   = new DateOnly(2026, 4, 13);

        var stat = PercentileCalculator.ComputeStats(1, "temperature", date, values, "°C");

        Assert.Equal(1,             stat.TentId);
        Assert.Equal("temperature", stat.MetricKey);
        Assert.Equal(date,          stat.Date);
        Assert.Equal(10.0,          stat.Min);
        Assert.Equal(50.0,          stat.Max);
        Assert.Equal(30.0,          stat.Median);
        Assert.Equal(5,             stat.Count);
        Assert.Equal("°C",          stat.Unit);
        Assert.True(stat.P5  < stat.Median);
        Assert.True(stat.P95 > stat.Median);
        Assert.Equal(30.0,          stat.Avg);
    }

    [Fact]
    public void ComputeStats_SortierungUnabhaengig()
    {
        var sorted   = new[] { 10.0, 20.0, 30.0, 40.0, 50.0 };
        var unsorted = new[] { 40.0, 10.0, 50.0, 20.0, 30.0 };
        var date     = new DateOnly(2026, 4, 13);

        var statSorted   = PercentileCalculator.ComputeStats(1, "temp", date, sorted,   null);
        var statUnsorted = PercentileCalculator.ComputeStats(1, "temp", date, unsorted, null);

        Assert.Equal(statSorted.Min,    statUnsorted.Min);
        Assert.Equal(statSorted.Max,    statUnsorted.Max);
        Assert.Equal(statSorted.Median, statUnsorted.Median);
        Assert.Equal(statSorted.P5,     statUnsorted.P5);
        Assert.Equal(statSorted.P95,    statUnsorted.P95);
        Assert.Equal(statSorted.Avg,    statUnsorted.Avg);
    }
}
