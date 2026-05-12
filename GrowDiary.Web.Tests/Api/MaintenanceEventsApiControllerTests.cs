using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

public sealed class MaintenanceEventsApiControllerTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;
    private readonly MaintenanceEventsApiController _controller;

    public MaintenanceEventsApiControllerTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-maintenance-api-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _paths = new AppPaths(_contentRoot);
        new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance).Initialize();
        _repository = new GrowRepository(_paths);
        _controller = new MaintenanceEventsApiController(_repository);
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Api_CreateListsGetsAndUpdatesMaintenanceEvent()
    {
        var hardware = CreateHardware();
        var dueAt = Utc(2026, 5, 25);

        var create = _controller.Create(new CreateMaintenanceEventRequest
        {
            HardwareItemId = hardware.Id,
            EventType = MaintenanceEventType.Inspection,
            Status = MaintenanceEventStatus.Planned,
            Result = MaintenanceResult.Unknown,
            Title = "Filter pruefen",
            DueAtUtc = dueAt,
            Notes = "Sichtpruefung"
        });
        var created = Assert.IsType<CreatedAtActionResult>(create.Result);
        var dto = Assert.IsType<MaintenanceEventDto>(created.Value);
        Assert.Equal(hardware.Id, dto.HardwareItemId);

        var detail = Assert.IsType<OkObjectResult>(_controller.Detail(dto.Id).Result);
        Assert.Equal(dto.Id, Assert.IsType<MaintenanceEventDto>(detail.Value).Id);

        var listByHardware = Assert.IsType<OkObjectResult>(_controller.List(hardware.Id, null).Result);
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<MaintenanceEventDto>>(listByHardware.Value));

        var dueList = Assert.IsType<OkObjectResult>(_controller.List(null, Utc(2026, 5, 26)).Result);
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<MaintenanceEventDto>>(dueList.Value));

        var update = _controller.Update(dto.Id, new UpdateMaintenanceEventRequest
        {
            HardwareItemId = hardware.Id,
            EventType = MaintenanceEventType.Inspection,
            Status = MaintenanceEventStatus.Completed,
            Result = MaintenanceResult.Passed,
            Title = "Filter geprueft",
            DueAtUtc = dueAt,
            PerformedAtUtc = Utc(2026, 5, 24),
            Notes = "OK"
        });
        var ok = Assert.IsType<OkObjectResult>(update.Result);
        var updated = Assert.IsType<MaintenanceEventDto>(ok.Value);
        Assert.Equal(MaintenanceEventStatus.Completed, updated.Status);
        Assert.Equal(MaintenanceResult.Passed, updated.Result);
    }

    [Fact]
    public void Api_ReturnsNotFoundForMissingMaintenanceEvent()
    {
        var result = _controller.Detail(9999).Result;

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiError>(notFound.Value);
        Assert.Equal("maintenance_event_not_found", error.Code);
    }

    [Fact]
    public void Api_RejectsInvalidReferencesEnumsAndDates()
    {
        var missingHardware = _controller.Create(new CreateMaintenanceEventRequest
        {
            HardwareItemId = 9999,
            EventType = MaintenanceEventType.Inspection,
            Status = MaintenanceEventStatus.Planned,
            Result = MaintenanceResult.Unknown,
            Title = "Bad"
        });
        Assert.Contains(nameof(CreateMaintenanceEventRequest.HardwareItemId), AssertValidationError(missingHardware.Result).FieldErrors!.Keys);

        var hardware = CreateHardware();

        _controller.ModelState.Clear();
        var badType = _controller.Create(new CreateMaintenanceEventRequest
        {
            HardwareItemId = hardware.Id,
            EventType = (MaintenanceEventType)99,
            Status = MaintenanceEventStatus.Planned,
            Result = MaintenanceResult.Unknown,
            Title = "Bad"
        });
        Assert.Contains(nameof(CreateMaintenanceEventRequest.EventType), AssertValidationError(badType.Result).FieldErrors!.Keys);

        _controller.ModelState.Clear();
        var badStatus = _controller.Create(new CreateMaintenanceEventRequest
        {
            HardwareItemId = hardware.Id,
            EventType = MaintenanceEventType.Inspection,
            Status = (MaintenanceEventStatus)99,
            Result = MaintenanceResult.Unknown,
            Title = "Bad"
        });
        Assert.Contains(nameof(CreateMaintenanceEventRequest.Status), AssertValidationError(badStatus.Result).FieldErrors!.Keys);

        _controller.ModelState.Clear();
        var badResult = _controller.Create(new CreateMaintenanceEventRequest
        {
            HardwareItemId = hardware.Id,
            EventType = MaintenanceEventType.Inspection,
            Status = MaintenanceEventStatus.Planned,
            Result = (MaintenanceResult)99,
            Title = "Bad"
        });
        Assert.Contains(nameof(CreateMaintenanceEventRequest.Result), AssertValidationError(badResult.Result).FieldErrors!.Keys);

        _controller.ModelState.Clear();
        var badDate = _controller.Create(new CreateMaintenanceEventRequest
        {
            HardwareItemId = hardware.Id,
            EventType = MaintenanceEventType.Inspection,
            Status = MaintenanceEventStatus.Completed,
            Result = MaintenanceResult.Passed,
            Title = "Bad Date",
            PerformedAtUtc = Utc(2026, 5, 20),
            NextDueAtUtc = Utc(2026, 5, 19)
        });
        Assert.Contains(nameof(CreateMaintenanceEventRequest.NextDueAtUtc), AssertValidationError(badDate.Result).FieldErrors!.Keys);
    }

    private HardwareItem CreateHardware()
    {
        var tent = _repository.GetTents().Single();
        return _repository.CreateHardwareItem(new HardwareItem
        {
            Name = "Filter",
            Category = "Filter",
            Status = HardwareItemStatus.Active,
            Criticality = HardwareItemCriticality.Medium,
            TentId = tent.Id
        });
    }

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
