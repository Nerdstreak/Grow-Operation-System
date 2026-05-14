using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

public sealed class GrowsApiControllerSetupTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _tempRoot;
    private readonly AppPaths _paths;
    private readonly GrowRepository _growRepository;
    private readonly GrowsApiController _controller;

    public GrowsApiControllerSetupTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"grow-test-{Guid.NewGuid():N}.db");
        _tempRoot = Path.Combine(Path.GetTempPath(), "GrowControllerTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        CopyDefaults(Path.Combine(FindProjectRoot(), "GrowDiary.Web", "wwwroot", "knowledge-defaults"), _tempRoot);
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", _dbPath);
        _paths = new AppPaths(_tempRoot);
        GrowDiary.Web.Tests.TestDatabase.InitializeWithDefaultTent(_paths);
        _growRepository = new GrowRepository(_paths);
        _controller = new GrowsApiController(
            _growRepository,
            new AuditRepository(_paths),
            new WeekCounterService(),
            CreateDeviationAnalyzer(),
            CreateTreatmentRecommender());
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
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

    [Theory]
    [InlineData(HydroStyle.DWC)]
    [InlineData(HydroStyle.RDWC)]
    public void Create_WithDwcOrRdwcHydroStyle_IsAccepted(HydroStyle hydroStyle)
    {
        var request = NewGrowRequest($"{hydroStyle} Grow");
        request.HydroStyle = hydroStyle;

        var result = _controller.Create(request);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<GrowDetailDto>(created.Value);
        Assert.Equal(hydroStyle, dto.HydroStyle);
    }

    [Theory]
    [InlineData(HydroStyle.NFT)]
    [InlineData(HydroStyle.Aeroponic)]
    [InlineData(HydroStyle.Other)]
    [InlineData(HydroStyle.None)]
    public void Create_WithNonDwcHydroStyle_IsRejected(HydroStyle hydroStyle)
    {
        var request = NewGrowRequest($"{hydroStyle} Grow");
        request.HydroStyle = hydroStyle;

        var result = _controller.Create(request);

        AssertHydroStyleValidationError(result.Result);
    }

    [Theory]
    [InlineData(HydroStyle.NFT)]
    [InlineData(HydroStyle.Aeroponic)]
    [InlineData(HydroStyle.Other)]
    [InlineData(HydroStyle.None)]
    public void Update_WithNonDwcHydroStyle_IsRejected(HydroStyle hydroStyle)
    {
        var growId = _growRepository.CreateGrow(new GrowRun
        {
            Name = "Original",
            StartDate = new DateTime(2026, 1, 1),
            Status = GrowStatus.Planning,
            HydroStyle = HydroStyle.RDWC
        });
        var request = NewGrowRequest("Updated");
        request.HydroStyle = hydroStyle;

        var result = _controller.Update(growId, request);

        AssertHydroStyleValidationError(result.Result);
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

    [Fact]
    public void Deviations_MissingGrow_Returns404()
    {
        var result = _controller.Deviations(9999).Result;

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiError>(notFound.Value);
        Assert.Equal("grow_not_found", error.Code);
    }

    [Fact]
    public void Deviations_WithHydroMeasurement_ReturnsStructuredDeviations()
    {
        var growId = _growRepository.CreateGrow(new GrowRun
        {
            Name = "Hydro Grow",
            StartDate = new DateTime(2026, 1, 1),
            MediumType = MediumType.Hydro,
            IrrigationType = IrrigationType.ActiveHydro,
            HydroStyle = HydroStyle.RDWC,
            Status = GrowStatus.Running
        });
        _growRepository.CreateMeasurement(new Measurement
        {
            GrowId = growId,
            Stage = GrowStage.Veg,
            TakenAt = DateTime.UtcNow,
            ReservoirWaterTempC = 25
        });

        var result = _controller.Deviations(growId).Result;

        var ok = Assert.IsType<OkObjectResult>(result);
        var deviations = Assert.IsAssignableFrom<IReadOnlyList<GrowDeviation>>(ok.Value);
        var deviation = Assert.Single(deviations);
        Assert.Equal(DeviationMetric.WaterTemp, deviation.Metric);
        Assert.Equal(DeviationSeverity.Critical, deviation.Severity);
    }

    [Fact]
    public void TreatmentRecommendations_MissingGrow_Returns404()
    {
        var result = _controller.TreatmentRecommendations(9999).Result;

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiError>(notFound.Value);
        Assert.Equal("grow_not_found", error.Code);
    }

    [Fact]
    public void TreatmentRecommendations_WithMatchingDeviation_ReturnsRecommendations()
    {
        var growId = _growRepository.CreateGrow(new GrowRun
        {
            Name = "Hydro Grow",
            StartDate = new DateTime(2026, 1, 1),
            MediumType = MediumType.Hydro,
            IrrigationType = IrrigationType.ActiveHydro,
            HydroStyle = HydroStyle.RDWC,
            Status = GrowStatus.Running
        });
        _growRepository.CreateMeasurement(new Measurement
        {
            GrowId = growId,
            Stage = GrowStage.Veg,
            TakenAt = DateTime.UtcNow,
            ReservoirPh = 6.7
        });

        var result = _controller.TreatmentRecommendations(growId).Result;

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<GrowTreatmentRecommendationDto>(ok.Value);
        Assert.Contains(dto.Recommendations, item => item.TreatmentId == "ph-correction-down");
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

    private DeviationAnalyzerService CreateDeviationAnalyzer()
    {
        var loader = new KnowledgeBaseLoader(_paths, NullLogger<KnowledgeBaseLoader>.Instance);
        loader.Initialize();
        return new DeviationAnalyzerService(new TargetValueService(loader));
    }

    private TreatmentRecommender CreateTreatmentRecommender()
    {
        var loader = new KnowledgeBaseLoader(_paths, NullLogger<KnowledgeBaseLoader>.Instance);
        loader.Initialize();
        return new TreatmentRecommender(loader);
    }

    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.sln").Length > 0 ||
                Directory.Exists(Path.Combine(dir, "GrowDiary.Web")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Project root not found");
    }

    private static void CopyDefaults(string source, string tempRoot)
    {
        var dest = Path.Combine(tempRoot, "wwwroot", "knowledge-defaults");
        foreach (var file in Directory.EnumerateFiles(source, "*.json", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, file);
            var target = Path.Combine(dest, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static void AssertSetupValidationError(ActionResult? result)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("validation_failed", error.Code);
        Assert.Contains(nameof(GrowUpsertRequest.SetupId), error.FieldErrors!.Keys);
    }

    private static void AssertHydroStyleValidationError(ActionResult? result)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("validation_failed", error.Code);
        Assert.Contains(nameof(GrowUpsertRequest.HydroStyle), error.FieldErrors!.Keys);
    }
}
