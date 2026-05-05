using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
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
}
