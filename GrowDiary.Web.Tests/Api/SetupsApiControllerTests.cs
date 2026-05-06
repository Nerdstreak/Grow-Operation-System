using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

public sealed class SetupsApiControllerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;
    private readonly SetupsApiController _controller;

    public SetupsApiControllerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"grow-test-{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", _dbPath);
        _paths = new AppPaths(Path.GetTempPath());
        var initializer = new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance);
        initializer.Initialize();
        _repository = new GrowRepository(_paths);
        _controller = new SetupsApiController(_repository);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
    }

    [Fact]
    public void CreateSetup_WithCompatibleTentType_CreatesSetup()
    {
        var tent = SetDefaultTentType(TentType.Production);

        var result = _controller.Create(new CreateSetupRequest
        {
            TentId = tent.Id,
            Name = "Production Setup",
            SetupType = SetupType.Production,
            Notes = "Start"
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<SetupDto>(created.Value);
        Assert.True(dto.Id > 0);
        Assert.Equal(tent.Id, dto.TentId);
        Assert.Equal("Production Setup", dto.Name);
        Assert.Equal(SetupType.Production, dto.SetupType);
        Assert.Equal(SetupStatus.Planning, dto.Status);
        Assert.Equal("Start", dto.Notes);
    }

    [Fact]
    public void CreateSetup_WithIncompatibleTentType_ReturnsValidationError()
    {
        var tent = SetDefaultTentType(TentType.Mother);

        var result = _controller.Create(new CreateSetupRequest
        {
            TentId = tent.Id,
            Name = "Wrong Setup",
            SetupType = SetupType.Quarantine
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("validation_failed", error.Code);
        Assert.Contains(nameof(CreateSetupRequest.SetupType), error.FieldErrors!.Keys);
    }

    [Fact]
    public void UpdateSetup_ChangesOnlyNameStatusAndNotes()
    {
        var tent = _repository.GetTents().Single();
        var setup = _repository.CreateSetup(new Setup
        {
            TentId = tent.Id,
            Name = "Original",
            SetupType = SetupType.Mother,
            Status = SetupStatus.Planning,
            Notes = "Old"
        });

        var result = _controller.Update(setup.Id, new UpdateSetupRequest
        {
            Name = "Updated",
            Status = SetupStatus.Archived,
            Notes = "New"
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SetupDto>(ok.Value);
        Assert.Equal("Updated", dto.Name);
        Assert.Equal(SetupStatus.Archived, dto.Status);
        Assert.Equal("New", dto.Notes);
        Assert.Equal(tent.Id, dto.TentId);
        Assert.Equal(SetupType.Mother, dto.SetupType);

        var loaded = _repository.GetSetup(setup.Id)!;
        Assert.Equal(tent.Id, loaded.TentId);
        Assert.Equal(SetupType.Mother, loaded.SetupType);
    }

    [Fact]
    public void List_WithTentId_ReturnsOnlySetupsForThatTent()
    {
        var firstTent = _repository.GetTents().Single();
        var secondTent = _repository.CreateTent("Quarantine Tent");
        secondTent = _repository.GetTent(secondTent.Id)!;

        _repository.CreateSetup(new Setup
        {
            TentId = firstTent.Id,
            Name = "First Setup",
            SetupType = SetupType.Production
        });
        _repository.CreateSetup(new Setup
        {
            TentId = secondTent.Id,
            Name = "Second Setup",
            SetupType = SetupType.Quarantine
        });

        var result = _controller.List(secondTent.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<SetupDto>>(ok.Value);
        var item = Assert.Single(items);
        Assert.Equal(secondTent.Id, item.TentId);
        Assert.Equal("Second Setup", item.Name);
    }

    private Tent SetDefaultTentType(TentType tentType)
    {
        var tent = _repository.GetTents().Single();
        tent.TentType = tentType;
        _repository.UpdateTent(tent);
        return _repository.GetTent(tent.Id)!;
    }
}
