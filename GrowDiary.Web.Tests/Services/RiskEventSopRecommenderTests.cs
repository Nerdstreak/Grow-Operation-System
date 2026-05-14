using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services;

public sealed class RiskEventSopRecommenderTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly GrowRepository _repository;
    private readonly KnowledgeBaseLoader _knowledgeBase;
    private readonly RiskEventSopRecommender _recommender;

    public RiskEventSopRecommenderTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-risk-sop-rec-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        CopyDefaults(Path.Combine(FindProjectRoot(), "GrowDiary.Web", "wwwroot", "knowledge-defaults"), _contentRoot);
        var paths = new AppPaths(_contentRoot);
        GrowDiary.Web.Tests.TestDatabase.InitializeWithDefaultTent(paths);
        _repository = new GrowRepository(paths);
        _knowledgeBase = new KnowledgeBaseLoader(paths, NullLogger<KnowledgeBaseLoader>.Instance);
        _knowledgeBase.Initialize();
        _recommender = new RiskEventSopRecommender(_knowledgeBase, _repository);
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    [Theory]
    [InlineData(RiskEventType.PowerOutage)]
    [InlineData(RiskEventType.UpsOnBattery)]
    [InlineData(RiskEventType.PumpOffline)]
    [InlineData(RiskEventType.CriticalDo)]
    public void Recommend_EmergencyPowerRecovery_ForEmergencyRiskTypes(RiskEventType eventType)
    {
        var risk = new RiskEvent
        {
            Id = 42,
            EventType = eventType,
            Severity = RiskEventSeverity.Critical,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Manual,
            Title = "Risk"
        };

        var recommendation = _recommender.Recommend(risk).Single();

        Assert.Equal(42, recommendation.RiskEventId);
        Assert.Equal("emergency-power-recovery", recommendation.SopId);
        Assert.Equal("High", recommendation.Confidence);
        Assert.False(recommendation.AlreadyActive);
    }

    [Fact]
    public void Recommend_DoesNotCreateFakeSop_ForLightMismatch()
    {
        var risk = new RiskEvent
        {
            Id = 1,
            EventType = RiskEventType.LightMismatch,
            Severity = RiskEventSeverity.Critical,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Manual,
            Title = "Licht weicht ab"
        };

        Assert.Empty(_recommender.Recommend(risk));
    }

    [Fact]
    public void Recommend_MarksAlreadyActive_WhenSameSopActiveForGrow()
    {
        var growId = _repository.CreateGrow(new GrowRun
        {
            Name = "Risk SOP Grow",
            StartDate = new DateTime(2026, 8, 1),
            Status = GrowStatus.Running
        });
        var sop = _knowledgeBase.Sops.Single(item => item.Id == "emergency-power-recovery");
        var instance = _repository.StartSopInstance(growId, sop, SopStartSource.Manual, null, null, null);

        var risk = new RiskEvent
        {
            Id = 2,
            EventType = RiskEventType.PowerOutage,
            Severity = RiskEventSeverity.Warning,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Manual,
            Title = "Power",
            GrowId = growId
        };

        var recommendation = _recommender.Recommend(risk).Single();

        Assert.True(recommendation.AlreadyActive);
        Assert.Equal(instance.Id, recommendation.ActiveSopInstanceId);
        Assert.Equal("Medium", recommendation.Confidence);
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
