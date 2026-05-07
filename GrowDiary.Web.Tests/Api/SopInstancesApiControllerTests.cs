using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

public sealed class SopInstancesApiControllerTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly GrowRepository _repository;
    private readonly TaskRepository _taskRepository;
    private readonly SopInstancesApiController _controller;

    public SopInstancesApiControllerTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-sop-api-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        CopyDefaults(Path.Combine(FindProjectRoot(), "GrowDiary.Web", "wwwroot", "knowledge-defaults"), _contentRoot);
        var paths = new AppPaths(_contentRoot);
        new DatabaseInitializer(paths, NullLogger<DatabaseInitializer>.Instance).Initialize();
        _repository = new GrowRepository(paths);
        _taskRepository = new TaskRepository(paths);
        var knowledgeBase = new KnowledgeBaseLoader(paths, NullLogger<KnowledgeBaseLoader>.Instance);
        knowledgeBase.Initialize();
        _controller = new SopInstancesApiController(_repository, _taskRepository, knowledgeBase);
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Start_List_Detail_AndSteps_ReturnMaterializedSop()
    {
        var growId = CreateGrow();

        var create = _controller.Start(new StartSopInstanceRequest
        {
            GrowId = growId,
            SopId = "weekly-water-change",
            Source = SopStartSource.Manual,
            Notes = "API Start"
        });
        var created = Assert.IsType<CreatedAtActionResult>(create.Result);
        var dto = Assert.IsType<SopInstanceDto>(created.Value);
        Assert.Equal("weekly-water-change", dto.SopId);
        Assert.Equal(SopInstanceStatus.Active, dto.Status);
        Assert.Equal("API Start", dto.Notes);
        Assert.True(dto.StepCount > 0);

        var list = Assert.IsType<OkObjectResult>(_controller.List(growId).Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<SopInstanceDto>>(list.Value);
        Assert.Contains(items, item => item.Id == dto.Id);

        var detail = Assert.IsType<OkObjectResult>(_controller.Detail(dto.Id).Result);
        Assert.Equal(dto.Id, Assert.IsType<SopInstanceDto>(detail.Value).Id);

        var stepsResult = Assert.IsType<OkObjectResult>(_controller.Steps(dto.Id).Result);
        var steps = Assert.IsAssignableFrom<IReadOnlyList<SopStepInstanceDto>>(stepsResult.Value);
        Assert.Equal(dto.StepCount, steps.Count);
        Assert.Equal(steps.OrderBy(step => step.Order).Select(step => step.Id), steps.Select(step => step.Id));
        Assert.Contains(steps, step => step.SubSopId == "mixing-order-rdwc-ro");
    }

    [Fact]
    public void Start_RejectsDuplicateActiveSop()
    {
        var growId = CreateGrow();
        var request = new StartSopInstanceRequest { GrowId = growId, SopId = "daily-measurement-routine" };

        Assert.IsType<CreatedAtActionResult>(_controller.Start(request).Result);
        _controller.ModelState.Clear();
        var duplicate = _controller.Start(request);

        var conflict = Assert.IsType<ConflictObjectResult>(duplicate.Result);
        var error = Assert.IsType<ApiError>(conflict.Value);
        Assert.Equal("active_sop_exists", error.Code);
    }

    [Fact]
    public void Start_WithRecommendationSource_StoresRecommendationReferences()
    {
        var growId = CreateGrow();

        var create = _controller.Start(new StartSopInstanceRequest
        {
            GrowId = growId,
            SopId = "emergency-power-recovery",
            Source = SopStartSource.Recommendation,
            SourceRecommendationKey = "deviation:do-critical:sop:emergency-power-recovery",
            TreatmentRecommendationStableKey = "treatment-rec-123",
            Notes = "Gestartet aus Diagnoseempfehlung"
        });

        var created = Assert.IsType<CreatedAtActionResult>(create.Result);
        var dto = Assert.IsType<SopInstanceDto>(created.Value);

        Assert.Equal(SopStartSource.Recommendation, dto.Source);
        Assert.Equal("deviation:do-critical:sop:emergency-power-recovery", dto.SourceRecommendationKey);
        Assert.Equal("treatment-rec-123", dto.TreatmentRecommendationStableKey);
        Assert.Equal("Gestartet aus Diagnoseempfehlung", dto.Notes);
    }

    [Fact]
    public void Start_WithRecommendationSource_RejectsDuplicateActiveSop()
    {
        var growId = CreateGrow();
        var request = new StartSopInstanceRequest
        {
            GrowId = growId,
            SopId = "emergency-power-recovery",
            Source = SopStartSource.Recommendation,
            SourceRecommendationKey = "first-key",
            TreatmentRecommendationStableKey = "first-stable-key"
        };

        Assert.IsType<CreatedAtActionResult>(_controller.Start(request).Result);
        _controller.ModelState.Clear();
        request.SourceRecommendationKey = "second-key";
        var duplicate = _controller.Start(request);

        var conflict = Assert.IsType<ConflictObjectResult>(duplicate.Result);
        var error = Assert.IsType<ApiError>(conflict.Value);
        Assert.Equal("active_sop_exists", error.Code);
    }

    [Fact]
    public void Start_ReturnsNotFoundForMissingGrow()
    {
        var result = _controller.Start(new StartSopInstanceRequest { GrowId = 9999, SopId = "daily-measurement-routine" }).Result;

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiError>(notFound.Value);
        Assert.Equal("grow_not_found", error.Code);
    }

    [Fact]
    public void Start_RejectsUnknownSop()
    {
        var growId = CreateGrow();
        var result = _controller.Start(new StartSopInstanceRequest { GrowId = growId, SopId = "missing-sop" }).Result;

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("validation_failed", error.Code);
        Assert.Contains(nameof(StartSopInstanceRequest.SopId), error.FieldErrors!.Keys);
    }

    [Fact]
    public void UpdateStep_SetztStatusUndNotes()
    {
        var instance = StartRoutine();
        var step = _repository.GetSopStepInstances(instance.Id).First();

        var result = _controller.UpdateStep(step.Id, new UpdateSopStepInstanceRequest
        {
            Status = SopStepInstanceStatus.InProgress,
            Notes = "Wird ausgefuehrt"
        }).Result;

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<SopStepInstanceDto>(ok.Value);
        Assert.Equal(SopStepInstanceStatus.InProgress, dto.Status);
        Assert.NotNull(dto.StartedAtUtc);
        Assert.Equal("Wird ausgefuehrt", dto.Notes);
    }

    [Fact]
    public void UpdateStep_ReturnsNotFoundForMissingStep()
    {
        var result = _controller.UpdateStep(999999, new UpdateSopStepInstanceRequest { Status = SopStepInstanceStatus.Done }).Result;

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiError>(notFound.Value);
        Assert.Equal("sop_step_not_found", error.Code);
    }

    [Fact]
    public void UpdateStep_RejectsInvalidStatus()
    {
        var instance = StartRoutine();
        var step = _repository.GetSopStepInstances(instance.Id).First();

        var result = _controller.UpdateStep(step.Id, new UpdateSopStepInstanceRequest { Status = (SopStepInstanceStatus)999 }).Result;

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("validation_failed", error.Code);
        Assert.Contains(nameof(UpdateSopStepInstanceRequest.Status), error.FieldErrors!.Keys);
    }

    [Fact]
    public void UpdateStep_ReturnsConflictForCompletedSop()
    {
        var instance = StartRoutine();
        foreach (var step in _repository.GetSopStepInstances(instance.Id))
        {
            _repository.UpdateSopStepInstance(step.Id, SopStepInstanceStatus.Done, null, null, null, null);
        }

        var completedStep = _repository.GetSopStepInstances(instance.Id).First();
        var result = _controller.UpdateStep(completedStep.Id, new UpdateSopStepInstanceRequest { Status = SopStepInstanceStatus.Pending }).Result;

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<ApiError>(conflict.Value);
        Assert.Equal("sop_instance_not_active", error.Code);
    }

    [Fact]
    public void Start_WaitStepMitDueAtUtc_ErstelltGrowTaskReminder()
    {
        var growId = CreateGrow();

        var create = _controller.Start(new StartSopInstanceRequest
        {
            GrowId = growId,
            SopId = "emergency-power-recovery",
            Source = SopStartSource.Manual
        });
        var created = Assert.IsType<CreatedAtActionResult>(create.Result);
        var dto = Assert.IsType<SopInstanceDto>(created.Value);

        var steps = _repository.GetSopStepInstances(dto.Id);
        var waitStep = steps.FirstOrDefault(s => s.WaitMinutes.HasValue);
        Assert.NotNull(waitStep);
        Assert.NotNull(waitStep.DueAtUtc);
        Assert.NotNull(waitStep.ReminderTaskId);

        var task = _taskRepository.Get(waitStep.ReminderTaskId!.Value);
        Assert.NotNull(task);
        Assert.Equal(growId, task.GrowId);
        Assert.StartsWith("SOP:", task.Title, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(task.DueAtUtc);
        Assert.Equal(GrowTaskStatus.Open, task.Status);
    }

    private int CreateGrow()
        => _repository.CreateGrow(new GrowRun
        {
            Name = "SOP API Grow",
            StartDate = new DateTime(2026, 5, 1),
            Status = GrowStatus.Running
        });

    private SopInstance StartRoutine()
    {
        var growId = CreateGrow();
        var create = _controller.Start(new StartSopInstanceRequest
        {
            GrowId = growId,
            SopId = "daily-measurement-routine"
        });
        var created = Assert.IsType<CreatedAtActionResult>(create.Result);
        return _repository.GetSopInstance(Assert.IsType<SopInstanceDto>(created.Value).Id)!;
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
