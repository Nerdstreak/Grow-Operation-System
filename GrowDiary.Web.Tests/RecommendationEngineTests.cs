using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Xunit;

namespace GrowDiary.Web.Tests;

public sealed class RecommendationEngineTests
{
    private static readonly RecommendationEngine Engine = new(
        new CultivationKnowledgeService(),
        new MeasurementSanityService());

    private static GrowRun CreateHydroGrow(HydroStyle style = HydroStyle.RDWC) => new()
    {
        Name = "Test Grow",
        MediumType = MediumType.Hydro,
        IrrigationType = IrrigationType.ActiveHydro,
        HydroStyle = style,
        Status = GrowStatus.Running,
        StartDate = DateTime.Today.AddDays(-14)
    };

    private static Measurement CreateMeasurement(GrowStage stage) => new()
    {
        Stage = stage,
        TakenAt = DateTime.Now
    };

    [Fact]
    public void KeineMessung_GibtInfoKarte()
    {
        var grow = CreateHydroGrow();

        var result = Engine.Evaluate(grow, null, null, null);

        Assert.Single(result);
        Assert.Equal("info", result[0].Severity);
    }

    [Fact]
    public void PhZuHoch_GibtWarning()
    {
        var grow = CreateHydroGrow();
        var m = CreateMeasurement(GrowStage.Veg);
        m.ReservoirPh = 6.4;

        var result = Engine.Evaluate(grow, m, null, null);

        Assert.Contains(result, c => c.Severity is "warning" or "danger");
    }

    [Fact]
    public void PhKritisch_GibtDanger()
    {
        var grow = CreateHydroGrow();
        var m = CreateMeasurement(GrowStage.Veg);
        m.ReservoirPh = 7.0;

        var result = Engine.Evaluate(grow, m, null, null);

        Assert.Contains(result, c => c.Severity == "danger");
    }

    [Fact]
    public void PhImBereich_KeinePhKarte()
    {
        var grow = CreateHydroGrow();
        var m = CreateMeasurement(GrowStage.Veg);
        m.ReservoirPh = 6.0;

        var result = Engine.Evaluate(grow, m, null, null);

        Assert.DoesNotContain(result, c => c.Title.Contains("pH"));
    }

    [Fact]
    public void WasserTempKritisch_GibtDanger()
    {
        var grow = CreateHydroGrow();
        var m = CreateMeasurement(GrowStage.Veg);
        m.ReservoirWaterTempC = 25.0;

        var result = Engine.Evaluate(grow, m, null, null);

        Assert.Contains(result, c => c.Severity == "danger");
    }

    [Fact]
    public void WasserTempErhoht_GibtWarning()
    {
        var grow = CreateHydroGrow();
        var m = CreateMeasurement(GrowStage.Veg);
        m.ReservoirWaterTempC = 22.5;

        var result = Engine.Evaluate(grow, m, null, null);

        Assert.Contains(result, c => c.Severity == "warning");
    }

    [Fact]
    public void DoNiedrig_GibtWarning()
    {
        var grow = CreateHydroGrow();
        var m = CreateMeasurement(GrowStage.Veg);
        m.DissolvedOxygenMgL = 6.8;

        var result = Engine.Evaluate(grow, m, null, null);

        Assert.Contains(result, c => c.Severity == "warning");
    }

    [Fact]
    public void DoKritisch_GibtDanger()
    {
        var grow = CreateHydroGrow();
        var m = CreateMeasurement(GrowStage.Veg);
        m.DissolvedOxygenMgL = 5.5;

        var result = Engine.Evaluate(grow, m, null, null);

        Assert.Contains(result, c => c.Severity == "danger");
    }

    [Fact]
    public void OrpImFenster_GibtSuccess()
    {
        var grow = CreateHydroGrow();
        var m = CreateMeasurement(GrowStage.Veg);
        m.OrpMv = 390;

        var result = Engine.Evaluate(grow, m, null, null);

        Assert.Contains(result, c => c.Severity == "success");
    }

    [Fact]
    public void OrpZuNiedrig_GibtWarning()
    {
        var grow = CreateHydroGrow();
        var m = CreateMeasurement(GrowStage.Veg);
        m.OrpMv = 280;

        var result = Engine.Evaluate(grow, m, null, null);

        Assert.Contains(result, c => c.Severity is "warning" or "danger");
    }

    [Fact]
    public void WasserTempUndDoKombination_GibtDanger()
    {
        var grow = CreateHydroGrow();
        var m = CreateMeasurement(GrowStage.Veg);
        m.ReservoirWaterTempC = 23.0;
        m.DissolvedOxygenMgL = 6.5;

        var result = Engine.Evaluate(grow, m, null, null);

        Assert.Contains(result, c =>
            c.Severity == "danger" &&
            c.Title.Contains("Root-Zone"));
    }

    [Fact]
    public void AlleWerteOk_GibtKeineDangerKarte()
    {
        var grow = CreateHydroGrow();
        var m = CreateMeasurement(GrowStage.Veg);
        m.ReservoirPh = 5.9;
        m.ReservoirWaterTempC = 19.0;
        m.DissolvedOxygenMgL = 8.0;
        m.OrpMv = 380;
        m.ReservoirEc = 1.0;

        var result = Engine.Evaluate(grow, m, null, null);

        Assert.DoesNotContain(result, c => c.Severity == "danger");
    }
}
