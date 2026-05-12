using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Infrastructure;

public sealed class RiskEventRepositoryTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly AppPaths _paths;

    public RiskEventRepositoryTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-risk-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _paths = new AppPaths(_contentRoot);
        new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance).Initialize();
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Initialize_CreatesRiskEventsTableAndIndexes()
    {
        using var connection = OpenConnection();

        using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'RiskEvents';";
        Assert.Equal(1L, tableCommand.ExecuteScalar());

        foreach (var index in new[]
        {
            "IX_RiskEvents_Status",
            "IX_RiskEvents_Severity",
            "IX_RiskEvents_EventType",
            "IX_RiskEvents_HardwareItemId",
            "IX_RiskEvents_TentId",
            "IX_RiskEvents_GrowId",
            "IX_RiskEvents_TentSensorId",
            "IX_RiskEvents_DedupeKey_Status",
            "IX_RiskEvents_StartedAtUtc"
        })
        {
            using var indexCommand = connection.CreateCommand();
            indexCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = $name;";
            indexCommand.Parameters.AddWithValue("$name", index);
            Assert.Equal(1L, indexCommand.ExecuteScalar());
        }
    }

    [Fact]
    public void RiskEvent_CreateGetUpdateAndListFilters()
    {
        var repo = new GrowRepository(_paths);
        var tent = repo.GetTents().Single();
        var growId = CreateGrow(repo, tent.Id);
        var hardware = CreateHardware(repo, tent.Id, growId);
        var startedAt = Utc(2026, 7, 1);

        var created = repo.CreateRiskEvent(new RiskEvent
        {
            EventType = RiskEventType.PumpOffline,
            Severity = RiskEventSeverity.Critical,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Manual,
            Title = "Ruecklaufpumpe offline",
            Description = "Keine Rueckmeldung",
            HardwareItemId = hardware.Id,
            TentId = tent.Id,
            GrowId = growId,
            HaEntityId = "switch.return_pump",
            StartedAtUtc = startedAt,
            DedupeKey = "pump:return",
            RawValue = "off",
            Notes = "Check"
        });

        var loaded = repo.GetRiskEvent(created.Id)!;
        Assert.Equal("Ruecklaufpumpe offline", loaded.Title);
        Assert.Equal(RiskEventSeverity.Critical, loaded.Severity);
        Assert.Equal(startedAt, loaded.StartedAtUtc);

        loaded.Status = RiskEventStatus.Acknowledged;
        loaded.Notes = "Gesehen";
        repo.UpdateRiskEvent(loaded);

        Assert.Equal(created.Id, repo.GetOpenRiskEvents().Single().Id);
        Assert.Equal(created.Id, repo.GetRiskEventsByStatus(RiskEventStatus.Acknowledged).Single().Id);
        Assert.Equal(created.Id, repo.GetRiskEventsByTent(tent.Id).Single().Id);
        Assert.Equal(created.Id, repo.GetRiskEventsByGrow(growId).Single().Id);
        Assert.Equal(created.Id, repo.GetRiskEventsByHardwareItem(hardware.Id).Single().Id);
    }

    [Fact]
    public void CreateRiskEvent_DedupeReturnsOpenOrAcknowledgedAndResolvedDoesNotBlock()
    {
        var repo = new GrowRepository(_paths);
        var first = repo.CreateRiskEvent(new RiskEvent
        {
            EventType = RiskEventType.HomeAssistantUnavailable,
            Severity = RiskEventSeverity.Warning,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.System,
            Title = "HA nicht erreichbar",
            StartedAtUtc = Utc(2026, 7, 1),
            DedupeKey = "ha:unavailable"
        });

        var duplicate = repo.CreateRiskEvent(new RiskEvent
        {
            EventType = RiskEventType.HomeAssistantUnavailable,
            Severity = RiskEventSeverity.Critical,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.System,
            Title = "HA weiterhin nicht erreichbar",
            StartedAtUtc = Utc(2026, 7, 2),
            DedupeKey = "ha:unavailable"
        });

        Assert.Equal(first.Id, duplicate.Id);
        Assert.NotNull(duplicate.LastSeenAtUtc);
        Assert.Single(repo.GetRiskEvents());

        repo.ResolveRiskEvent(first.Id, Utc(2026, 7, 3), "Erholt");
        var afterResolved = repo.CreateRiskEvent(new RiskEvent
        {
            EventType = RiskEventType.HomeAssistantUnavailable,
            Severity = RiskEventSeverity.Warning,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.System,
            Title = "HA wieder nicht erreichbar",
            StartedAtUtc = Utc(2026, 7, 4),
            DedupeKey = "ha:unavailable"
        });

        Assert.NotEqual(first.Id, afterResolved.Id);
        Assert.Equal(2, repo.GetRiskEvents().Count);
    }

    [Fact]
    public void ResolveAndAcknowledge_SetStatusAndTimestamps()
    {
        var repo = new GrowRepository(_paths);
        var risk = repo.CreateRiskEvent(new RiskEvent
        {
            EventType = RiskEventType.CriticalDo,
            Severity = RiskEventSeverity.Critical,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Manual,
            Title = "DO kritisch",
            StartedAtUtc = Utc(2026, 7, 1)
        });

        var acknowledged = repo.AcknowledgeRiskEvent(risk.Id, Utc(2026, 7, 1).AddHours(1), "Pruefung gestartet");
        Assert.Equal(RiskEventStatus.Acknowledged, acknowledged.Status);
        Assert.NotNull(acknowledged.AcknowledgedAtUtc);
        Assert.Contains("Pruefung gestartet", acknowledged.Notes);

        var resolved = repo.ResolveRiskEvent(risk.Id, Utc(2026, 7, 1).AddHours(2), "Belueftung stabil");
        Assert.Equal(RiskEventStatus.Resolved, resolved.Status);
        Assert.NotNull(resolved.ResolvedAtUtc);
        Assert.Contains("Belueftung stabil", resolved.Notes);
    }

    [Fact]
    public void RiskEvent_RejectsInvalidReferencesEnumsAndDates()
    {
        var repo = new GrowRepository(_paths);

        Assert.Throws<InvalidOperationException>(() => repo.CreateRiskEvent(new RiskEvent
        {
            EventType = RiskEventType.PowerOutage,
            Severity = RiskEventSeverity.Critical,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Manual,
            Title = "Bad Hardware",
            HardwareItemId = 9999
        }));

        Assert.Throws<InvalidOperationException>(() => repo.CreateRiskEvent(new RiskEvent
        {
            EventType = (RiskEventType)99,
            Severity = RiskEventSeverity.Critical,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Manual,
            Title = "Bad Enum"
        }));

        Assert.Throws<InvalidOperationException>(() => repo.CreateRiskEvent(new RiskEvent
        {
            EventType = RiskEventType.PowerOutage,
            Severity = RiskEventSeverity.Critical,
            Status = RiskEventStatus.Resolved,
            Source = RiskEventSource.Manual,
            Title = "Bad Date",
            StartedAtUtc = Utc(2026, 7, 2),
            ResolvedAtUtc = Utc(2026, 7, 1)
        }));

        Assert.Throws<InvalidOperationException>(() => repo.CreateRiskEvent(new RiskEvent
        {
            EventType = RiskEventType.PowerOutage,
            Severity = RiskEventSeverity.Critical,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Manual,
            Title = "Bad Tent",
            TentId = 9999
        }));

        Assert.Throws<InvalidOperationException>(() => repo.CreateRiskEvent(new RiskEvent
        {
            EventType = RiskEventType.PowerOutage,
            Severity = RiskEventSeverity.Critical,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Manual,
            Title = "Bad Grow",
            GrowId = 9999
        }));

        Assert.Throws<InvalidOperationException>(() => repo.CreateRiskEvent(new RiskEvent
        {
            EventType = RiskEventType.SensorUnavailable,
            Severity = RiskEventSeverity.Warning,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Manual,
            Title = "Bad Sensor",
            TentSensorId = 9999
        }));
    }

    private int CreateGrow(GrowRepository repo, int tentId)
        => repo.CreateGrow(new GrowRun
        {
            TentId = tentId,
            Name = "Risk Grow",
            StartDate = Utc(2026, 7, 1),
            Status = GrowStatus.Running
        });

    private HardwareItem CreateHardware(GrowRepository repo, int tentId, int growId)
        => repo.CreateHardwareItem(new HardwareItem
        {
            Name = "Ruecklaufpumpe",
            Category = "Pump",
            Status = HardwareItemStatus.Active,
            Criticality = HardwareItemCriticality.Critical,
            TentId = tentId,
            GrowId = growId
        });

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath }.ToString());
        connection.Open();
        return connection;
    }

    private static DateTime Utc(int year, int month, int day)
        => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
