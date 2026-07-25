using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests;

/// <summary>
/// The holiday guard has two ways to fail, and both are bad: staying quiet while a run
/// slowly dies, and crying wolf until the notifications get muted. Both directions are
/// asserted here.
/// </summary>
public sealed class TrendWatchServiceTests
{
    private static readonly DateTime Now = new(2026, 7, 25, 20, 0, 0, DateTimeKind.Unspecified);

    private static readonly HydroTargetValues Targets = new(
        PhMin: 5.8, PhMax: 6.2,
        EcMin: 1.0, EcMax: 1.2,
        OrpMin: 400, OrpMax: 450,
        WaterTempDayC: 20, WaterTempNightC: 18,
        VpdMin: 1.0, VpdMax: 1.2,
        PpfdMin: 800, PpfdMax: 1000,
        Co2Min: 1200, Co2Max: 1400);

    /// <summary>One measurement per day, counting backwards from today.</summary>
    private static List<Measurement> Series(params (int DaysAgo, double? Ph, double? Ec)[] points) =>
        points.Select(point => new Measurement
        {
            TakenAt = Now.Date.AddDays(-point.DaysAgo).AddHours(8),
            ReservoirPh = point.Ph,
            ReservoirEc = point.Ec,
        }).ToList();

    [Fact]
    public void PhCreepingUpForDays_IsReported_EvenThoughEveryReadingIsInsideTheBand()
    {
        // This is the whole reason the guard exists: no threshold ever fires, and by the
        // time one does the run has been drifting for the better part of a week.
        var measurements = Series(
            (4, 5.85, null), (3, 5.95, null), (2, 6.05, null), (1, 6.12, null), (0, 6.18, null));

        var findings = TrendWatchService.Evaluate(measurements, Targets, Now);

        var drift = Assert.Single(findings, finding => finding.Code == "trend.ph.drift");
        Assert.Equal(TrendSeverity.Info, drift.Severity);
        Assert.Contains("steigt", drift.Detail);
        Assert.Contains("noch im erlaubten Bereich", drift.Detail);
        Assert.Equal("ph-drift-band", drift.GuidanceId);
    }

    [Fact]
    public void PhIsJudgedByTheGrowplanBand_NotByTheNarrowerMixingTarget()
    {
        // The Veg setpoint is 6.0–6.1, but the growplan explicitly allows drift across
        // 5.8–6.2. Judging by the setpoint would make the guard nag about exactly the
        // behaviour the plan tells you to leave alone — the mistake fixed in the deviation
        // analyser, which must not reappear here.
        var narrow = Targets with { PhMin = 6.0, PhMax = 6.1 };
        var measurements = Series(
            (4, 5.86, null), (3, 5.94, null), (2, 6.02, null), (1, 6.10, null), (0, 6.18, null));

        var drift = Assert.Single(TrendWatchService.Evaluate(measurements, narrow, Now), f => f.Code == "trend.ph.drift");

        Assert.Equal(TrendSeverity.Info, drift.Severity);
        Assert.Contains("noch im erlaubten Bereich", drift.Detail);
    }

    [Fact]
    public void ADriftThatLeftTheBand_IsRaisedToWarning()
    {
        var measurements = Series(
            (4, 6.05, null), (3, 6.15, null), (2, 6.25, null), (1, 6.35, null), (0, 6.45, null));

        var drift = Assert.Single(TrendWatchService.Evaluate(measurements, Targets, Now), f => f.Code == "trend.ph.drift");

        Assert.Equal(TrendSeverity.Warning, drift.Severity);
        Assert.DoesNotContain("noch im Zielbereich", drift.Detail);
    }

    [Fact]
    public void NormalWobble_IsNotADrift()
    {
        // Up, down, up: this is what a healthy reservoir looks like. Reporting it would
        // train the user to ignore the guard.
        var measurements = Series(
            (4, 5.90, null), (3, 6.05, null), (2, 5.95, null), (1, 6.10, null), (0, 6.00, null));

        Assert.DoesNotContain(TrendWatchService.Evaluate(measurements, Targets, Now), f => f.Code == "trend.ph.drift");
    }

    [Fact]
    public void AMoveTooSmallToMatter_IsIgnored()
    {
        // Monotonic, but 0.08 total over four days is measurement resolution, not a trend.
        var measurements = Series(
            (3, 5.96, null), (2, 5.98, null), (1, 6.00, null), (0, 6.04, null));

        Assert.DoesNotContain(TrendWatchService.Evaluate(measurements, Targets, Now), f => f.Code == "trend.ph.drift");
    }

    [Fact]
    public void TooFewDays_IsNotYetATrend()
    {
        var measurements = Series((2, 5.8, null), (1, 6.0, null), (0, 6.3, null));

        Assert.DoesNotContain(TrendWatchService.Evaluate(measurements, Targets, Now), f => f.Code == "trend.ph.drift");
    }

