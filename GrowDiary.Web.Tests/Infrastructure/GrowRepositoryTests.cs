using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Infrastructure;

public sealed class GrowRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppPaths _paths;

    public GrowRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"grow-test-{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", _dbPath);
        _paths = new AppPaths(Path.GetTempPath());
        var initializer = new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance);
        initializer.Initialize();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
    }

    [Fact]
    public void Initialize_CreatesSetupsTableAndNullableGrowSetupId()
    {
        using var connection = OpenConnection();

        using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Setups';";
        Assert.Equal(1L, tableCommand.ExecuteScalar());

        using var columnCommand = connection.CreateCommand();
        columnCommand.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Grows') WHERE name = 'SetupId' AND [notnull] = 0;";
        Assert.Equal(1L, columnCommand.ExecuteScalar());
    }

    [Fact]
    public void CreateGrow_WithoutSetupId_RemainsLoadable()
    {
        var repo = new GrowRepository(_paths);

        var growId = repo.CreateGrow(new GrowRun
        {
            Name = "Grow ohne Setup",
            StartDate = new DateTime(2026, 1, 1),
            Status = GrowStatus.Planning
        });

        var loaded = repo.GetGrow(growId);

        Assert.NotNull(loaded);
        Assert.Null(loaded!.SetupId);
        Assert.Equal("Grow ohne Setup", loaded.Name);
    }

    [Fact]
    public void CreateAndUpdateGrow_PreservesSetupIdWhenSet()
    {
        var repo = new GrowRepository(_paths);
        var tent = repo.GetTents().Single();
        var setup = repo.CreateSetup(new Setup
        {
            TentId = tent.Id,
            Name = "Production Setup",
            SetupType = SetupType.Production,
            Status = SetupStatus.Active
        });

        var growId = repo.CreateGrow(new GrowRun
        {
            Name = "Setup Grow",
            TentId = tent.Id,
            SetupId = setup.Id,
            StartDate = new DateTime(2026, 1, 1),
            Status = GrowStatus.Planning
        });

        var grow = repo.GetGrow(growId)!;
        Assert.Equal(setup.Id, grow.SetupId);

        grow.Name = "Edited Setup Grow";
        repo.UpdateGrow(grow);

        var loaded = repo.GetGrow(growId)!;
        Assert.Equal(setup.Id, loaded.SetupId);
        Assert.Equal("Edited Setup Grow", loaded.Name);
    }

    [Fact]
    public void SetupCrud_PersistsAndLoadsByTent()
    {
        var repo = new GrowRepository(_paths);
        var tent = repo.GetTents().Single();

        var created = repo.CreateSetup(new Setup
        {
            TentId = tent.Id,
            Name = "Mutter Setup",
            SetupType = SetupType.Mother,
            Status = SetupStatus.Planning,
            Notes = "Initial"
        });

        Assert.True(created.Id > 0);
        var loaded = repo.GetSetup(created.Id);
        Assert.NotNull(loaded);
        Assert.Equal(SetupType.Mother, loaded!.SetupType);

        created.Status = SetupStatus.Active;
        created.Notes = "Aktiv";
        repo.UpdateSetup(created);

        var tentSetups = repo.GetSetupsForTent(tent.Id);
        Assert.Single(tentSetups);
        Assert.Equal(SetupStatus.Active, tentSetups[0].Status);
        Assert.Equal("Aktiv", tentSetups[0].Notes);
    }

    [Fact]
    public void UpdateGrow_PreservesSystemIdWhenSet()
    {
        var repo = new GrowRepository(_paths);
        var system = repo.CreateSystem(new GrowSystem
        {
            Name = "RDWC System",
            HydroStyle = HydroStyle.RDWC.ToString()
        });
        var growId = repo.CreateGrow(new GrowRun
        {
            Name = "Original Grow",
            SystemId = system.Id,
            StartDate = new DateTime(2026, 1, 1),
            Status = GrowStatus.Planning
        });

        var grow = repo.GetGrow(growId)!;
        grow.Name = "Edited Grow";
        repo.UpdateGrow(grow);

        var loaded = repo.GetGrow(growId)!;
        Assert.Equal(system.Id, loaded.SystemId);
        Assert.Equal("Edited Grow", loaded.Name);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath }.ToString());
        connection.Open();
        return connection;
    }
}
