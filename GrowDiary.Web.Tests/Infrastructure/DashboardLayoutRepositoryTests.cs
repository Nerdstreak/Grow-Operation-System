using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Infrastructure;

public sealed class DashboardLayoutRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DashboardLayoutRepository _repository;

    public DashboardLayoutRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"growos-dash-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbPath);
        var paths = new AppPaths(_dbPath);
        new DatabaseInitializer(paths, NullLogger<DatabaseInitializer>.Instance).Initialize();
        _repository = new DashboardLayoutRepository(paths);
    }

    [Fact]
    public void WithoutCustomisation_TheBuiltInLayoutIsUsed()
    {
        var layout = _repository.Get(1);

        Assert.Equal(2, layout.Sections.Count);
        Assert.Contains(layout.Sections, section => section.Title == "Klima");
        Assert.Contains(layout.Sections, section => section.Title == "Reservoir");
    }

    [Fact]
    public void SavedLayout_SurvivesTheRoundTrip_IncludingCustomEntityTiles()
    {
        var saved = new DashboardLayout
        {
            TentId = 1,
            Sections =
            [
                new DashboardSection
                {
                    Id = "mine",
                    Title = "Technik",
                    Tiles =
                    [
                        new DashboardTile { Id = "uv", Kind = DashboardTileKind.Entity, EntityId = "switch.eheim_uv", Label = "UV-Klärer" },
                        new DashboardTile { Id = "t", Kind = DashboardTileKind.Metric, MetricKey = "temperature" },
                    ],
                },
            ],
        };

        _repository.Save(saved);
        var loaded = _repository.Get(1);

        var section = Assert.Single(loaded.Sections);
        Assert.Equal("Technik", section.Title);
        Assert.Equal(2, section.Tiles.Count);
        var custom = section.Tiles[0];
        Assert.Equal(DashboardTileKind.Entity, custom.Kind);
        Assert.Equal("switch.eheim_uv", custom.EntityId);
        Assert.Equal("UV-Klärer", custom.Label);
    }

    [Fact]
    public void Reset_FallsBackToTheBuiltInLayout()
    {
        _repository.Save(new DashboardLayout
        {
            TentId = 1,
            Sections = [new DashboardSection { Title = "Nur eins", Tiles = [new DashboardTile { MetricKey = "temperature" }] }],
        });

        _repository.Reset(1);

        Assert.Equal(2, _repository.Get(1).Sections.Count);
    }

    [Fact]
    public void EmptyLayout_IsTreatedAsNoCustomisation()
    {
        // Saving "nothing" must not leave the user with a blank dashboard.
        _repository.Save(new DashboardLayout { TentId = 1, Sections = [] });

        Assert.Equal(2, _repository.Get(1).Sections.Count);
    }

    [Fact]
    public void LayoutsAreKeptPerTent()
    {
        _repository.Save(new DashboardLayout
        {
            TentId = 2,
            Sections = [new DashboardSection { Title = "Zelt 2", Tiles = [new DashboardTile { MetricKey = "humidity" }] }],
        });

        Assert.Equal("Zelt 2", Assert.Single(_repository.Get(2).Sections).Title);
        Assert.Equal(2, _repository.Get(1).Sections.Count); // untouched
    }

    public void Dispose()
    {
        try { Directory.Delete(_dbPath, recursive: true); } catch { /* temp dir */ }
    }
}
