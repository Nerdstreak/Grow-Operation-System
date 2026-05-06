using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Infrastructure;

public sealed class StrainPlantRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppPaths _paths;

    public StrainPlantRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"grow-test-{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", _dbPath);
        _paths = new AppPaths(Path.GetTempPath());
        new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance).Initialize();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
    }

    [Fact]
    public void Initialize_CreatesStrainsAndPlantInstancesTables()
    {
        using var connection = OpenConnection();

        foreach (var table in new[] { "Strains", "PlantInstances" })
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
            command.Parameters.AddWithValue("$name", table);
            Assert.Equal(1L, command.ExecuteScalar());
        }

        foreach (var index in new[] { "IX_PlantInstances_SetupId", "IX_PlantInstances_GrowId", "IX_PlantInstances_ParentPlantId", "IX_PlantInstances_StrainId" })
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = $name;";
            command.Parameters.AddWithValue("$name", index);
            Assert.Equal(1L, command.ExecuteScalar());
        }
    }

    [Fact]
    public void StrainCrud_PersistsAndLoadsExpectedFields()
    {
        var repo = new GrowRepository(_paths);
        var strain = repo.CreateStrain(new Strain
        {
            Name = "Blue Test",
            Breeder = "Lab",
            Dominance = StrainDominance.Hybrid,
            FlowerWeeksMin = 8,
            FlowerWeeksMax = 10,
            NutrientDemandFactor = 1.1,
            StretchFactor = 1.2,
            VpdPreferenceShift = 0.1,
            Notes = "Stable"
        });

        var loaded = repo.GetStrain(strain.Id)!;
        Assert.Equal("Blue Test", loaded.Name);
        Assert.Equal(StrainDominance.Hybrid, loaded.Dominance);
        Assert.Equal(8, loaded.FlowerWeeksMin);
        Assert.Equal(1.1, loaded.NutrientDemandFactor);

        loaded.Name = "Blue Test Updated";
        loaded.Dominance = StrainDominance.Indica;
        repo.UpdateStrain(loaded);

        var updated = repo.GetStrains().Single(item => item.Id == strain.Id);
        Assert.Equal("Blue Test Updated", updated.Name);
        Assert.Equal(StrainDominance.Indica, updated.Dominance);
    }

    [Fact]
    public void PlantCrud_LoadsBySetupAndGrow()
    {
        var repo = new GrowRepository(_paths);
        var tent = repo.GetTents().Single();
        var strain = repo.CreateStrain(new Strain { Name = "Clone Line" });
        var setup = repo.CreateSetup(new Setup { TentId = tent.Id, Name = "Mother Setup", SetupType = SetupType.Mother });
        var growId = repo.CreateGrow(new GrowRun { TentId = tent.Id, Name = "Production Grow", StartDate = new DateTime(2026, 1, 1), Status = GrowStatus.Planning });

        var parent = repo.CreatePlant(new PlantInstance
        {
            StrainId = strain.Id,
            SetupId = setup.Id,
            Label = "Mother A",
            PlantRole = PlantRole.Mother,
            PlantStatus = PlantStatus.Active,
            PhenoLabel = "A"
        });
        var clone = repo.CreatePlant(new PlantInstance
        {
            StrainId = strain.Id,
            GrowId = growId,
            ParentPlantId = parent.Id,
            Label = "Clone A1",
            PlantRole = PlantRole.Clone,
            PlantStatus = PlantStatus.Active
        });

        var loadedParent = repo.GetPlant(parent.Id)!;
        Assert.Equal("Clone Line", loadedParent.StrainName);
        Assert.Equal("Mother A", repo.GetPlantsBySetup(setup.Id).Single().Label);
        Assert.Equal("Clone A1", repo.GetPlantsByGrow(growId).Single().Label);

        clone.PlantStatus = PlantStatus.Archived;
        clone.Notes = "Moved";
        repo.UpdatePlant(clone);

        var updatedClone = repo.GetPlant(clone.Id)!;
        Assert.Equal(PlantStatus.Archived, updatedClone.PlantStatus);
        Assert.Equal(parent.Id, updatedClone.ParentPlantId);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath }.ToString());
        connection.Open();
        return connection;
    }
}
