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
    public void CameraTilesAndTheirWidth_SurviveTheRoundTrip()
    {
        // A tent with three cameras should be able to show all three at once, each as
        // wide as the user made it.
        _repository.Save(new DashboardLayout
        {
            TentId = 1,
            Sections =
            [
                new DashboardSection
                {
                    Title = "Kameras",
                    Tiles =
                    [
                        new DashboardTile { Id = "c1", Kind = DashboardTileKind.Camera, EntityId = "camera.links", Label = "Links", Span = 2 },
                        new DashboardTile { Id = "c2", Kind = DashboardTileKind.Camera, EntityId = "camera.mitte", Label = "Mitte" },
                        new DashboardTile { Id = "c3", Kind = DashboardTileKind.Camera, EntityId = "camera.rechts", Label = "Rechts" },
                    ],
                },
            ],
        });

        var tiles = Assert.Single(_repository.Get(1).Sections).Tiles;

        Assert.Equal(3, tiles.Count);
        Assert.All(tiles, tile => Assert.Equal(DashboardTileKind.Camera, tile.Kind));
        Assert.Equal(["camera.links", "camera.mitte", "camera.rechts"], tiles.Select(tile => tile.EntityId));
        Assert.Equal(2, tiles[0].Span);
        Assert.Equal(1, tiles[1].Span);
    }

    [Fact]
    public void SectionOrder_IsPreservedAsSaved()
    {
        // The order is the arrangement — if it drifted, moving a section would do nothing.
        _repository.Save(new DashboardLayout
        {
            TentId = 1,
            Sections =
            [
                new DashboardSection { Id = "b", Title = "Zweiter", Tiles = [new DashboardTile { MetricKey = "humidity" }] },
                new DashboardSection { Id = "a", Title = "Erster", Tiles = [new DashboardTile { MetricKey = "temperature" }] },
            ],
        });

        Assert.Equal(["Zweiter", "Erster"], _repository.Get(1).Sections.Select(section => section.Title));
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
    public void GetSaved_TellsCustomisedFromShipped()
    {
        // The screen needs this distinction: without a saved layout it draws its own
        // built-in arrangement, which knows things a stored layout cannot.
        Assert.Null(_repository.GetSaved(1));

        _repository.Save(new DashboardLayout
        {
            TentId = 1,
            Sections = [new DashboardSection { Title = "Meins", Tiles = [new DashboardTile { MetricKey = "vpd" }] }],
        });

        Assert.Equal("Meins", Assert.Single(_repository.GetSaved(1)!.Sections).Title);

        _repository.Reset(1);
        Assert.Null(_repository.GetSaved(1));
    }

    [Fact]
    public void GetSaved_TreatsAnEmptySaveAsNoCustomisation()
    {
        _repository.Save(new DashboardLayout { TentId = 1, Sections = [] });

        Assert.Null(_repository.GetSaved(1));
    }

    [Fact]
    public void LayoutFromBeforeTheRebuild_IsNotRevived()
    {
        // Ein Layout aus 1.6.0 wurde gegen einen anderen Bildschirm gebaut. Es
        // wiederzubeleben nähme dem Nutzer beim Update wortlos Werte weg, die er
        // seit Monaten sieht. Der Eintrag bleibt liegen, gilt aber nicht.
        var alt = new DashboardLayout
        {
            TentId = 1,
            Sections = [new DashboardSection { Title = "Kameras", Tiles = [new DashboardTile { MetricKey = "temperature" }] }],
        };
        _repository.Save(alt);
        DowngradeStoredVersion(1);

        Assert.Null(_repository.GetSaved(1));
        Assert.Equal(2, _repository.Get(1).Sections.Count); // der eingebaute Standard
    }

    [Fact]
    public void SavingStampsTheCurrentVersion()
    {
        _repository.Save(new DashboardLayout
        {
            TentId = 1,
            Version = 0,
            Sections = [new DashboardSection { Title = "Meins", Tiles = [new DashboardTile { MetricKey = "vpd" }] }],
        });

        Assert.Equal(DashboardLayout.CurrentVersion, _repository.GetSaved(1)!.Version);
    }

    /// <summary>Schreibt die gespeicherte Version zurück — so sah ein 1.6.0-Eintrag aus.</summary>
    private void DowngradeStoredVersion(int tentId)
    {
        var paths = new AppPaths(_dbPath);
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={paths.DatabasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE AppSettings SET Value = REPLACE(Value, '\"version\":2', '\"version\":0') WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", $"dashboard:tent:{tentId}");
        command.ExecuteNonQuery();
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
