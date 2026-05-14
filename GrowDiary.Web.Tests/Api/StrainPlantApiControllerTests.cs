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
        GrowDiary.Web.Tests.TestDatabase.InitializeWithDefaultTent(_paths);
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

    [Fact]
    public void CloneFromMother_CreatesCloneWithParentAndInheritedStrain()
    {
        var tent = _repository.GetTents().Single();
        var strain = _repository.CreateStrain(new Strain { Name = "Mother Line" });
        var motherSetup = _repository.CreateSetup(new Setup { TentId = tent.Id, Name = "Mother Setup", SetupType = SetupType.Mother });
        var quarantineSetup = _repository.CreateSetup(new Setup { TentId = tent.Id, Name = "Quarantine Setup", SetupType = SetupType.Quarantine });
        var mother = _repository.CreatePlant(new PlantInstance
        {
            StrainId = strain.Id,
            SetupId = motherSetup.Id,
            Label = "Mother A",
            PlantRole = PlantRole.Mother,
            PlantStatus = PlantStatus.Active
        });

        var result = _plantsController.CloneFromMother(new CreateCloneFromMotherRequest
        {
            MotherPlantId = mother.Id,
            TargetSetupId = quarantineSetup.Id,
            Label = "Clone A1",
            PhenoLabel = "A",
            Notes = "fresh cut",
            CutAt = new DateTime(2026, 3, 4, 10, 30, 0)
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var clone = Assert.IsType<PlantInstanceDto>(created.Value);
        Assert.Equal(PlantRole.Clone, clone.PlantRole);
        Assert.Equal(PlantStatus.Active, clone.PlantStatus);
        Assert.Equal(mother.Id, clone.ParentPlantId);
        Assert.Equal(strain.Id, clone.StrainId);
        Assert.Equal(quarantineSetup.Id, clone.SetupId);
        Assert.Null(clone.GrowId);
    }

    [Fact]
    public void CloneFromMother_UpdatesMotherSetupCounterAndLastCloneCutAt()
    {
        var tent = _repository.GetTents().Single();
        var cutAt = new DateTime(2026, 3, 5, 9, 15, 0);
        var motherSetup = _repository.CreateSetup(new Setup
        {
            TentId = tent.Id,
            Name = "Mother Setup",
            SetupType = SetupType.Mother,
            CloneCounterTotal = 2
        });
        var mother = _repository.CreatePlant(new PlantInstance
        {
            SetupId = motherSetup.Id,
            Label = "Mother A",
            PlantRole = PlantRole.Mother,
            PlantStatus = PlantStatus.Active
        });

        var result = _plantsController.CloneFromMother(new CreateCloneFromMotherRequest
        {
            MotherPlantId = mother.Id,
            Label = "Clone A1",
            CutAt = cutAt
        });

        Assert.IsType<CreatedAtActionResult>(result.Result);
        var updatedSetup = _repository.GetSetup(motherSetup.Id)!;
        Assert.Equal(3, updatedSetup.CloneCounterTotal);
        Assert.Equal(cutAt, updatedSetup.LastCloneCutAt);
    }

    [Fact]
    public void CloneFromMother_RejectsInvalidSourcesTargetsAndStrains()
    {
        var tent = _repository.GetTents().Single();
        var motherSetup = _repository.CreateSetup(new Setup { TentId = tent.Id, Name = "Mother Setup", SetupType = SetupType.Mother });
        var productionSetup = _repository.CreateSetup(new Setup { TentId = tent.Id, Name = "Production Setup", SetupType = SetupType.Production });
        var source = _repository.CreatePlant(new PlantInstance
        {
            SetupId = motherSetup.Id,
            Label = "Not Mother",
            PlantRole = PlantRole.Clone,
            PlantStatus = PlantStatus.Active
        });

        var nonMother = _plantsController.CloneFromMother(new CreateCloneFromMotherRequest { MotherPlantId = source.Id, Label = "Clone A1" });
        Assert.Contains(nameof(CreateCloneFromMotherRequest.MotherPlantId), AssertValidationError(nonMother.Result).FieldErrors!.Keys);

        _plantsController.ModelState.Clear();
        var missingMother = _plantsController.CloneFromMother(new CreateCloneFromMotherRequest { MotherPlantId = 9999, Label = "Clone A1" });
        Assert.Contains(nameof(CreateCloneFromMotherRequest.MotherPlantId), AssertValidationError(missingMother.Result).FieldErrors!.Keys);

        _plantsController.ModelState.Clear();
        var badTarget = _plantsController.CloneFromMother(new CreateCloneFromMotherRequest
        {
            MotherPlantId = _repository.CreatePlant(new PlantInstance { SetupId = motherSetup.Id, Label = "Mother A", PlantRole = PlantRole.Mother }).Id,
            TargetSetupId = productionSetup.Id,
            Label = "Clone A1"
        });
        Assert.Contains(nameof(CreateCloneFromMotherRequest.TargetSetupId), AssertValidationError(badTarget.Result).FieldErrors!.Keys);

        _plantsController.ModelState.Clear();
        var motherTarget = _plantsController.CloneFromMother(new CreateCloneFromMotherRequest
        {
            MotherPlantId = _repository.GetPlantsBySetup(motherSetup.Id).Single(plant => plant.PlantRole == PlantRole.Mother).Id,
            TargetSetupId = motherSetup.Id,
            Label = "Clone A1"
        });
        Assert.Contains(nameof(CreateCloneFromMotherRequest.TargetSetupId), AssertValidationError(motherTarget.Result).FieldErrors!.Keys);

        _plantsController.ModelState.Clear();
        var badStrain = _plantsController.CloneFromMother(new CreateCloneFromMotherRequest
        {
            MotherPlantId = _repository.GetPlantsBySetup(motherSetup.Id).Single(plant => plant.PlantRole == PlantRole.Mother).Id,
            StrainId = 9999,
            Label = "Clone A1"
        });
        Assert.Contains(nameof(CreateCloneFromMotherRequest.StrainId), AssertValidationError(badStrain.Result).FieldErrors!.Keys);
    }

    [Fact]
    public void DecideQuarantine_ClearedWithoutTargetKeepsPlantInQuarantineAndSetsResult()
    {
        var tent = _repository.GetTents().Single();
        var quarantineSetup = _repository.CreateSetup(new Setup { TentId = tent.Id, Name = "Quarantine Setup", SetupType = SetupType.Quarantine });
        var plant = _repository.CreatePlant(new PlantInstance
        {
            SetupId = quarantineSetup.Id,
            Label = "Clone A1",
            PlantRole = PlantRole.Clone,
            PlantStatus = PlantStatus.Planned
        });

        var result = _plantsController.DecideQuarantine(new DecideQuarantinePlantRequest
        {
            PlantId = plant.Id,
            Decision = "Cleared",
            Notes = "clean"
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var updated = Assert.IsType<PlantInstanceDto>(ok.Value);
        Assert.Equal(quarantineSetup.Id, updated.SetupId);
        Assert.Null(updated.GrowId);
        Assert.Equal(PlantRole.Clone, updated.PlantRole);
        Assert.Equal(PlantStatus.Active, updated.PlantStatus);
        Assert.Equal("clean", updated.Notes);
        Assert.Equal("Cleared", _repository.GetSetup(quarantineSetup.Id)!.QuarantineResult);
    }

    [Fact]
    public void DecideQuarantine_ClearedCanMovePlantToProductionSetupAndGrow()
    {
        var tent = _repository.GetTents().Single();
        var quarantineSetup = _repository.CreateSetup(new Setup { TentId = tent.Id, Name = "Quarantine Setup", SetupType = SetupType.Quarantine });
        var productionSetup = _repository.CreateSetup(new Setup { TentId = tent.Id, Name = "Production Setup", SetupType = SetupType.Production });
        var growId = _repository.CreateGrow(new GrowRun
        {
            TentId = tent.Id,
            SetupId = productionSetup.Id,
            Name = "Production Grow",
            StartDate = new DateTime(2026, 4, 1),
            Status = GrowStatus.Planning
        });
        var plant = _repository.CreatePlant(new PlantInstance
        {
            SetupId = quarantineSetup.Id,
            Label = "Clone A1",
            PlantRole = PlantRole.Clone,
            PlantStatus = PlantStatus.Active
        });

        var result = _plantsController.DecideQuarantine(new DecideQuarantinePlantRequest
        {
            PlantId = plant.Id,
            Decision = "Cleared",
            TargetSetupId = productionSetup.Id,
            TargetGrowId = growId
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var updated = Assert.IsType<PlantInstanceDto>(ok.Value);
        Assert.Equal(productionSetup.Id, updated.SetupId);
        Assert.Equal(growId, updated.GrowId);
        Assert.Equal(PlantRole.Production, updated.PlantRole);
        Assert.Equal(PlantStatus.Active, updated.PlantStatus);
        Assert.Equal("Cleared", _repository.GetSetup(quarantineSetup.Id)!.QuarantineResult);
    }

    [Fact]
    public void DecideQuarantine_RejectedCullsPlantAndSetsResult()
    {
        var tent = _repository.GetTents().Single();
        var decidedAt = new DateTime(2026, 4, 2, 8, 45, 0);
        var quarantineSetup = _repository.CreateSetup(new Setup { TentId = tent.Id, Name = "Quarantine Setup", SetupType = SetupType.Quarantine });
        var plant = _repository.CreatePlant(new PlantInstance
        {
            SetupId = quarantineSetup.Id,
            Label = "Clone A1",
            PlantRole = PlantRole.Clone,
            PlantStatus = PlantStatus.Active
        });

        var result = _plantsController.DecideQuarantine(new DecideQuarantinePlantRequest
        {
            PlantId = plant.Id,
            Decision = "Rejected",
            DecidedAt = decidedAt,
            Notes = "discard"
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var updated = Assert.IsType<PlantInstanceDto>(ok.Value);
        Assert.Equal(PlantStatus.Culled, updated.PlantStatus);
        Assert.Equal(decidedAt, updated.EndedAt);
        Assert.Equal("discard", updated.Notes);
        Assert.Equal("Rejected", _repository.GetSetup(quarantineSetup.Id)!.QuarantineResult);
    }

    [Fact]
    public void DecideQuarantine_RejectsInvalidDecisionTargetsAndNonQuarantinePlants()
    {
        var tent = _repository.GetTents().Single();
        var quarantineSetup = _repository.CreateSetup(new Setup { TentId = tent.Id, Name = "Quarantine Setup", SetupType = SetupType.Quarantine });
        var motherSetup = _repository.CreateSetup(new Setup { TentId = tent.Id, Name = "Mother Setup", SetupType = SetupType.Mother });
        var productionSetup = _repository.CreateSetup(new Setup { TentId = tent.Id, Name = "Production Setup", SetupType = SetupType.Production });
        var quarantinePlant = _repository.CreatePlant(new PlantInstance { SetupId = quarantineSetup.Id, Label = "Clone A1", PlantRole = PlantRole.Clone });
        var motherPlant = _repository.CreatePlant(new PlantInstance { SetupId = motherSetup.Id, Label = "Mother A", PlantRole = PlantRole.Mother });
        var growId = _repository.CreateGrow(new GrowRun { TentId = tent.Id, Name = "Grow", StartDate = new DateTime(2026, 4, 1), Status = GrowStatus.Planning });

        var invalidDecision = _plantsController.DecideQuarantine(new DecideQuarantinePlantRequest { PlantId = quarantinePlant.Id, Decision = "Pending" });
        Assert.Contains(nameof(DecideQuarantinePlantRequest.Decision), AssertValidationError(invalidDecision.Result).FieldErrors!.Keys);

        _plantsController.ModelState.Clear();
        var badSetup = _plantsController.DecideQuarantine(new DecideQuarantinePlantRequest { PlantId = quarantinePlant.Id, Decision = "Cleared", TargetSetupId = motherSetup.Id });
        Assert.Contains(nameof(DecideQuarantinePlantRequest.TargetSetupId), AssertValidationError(badSetup.Result).FieldErrors!.Keys);

        _plantsController.ModelState.Clear();
        var rejectedWithTarget = _plantsController.DecideQuarantine(new DecideQuarantinePlantRequest
        {
            PlantId = quarantinePlant.Id,
            Decision = "Rejected",
            TargetSetupId = productionSetup.Id,
            TargetGrowId = growId
        });
        Assert.Contains(nameof(DecideQuarantinePlantRequest.TargetSetupId), AssertValidationError(rejectedWithTarget.Result).FieldErrors!.Keys);
        Assert.Contains(nameof(DecideQuarantinePlantRequest.TargetGrowId), AssertValidationError(rejectedWithTarget.Result).FieldErrors!.Keys);

        _plantsController.ModelState.Clear();
        var nonQuarantine = _plantsController.DecideQuarantine(new DecideQuarantinePlantRequest { PlantId = motherPlant.Id, Decision = "Cleared" });
        Assert.Contains(nameof(DecideQuarantinePlantRequest.PlantId), AssertValidationError(nonQuarantine.Result).FieldErrors!.Keys);
    }

    [Fact]
    public void DecideQuarantine_RejectsMismatchedProductionSetupAndGrow()
    {
        var tent = _repository.GetTents().Single();
        var otherTent = _repository.CreateTent("Other Tent");
        var quarantineSetup = _repository.CreateSetup(new Setup { TentId = tent.Id, Name = "Quarantine Setup", SetupType = SetupType.Quarantine });
        var productionSetup = _repository.CreateSetup(new Setup { TentId = tent.Id, Name = "Production Setup", SetupType = SetupType.Production });
        var otherSetup = _repository.CreateSetup(new Setup { TentId = otherTent.Id, Name = "Other Production", SetupType = SetupType.Production });
        var otherGrowId = _repository.CreateGrow(new GrowRun
        {
            TentId = otherTent.Id,
            SetupId = otherSetup.Id,
            Name = "Other Grow",
            StartDate = new DateTime(2026, 4, 1),
            Status = GrowStatus.Planning
        });
        var plant = _repository.CreatePlant(new PlantInstance { SetupId = quarantineSetup.Id, Label = "Clone A1", PlantRole = PlantRole.Clone });

        var result = _plantsController.DecideQuarantine(new DecideQuarantinePlantRequest
        {
            PlantId = plant.Id,
            Decision = "Cleared",
            TargetSetupId = productionSetup.Id,
            TargetGrowId = otherGrowId
        });

        var error = AssertValidationError(result.Result);
        Assert.Contains(nameof(DecideQuarantinePlantRequest.TargetGrowId), error.FieldErrors!.Keys);
    }

    private static ApiError AssertValidationError(ActionResult? result)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("validation_failed", error.Code);
        return error;
    }
}
