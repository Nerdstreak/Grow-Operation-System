using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

public sealed class RiskEventsApiControllerTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;
    private readonly RiskEventsApiController _controller;

    public RiskEventsApiControllerTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-risk-api-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _paths = new AppPaths(_contentRoot);
        GrowDiary.Web.Tests.TestDatabase.InitializeWithDefaultTent(_paths);
        _repository = new GrowRepository(_paths);
        var knowledgeBase = new KnowledgeBaseLoader(_paths, NullLogger<KnowledgeBaseLoader>.Instance);
        knowledgeBase.Initialize();
        var recommender = new RiskEventSopRecommender(knowledgeBase, _repository);
        _controller = new RiskEventsApiController(_repository, new TaskRepository(_paths), knowledgeBase, recommender);
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Api_CreateListsGetsUpdatesAcknowledgesAndResolvesRiskEvent()
    {
        var tent = _repository.GetTents().Single();
        var growId = CreateGrow(tent.Id);
        var hardware = CreateHardware(tent.Id, growId);

        var create = _controller.Create(new CreateRiskEventRequest
        {
            EventType = RiskEventType.PumpOffline,
            Severity = RiskEventSeverity.Critical,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Manual,
            Title = "Pumpe offline",
            HardwareItemId = hardware.Id,
            TentId = tent.Id,
            GrowId = growId,
            DedupeKey = "pump:offline",
            Notes = "Manuell"
        });
        var created = Assert.IsType<CreatedAtActionResult>(create.Result);
        var dto = Assert.IsType<RiskEventDto>(created.Value);
        Assert.Equal(hardware.Id, dto.HardwareItemId);

        var detail = Assert.IsType<OkObjectResult>(_controller.Detail(dto.Id).Result);
        Assert.Equal(dto.Id, Assert.IsType<RiskEventDto>(detail.Value).Id);

        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<RiskEventDto>>(Assert.IsType<OkObjectResult>(_controller.List(RiskEventStatus.Open, null, null, null).Result).Value));
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<RiskEventDto>>(Assert.IsType<OkObjectResult>(_controller.List(null, tent.Id, null, null).Result).Value));
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<RiskEventDto>>(Assert.IsType<OkObjectResult>(_controller.List(null, null, growId, null).Result).Value));
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<RiskEventDto>>(Assert.IsType<OkObjectResult>(_controller.List(null, null, null, hardware.Id).Result).Value));

        var update = _controller.Update(dto.Id, new UpdateRiskEventRequest
        {
            EventType = RiskEventType.PumpOffline,
            Severity = RiskEventSeverity.Warning,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Manual,
            Title = "Pumpe instabil",
            HardwareItemId = hardware.Id,
            TentId = tent.Id,
            GrowId = growId,
            StartedAtUtc = dto.StartedAtUtc,
            DedupeKey = dto.DedupeKey,
            Notes = "Update"
        });
        var updated = Assert.IsType<RiskEventDto>(Assert.IsType<OkObjectResult>(update.Result).Value);
        Assert.Equal(RiskEventSeverity.Warning, updated.Severity);

        var acknowledged = Assert.IsType<RiskEventDto>(Assert.IsType<OkObjectResult>(_controller.Acknowledge(dto.Id, new AcknowledgeRiskEventRequest { Notes = "Gesehen" }).Result).Value);
        Assert.Equal(RiskEventStatus.Acknowledged, acknowledged.Status);
        Assert.NotNull(acknowledged.AcknowledgedAtUtc);

        var resolved = Assert.IsType<RiskEventDto>(Assert.IsType<OkObjectResult>(_controller.Resolve(dto.Id, new ResolveRiskEventRequest { Notes = "Behoben" }).Result).Value);
        Assert.Equal(RiskEventStatus.Resolved, resolved.Status);
        Assert.NotNull(resolved.ResolvedAtUtc);
    }

    [Fact]
    public void Api_ReturnsNotFoundForMissingRiskEvent()
    {
        Assert.IsType<NotFoundObjectResult>(_controller.Detail(9999).Result);
        Assert.IsType<NotFoundObjectResult>(_controller.Update(9999, ValidUpdate()).Result);
        Assert.IsType<NotFoundObjectResult>(_controller.Acknowledge(9999, new AcknowledgeRiskEventRequest()).Result);
        Assert.IsType<NotFoundObjectResult>(_controller.Resolve(9999, new ResolveRiskEventRequest()).Result);
    }

    [Fact]
    public void Api_RejectsAcknowledgeBeforeStartedAt()
    {
        var risk = CreateRisk(startedAtUtc: Utc(2026, 7, 2));

        var result = _controller.Acknowledge(risk.Id, new AcknowledgeRiskEventRequest
        {
            AcknowledgedAtUtc = Utc(2026, 7, 1)
        });

        var error = AssertValidationError(result.Result);
        Assert.Contains(nameof(AcknowledgeRiskEventRequest.AcknowledgedAtUtc), error.FieldErrors!.Keys);
    }

    [Fact]
    public void Api_RejectsResolveBeforeStartedAt()
    {
        var risk = CreateRisk(startedAtUtc: Utc(2026, 7, 2));

        var result = _controller.Resolve(risk.Id, new ResolveRiskEventRequest
        {
            ResolvedAtUtc = Utc(2026, 7, 1)
        });

        var error = AssertValidationError(result.Result);
        Assert.Contains(nameof(ResolveRiskEventRequest.ResolvedAtUtc), error.FieldErrors!.Keys);
    }

    [Fact]
    public void Api_RejectsInvalidReferencesEnumsAndDates()
    {
        var missingHardware = _controller.Create(new CreateRiskEventRequest
        {
            EventType = RiskEventType.PumpOffline,
            Severity = RiskEventSeverity.Critical,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Manual,
            Title = "Bad",
            HardwareItemId = 9999
        });
        Assert.Contains(nameof(CreateRiskEventRequest.HardwareItemId), AssertValidationError(missingHardware.Result).FieldErrors!.Keys);

        _controller.ModelState.Clear();
        var badEnum = _controller.Create(new CreateRiskEventRequest
        {
            EventType = (RiskEventType)99,
            Severity = RiskEventSeverity.Critical,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Manual,
            Title = "Bad"
        });
        Assert.Contains(nameof(CreateRiskEventRequest.EventType), AssertValidationError(badEnum.Result).FieldErrors!.Keys);

        _controller.ModelState.Clear();
        var badDate = _controller.Create(new CreateRiskEventRequest
        {
            EventType = RiskEventType.PowerOutage,
            Severity = RiskEventSeverity.Critical,
            Status = RiskEventStatus.Resolved,
            Source = RiskEventSource.Manual,
            Title = "Bad Date",
            StartedAtUtc = Utc(2026, 7, 2),
            ResolvedAtUtc = Utc(2026, 7, 1)
        });
        Assert.Contains(nameof(CreateRiskEventRequest.ResolvedAtUtc), AssertValidationError(badDate.Result).FieldErrors!.Keys);
    }

    private UpdateRiskEventRequest ValidUpdate() => new()
    {
        EventType = RiskEventType.PowerOutage,
        Severity = RiskEventSeverity.Critical,
        Status = RiskEventStatus.Open,
        Source = RiskEventSource.Manual,
        Title = "Valid",
        StartedAtUtc = Utc(2026, 7, 1)
    };

    private int CreateGrow(int tentId)
        => _repository.CreateGrow(new GrowRun
        {
            TentId = tentId,
            Name = "Risk API Grow",
            StartDate = Utc(2026, 7, 1),
            Status = GrowStatus.Running
        });

    private HardwareItem CreateHardware(int tentId, int growId)
        => _repository.CreateHardwareItem(new HardwareItem
        {
            Name = "Pumpe",
            Category = "Pump",
            Status = HardwareItemStatus.Active,
            Criticality = HardwareItemCriticality.Critical,
            TentId = tentId,
            GrowId = growId
        });

    private RiskEvent CreateRisk(DateTime startedAtUtc)
        => _repository.CreateRiskEvent(new RiskEvent
        {
            EventType = RiskEventType.PowerOutage,
            Severity = RiskEventSeverity.Critical,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Manual,
            Title = "Risk",
            StartedAtUtc = startedAtUtc
        });

    private static ApiError AssertValidationError(ActionResult? result)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("validation_failed", error.Code);
        return error;
    }

    private static DateTime Utc(int year, int month, int day)
        => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
