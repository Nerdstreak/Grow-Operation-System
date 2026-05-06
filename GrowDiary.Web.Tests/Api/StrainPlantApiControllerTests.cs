using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

public sealed class StrainPlantApiControllerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;
    private readonly StrainsApiController _strainsController;
    private readonly PlantsApiController _plantsController;

    public StrainPlantApiControllerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"grow-test-{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", _dbPath);
        _paths = new AppPaths(Path.GetTempPath());
        new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance).Initialize();
        _repository = new GrowRepository(_paths);
        _strainsController = new StrainsApiController(_repository);
        _plantsController = new PlantsApiController(_repository);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
    }

    [Fact]
    public void StrainApi_CreatesAndUpdatesStrain()
    {
        var create = _strainsController.Create(new CreateStrainRequest
        {
            Name = "API Strain",
            Dominance = StrainDominance.Sativa,
            FlowerWeeksMin = 9,
            FlowerWeeksMax = 11,
            NutrientDemandFactor = 0.9
        });
        var created = Assert.IsType<CreatedAtActionResult>(create.Result);
        var dto = Assert.IsType<StrainDto>(created.Value);

        var update = _strainsController.Update(dto.Id, new UpdateStrainRequest
        {
            Name = "API Strain Updated",
            Dominance = StrainDominance.Hybrid,
            FlowerWeeksMin = 8,
            FlowerWeeksMax = 10,
            StretchFactor = 1.2
        });
        var ok = Assert.IsType<OkObjectResult>(update.Result);
        var updated = Assert.IsType<StrainDto>(ok.Value);
        Assert.Equal("API Strain Updated", updated.Name);
        Assert.Equal(StrainDominance.Hybrid, updated.Dominance);
    }

    [Fact]
    public void StrainApi_RejectsInvalidFlowerWeeksAndFactors()
    {
        var flowerWeeks = _strainsController.Create(new CreateStrainRequest { Name = "Bad", FlowerWeeksMin = 12, FlowerWeeksMax = 8 });
        Assert.Contains(nameof(CreateStrainRequest.FlowerWeeksMin), AssertValidationError(flowerWeeks.Result).FieldErrors!.Keys);

        _strainsController.ModelState.Clear();
        var factor = _strainsController.Create(new CreateStrainRequest { Name = "Bad Factor", NutrientDemandFactor = 0 });
        Assert.Contains("Factors", AssertValidationError(factor.Result).FieldErrors!.Keys);
    }

    [Fact]
    public void PlantApi_CreatesAndUpdatesPlant()
    {
        var strain = _repository.CreateStrain(new Strain { Name = "Parent Line" });
        var tent = _repository.GetTents().Single();
        var setup = _repository.CreateSetup(new Setup { TentId = tent.Id, Name = "Mother Setup", SetupType = SetupType.Mother });
        var growId = _repository.CreateGrow(new GrowRun { TentId = tent.Id, Name = "Grow", StartDate = new DateTime(2026, 1, 1), Status = GrowStatus.Planning });

        var create = _plantsController.Create(new CreatePlantInstanceRequest
        {
            StrainId = strain.Id,
            SetupId = setup.Id,
            Label = "Mother A",
            PlantRole = PlantRole.Mother,
            PlantStatus = PlantStatus.Active,
            StartedAt = new DateTime(2026, 1, 1)
        });
        var created = Assert.IsType<CreatedAtActionResult>(create.Result);
        var dto = Assert.IsType<PlantInstanceDto>(created.Value);
        Assert.Equal("Parent Line", dto.StrainName);

        var update = _plantsController.Update(dto.Id, new UpdatePlantInstanceRequest
        {
            StrainId = strain.Id,
            GrowId = growId,
            ParentPlantId = null,
            Label = "Mother A Updated",
            PlantRole = PlantRole.Production,
            PlantStatus = PlantStatus.Active
        });
        var ok = Assert.IsType<OkObjectResult>(update.Result);
        var updated = Assert.IsType<PlantInstanceDto>(ok.Value);
        Assert.Equal(growId, updated.GrowId);
        Assert.Equal("Mother A Updated", updated.Label);
    }

    [Fact]
    public void PlantApi_RejectsSelfParentAndInvalidReferences()
    {
        var plant = _repository.CreatePlant(new PlantInstance { Label = "Plant", PlantRole = PlantRole.Clone, PlantStatus = PlantStatus.Active });

        var selfParent = _plantsController.Update(plant.Id, new UpdatePlantInstanceRequest
        {
            Label = plant.Label,
            PlantRole = plant.PlantRole,
            PlantStatus = plant.PlantStatus,
            ParentPlantId = plant.Id
        });
        Assert.Contains(nameof(UpdatePlantInstanceRequest.ParentPlantId), AssertValidationError(selfParent.Result).FieldErrors!.Keys);

        _plantsController.ModelState.Clear();
        var missingParent = _plantsController.Create(new CreatePlantInstanceRequest { Label = "Clone", ParentPlantId = 9999 });
        Assert.Contains(nameof(CreatePlantInstanceRequest.ParentPlantId), AssertValidationError(missingParent.Result).FieldErrors!.Keys);

        _plantsController.ModelState.Clear();
        var missingStrain = _plantsController.Create(new CreatePlantInstanceRequest { Label = "Clone", StrainId = 9999 });
        Assert.Contains(nameof(CreatePlantInstanceRequest.StrainId), AssertValidationError(missingStrain.Result).FieldErrors!.Keys);
    }

    [Fact]
    public void PlantApi_RejectsInvalidDatesAndReferences()
    {
        var missingSetup = _plantsController.Create(new CreatePlantInstanceRequest { Label = "Plant", SetupId = 9999 });
        Assert.Contains(nameof(CreatePlantInstanceRequest.SetupId), AssertValidationError(missingSetup.Result).FieldErrors!.Keys);

        _plantsController.ModelState.Clear();
        var missingGrow = _plantsController.Create(new CreatePlantInstanceRequest { Label = "Plant", GrowId = 9999 });
        Assert.Contains(nameof(CreatePlantInstanceRequest.GrowId), AssertValidationError(missingGrow.Result).FieldErrors!.Keys);

        _plantsController.ModelState.Clear();
        var dates = _plantsController.Create(new CreatePlantInstanceRequest
        {
            Label = "Plant",
            StartedAt = new DateTime(2026, 2, 1),
            EndedAt = new DateTime(2026, 1, 1)
        });
        Assert.Contains(nameof(CreatePlantInstanceRequest.EndedAt), AssertValidationError(dates.Result).FieldErrors!.Keys);
    }

    private static ApiError AssertValidationError(ActionResult? result)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("validation_failed", error.Code);
        return error;
    }
}
