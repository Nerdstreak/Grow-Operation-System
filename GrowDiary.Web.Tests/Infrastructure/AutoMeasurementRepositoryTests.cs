using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Infrastructure;

public sealed class AutoMeasurementRepositoryTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly AppPaths _paths;

    public AutoMeasurementRepositoryTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _paths = new AppPaths(_contentRoot);
        new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance).Initialize();
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Initialize_CreatesAutoMeasurementTablesAndIndexes()
    {
        using var connection = OpenConnection();

        foreach (var table in new[] { "AutoMeasurementConfigs", "AutoMeasurementFieldMappings", "AutoMeasurementRuns" })
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
            command.Parameters.AddWithValue("$name", table);
            Assert.Equal(1L, command.ExecuteScalar());
        }

        foreach (var index in new[] { "IX_AutoMeasurementConfigs_GrowId", "IX_AutoMeasurementFieldMappings_ConfigId", "IX_AutoMeasurementRuns_ConfigTriggerSchedule" })
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = $name;";
            command.Parameters.AddWithValue("$name", index);
            Assert.Equal(1L, command.ExecuteScalar());
        }
    }

    [Fact]
    public void ConfigCrud_PersistsAndLoadsByGrow()
    {
        var repo = new GrowRepository(_paths);
        var tent = repo.GetTents().Single();
        var growId = repo.CreateGrow(new GrowRun { TentId = tent.Id, Name = "Auto Grow", StartDate = new DateTime(2026, 5, 1), Status = GrowStatus.Planning });

        var config = repo.CreateAutoMeasurementConfig(new AutoMeasurementConfig
        {
            GrowId = growId,
            TentId = tent.Id,
            Name = "Licht an",
            Status = AutoMeasurementStatus.Enabled,
            TriggerKind = AutoMeasurementTriggerKind.LightOnDelay,
            DelayMinutes = 30,
            WindowMinutes = 20
        });

        var loaded = repo.GetAutoMeasurementConfig(config.Id)!;
        Assert.Equal(growId, loaded.GrowId);
        Assert.Equal(tent.Id, loaded.TentId);
        Assert.Equal(AutoMeasurementTriggerKind.LightOnDelay, loaded.TriggerKind);

        loaded.Name = "Licht an aktualisiert";
        loaded.Status = AutoMeasurementStatus.Disabled;
        loaded.WindowMinutes = 45;
        repo.UpdateAutoMeasurementConfig(loaded);

        var byGrow = repo.GetAutoMeasurementConfigsByGrow(growId).Single();
        Assert.Equal("Licht an aktualisiert", byGrow.Name);
        Assert.Equal(AutoMeasurementStatus.Disabled, byGrow.Status);
        Assert.Equal(45, byGrow.WindowMinutes);
        Assert.Equal(config.Id, repo.GetAutoMeasurementConfigs().Single().Id);
    }

    [Fact]
    public void FieldMappings_ReplaceAndLoad()
    {
        var repo = new GrowRepository(_paths);
        var growId = repo.CreateGrow(new GrowRun { Name = "Auto Grow", StartDate = new DateTime(2026, 5, 1), Status = GrowStatus.Planning });
        var config = repo.CreateAutoMeasurementConfig(new AutoMeasurementConfig { GrowId = growId, Name = "Manual", WindowMinutes = 20 });

        repo.ReplaceAutoMeasurementFieldMappings(config.Id, new[]
        {
            new AutoMeasurementFieldMapping
            {
                MeasurementField = AutoMeasurementField.AirTemperatureC,
                MetricKey = "temperature",
                Aggregation = AutoMeasurementAggregation.Latest,
                IsRequired = true
            },
            new AutoMeasurementFieldMapping
            {
                MeasurementField = AutoMeasurementField.HumidityPercent,
                MetricKey = "humidity",
                Aggregation = AutoMeasurementAggregation.Median,
                IsRequired = false
            }
        });

        var firstLoad = repo.GetAutoMeasurementFieldMappings(config.Id);
        Assert.Equal(2, firstLoad.Count);
        Assert.Contains(firstLoad, item => item.MeasurementField == AutoMeasurementField.AirTemperatureC && item.IsRequired);

        repo.ReplaceAutoMeasurementFieldMappings(config.Id, new[]
        {
            new AutoMeasurementFieldMapping
            {
                MeasurementField = AutoMeasurementField.ReservoirPh,
                MetricKey = "reservoir-ph",
                Aggregation = AutoMeasurementAggregation.Average,
                IsRequired = true
            }
        });

        var secondLoad = repo.GetAutoMeasurementFieldMappings(config.Id);
        Assert.Single(secondLoad);
        Assert.Equal(AutoMeasurementField.ReservoirPh, secondLoad[0].MeasurementField);
    }

    [Fact]
    public void Runs_CreateIfNotExistsKeepsConfigTriggerScheduleUnique()
    {
        var repo = new GrowRepository(_paths);
        var growId = repo.CreateGrow(new GrowRun { Name = "Auto Grow", StartDate = new DateTime(2026, 5, 1), Status = GrowStatus.Planning });
        var config = repo.CreateAutoMeasurementConfig(new AutoMeasurementConfig { GrowId = growId, Name = "Manual", WindowMinutes = 20 });
        var scheduledFor = new DateTime(2026, 5, 2, 8, 0, 0, DateTimeKind.Utc);

        var first = repo.CreateAutoMeasurementRunIfNotExists(new AutoMeasurementRun
        {
            ConfigId = config.Id,
            GrowId = growId,
            TriggerKind = AutoMeasurementTriggerKind.Manual,
            ScheduledForUtc = scheduledFor,
            Status = AutoMeasurementRunStatus.Pending
        });
        var second = repo.CreateAutoMeasurementRunIfNotExists(new AutoMeasurementRun
        {
            ConfigId = config.Id,
            GrowId = growId,
            TriggerKind = AutoMeasurementTriggerKind.Manual,
            ScheduledForUtc = scheduledFor,
            Status = AutoMeasurementRunStatus.Pending
        });

        Assert.Equal(first.Id, second.Id);
        Assert.Single(repo.GetAutoMeasurementRunsByConfig(config.Id));
        Assert.Single(repo.GetAutoMeasurementRunsByGrow(growId));
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath }.ToString());
        connection.Open();
        return connection;
    }
}
