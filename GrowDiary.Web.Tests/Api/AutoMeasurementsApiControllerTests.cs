using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

public sealed class AutoMeasurementsApiControllerTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;
    private readonly AutoMeasurementsApiController _controller;

    public AutoMeasurementsApiControllerTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _paths = new AppPaths(_contentRoot);
        new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance).Initialize();
        _repository = new GrowRepository(_paths);
        _controller = new AutoMeasurementsApiController(_repository);
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    [Fact]
    public void ConfigApi_CreatesReadsAndUpdatesConfig()
    {
        var tent = _repository.GetTents().Single();
        var growId = _repository.CreateGrow(new GrowRun { TentId = tent.Id, Name = "API Grow", StartDate = new DateTime(2026, 5, 1), Status = GrowStatus.Planning });

        var create = _controller.CreateConfig(new CreateAutoMeasurementConfigRequest
        {
            GrowId = growId,
            TentId = tent.Id,
            Name = "Licht an",
            Status = AutoMeasurementStatus.Enabled,
            TriggerKind = AutoMeasurementTriggerKind.LightOnDelay,
            DelayMinutes = 15,
            WindowMinutes = 20
        });
        var created = Assert.IsType<CreatedAtActionResult>(create.Result);
        var dto = Assert.IsType<AutoMeasurementConfigDto>(created.Value);
        Assert.Equal(growId, dto.GrowId);

        var update = _controller.UpdateConfig(dto.Id, new UpdateAutoMeasurementConfigRequest
        {
            Name = "Licht aus",
            Status = AutoMeasurementStatus.Disabled,
            TriggerKind = AutoMeasurementTriggerKind.LightOffDelay,
            DelayMinutes = 20,
            WindowMinutes = 30,
            TentId = tent.Id
        });
        var ok = Assert.IsType<OkObjectResult>(update.Result);
        var updated = Assert.IsType<AutoMeasurementConfigDto>(ok.Value);
        Assert.Equal("Licht aus", updated.Name);
        Assert.Equal(AutoMeasurementStatus.Disabled, updated.Status);

        var list = Assert.IsType<OkObjectResult>(_controller.ListConfigs(growId).Result);
        var configs = Assert.IsAssignableFrom<IReadOnlyList<AutoMeasurementConfigDto>>(list.Value);
        Assert.Single(configs);
    }

    [Fact]
    public void ConfigApi_RejectsInvalidReferencesAndWindow()
    {
        var missingGrow = _controller.CreateConfig(new CreateAutoMeasurementConfigRequest { GrowId = 9999, Name = "Bad", WindowMinutes = 20 });
        Assert.Contains(nameof(CreateAutoMeasurementConfigRequest.GrowId), AssertValidationError(missingGrow.Result).FieldErrors!.Keys);

        _controller.ModelState.Clear();
        var growId = _repository.CreateGrow(new GrowRun { Name = "API Grow", StartDate = new DateTime(2026, 5, 1), Status = GrowStatus.Planning });
        var missingTent = _controller.CreateConfig(new CreateAutoMeasurementConfigRequest { GrowId = growId, TentId = 9999, Name = "Bad", WindowMinutes = 20 });
        Assert.Contains(nameof(CreateAutoMeasurementConfigRequest.TentId), AssertValidationError(missingTent.Result).FieldErrors!.Keys);

        _controller.ModelState.Clear();
        var badWindow = _controller.CreateConfig(new CreateAutoMeasurementConfigRequest { GrowId = growId, Name = "Bad", WindowMinutes = 0 });
        Assert.Contains(nameof(CreateAutoMeasurementConfigRequest.WindowMinutes), AssertValidationError(badWindow.Result).FieldErrors!.Keys);
    }

    [Fact]
    public void MappingApi_ReplacesMappingsAndRejectsEmptyMetricKey()
    {
        var growId = _repository.CreateGrow(new GrowRun { Name = "API Grow", StartDate = new DateTime(2026, 5, 1), Status = GrowStatus.Planning });
        var config = _repository.CreateAutoMeasurementConfig(new AutoMeasurementConfig { GrowId = growId, Name = "Manual", WindowMinutes = 20 });

        var replace = _controller.ReplaceMappings(config.Id, new ReplaceAutoMeasurementFieldMappingsRequest
        {
            Mappings =
            [
                new AutoMeasurementFieldMappingUpsertRequest
                {
                    MeasurementField = AutoMeasurementField.AirTemperatureC,
                    MetricKey = "temperature",
                    Aggregation = AutoMeasurementAggregation.Latest,
                    IsRequired = true
                }
            ]
        });
        var ok = Assert.IsType<OkObjectResult>(replace.Result);
        var mappings = Assert.IsAssignableFrom<IReadOnlyList<AutoMeasurementFieldMappingDto>>(ok.Value);
        Assert.Single(mappings);

        _controller.ModelState.Clear();
        var invalid = _controller.ReplaceMappings(config.Id, new ReplaceAutoMeasurementFieldMappingsRequest
        {
            Mappings =
            [
                new AutoMeasurementFieldMappingUpsertRequest
                {
                    MeasurementField = AutoMeasurementField.HumidityPercent,
                    MetricKey = " ",
                    Aggregation = AutoMeasurementAggregation.Average
                }
            ]
        });
        Assert.Contains(nameof(AutoMeasurementFieldMappingUpsertRequest.MetricKey), AssertValidationError(invalid.Result).FieldErrors!.Keys);
    }

    private static ApiError AssertValidationError(ActionResult? result)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("validation_failed", error.Code);
        return error;
    }
}
