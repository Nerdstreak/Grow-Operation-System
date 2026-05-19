using GrowDiary.Web.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Infrastructure;

public sealed class HaConfigLoaderTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _dbPath;
    private readonly AppPaths _paths;

    public HaConfigLoaderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"grow-ha-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempRoot, "App_Data"));

        _dbPath = Path.Combine(_tempRoot, "grow-diary.db");
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", _dbPath);
        Environment.SetEnvironmentVariable("GROWDIARY_SEED_DEMO_DATA", null);
        _paths = new AppPaths(_tempRoot);
        new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance).Initialize();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        Environment.SetEnvironmentVariable("GROWDIARY_SEED_DEMO_DATA", null);
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Apply_DoesNotImportLocalConfigUnlessDemoSeedOptInIsEnabled()
    {
        WriteLocalHaConfig();
        var repository = new GrowRepository(_paths);

        HaConfigLoader.Apply(_paths, repository);

        Assert.Empty(repository.GetTents(includeArchived: true));
        Assert.Empty(repository.GetHardwareItems());
        var settings = repository.GetHomeAssistantSettings();
        Assert.Null(settings.BaseUrl);
        Assert.Null(settings.AccessToken);
        Assert.False(settings.Enabled);
    }

    [Fact]
    public void Apply_ImportsLocalConfigWhenDemoSeedOptInIsEnabled()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_SEED_DEMO_DATA", "true");
        WriteLocalHaConfig();
        var repository = new GrowRepository(_paths);

        HaConfigLoader.Apply(_paths, repository);

        Assert.Contains(repository.GetTents(includeArchived: true), tent => tent.Name == "Hauptzelt");
        Assert.Contains(repository.GetTentSensors(repository.GetTents().Single().Id), sensor => sensor.HaEntityId == "sensor.test_temp");
        var settings = repository.GetHomeAssistantSettings();
        Assert.Equal("http://homeassistant.local:8123", settings.BaseUrl);
        Assert.Equal("local-test-token", settings.AccessToken);
        Assert.True(settings.Enabled);
    }

    private void WriteLocalHaConfig()
    {
        File.WriteAllText(
            Path.Combine(_tempRoot, "App_Data", "ha-config.json"),
            """
            {
              "homeAssistant": {
                "url": "http://homeassistant.local:8123",
                "token": "local-test-token"
              },
              "tents": [
                {
                  "name": "Hauptzelt",
                  "tentType": "Production",
                  "sensors": [
                    { "metricType": "AirTemperature", "haEntityId": "sensor.test_temp" }
                  ]
                }
              ]
            }
            """);
    }
}
