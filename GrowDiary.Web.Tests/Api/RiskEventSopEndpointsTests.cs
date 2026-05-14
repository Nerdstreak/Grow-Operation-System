using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

public sealed class RiskEventSopEndpointsTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly GrowRepository _repository;
    private readonly RiskEventsApiController _controller;

    public RiskEventSopEndpointsTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-risk-sop-api-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        CopyDefaults(Path.Combine(FindProjectRoot(), "GrowDiary.Web", "wwwroot", "knowledge-defaults"), _contentRoot);
        var paths = new AppPaths(_contentRoot);
        GrowDiary.Web.Tests.TestDatabase.InitializeWithDefaultTent(paths);
        _repository = new GrowRepository(paths);
        var taskRepository = new TaskRepository(paths);
        var knowledgeBase = new KnowledgeBaseLoader(paths, NullLogger<KnowledgeBaseLoader>.Instance);
        knowledgeBase.Initialize();
        var recommender = new RiskEventSopRecommender(knowledgeBase, _repository);
        _controller = new RiskEventsApiController(_repository, taskRepository, knowledgeBase, recommender);
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Recommendations_ReturnEmergencySopAndNotFoundForMissingRiskEvent()
    {
        var growId = CreateGrow();
        var risk = CreateRisk(growId, RiskEventType.PowerOutage);

        var result = Assert.IsType<OkObjectResult>(_controller.SopRecommendations(risk.Id).Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<RiskEventSopRecommendationDto>>(result.Value);
        var recommendation = Assert.Single(items);
        Assert.Equal("emergency-power-recovery", recommendation.SopId);
        Assert.False(recommendation.AlreadyActive);

        Assert.IsType<NotFoundObjectResult>(_controller.SopRecommendations(9999).Result);
    }

    [Fact]
    public void StartSop_StartsInstanceAndStoresSopInstanceIdOnRiskEvent()
    {
        var growId = CreateGrow();
        var risk = CreateRisk(growId, RiskEventType.CriticalDo);

        var result = _controller.StartSop(risk.Id, new StartRiskEventSopRequest
        {
            SopId = "emergency-power-recovery",
            Notes = "Operator gestartet"
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<SopInstanceDto>(created.Value);
        Assert.Equal(growId, dto.GrowId);
        Assert.Equal("emergency-power-recovery", dto.SopId);
        Assert.Equal(SopStartSource.Recommendation, dto.Source);
        Assert.Equal($"risk-event:{risk.Id}:emergency-power-recovery", dto.SourceRecommendationKey);

        var updatedRisk = _repository.GetRiskEvent(risk.Id)!;
        Assert.Equal(dto.Id, updatedRisk.SopInstanceId);
        Assert.Equal(RiskEventStatus.Open, updatedRisk.Status);
    }

    [Fact]
    public void StartSop_RejectsMissingGrowDuplicateAndUnknownSop()
    {
        var riskWithoutGrow = CreateRisk(null, RiskEventType.PowerOutage);
        var noGrow = _controller.StartSop(riskWithoutGrow.Id, new StartRiskEventSopRequest { SopId = "emergency-power-recovery" });
        var noGrowError = Assert.IsType<ApiError>(Assert.IsType<BadRequestObjectResult>(noGrow.Result).Value);
        Assert.Equal("risk_event_has_no_grow", noGrowError.Code);

        var growId = CreateGrow();
        var risk = CreateRisk(growId, RiskEventType.PowerOutage);
        var missingSop = _controller.StartSop(risk.Id, new StartRiskEventSopRequest { SopId = "missing-sop" });
        var missingSopError = Assert.IsType<ApiError>(Assert.IsType<BadRequestObjectResult>(missingSop.Result).Value);
        Assert.Equal("validation_failed", missingSopError.Code);

        _controller.ModelState.Clear();
        Assert.IsType<CreatedAtActionResult>(_controller.StartSop(risk.Id, new StartRiskEventSopRequest { SopId = "emergency-power-recovery" }).Result);
        var duplicate = _controller.StartSop(risk.Id, new StartRiskEventSopRequest { SopId = "emergency-power-recovery" });
        var conflict = Assert.IsType<ConflictObjectResult>(duplicate.Result);
        var conflictError = Assert.IsType<ApiError>(conflict.Value);
        Assert.Equal("active_sop_exists", conflictError.Code);
    }

    private RiskEvent CreateRisk(int? growId, RiskEventType eventType)
        => _repository.CreateRiskEvent(new RiskEvent
        {
            EventType = eventType,
            Severity = RiskEventSeverity.Critical,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Manual,
            Title = "Risk",
            GrowId = growId
        });

    private int CreateGrow()
        => _repository.CreateGrow(new GrowRun
        {
            Name = "Risk SOP API Grow",
            StartDate = new DateTime(2026, 8, 1),
            Status = GrowStatus.Running
        });

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
