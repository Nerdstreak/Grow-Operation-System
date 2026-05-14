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
        GrowDiary.Web.Tests.TestDatabase.InitializeWithDefaultTent(_paths);
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

        foreach (var column in new[] { "CloneCounterTotal", "LastCloneCutAt", "MotherHealthStatus", "QuarantineStartedAt", "QuarantinePlannedEndAt", "QuarantineResult" })
        {
            using var setupColumnCommand = connection.CreateCommand();
            setupColumnCommand.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Setups') WHERE name = $column AND [notnull] = 0;";
            setupColumnCommand.Parameters.AddWithValue("$column", column);
            Assert.Equal(1L, setupColumnCommand.ExecuteScalar());
        }
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
    public void SetupCrud_PersistsMotherAndQuarantineBasisFields()
    {
        var repo = new GrowRepository(_paths);
        var tent = repo.GetTents().Single();
        var lastCloneCutAt = new DateTime(2026, 2, 3);
        var quarantineStartedAt = new DateTime(2026, 3, 1);
        var quarantinePlannedEndAt = new DateTime(2026, 3, 14);

        var created = repo.CreateSetup(new Setup
        {
            TentId = tent.Id,
            Name = "Basis Setup",
            SetupType = SetupType.Mother,
            Status = SetupStatus.Active,
            CloneCounterTotal = 12,
            LastCloneCutAt = lastCloneCutAt,
            MotherHealthStatus = "Stable",
            QuarantineStartedAt = quarantineStartedAt,
            QuarantinePlannedEndAt = quarantinePlannedEndAt,
            QuarantineResult = "Pending"
        });

        var loaded = repo.GetSetup(created.Id)!;
        Assert.Equal(12, loaded.CloneCounterTotal);
        Assert.Equal(lastCloneCutAt, loaded.LastCloneCutAt);
        Assert.Equal("Stable", loaded.MotherHealthStatus);
        Assert.Equal(quarantineStartedAt, loaded.QuarantineStartedAt);
        Assert.Equal(quarantinePlannedEndAt, loaded.QuarantinePlannedEndAt);
        Assert.Equal("Pending", loaded.QuarantineResult);

        loaded.CloneCounterTotal = 15;
        loaded.MotherHealthStatus = "Watch";
        loaded.QuarantineResult = "Cleared";
        repo.UpdateSetup(loaded);

        var updated = repo.GetSetupsForTent(tent.Id).Single();
        Assert.Equal(15, updated.CloneCounterTotal);
        Assert.Equal("Watch", updated.MotherHealthStatus);
        Assert.Equal("Cleared", updated.QuarantineResult);
    }

    [Fact]
    public void GetTents_CountsActiveAndArchivedSetupsWithoutChangingGrowCounts()
    {
        var repo = new GrowRepository(_paths);
        var tent = repo.GetTents().Single();

        repo.CreateSetup(new Setup { TentId = tent.Id, Name = "Planning Setup", SetupType = SetupType.Production, Status = SetupStatus.Planning });
        repo.CreateSetup(new Setup { TentId = tent.Id, Name = "Active Setup", SetupType = SetupType.Mother, Status = SetupStatus.Active });
        repo.CreateSetup(new Setup { TentId = tent.Id, Name = "Archived Setup", SetupType = SetupType.Quarantine, Status = SetupStatus.Archived });

        var activeGrowId = repo.CreateGrow(new GrowRun
        {
            Name = "Active Grow",
            TentId = tent.Id,
            StartDate = new DateTime(2026, 1, 1),
            Status = GrowStatus.Planning
        });
        repo.CreateGrow(new GrowRun
        {
            Name = "Archived Grow",
            TentId = tent.Id,
            StartDate = new DateTime(2025, 1, 1),
            Status = GrowStatus.Completed
        });

        var loadedFromList = repo.GetTents().Single(item => item.Id == tent.Id);
        var loadedById = repo.GetTent(tent.Id)!;
        var loadedForGrow = repo.GetTentForGrow(activeGrowId)!;

        foreach (var loaded in new[] { loadedFromList, loadedById, loadedForGrow })
        {
            Assert.Equal(1, loaded.ActiveGrowCount);
            Assert.Equal(1, loaded.ArchivedGrowCount);
            Assert.Equal(2, loaded.ActiveSetupCount);
            Assert.Equal(1, loaded.ArchivedSetupCount);
        }
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
