using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Xunit;

namespace GrowDiary.Web.Tests;

public sealed class DeviationAnalyzerServiceTests
{
    private static readonly DeviationAnalyzerService Svc = new();

    private static GrowRun CreateHydroGrow() => new()
    {
        Name = "Test",
        MediumType = MediumType.Hydro,
        IrrigationType = IrrigationType.ActiveHydro,
        HydroStyle = HydroStyle.RDWC
    };

    private static Measurement CreateMeasurement(GrowStage stage) => new()
    {
        Stage = stage,
        TakenAt = DateTime.Now,
        // Veg OK-Bereich: pH 6.0–6.1, EC 0.6–0.8
        ReservoirPh = 6.05,
        ReservoirEc = 0.7,
        ReservoirWaterTempC = 20.0,
        DissolvedOxygenMgL = 8.0
    };

    [Fact]
    public void EineMessungOhneAuffaelligkeiten_GibtLeersteListe()
    {
        var grow = CreateHydroGrow();
        var measurements = new List<Measurement> { CreateMeasurement(GrowStage.Veg) };

        var result = Svc.Analyze(grow, measurements);

        Assert.Empty(result);
    }

    [Fact]
    public void KeineMEssungen_GibtLeersteListe()
    {
        var grow = CreateHydroGrow();

        var result = Svc.Analyze(grow, new List<Measurement>());

        Assert.Empty(result);
    }

    [Fact]
    public void Ph_ZuHoch_Warning()
    {
        var grow = CreateHydroGrow();
        var m = CreateMeasurement(GrowStage.Veg);
        // Veg-Max = 6.1; Critical ab PhMax + 0.3 = 6.4 → 6.3 ist Warning
        m.ReservoirPh = 6.3;

        var result = Svc.Analyze(grow, new List<Measurement> { m });

        var dev = Assert.Single(result.Where(d => d.Metric == DeviationMetric.Ph));
        Assert.Equal(DeviationSeverity.Warning, dev.Severity);
    }

    [Fact]
    public void Ph_ZuHoch_DreiMessungen_Critical()
    {
        var grow = CreateHydroGrow();
        var measurements = Enumerable.Range(0, 3).Select(i =>
        {
            var m = CreateMeasurement(GrowStage.Veg);
            m.ReservoirPh = 6.5;
            m.TakenAt = DateTime.Now.AddHours(-i);
            return m;
        }).ToList();

        var result = Svc.Analyze(grow, measurements);

        var dev = Assert.Single(result.Where(d => d.Metric == DeviationMetric.Ph));
        Assert.Equal(DeviationSeverity.Critical, dev.Severity);
        Assert.Equal(3, dev.ConsecutiveCount);
    }

    [Fact]
    public void Ph_ImBereich_KeineDeviation()
    {
        var grow = CreateHydroGrow();
        var m = CreateMeasurement(GrowStage.Veg);
        m.ReservoirPh = 6.0; // Veg-Bereich 6.0–6.1

        var result = Svc.Analyze(grow, new List<Measurement> { m });

        Assert.DoesNotContain(result, d => d.Metric == DeviationMetric.Ph);
    }

    [Fact]
    public void EC_Gefallen_Warning()
    {
        var grow = CreateHydroGrow();
        var m1 = CreateMeasurement(GrowStage.Veg);
        m1.ReservoirEc = 0.6;
        m1.TakenAt = DateTime.Now;

        var m2 = CreateMeasurement(GrowStage.Veg);
        m2.ReservoirEc = 0.9;
        m2.TakenAt = DateTime.Now.AddHours(-1);

        var result = Svc.Analyze(grow, new List<Measurement> { m1, m2 });

        var dev = Assert.Single(result.Where(d => d.Metric == DeviationMetric.Ec));
        Assert.Contains("gefallen", dev.Recommendation);
    }

    [Fact]
    public void EC_Gestiegen_Warning()
    {
        var grow = CreateHydroGrow();
        var m1 = CreateMeasurement(GrowStage.Veg);
        m1.ReservoirEc = 1.2;
        m1.TakenAt = DateTime.Now;

        var m2 = CreateMeasurement(GrowStage.Veg);
        m2.ReservoirEc = 0.9;
        m2.TakenAt = DateTime.Now.AddHours(-1);

        var result = Svc.Analyze(grow, new List<Measurement> { m1, m2 });

        var dev = Assert.Single(result.Where(d => d.Metric == DeviationMetric.Ec));
        Assert.Contains("gestiegen", dev.Recommendation);
    }

    [Fact]
    public void WasserTemp_Kritisch_Critical()
    {
        var grow = CreateHydroGrow();
        var m = CreateMeasurement(GrowStage.Veg);
        m.ReservoirWaterTempC = 25.0; // über 24°C Critical-Schwelle

        var result = Svc.Analyze(grow, new List<Measurement> { m });

        var dev = Assert.Single(result.Where(d => d.Metric == DeviationMetric.WaterTemp));
        Assert.Equal(DeviationSeverity.Critical, dev.Severity);
    }

    [Fact]
    public void DO_Niedrig_Warning()
    {
        var grow = CreateHydroGrow();
        var m = CreateMeasurement(GrowStage.Veg);
        m.DissolvedOxygenMgL = 6.5; // unter 7.0 Warning-Schwelle

        var result = Svc.Analyze(grow, new List<Measurement> { m });

        Assert.Contains(result, d => d.Metric == DeviationMetric.DissolvedOxygen);
    }

    [Fact]
    public void DO_SehrNiedrig_Critical()
    {
        var grow = CreateHydroGrow();
        var m = CreateMeasurement(GrowStage.Veg);
        m.DissolvedOxygenMgL = 4.8; // unter 5.0 Critical-Schwelle

        var result = Svc.Analyze(grow, new List<Measurement> { m });

        var dev = Assert.Single(result.Where(d => d.Metric == DeviationMetric.DissolvedOxygen));
        Assert.Equal(DeviationSeverity.Critical, dev.Severity);
    }

    [Fact]
    public void AlleWerteOK_KeinDeviations()
    {
        var grow = CreateHydroGrow();
        var m = CreateMeasurement(GrowStage.Veg);
        // Alle Werte sicher im Veg-Bereich: pH 6.05, EC 0.7, Temp 20, DO 8.0

        var result = Svc.Analyze(grow, new List<Measurement> { m });

        Assert.Empty(result);
    }
}
