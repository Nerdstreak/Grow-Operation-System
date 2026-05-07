using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Infrastructure;

public sealed class LightScheduleRepositoryTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly AppPaths _paths;

    public LightScheduleRepositoryTests()
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
    public void Initialize_CreatesLightScheduleAndTransitionTables()
    {
        using var connection = OpenConnection();

        foreach (var table in new[] { "LightSchedules", "LightTransitionEvents" })
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
            command.Parameters.AddWithValue("$name", table);
            Assert.Equal(1L, command.ExecuteScalar());
        }

        using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_LightTransitionEvents_TentKindOccurred';";
        Assert.Equal(1L, indexCommand.ExecuteScalar());
    }

    [Fact]
    public void LightSchedule_CreateGetUpdateAndActiveByTent()
    {
        var repo = new GrowRepository(_paths);
        var tent = repo.GetTents().Single();

        var schedule = repo.CreateLightSchedule(new LightSchedule
        {
            TentId = tent.Id,
            Name = "Bluete 12/12",
            IsActive = true,
            LightsOnTime = "08:00",
            LightsOffTime = "20:00",
            TimeZoneId = "Europe/Berlin",
            Source = LightSource.Manual
        });

        var loaded = repo.GetLightSchedule(schedule.Id)!;
        Assert.Equal(tent.Id, loaded.TentId);
        Assert.Equal("08:00", loaded.LightsOnTime);
        Assert.Equal(LightSource.Manual, loaded.Source);

        loaded.Name = "Bluete aktualisiert";
        loaded.IsActive = false;
        loaded.LightsOnTime = "09:00";
        loaded.LightsOffTime = "21:00";
        loaded.TimeZoneId = null;
        loaded.Source = LightSource.HomeAssistant;
        repo.UpdateLightSchedule(loaded);

        var byTent = repo.GetLightSchedulesByTent(tent.Id).Single();
        Assert.Equal("Bluete aktualisiert", byTent.Name);
        Assert.Equal("09:00", byTent.LightsOnTime);
        Assert.Equal(LightSource.HomeAssistant, byTent.Source);
        Assert.Null(repo.GetActiveLightScheduleForTent(tent.Id));

        byTent.IsActive = true;
        repo.UpdateLightSchedule(byTent);
        Assert.Equal(byTent.Id, repo.GetActiveLightScheduleForTent(tent.Id)!.Id);
    }

    [Fact]
    public void LightTransition_CreateDeduplicatesAndLoadsLatest()
    {
        var repo = new GrowRepository(_paths);
        var tent = repo.GetTents().Single();
        var occurredAt = new DateTime(2026, 5, 7, 8, 0, 0, DateTimeKind.Utc);

        var first = repo.CreateLightTransitionIfNotDuplicate(new LightTransitionEvent
        {
            TentId = tent.Id,
            Kind = LightTransitionKind.LightOn,
            OccurredAtUtc = occurredAt,
            Source = LightSource.HomeAssistant,
            RawState = "on"
        });
        var duplicate = repo.CreateLightTransitionIfNotDuplicate(new LightTransitionEvent
        {
            TentId = tent.Id,
            Kind = LightTransitionKind.LightOn,
            OccurredAtUtc = occurredAt.AddSeconds(90),
            Source = LightSource.HomeAssistant,
            RawState = "true"
        });
        var later = repo.CreateLightTransitionIfNotDuplicate(new LightTransitionEvent
        {
            TentId = tent.Id,
            Kind = LightTransitionKind.LightOff,
            OccurredAtUtc = occurredAt.AddMinutes(5),
            Source = LightSource.HomeAssistant,
            RawState = "off"
        });

        Assert.Equal(first.Id, duplicate.Id);
        Assert.NotEqual(first.Id, later.Id);
        Assert.Equal(2, repo.GetLightTransitionsByTent(tent.Id).Count);
        Assert.Equal(LightTransitionKind.LightOff, repo.GetLatestLightTransitionForTent(tent.Id)!.Kind);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath }.ToString());
        connection.Open();
        return connection;
    }
}
