using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

public sealed class HydroSetupsApiControllerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;
    private readonly HydroSetupsApiController _controller;

    public HydroSetupsApiControllerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"grow-test-{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", _dbPath);
        _paths = new AppPaths(Path.GetTempPath());
        GrowDiary.Web.Tests.TestDatabase.InitializeWithDefaultTent(_paths);
        _repository = new GrowRepository(_paths);
        _controller = new HydroSetupsApiController(_repository);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
    }

    [Fact]
    public void Create_WithDwcSetup_IsAcceptedAndDefaultsPotCount()
    {
        var tent = DefaultTent();

        var result = _controller.Create(new CreateHydroSetupRequest
        {
            TentId = tent.Id,
            Name = "DWC 25L Eimer",
            HydroStyle = HydroStyle.DWC,
            PotSizeLiters = 25
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<HydroSetupDto>(created.Value);
        Assert.Equal(tent.Id, dto.TentId);
        Assert.Equal(tent.Name, dto.TentName);
        Assert.Equal(HydroStyle.DWC, dto.HydroStyle);
        Assert.Equal(1, dto.PotCount);
        Assert.Equal(25, dto.TotalVolumeLiters);
        Assert.Equal(HydroSetupStatus.Active, dto.Status);
    }

    [Fact]
    public void Create_WithRdwcSetup_IsAcceptedAndCalculatesTotalVolume()
    {
        var tent = DefaultTent();

        var result = _controller.Create(new CreateHydroSetupRequest
        {
            TentId = tent.Id,
            Name = "RDWC 4-Site 19L + 60L Tank",
            HydroStyle = HydroStyle.RDWC,
            PotCount = 4,
            PotSizeLiters = 19,
            ReservoirLiters = 60,
            LayoutType = HydroSetupLayoutType.Grid2x2,
            ReservoirPosition = ReservoirPosition.External,
            HasCirculationPump = true,
            HasAirPump = true,
            AirStoneCount = 4
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<HydroSetupDto>(created.Value);
        Assert.Equal(HydroStyle.RDWC, dto.HydroStyle);
        Assert.Equal(136, dto.TotalVolumeLiters);
        Assert.Equal(HydroSetupLayoutType.Grid2x2, dto.LayoutType);
        Assert.Equal(ReservoirPosition.External, dto.ReservoirPosition);
    }

    [Theory]
    [InlineData(HydroStyle.NFT)]
    [InlineData(HydroStyle.Aeroponic)]
    [InlineData(HydroStyle.Other)]
    [InlineData(HydroStyle.None)]
    public void Create_WithNonDwcHydroStyle_IsRejected(HydroStyle hydroStyle)
    {
        var result = _controller.Create(new CreateHydroSetupRequest
        {
            TentId = DefaultTent().Id,
            Name = "Invalid",
            HydroStyle = hydroStyle,
            PotCount = 2,
            PotSizeLiters = 19
        });

        var error = AssertValidationError(result.Result);
        Assert.Contains(nameof(CreateHydroSetupRequest.HydroStyle), error.FieldErrors!.Keys);
    }

    [Fact]
    public void Create_WithBlankName_IsRejected()
    {
        var result = _controller.Create(new CreateHydroSetupRequest
        {
            TentId = DefaultTent().Id,
            Name = " ",
            HydroStyle = HydroStyle.DWC
        });

        var error = AssertValidationError(result.Result);
        Assert.Contains(nameof(CreateHydroSetupRequest.Name), error.FieldErrors!.Keys);
    }

    [Fact]
    public void Create_WithInvalidTentId_IsRejected()
    {
        var result = _controller.Create(new CreateHydroSetupRequest
        {
            TentId = 9999,
            Name = "DWC",
            HydroStyle = HydroStyle.DWC
        });

        var error = AssertValidationError(result.Result);
        Assert.Contains(nameof(CreateHydroSetupRequest.TentId), error.FieldErrors!.Keys);
    }

    [Fact]
    public void Create_WithRdwcPotCountBelowTwo_IsRejected()
    {
        var result = _controller.Create(new CreateHydroSetupRequest
        {
            TentId = DefaultTent().Id,
            Name = "Too small RDWC",
            HydroStyle = HydroStyle.RDWC,
            PotCount = 1,
            PotSizeLiters = 19
        });

        var error = AssertValidationError(result.Result);
        Assert.Contains(nameof(CreateHydroSetupRequest.PotCount), error.FieldErrors!.Keys);
    }

    [Fact]
    public void Create_WithNegativeVolume_IsRejected()
    {
        var result = _controller.Create(new CreateHydroSetupRequest
        {
            TentId = DefaultTent().Id,
            Name = "Negative",
            HydroStyle = HydroStyle.DWC,
            PotSizeLiters = -1
        });

        var error = AssertValidationError(result.Result);
        Assert.Contains(nameof(CreateHydroSetupRequest.PotSizeLiters), error.FieldErrors!.Keys);
    }

    [Fact]
    public void Create_WithRdwcSingleBucketLayout_IsRejected()
    {
        var result = _controller.Create(new CreateHydroSetupRequest
        {
            TentId = DefaultTent().Id,
            Name = "Invalid RDWC Layout",
            HydroStyle = HydroStyle.RDWC,
            PotCount = 4,
            PotSizeLiters = 19,
            ReservoirPosition = ReservoirPosition.External,
            LayoutType = HydroSetupLayoutType.SingleBucket
        });

        var error = AssertValidationError(result.Result);
        Assert.Contains(nameof(CreateHydroSetupRequest.LayoutType), error.FieldErrors!.Keys);
    }

    [Fact]
    public void Create_WithRdwcMissingTankPosition_IsRejected()
    {
        var result = _controller.Create(new CreateHydroSetupRequest
        {
            TentId = DefaultTent().Id,
            Name = "Invalid RDWC Tank",
            HydroStyle = HydroStyle.RDWC,
            PotCount = 4,
            PotSizeLiters = 19,
            LayoutType = HydroSetupLayoutType.Grid2x2,
            ReservoirPosition = ReservoirPosition.None
        });

        var error = AssertValidationError(result.Result);
        Assert.Contains(nameof(CreateHydroSetupRequest.ReservoirPosition), error.FieldErrors!.Keys);
    }

    [Fact]
    public void Create_WithNegativeDisplayOrder_IsRejected()
    {
        var result = _controller.Create(new CreateHydroSetupRequest
        {
            TentId = DefaultTent().Id,
            Name = "Invalid Order",
            HydroStyle = HydroStyle.DWC,
            PotSizeLiters = 25,
            DisplayOrder = -1
        });

        var error = AssertValidationError(result.Result);
        Assert.Contains(nameof(CreateHydroSetupRequest.DisplayOrder), error.FieldErrors!.Keys);
    }

    [Fact]
    public void Archive_SetsStatusArchived()
    {
        var setup = CreateRdwcSetup(DefaultTent().Id, "Archive Me");

        var result = _controller.Archive(setup.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<HydroSetupDto>(ok.Value);
        Assert.Equal(HydroSetupStatus.Archived, dto.Status);
        Assert.Equal(HydroSetupStatus.Archived, _repository.GetHydroSetup(setup.Id)!.Status);
    }

    [Fact]
    public void List_WithTentId_ReturnsOnlyHydroSetupsForTent()
    {
        var firstTent = DefaultTent();
        var secondTent = _repository.CreateTent(new Tent { Name = "Mutter Zelt 1", TentType = TentType.Mother });
        CreateRdwcSetup(firstTent.Id, "First");
        CreateRdwcSetup(secondTent.Id, "Second");

        var result = _controller.List(secondTent.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<HydroSetupDto>>(ok.Value);
        var item = Assert.Single(items);
        Assert.Equal(secondTent.Id, item.TentId);
        Assert.Equal("Second", item.Name);
    }

    [Fact]
    public void List_ExcludesArchivedHydroSetupsByDefault()
    {
        var tent = DefaultTent();
        var active = CreateRdwcSetup(tent.Id, "Active");
        var archived = CreateRdwcSetup(tent.Id, "Archived");
        _repository.ArchiveHydroSetup(archived.Id);

        var result = _controller.List();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<HydroSetupDto>>(ok.Value);
        var item = Assert.Single(items);
        Assert.Equal(active.Id, item.Id);
        Assert.Equal(HydroSetupStatus.Active, item.Status);
    }

    [Fact]
    public void List_WithIncludeArchived_ReturnsArchivedHydroSetups()
    {
        var tent = DefaultTent();
        CreateRdwcSetup(tent.Id, "Active");
        var archived = CreateRdwcSetup(tent.Id, "Archived");
        _repository.ArchiveHydroSetup(archived.Id);

        var result = _controller.List(includeArchived: true);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<HydroSetupDto>>(ok.Value);
        Assert.Equal(2, items.Count);
        Assert.Contains(items, item => item.Id == archived.Id && item.Status == HydroSetupStatus.Archived);
    }

    [Fact]
    public void Update_WithInvalidStatus_IsRejected()
    {
        var setup = CreateRdwcSetup(DefaultTent().Id, "Invalid Status");

        var result = _controller.Update(setup.Id, new UpdateHydroSetupRequest
        {
            TentId = setup.TentId,
            Name = setup.Name,
            HydroStyle = HydroStyle.RDWC,
            PotCount = setup.PotCount,
            PotSizeLiters = setup.PotSizeLiters,
            ReservoirLiters = setup.ReservoirLiters,
            LayoutType = setup.LayoutType,
            ReservoirPosition = setup.ReservoirPosition,
            Status = (HydroSetupStatus)999
        });

        var error = AssertValidationError(result.Result);
        Assert.Contains(nameof(UpdateHydroSetupRequest.Status), error.FieldErrors!.Keys);
    }

    private GrowSystem CreateRdwcSetup(int tentId, string name)
        => _repository.CreateHydroSetup(new GrowSystem
        {
            TentId = tentId,
            Name = name,
            HydroStyle = HydroStyle.RDWC.ToString(),
            PotCount = 4,
            PotSizeLiters = 19,
            ReservoirLiters = 60,
            LayoutType = HydroSetupLayoutType.Grid2x2,
            ReservoirPosition = ReservoirPosition.External
        });

    private Tent DefaultTent()
        => _repository.GetTents().Single();

    private static ApiError AssertValidationError(ActionResult? result)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("validation_failed", error.Code);
        return error;
    }
}