    [Fact]
    public void SeveralReadingsOnOneDay_CountAsOneDay()
    {
        // Otherwise a busy afternoon of measuring would look like a multi-day trend.
        var measurements = new List<Measurement>
        {
            new() { TakenAt = Now.Date.AddHours(8), ReservoirPh = 5.9 },
            new() { TakenAt = Now.Date.AddHours(12), ReservoirPh = 6.1 },
            new() { TakenAt = Now.Date.AddHours(16), ReservoirPh = 6.3 },
            new() { TakenAt = Now.Date.AddHours(20), ReservoirPh = 6.5 },
        };

        Assert.Empty(TrendWatchService.Evaluate(measurements, Targets, Now));
    }

    [Fact]
    public void EcFallingSteadily_PointsAtTheHungryRule()
    {
        var measurements = Series(
            (4, 1.50, 1.50), (3, null, 1.38), (2, null, 1.26), (1, null, 1.14), (0, null, 1.05));

        var drift = Assert.Single(TrendWatchService.Evaluate(measurements, Targets, Now), f => f.Code == "trend.ec.drift");

        Assert.Contains("fällt", drift.Detail);
        Assert.Equal("ec-keep-hungry", drift.GuidanceId);
    }

    [Fact]
    public void AnOverdueWaterChange_IsReported()
    {
        var measurements = new List<Measurement>
        {
            new() { TakenAt = Now.Date.AddDays(-11).AddHours(9), SolutionChange = true },
            new() { TakenAt = Now.Date.AddHours(9), ReservoirPh = 6.0 },
        };

        var finding = Assert.Single(TrendWatchService.Evaluate(measurements, null, Now), f => f.Code == "trend.waterchange.overdue");

        Assert.Equal(TrendSeverity.Warning, finding.Severity);
        Assert.Equal("weekly-water-change", finding.GuidanceId);
    }

    [Fact]
    public void AWaterChangeWithinTheWeek_IsFine()
    {
        var measurements = new List<Measurement>
        {
            new() { TakenAt = Now.Date.AddDays(-3), SolutionChange = true },
        };

        Assert.Empty(TrendWatchService.Evaluate(measurements, null, Now));
    }

    [Fact]
    public void NeverHavingLoggedAWaterChange_IsNotTreatedAsOverdue()
    {
        // Plenty of people don't tick the box. Telling them they are overdue would be a
        // guess dressed up as a fact.
        var measurements = Series((3, 6.0, null), (2, 6.0, null), (1, 6.0, null), (0, 6.0, null));

        Assert.DoesNotContain(TrendWatchService.Evaluate(measurements, null, Now), f => f.Code.StartsWith("trend.waterchange"));
    }

    [Fact]
    public void CollapsingConsumption_IsReported()
    {
        var measurements = Consumption(4.0, 4.2, 3.8, 1.2, 1.0);

        var finding = Assert.Single(TrendWatchService.Evaluate(measurements, null, Now), f => f.Code == "trend.consumption.drop");

        Assert.Equal(TrendSeverity.Warning, finding.Severity);
        Assert.Contains("Wurzeln", finding.Detail);
    }

    [Fact]
    public void ConsumptionDoubling_ReadsAsAPossibleLeak()
    {
        var measurements = Consumption(1.0, 1.2, 1.1, 4.0, 4.4);

        var finding = Assert.Single(TrendWatchService.Evaluate(measurements, null, Now), f => f.Code == "trend.consumption.spike");

        Assert.Contains("Leck", finding.Detail);
    }

    [Fact]
    public void GentlyRisingConsumption_IsJustGrowth()
    {
        Assert.Empty(TrendWatchService.Evaluate(Consumption(2.0, 2.2, 2.4, 2.6, 2.9), null, Now));
    }

    [Fact]
    public void NoMeasurementsAtAll_ProducesNothing()
    {
        Assert.Empty(TrendWatchService.Evaluate([], Targets, Now));
    }

    [Fact]
    public void ReadingsOlderThanTheWindow_AreIgnored()
    {
        var measurements = Series(
            (30, 5.5, null), (29, 5.7, null), (28, 5.9, null), (27, 6.4, null));

        Assert.Empty(TrendWatchService.Evaluate(measurements, Targets, Now));
    }

    private static List<Measurement> Consumption(params double[] litersOldestFirst) =>
        litersOldestFirst
            .Select((liters, index) => new Measurement
            {
                TakenAt = Now.Date.AddDays(-(litersOldestFirst.Length - 1 - index)).AddHours(9),
                TopOffLiters = liters,
            })
            .ToList();
}
