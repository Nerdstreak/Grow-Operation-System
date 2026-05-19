using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

public sealed class HardwareItemsApiControllerTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;
    private readonly HardwareItemsApiController _controller;

    public HardwareItemsApiControllerTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-hardware-api-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        CopyKnowledgeDefaults(_contentRoot);
        _paths = new AppPaths(_contentRoot);
        GrowDiary.Web.Tests.TestDatabase.InitializeWithDefaultTent(_paths);
        _repository = new GrowRepository(_paths);
        var knowledgeBase = new KnowledgeBaseLoader(_paths, NullLogger<KnowledgeBaseLoader>.Instance);
        knowledgeBase.Initialize();
        _controller = new HardwareItemsApiController(_repository, knowledgeBase);
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Api_CreateFromWearTemplateListsGetsAndUpdatesHardwareItem()
    {
        var tent = _repository.GetTents().Single();
        var hydroSetup = CreateHydroSetup(tent.Id);

        var create = _controller.Create(new CreateHardwareItemRequest
        {
            TentId = tent.Id,
            HydroSetupId = hydroSetup.Id,
            WearTemplateId = "ph-probe",
            Status = HardwareItemStatus.Active,
            Criticality = HardwareItemCriticality.High,
            InstalledAtUtc = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc)
        });
        var created = Assert.IsType<CreatedAtActionResult>(create.Result);
        var dto = Assert.IsType<HardwareItemDto>(created.Value);
        Assert.Equal("pH-Sonde", dto.Name);
        Assert.Equal("Sensor", dto.Category);
        Assert.Equal(450, dto.ExpectedLifespanDays);
        Assert.Equal(30, dto.InspectionIntervalDays);
        Assert.Equal(hydroSetup.Id, dto.HydroSetupId);

        var detail = Assert.IsType<OkObjectResult>(_controller.Detail(dto.Id).Result);
        Assert.Equal(dto.Id, Assert.IsType<HardwareItemDto>(detail.Value).Id);

        var listByTent = Assert.IsType<OkObjectResult>(_controller.List(tent.Id, null).Result);
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<HardwareItemDto>>(listByTent.Value));

        var listByStatus = Assert.IsType<OkObjectResult>(_controller.List(null, HardwareItemStatus.Active).Result);
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<HardwareItemDto>>(listByStatus.Value));

        var listByHydroSetup = Assert.IsType<OkObjectResult>(_controller.List(hydroSetupId: hydroSetup.Id).Result);
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<HardwareItemDto>>(listByHydroSetup.Value));

        var update = _controller.Update(dto.Id, new UpdateHardwareItemRequest
        {
            Name = "pH Sonde aktualisiert",
            Category = "Sensor",
            Status = HardwareItemStatus.Retired,
            Criticality = HardwareItemCriticality.Critical,
            TentId = tent.Id,
            HydroSetupId = hydroSetup.Id,
            WearTemplateId = dto.WearTemplateId,
            InstalledAtUtc = dto.InstalledAtUtc,
            Notes = "Ausgetauscht"
        });
        var ok = Assert.IsType<OkObjectResult>(update.Result);
        var updated = Assert.IsType<HardwareItemDto>(ok.Value);
        Assert.Equal("pH Sonde aktualisiert", updated.Name);
        Assert.Equal(HardwareItemStatus.Retired, updated.Status);
        Assert.Equal(hydroSetup.Id, updated.HydroSetupId);
        Assert.NotNull(updated.RetiredAtUtc);
    }

    [Fact]
    public void Api_ReturnsNotFoundForMissingHardwareItem()
    {
        var result = _controller.Detail(9999).Result;

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiError>(notFound.Value);
        Assert.Equal("hardware_item_not_found", error.Code);
    }

    [Fact]
    public void Delete_RemovesHardwareItemFromListAndDetail()
    {
        var tent = _repository.GetTents().Single();
        var created = Assert.IsType<HardwareItemDto>(Assert.IsType<CreatedAtActionResult>(_controller.Create(new CreateHardwareItemRequest
        {
            Name = "Delete Sensor",
            Category = "Sensor",
            Status = HardwareItemStatus.Active,
            Criticality = HardwareItemCriticality.High,
            TentId = tent.Id,
            InstalledAtUtc = new DateTime(2026, 5, 19, 8, 0, 0, DateTimeKind.Utc)
        }).Result).Value);

        var result = _controller.Delete(created.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.IsType<NotFoundObjectResult>(_controller.Detail(created.Id).Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<HardwareItemDto>>(Assert.IsType<OkObjectResult>(_controller.List().Result).Value);
        Assert.DoesNotContain(list, item => item.Id == created.Id);
    }

    [Fact]
    public void Api_RejectsInvalidReferencesEnumsAndDates()
    {
        var missingTent = _controller.Create(new CreateHardwareItemRequest
        {
            Name = "Bad",
            Category = "Sensor",
            TentId = 9999
        });
        Assert.Contains(nameof(CreateHardwareItemRequest.TentId), AssertValidationError(missingTent.Result).FieldErrors!.Keys);

        _controller.ModelState.Clear();
        var missingSetup = _controller.Create(new CreateHardwareItemRequest
        {
            Name = "Bad",
            Category = "Sensor",
            SetupId = 9999
        });
        Assert.Contains(nameof(CreateHardwareItemRequest.SetupId), AssertValidationError(missingSetup.Result).FieldErrors!.Keys);

        _controller.ModelState.Clear();
        var missingHydroSetup = _controller.Create(new CreateHardwareItemRequest
        {
            Name = "Bad",
            Category = "Sensor",
            HydroSetupId = 9999
        });
        Assert.Contains(nameof(CreateHardwareItemRequest.HydroSetupId), AssertValidationError(missingHydroSetup.Result).FieldErrors!.Keys);

        _controller.ModelState.Clear();
        var missingGrow = _controller.Create(new CreateHardwareItemRequest
        {
            Name = "Bad",
            Category = "Sensor",
            GrowId = 9999
        });
        Assert.Contains(nameof(CreateHardwareItemRequest.GrowId), AssertValidationError(missingGrow.Result).FieldErrors!.Keys);

        _controller.ModelState.Clear();
        var missingSensor = _controller.Create(new CreateHardwareItemRequest
        {
            Name = "Bad",
            Category = "Sensor",
            TentSensorId = 9999
        });
        Assert.Contains(nameof(CreateHardwareItemRequest.TentSensorId), AssertValidationError(missingSensor.Result).FieldErrors!.Keys);

        _controller.ModelState.Clear();
        var badDate = _controller.Create(new CreateHardwareItemRequest
        {
            Name = "Bad",
            Category = "Sensor",
            InstalledAtUtc = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc),
            RetiredAtUtc = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        Assert.Contains(nameof(CreateHardwareItemRequest.RetiredAtUtc), AssertValidationError(badDate.Result).FieldErrors!.Keys);

        _controller.ModelState.Clear();
        var badStatus = _controller.Create(new CreateHardwareItemRequest
        {
            Name = "Bad",
            Category = "Sensor",
            Status = (HardwareItemStatus)99
        });
        Assert.Contains(nameof(CreateHardwareItemRequest.Status), AssertValidationError(badStatus.Result).FieldErrors!.Keys);

        _controller.ModelState.Clear();
        var badCriticality = _controller.Create(new CreateHardwareItemRequest
        {
            Name = "Bad",
            Category = "Sensor",
            Criticality = (HardwareItemCriticality)99
        });
        Assert.Contains(nameof(CreateHardwareItemRequest.Criticality), AssertValidationError(badCriticality.Result).FieldErrors!.Keys);
    }

    private GrowSystem CreateHydroSetup(int tentId)
    {
        return _repository.CreateHydroSetup(new GrowSystem
        {
            TentId = tentId,
            Name = "RDWC Testsystem",
            HydroStyle = HydroStyle.RDWC.ToString(),
            PotCount = 4,
            PotSizeLiters = 19d,
            ReservoirLiters = 60d,
            LayoutType = HydroSetupLayoutType.Grid2x2,
            ReservoirPosition = ReservoirPosition.External,
            Status = HydroSetupStatus.Active
        });
    }

    private static ApiError AssertValidationError(ActionResult? result)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("validation_failed", error.Code);
        return error;
    }

    private static void CopyKnowledgeDefaults(string contentRoot)
    {
        var source = Path.Combine(FindProjectRoot(), "GrowDiary.Web", "wwwroot", "knowledge-defaults");
        var destination = Path.Combine(contentRoot, "wwwroot", "knowledge-defaults");
        foreach (var file in Directory.EnumerateFiles(source, "*.json", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "GrowDiary.Web")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Project root not found.");
    }
}
