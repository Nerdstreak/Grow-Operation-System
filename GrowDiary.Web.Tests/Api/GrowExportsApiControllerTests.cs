using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Tests.Api;

public sealed class GrowExportsApiControllerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;
    private readonly GrowExportsApiController _controller;

    public GrowExportsApiControllerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"grow-export-test-{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", _dbPath);
        _paths = new AppPaths(AppContext.BaseDirectory);
        TestDatabase.InitializeWithDefaultTent(_paths);
        _repository = new GrowRepository(_paths);
        _controller = new GrowExportsApiController(
            _repository,
            new JournalRepository(_paths),
            new TaskRepository(_paths),
            new HarvestRepository(_paths));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
    }

    [Fact]
    public void ExportGrow_IncludesIntegrityHashSectionCountsAndHydroSnapshot()
    {
        var growId = CreateGrowWithOperations();

        var export = Export(growId);

        Assert.Equal("grow-os.grow-export.v1", export.SchemaVersion);
        Assert.StartsWith($"grow-{growId}-", export.ExportId);
        Assert.False(string.IsNullOrWhiteSpace(export.IntegrityHash));
        Assert.Equal(64, export.IntegrityHash.Length);
        Assert.NotNull(export.HydroSetupSnapshot);
        Assert.Equal(1, export.SectionCounts.Measurements);
        Assert.Equal(1, export.SectionCounts.AddbackLogs);
        Assert.Equal(1, export.SectionCounts.Changeouts);
        Assert.Equal(export.Measurements.Count, export.SectionCounts.Measurements);
        Assert.Equal(export.AddbackLogs.Count, export.SectionCounts.AddbackLogs);
        Assert.Equal(export.Changeouts.Count, export.SectionCounts.Changeouts);
    }

    [Fact]
    public void ValidateExport_AcceptsFreshExportWithoutImportingData()
    {
        var growId = CreateGrowWithOperations();
        var export = Export(growId);

        var result = _controller.ValidateExport(export);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<GrowExportValidationDto>(ok.Value);
        Assert.True(dto.IsValid);
        Assert.True(dto.IntegrityHashValid);
        Assert.True(dto.SectionCountsValid);
        Assert.False(dto.ContainsPotentialSecrets);
        Assert.Empty(dto.Errors);
        Assert.Equal(export.SectionCounts, dto.ActualSectionCounts);
        Assert.Single(_repository.GetAllGrows());
    }

    [Fact]
    public void ValidateExport_RejectsTamperedSectionCountsAndHash()
    {
        var growId = CreateGrowWithOperations();
        var export = Export(growId);
        var tampered = export with
        {
            SectionCounts = export.SectionCounts with { Measurements = export.SectionCounts.Measurements + 1 }
        };

        var result = _controller.ValidateExport(tampered);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<GrowExportValidationDto>(ok.Value);
        Assert.False(dto.IsValid);
        Assert.False(dto.SectionCountsValid);
        Assert.False(dto.IntegrityHashValid);
        Assert.Contains(dto.Errors, error => error.Contains("SectionCounts", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dto.Errors, error => error.Contains("IntegrityHash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateExport_RejectsPotentialSecrets()
    {
        var growId = CreateGrowWithOperations();
        var export = Export(growId);
        var tampered = export with
        {
            Grow = export.Grow with { Notes = "secret-token" }
        };

        var result = _controller.ValidateExport(tampered);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<GrowExportValidationDto>(ok.Value);
        Assert.False(dto.IsValid);
        Assert.True(dto.ContainsPotentialSecrets);
        Assert.Contains(dto.Errors, error => error.Contains("Secrets", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void ImportPlan_ForValidExportPlansImportWithoutWritingData()
    {
        var growId = CreateGrowWithOperations();
        var export = Export(growId);

        var beforeCount = _repository.GetAllGrows().Count;
        var result = _controller.CreateImportPlan(export);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<GrowImportPlanDto>(ok.Value);
        Assert.Equal("grow-os.grow-import-plan.v1", dto.ImportPlanSchema);
        Assert.True(dto.ExportValid);
        Assert.False(dto.ImportSupported);
        Assert.False(dto.WouldModifyDatabase);
        Assert.Equal(export.ExportId, dto.ExportId);
        Assert.Equal(export.SectionCounts, dto.SectionCounts);
        Assert.Contains(dto.PlannedItems, item => item.Kind == "grow" && item.Action == "create-new-local-grow" && item.Count == 1);
        Assert.Contains(dto.PlannedItems, item => item.Kind == "measurements" && item.Count == 1);
        Assert.Contains(dto.PlannedItems, item => item.Kind == "addback-logs" && item.Count == 1);
        Assert.Contains(dto.PlannedItems, item => item.Kind == "changeouts" && item.Count == 1);
        Assert.Contains(dto.Conflicts, conflict => conflict.Kind == "possible-duplicate-grow");
        Assert.Equal(beforeCount, _repository.GetAllGrows().Count);
    }

    [Fact]
    public void ImportPlan_ForInvalidExportReturnsBlockersAndDoesNotPlanWrites()
    {
        var growId = CreateGrowWithOperations();
        var export = Export(growId);
        var tampered = export with
        {
            SectionCounts = export.SectionCounts with { AddbackLogs = export.SectionCounts.AddbackLogs + 1 }
        };

        var beforeCount = _repository.GetAllGrows().Count;
        var result = _controller.CreateImportPlan(tampered);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<GrowImportPlanDto>(ok.Value);
        Assert.False(dto.ExportValid);
        Assert.False(dto.ImportSupported);
        Assert.False(dto.WouldModifyDatabase);
        Assert.Empty(dto.PlannedItems);
        Assert.Contains(dto.Blockers, blocker => blocker.Contains("SectionCounts", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dto.Blockers, blocker => blocker.Contains("IntegrityHash", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(beforeCount, _repository.GetAllGrows().Count);
    }

    private GrowExportDto Export(int growId)
    {
        var result = _controller.ExportGrow(growId);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<GrowExportDto>(ok.Value);
    }

    private int CreateGrowWithOperations()
    {
        var tent = _repository.GetTents().Single();
        var hydroSetup = _repository.CreateHydroSetup(new GrowSystem
        {
            TentId = tent.Id,
            Name = "RDWC Export System",
            HydroStyle = HydroStyle.RDWC.ToString(),
            PotCount = 4,
            PotSizeLiters = 19,
            ReservoirLiters = 60,
            LayoutType = HydroSetupLayoutType.Grid2x2,
            ReservoirPosition = ReservoirPosition.External,
            Status = HydroSetupStatus.Active
        });

        var growId = _repository.CreateGrow(new GrowRun
        {
            TentId = tent.Id,
            SystemId = hydroSetup.Id,
            Name = "Export Grow",
            StartDate = new DateTime(2026, 1, 1),
            Status = GrowStatus.Running,
            MediumType = MediumType.Hydro,
            HydroStyle = HydroStyle.RDWC
        });

        _repository.CreateMeasurement(new Measurement
        {
            GrowId = growId,
            Stage = GrowStage.Veg,
            TakenAt = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc),
            ReservoirEc = 0.9,
            ReservoirPh = 5.9
        });

        _repository.CreateAddbackLog(new AddbackLogEntry
        {
            GrowId = growId,
            HydroSetupId = hydroSetup.Id,
            Kind = AddbackLogKind.Addback,
            ReservoirLiters = 136,
            EcBefore = 0.9,
            EcTarget = 1.2,
            LitersAdded = 12,
            UsedHydroSetupVolume = true
        });

        _repository.CreateChangeout(new ChangeoutEntry
        {
            GrowId = growId,
            HydroSetupId = hydroSetup.Id,
            Kind = ChangeoutKind.Partial,
            VolumeChangedLiters = 40,
            PercentChanged = 30
        });

        return growId;
    }
}
