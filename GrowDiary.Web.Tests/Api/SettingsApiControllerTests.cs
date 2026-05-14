using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

public sealed class SettingsApiControllerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;
    private readonly SettingsApiController _controller;

    public SettingsApiControllerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"grow-test-{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", _dbPath);
        _paths = new AppPaths(Path.GetTempPath());
        var initializer = new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance);
        initializer.Initialize();
        _repository = new GrowRepository(_paths);
        _controller = new SettingsApiController(_repository);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
    }

    [Fact]
    public void CreateTent_WithValidRequest_CreatesTentAndAppearsInList()
    {
        var result = _controller.CreateTent(new CreateTentRequest
        {
            Name = "Mutter Zelt 1",
            TentType = TentType.Mother.ToString(),
            Notes = "Mutterpflanzen und Stecklinge",
            DisplayOrder = 2
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<TentDto>(created.Value);
        Assert.True(dto.Id > 0);
        Assert.Equal("Mutter Zelt 1", dto.Name);
        Assert.Equal(TentType.Mother.ToString(), dto.TentType);

        var tents = Assert.IsAssignableFrom<IReadOnlyList<TentDto>>(Assert.IsType<OkObjectResult>(_controller.Tents().Result).Value);
        Assert.Contains(tents, tent => tent.Id == dto.Id && tent.Name == "Mutter Zelt 1");
    }

    [Fact]
    public void CreateTent_WithBlankName_ReturnsValidationError()
    {
        var result = _controller.CreateTent(new CreateTentRequest
        {
            Name = " ",
            TentType = TentType.Production.ToString()
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("validation_failed", error.Code);
        Assert.Contains(nameof(CreateTentRequest.Name), error.FieldErrors!.Keys);
    }

    [Fact]
    public void CreateTent_WithInvalidTentType_ReturnsValidationError()
    {
        var result = _controller.CreateTent(new CreateTentRequest
        {
            Name = "Testzelt",
            TentType = "FlowerOnly"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("validation_failed", error.Code);
        Assert.Contains(nameof(CreateTentRequest.TentType), error.FieldErrors!.Keys);
    }
}
