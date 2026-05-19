using GrowDiary.Web.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Infrastructure;

public sealed class FreshInstallTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _dbPath;
    private readonly AppPaths _paths;

    public FreshInstallTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"grow-fresh-install-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);

        _dbPath = Path.Combine(_tempRoot, "grow-diary.db");
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", _dbPath);
        Environment.SetEnvironmentVariable("GROWDIARY_SEED_DEMO_DATA", null);
        _paths = new AppPaths(_tempRoot);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        Environment.SetEnvironmentVariable("GROWDIARY_SEED_DEMO_DATA", null);
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Initialize_CreatesSchemaWithoutProductOrUserData()
    {
        new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance).Initialize();

        var repository = new GrowRepository(_paths);

        Assert.Empty(repository.GetTents(includeArchived: true));
        Assert.Empty(repository.GetHydroSetups(includeArchived: true));
        Assert.Empty(repository.GetAllGrows());
        Assert.Empty(repository.GetHardwareItems());

        var homeAssistant = repository.GetHomeAssistantSettings();
        Assert.Null(homeAssistant.BaseUrl);
        Assert.Null(homeAssistant.AccessToken);
        Assert.False(homeAssistant.Enabled);
    }
}
