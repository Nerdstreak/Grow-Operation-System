using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

public sealed class LightSchedulesApiControllerTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;
    private readonly LightSchedulesApiController _controller;

    public LightSchedulesApiControllerTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _paths = new AppPaths(_contentRoot);
        new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance).Initialize();
        _repository = new GrowRepository(_paths);
        _controller = new LightSchedulesApiController(_repository);
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    [Fact]
    public void ScheduleApi_CreatesListsAndUpdatesSchedule()
    {
        var tent = _repository.GetTents().Single();

        var create = _controller.Create(new CreateLightScheduleRequest
        {
            TentId = tent.Id,
            Name = "Lichtplan",
            IsActive = true,
            LightsOnTime = "08:00",
            LightsOffTime = "20:00",
            TimeZoneId = "",
            Source = LightSource.Manual
        });
        var created = Assert.IsType<CreatedAtActionResult>(create.Result);
        var dto = Assert.IsType<LightScheduleDto>(created.Value);
        Assert.Equal("Lichtplan", dto.Name);

        var update = _controller.Update(dto.Id, new UpdateLightScheduleRequest
        {
            Name = "Lichtplan 12/12",
            IsActive = false,
            LightsOnTime = "09:00",
            LightsOffTime = "21:00",
            TimeZoneId = "Europe/Berlin",
            Source = LightSource.HomeAssistant
        });
        var ok = Assert.IsType<OkObjectResult>(update.Result);
        var updated = Assert.IsType<LightScheduleDto>(ok.Value);
        Assert.Equal("Lichtplan 12/12", updated.Name);
        Assert.Equal(LightSource.HomeAssistant, updated.Source);

        var list = Assert.IsType<OkObjectResult>(_controller.List(tent.Id).Result);
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<LightScheduleDto>>(list.Value));
    }

    [Fact]
    public void ScheduleApi_RejectsInvalidReferencesTimesAndSource()
    {
        var missingTent = _controller.Create(new CreateLightScheduleRequest
        {
            TentId = 9999,
            Name = "Bad",
            LightsOnTime = "08:00",
            LightsOffTime = "20:00",
            Source = LightSource.Manual
        });
        Assert.Contains(nameof(CreateLightScheduleRequest.TentId), AssertValidationError(missingTent.Result).FieldErrors!.Keys);

        _controller.ModelState.Clear();
        var tent = _repository.GetTents().Single();
        var badTime = _controller.Create(new CreateLightScheduleRequest
        {
            TentId = tent.Id,
            Name = "Bad",
            LightsOnTime = "8 Uhr",
            LightsOffTime = "20:00",
            Source = LightSource.Manual
        });
        Assert.Contains(nameof(CreateLightScheduleRequest.LightsOnTime), AssertValidationError(badTime.Result).FieldErrors!.Keys);

        _controller.ModelState.Clear();
        var sameTime = _controller.Create(new CreateLightScheduleRequest
        {
            TentId = tent.Id,
            Name = "Bad",
            LightsOnTime = "08:00",
            LightsOffTime = "08:00",
            Source = LightSource.Manual
        });
        Assert.Contains(nameof(CreateLightScheduleRequest.LightsOffTime), AssertValidationError(sameTime.Result).FieldErrors!.Keys);

        _controller.ModelState.Clear();
        var badSource = _controller.Create(new CreateLightScheduleRequest
        {
            TentId = tent.Id,
            Name = "Bad",
            LightsOnTime = "08:00",
            LightsOffTime = "20:00",
            Source = (LightSource)99
        });
        Assert.Contains(nameof(CreateLightScheduleRequest.Source), AssertValidationError(badSource.Result).FieldErrors!.Keys);
    }

    private static ApiError AssertValidationError(ActionResult? result)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("validation_failed", error.Code);
        return error;
    }
}
