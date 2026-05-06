using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

public sealed class GrowsApiControllerSetupTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppPaths _paths;
    private readonly GrowRepository _growRepository;
    private readonly GrowsApiController _controller;

    public GrowsApiControllerSetupTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"grow-test-{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", _dbPath);
        _paths = new AppPaths(Path.GetTempPath());
        var initializer = new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance);
        initializer.Initialize();
        _growRepository = new GrowRepository(_paths);
        _controller = new GrowsApiController(
            _growRepository,
            new AuditRepository(_paths),
            new WeekCounterService());
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
    }

    [Fact]
    public void Create_WithoutSetupId_RemainsValid()
    {
        var result = _controller.Create(NewGrowRequest("Grow ohne Setup"));

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<GrowDetailDto>(created.Value);
        Assert.Null(dto.SetupId);
    }

    [Fact]
    public void Create_WithProductionSetupAndMatchingTent_IsAccepted()
    {
        var tent = DefaultTent();
        var setup = CreateSetup(tent.Id, SetupType.Production);

        var result = _controller.Create(NewGrowRequest("Production Grow", tent.Id, setup.Id));

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<GrowDetailDto>(created.Value);
        Assert.Equal(setup.Id, dto.SetupId);
        Assert.Equal(tent.Id, dto.TentId);
        Assert.Equal(setup.Id, _growRepository.GetGrow(dto.Id)!.SetupId);
    }

    [Fact]
    public void Create_WithMotherSetup_IsRejected()
    {
        var tent = DefaultTent();
        var setup = CreateSetup(tent.Id, SetupType.Mother);

        var result = _controller.Create(NewGrowRequest("Mother Grow", tent.Id, setup.Id));

        AssertSetupValidationError(result.Result);
    }

    [Fact]
    public void Create_WithQuarantineSetup_IsRejected()
    {
        var tent = DefaultTent();
        var setup = CreateSetup(tent.Id, SetupType.Quarantine);

        var result = _controller.Create(NewGrowRequest("Quarantine Grow", tent.Id, setup.Id));

        AssertSetupValidationError(result.Result);
    }

    [Fact]
    public void Create_WithProductionSetupFromDifferentTent_IsRejected()
    {
        var growTent = DefaultTent();
        var setupTent = _growRepository.CreateTent("Second Tent");
        var setup = CreateSetup(setupTent.Id, SetupType.Production);

        var result = _controller.Create(NewGrowRequest("Wrong Tent Grow", growTent.Id, setup.Id));

        AssertSetupValidationError(result.Result);
    }

    [Fact]
    public void Update_WithProductionSetup_PersistsSetupId()
    {
        var tent = DefaultTent();
        var setup = CreateSetup(tent.Id, SetupType.Production);
        var growId = _growRepository.CreateGrow(new GrowRun
        {
            TentId = tent.Id,
            Name = "Original",
            StartDate = new DateTime(2026, 1, 1),
            Status = GrowStatus.Planning
        });

        var result = _controller.Update(growId, NewGrowRequest("Updated", tent.Id, setup.Id));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<GrowDetailDto>(ok.Value);
        Assert.Equal(setup.Id, dto.SetupId);
        Assert.Equal(setup.Id, _growRepository.GetGrow(growId)!.SetupId);
    }

    private Tent DefaultTent()
        => _growRepository.GetTents().Single();

    private Setup CreateSetup(int tentId, SetupType setupType)
        => _growRepository.CreateSetup(new Setup
        {
            TentId = tentId,
            Name = $"{setupType} Setup",
            SetupType = setupType,
            Status = SetupStatus.Active
        });

    private static GrowUpsertRequest NewGrowRequest(string name, int? tentId = null, int? setupId = null)
        => new()
        {
            Name = name,
            TentId = tentId,
            SetupId = setupId,
            HydroStyle = HydroStyle.RDWC,
            StartDate = "2026-01-01",
            Status = GrowStatus.Planning,
            Environment = GrowEnvironment.Indoor
        };

    private static void AssertSetupValidationError(ActionResult? result)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("validation_failed", error.Code);
        Assert.Contains(nameof(GrowUpsertRequest.SetupId), error.FieldErrors!.Keys);
    }
}
