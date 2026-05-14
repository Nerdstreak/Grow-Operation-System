using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using GrowDiary.Web.ViewModels.Live;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests;

public sealed class GrowAlertServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _tempRoot;
    private readonly GrowRepository _repository;
    private readonly GrowAlertService _service;

    public GrowAlertServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"grow-alert-test-{Guid.NewGuid():N}.db");
        _tempRoot = Path.Combine(Path.GetTempPath(), "GrowAlertServiceTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        CopyDefaults(Path.Combine(FindProjectRoot(), "GrowDiary.Web", "wwwroot", "knowledge-defaults"), _tempRoot);
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", _dbPath);

        var paths = new AppPaths(_tempRoot);
        GrowDiary.Web.Tests.TestDatabase.InitializeWithDefaultTent(paths);
        _repository = new GrowRepository(paths);

        var loader = new KnowledgeBaseLoader(paths, NullLogger<KnowledgeBaseLoader>.Instance);
        loader.Initialize();
        var recommendationEngine = new RecommendationEngine(
            new CultivationKnowledgeService(loader),
            new MeasurementSanityService());
        var deviationAnalyzer = new DeviationAnalyzerService(new TargetValueService(loader));
        var treatmentRecommender = new TreatmentRecommender(loader);
        _service = new GrowAlertService(_repository, recommendationEngine, deviationAnalyzer, treatmentRecommender);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void ResolveStateTone_GibtCriticalBeiDanger()
    {
        var alerts = new[]
        {
            new RecommendationCard { Severity = "info" },
            new RecommendationCard { Severity = "danger" }
        };

        var tone = GrowAlertService.ResolveStateTone(alerts, homeAssistantConfigured: true);

        Assert.Equal("critical", tone);
    }

    [Fact]
    public void ResolveStateTone_GibtHealthyOhneWarnungenUndMitHa()
    {
        var alerts = new[]
        {
            new RecommendationCard { Severity = "success" }
        };

        var tone = GrowAlertService.ResolveStateTone(alerts, homeAssistantConfigured: true);

        Assert.Equal("healthy", tone);
    }

    [Fact]
    public void ResolveStateToneFromDeviations_GibtCriticalBeiCriticalDeviation()
    {
        var deviations = new[]
        {
            new GrowDeviation { Severity = DeviationSeverity.Critical }
        };

        var tone = GrowAlertService.ResolveStateToneFromDeviations(deviations, homeAssistantConfigured: true);

        Assert.Equal("critical", tone);
    }

    [Fact]
    public void ResolveStateToneFromDeviations_GibtWarningBeiWarningDeviation()
    {
        var deviations = new[]
        {
            new GrowDeviation { Severity = DeviationSeverity.Warning }
        };

        var tone = GrowAlertService.ResolveStateToneFromDeviations(deviations, homeAssistantConfigured: true);

        Assert.Equal("attention", tone);
    }

    [Fact]
    public void ResolveStateToneFromDeviations_GibtHealthyOhneDeviations()
    {
        var tone = GrowAlertService.ResolveStateToneFromDeviations(Array.Empty<GrowDeviation>(), homeAssistantConfigured: true);

        Assert.Equal("healthy", tone);
    }

    [Fact]
    public void BuildAlertsForGrow_D1CriticalBleibtCritical()
    {
        var grow = CreateHydroGrow();
        AddMeasurement(grow.Id, DateTime.UtcNow, measurement => measurement.ReservoirPh = 6.7);

        var alerts = _service.BuildAlertsForGrow(grow);
        var tone = GrowAlertService.ResolveStateTone(alerts, homeAssistantConfigured: true);

        Assert.Contains(alerts, alert => alert.Severity == "danger" && alert.Title.Contains("Ph", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("critical", tone);
    }

    [Fact]
    public void BuildAlertsForGrow_ErgaenztLegacyWarningWennD1KeineDeviationFindet()
    {
        var grow = CreateHydroGrow();
        AddMeasurement(grow.Id, DateTime.UtcNow, measurement => measurement.ReservoirWaterTempC = 17.5);

        var alerts = _service.BuildAlertsForGrow(grow);
        var tone = GrowAlertService.ResolveStateTone(alerts, homeAssistantConfigured: true);

        Assert.Contains(alerts, alert => alert.Severity == "warning" && alert.Title.Contains("Wassertemperatur"));
        Assert.DoesNotContain(alerts, alert => alert.Severity == "success");
        Assert.Equal("attention", tone);
    }

    [Fact]
    public void BuildAlertsForGrow_OhneD1UndLegacyWarnungBleibtHealthy()
    {
        var grow = CreateHydroGrow();
        AddMeasurement(grow.Id, DateTime.UtcNow, measurement =>
        {
            measurement.ReservoirPh = 6.0;
            measurement.ReservoirEc = 0.7;
            measurement.ReservoirWaterTempC = 20;
            measurement.DissolvedOxygenMgL = 8;
        });

        var alerts = _service.BuildAlertsForGrow(grow);
        var tone = GrowAlertService.ResolveStateTone(alerts, homeAssistantConfigured: true);

        Assert.DoesNotContain(alerts, alert => alert.Severity is "danger" or "warning");
        Assert.Equal("healthy", tone);
    }

    [Fact]
    public void BuildAlertsForGrow_DedupliziertAehnlicheD1UndLegacyPhWarning()
    {
        var grow = CreateHydroGrow();
        AddMeasurement(grow.Id, DateTime.UtcNow, measurement => measurement.ReservoirPh = 6.3);

        var alerts = _service.BuildAlertsForGrow(grow);

        Assert.Single(alerts, alert => alert.Title.Contains("pH", StringComparison.OrdinalIgnoreCase) || alert.Title.Contains("Ph", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildAlertsForGrow_ErhaeltLegacySolutionChangeHinweis()
    {
        var grow = CreateHydroGrow();
        AddMeasurement(grow.Id, DateTime.UtcNow.AddDays(-11), measurement =>
        {
            measurement.SolutionChange = true;
            measurement.ReservoirPh = 6.0;
            measurement.ReservoirEc = 0.7;
            measurement.ReservoirWaterTempC = 20;
            measurement.DissolvedOxygenMgL = 8;
        });
        AddMeasurement(grow.Id, DateTime.UtcNow, measurement =>
        {
            measurement.ReservoirPh = 6.0;
            measurement.ReservoirEc = 0.7;
            measurement.ReservoirWaterTempC = 20;
            measurement.DissolvedOxygenMgL = 8;
        });

        var alerts = _service.BuildAlertsForGrow(grow);

        Assert.Contains(alerts, alert => alert.Title.Contains("wechsel", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("critical", "kritisch")]
    [InlineData("attention", "beobachten")]
    [InlineData("healthy", "stabil")]
    [InlineData("neutral", "neutral")]
    public void ResolveStateLabel_MapptBekannteTones(string tone, string expectedLabel)
    {
        var label = GrowAlertService.ResolveStateLabel(tone);

        Assert.Equal(expectedLabel, label);
    }

    [Fact]
    public void ToPayload_UebernimmtMetricCardFelder()
    {
        var metric = new MetricCard
        {
            Key = "air-temp",
            Label = "Lufttemperatur",
            Value = "24.1",
            Unit = "C",
            Tone = "ok",
            Hint = "im Zielbereich"
        };

        var payload = metric.ToPayload();

        Assert.Equal(metric.Key, payload.Key);
        Assert.Equal(metric.Label, payload.Label);
        Assert.Equal(metric.Value, payload.Value);
        Assert.Equal(metric.Unit, payload.Unit);
        Assert.Equal(metric.Tone, payload.Tone);
        Assert.Equal(metric.Hint, payload.Hint);
    }

    private GrowRun CreateHydroGrow()
    {
        var grow = new GrowRun
        {
            Name = "Hydro",
            MediumType = MediumType.Hydro,
            IrrigationType = IrrigationType.ActiveHydro,
            HydroStyle = HydroStyle.RDWC,
            Status = GrowStatus.Running,
            StartDate = DateTime.Today.AddDays(-14)
        };
        grow.Id = _repository.CreateGrow(grow);
        return _repository.GetGrow(grow.Id)!;
    }

    private void AddMeasurement(int growId, DateTime takenAt, Action<Measurement> configure)
    {
        var measurement = new Measurement
        {
            GrowId = growId,
            Stage = GrowStage.Veg,
            TakenAt = takenAt
        };
        configure(measurement);
        _repository.CreateMeasurement(measurement);
    }

    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.sln").Length > 0 ||
                Directory.Exists(Path.Combine(dir, "GrowDiary.Web")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Project root not found");
    }

    private static void CopyDefaults(string source, string tempRoot)
    {
        var dest = Path.Combine(tempRoot, "wwwroot", "knowledge-defaults");
        foreach (var file in Directory.EnumerateFiles(source, "*.json", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, file);
            var target = Path.Combine(dest, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
