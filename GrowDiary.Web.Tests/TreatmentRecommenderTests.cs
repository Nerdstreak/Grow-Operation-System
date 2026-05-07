using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests;

public sealed class TreatmentRecommenderTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly TreatmentRecommender _recommender;

    public TreatmentRecommenderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "TreatmentRecommenderTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        var projectRoot = FindProjectRoot();
        var defaultsSource = Path.Combine(projectRoot, "GrowDiary.Web", "wwwroot", "knowledge-defaults");
        CopyDefaults(defaultsSource, _tempRoot);

        var paths = new AppPaths(_tempRoot);
        var loader = new KnowledgeBaseLoader(paths, NullLogger<KnowledgeBaseLoader>.Instance);
        loader.Initialize();

        _recommender = new TreatmentRecommender(loader);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void PassendeSymptomId_ErzeugtTreatmentEmpfehlung()
    {
        var grow = CreateHydroGrow();
        var deviation = CreateDeviation(DeviationMetric.Ph, actualValue: 6.4, targetMin: 6.0, targetMax: 6.1);

        var result = _recommender.Recommend(grow, new[] { deviation });

        var recommendation = Assert.Single(result.Recommendations, item => item.TreatmentId == "ph-correction-down");
        Assert.Equal(grow.Id, result.GrowId);
        Assert.Equal("ph-too-high", recommendation.SymptomId);
        Assert.Equal("pH Korrektur nach unten (pH-Minus)", recommendation.TreatmentName);
        Assert.Equal(TreatmentRecommendationConfidence.Medium, recommendation.Confidence);
        Assert.Contains("pH-Sensor", recommendation.HardwareRequirements);
        Assert.NotEmpty(recommendation.SourceDocumentIds);
    }

    [Fact]
    public void SuggestedSopIds_WerdenZuSopEmpfehlungenAufgeloest()
    {
        var grow = CreateHydroGrow();
        var deviation = CreateDeviation(DeviationMetric.DissolvedOxygen, actualValue: 3.2, targetMin: 6, targetMax: null, severity: DeviationSeverity.Critical);

        var result = _recommender.Recommend(grow, new[] { deviation });

        var sopRecommendation = Assert.Single(result.Recommendations, item => item.SopId == "emergency-power-recovery");
        Assert.Equal("Notfall-Recovery nach Stromausfall", sopRecommendation.SopTitle);
        Assert.Equal(TreatmentRecommendationConfidence.High, sopRecommendation.Confidence);
    }

    [Fact]
    public void OhnePassendeSymptomId_CrashtNichtUndErzeugtKeineFakeId()
    {
        var grow = CreateHydroGrow();
        var deviation = CreateDeviation(DeviationMetric.Co2, actualValue: 2600, targetMin: null, targetMax: 1600, severity: DeviationSeverity.Critical);

        var result = _recommender.Recommend(grow, new[] { deviation });

        var recommendation = Assert.Single(result.Recommendations);
        Assert.Null(recommendation.SymptomId);
        Assert.Null(recommendation.TreatmentId);
        Assert.Null(recommendation.SopId);
        Assert.Contains("Keine passende Knowledge-Symptom-ID", recommendation.Reason);
    }

    [Fact]
    public void CriticalDeviation_ErgibtHighConfidence()
    {
        var grow = CreateHydroGrow();
        var deviation = CreateDeviation(DeviationMetric.Ph, actualValue: 6.7, targetMin: 6.0, targetMax: 6.1, severity: DeviationSeverity.Critical);

        var result = _recommender.Recommend(grow, new[] { deviation });

        Assert.All(result.Recommendations, item => Assert.Equal(TreatmentRecommendationConfidence.High, item.Confidence));
    }

    [Fact]
    public void ConsecutiveCountDrei_ErhoehtConfidence()
    {
        var grow = CreateHydroGrow();
        var deviation = CreateDeviation(DeviationMetric.Ph, actualValue: 6.3, targetMin: 6.0, targetMax: 6.1);
        deviation.ConsecutiveCount = 3;

        var result = _recommender.Recommend(grow, new[] { deviation });

        Assert.All(result.Recommendations, item => Assert.Equal(TreatmentRecommendationConfidence.High, item.Confidence));
    }

    [Fact]
    public void TreatmentKonflikteUndHardwareRequirements_WerdenAlsHinweiseUebernommen()
    {
        var grow = CreateHydroGrow();
        var deviation = CreateDeviation(DeviationMetric.DissolvedOxygen, actualValue: 3.1, targetMin: 6, targetMax: null, severity: DeviationSeverity.Critical);

        var result = _recommender.Recommend(grow, new[] { deviation });

        var recommendation = Assert.Single(result.Recommendations, item => item.TreatmentId == "h2o2-do-emergency");
        Assert.Contains("hocl-orp-boost-standard", recommendation.ConflictTreatmentIds);
        Assert.Contains("DO-Sensor", recommendation.HardwareRequirements);
        Assert.Contains(recommendation.SafetyNotes, note => note.Contains("Konflikt", StringComparison.OrdinalIgnoreCase));
        Assert.Null(recommendation.PhaseAllowed);
    }

    private static GrowRun CreateHydroGrow() => new()
    {
        Id = 42,
        Name = "Hydro",
        MediumType = MediumType.Hydro,
        IrrigationType = IrrigationType.ActiveHydro,
        HydroStyle = HydroStyle.RDWC
    };

    private static GrowDeviation CreateDeviation(
        DeviationMetric metric,
        double actualValue,
        double? targetMin,
        double? targetMax,
        DeviationSeverity severity = DeviationSeverity.Warning)
        => new()
        {
            GrowId = 42,
            StableKey = $"test.{metric}",
            Metric = metric,
            Severity = severity,
            ActualValue = actualValue,
            TargetMin = targetMin,
            TargetMax = targetMax,
            Unit = "x",
            Message = "Test-Deviation",
            ConsecutiveCount = 1
        };

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
