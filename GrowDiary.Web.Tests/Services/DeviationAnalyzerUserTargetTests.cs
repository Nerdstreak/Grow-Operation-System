using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Trägt jemand eigene Grenzwerte ein, dann gelten die — auch in der Diagnose.
/// </summary>
/// <remarks>
/// Das war lange kaputt und niemandem aufgefallen: der Analysedienst nahm die
/// Regeln im Konstruktor entgegen und legte sie nicht ab. Das Feld blieb null,
/// die Überlagerung lief ins Leere, und die Diagnose las weiter nur das
/// mitgelieferte Wissen — während die Alarme schon die Werte des Nutzers nahmen.
/// Zwei Seiten derselben App mit zwei Meinungen über denselben Messwert.
///
/// Der Test greift deshalb nicht die Rechnung ab, sondern die Verdrahtung: eine
/// Regel, die dem Wissen widerspricht, muss das Ergebnis kippen.
/// </remarks>
public sealed class DeviationAnalyzerUserTargetTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly AppPaths _paths;
    private readonly Tent _tent;
    private readonly AlertRuleRepository _rules;

    public DeviationAnalyzerUserTargetTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-devtarget-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _paths = new AppPaths(_contentRoot);
        _tent = TestDatabase.InitializeWithDefaultTent(_paths);
        _rules = new AlertRuleRepository(_paths);
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    private DeviationAnalyzerService Analyzer()
        => new(TestKnowledgeBase.TargetValues(), _rules);

    private GrowRun Grow() => new()
    {
        Id = 1,
        Name = "Test",
        TentId = _tent.Id,
        MediumType = MediumType.Hydro,
        IrrigationType = IrrigationType.ActiveHydro,
        HydroStyle = HydroStyle.RDWC,
    };

    private static Measurement WithPh(double ph) => new()
    {
        Id = 1,
        GrowId = 1,
        Stage = GrowStage.Veg,
        TakenAt = DateTime.Now,
        ReservoirPh = ph,
        ReservoirEc = 0.7,
        ReservoirWaterTempC = 20,
        AirTemperatureC = 24,
        HumidityPercent = 60,
    };

    [Fact]
    public void WithoutOwnRules_TheShippedKnowledgeDecides()
    {
        // pH 6,05 liegt im mitgelieferten Veg-Band — es gibt nichts zu melden.
        var abweichungen = Analyzer().Analyze(Grow(), new[] { WithPh(6.05) });

        Assert.DoesNotContain(abweichungen, deviation => deviation.Metric == DeviationMetric.Ph);
    }

    [Fact]
    public void AnOwnRule_MovesTheBandAndChangesTheVerdict()
    {
        // Jemand fährt bewusst saurer und trägt 5,6–5,9 ein. Derselbe Messwert,
        // der eben noch in Ordnung war, liegt jetzt darüber.
        _rules.ReplaceForTent(_tent.Id, new[]
        {
            new TentAlertRule { TentId = _tent.Id, MetricKey = "reservoir-ph", MinValue = 5.6, MaxValue = 5.9, Enabled = true },
        });

        var abweichungen = Analyzer().Analyze(Grow(), new[] { WithPh(6.05) });

        Assert.Contains(abweichungen, deviation => deviation.Metric == DeviationMetric.Ph);
    }

    [Fact]
    public void ADisabledRule_DoesNotCount()
    {
        // Ausgeschaltet heisst ausgeschaltet — sonst wirkt eine Regel weiter,
        // die auf der Alarmseite sichtbar aus ist.
        _rules.ReplaceForTent(_tent.Id, new[]
        {
            new TentAlertRule { TentId = _tent.Id, MetricKey = "reservoir-ph", MinValue = 5.6, MaxValue = 5.9, Enabled = false },
        });

        var abweichungen = Analyzer().Analyze(Grow(), new[] { WithPh(6.05) });

        Assert.DoesNotContain(abweichungen, deviation => deviation.Metric == DeviationMetric.Ph);
    }
}
