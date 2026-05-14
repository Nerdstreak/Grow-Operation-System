using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Infrastructure;

public sealed class MaintenanceEventRepositoryTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly AppPaths _paths;

    public MaintenanceEventRepositoryTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-maintenance-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _paths = new AppPaths(_contentRoot);
        GrowDiary.Web.Tests.TestDatabase.InitializeWithDefaultTent(_paths);
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Initialize_CreatesMaintenanceEventsTableAndIndexes()
    {
        using var connection = OpenConnection();

        using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'MaintenanceEvents';";
        Assert.Equal(1L, tableCommand.ExecuteScalar());

        foreach (var index in new[]
        {
            "IX_MaintenanceEvents_HardwareItemId",
            "IX_MaintenanceEvents_Status",
            "IX_MaintenanceEvents_DueAtUtc",
            "IX_MaintenanceEvents_NextDueAtUtc",
            "IX_MaintenanceEvents_GrowTaskId",
            "IX_MaintenanceEvents_SopInstanceId"
        })
        {
            using var indexCommand = connection.CreateCommand();
            indexCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = $name;";
            indexCommand.Parameters.AddWithValue("$name", index);
            Assert.Equal(1L, indexCommand.ExecuteScalar());
        }
    }

    [Fact]
    public void MaintenanceEvent_CreateGetUpdateAndListFilters()
    {
        var repo = new GrowRepository(_paths);
        var hardware = CreateHardware(repo);
        var dueAt = Utc(2026, 5, 20);

        var created = repo.CreateMaintenanceEvent(new MaintenanceEvent
        {
            HardwareItemId = hardware.Id,
            EventType = MaintenanceEventType.Inspection,
            Status = MaintenanceEventStatus.Planned,
            Result = MaintenanceResult.Unknown,
            Title = "Monatscheck",
            Description = "Sichtpruefung",
            DueAtUtc = dueAt,
            Notes = "Geplant"
        });

        var loaded = repo.GetMaintenanceEvent(created.Id)!;
        Assert.Equal("Monatscheck", loaded.Title);
        Assert.Equal(dueAt, loaded.DueAtUtc);
        Assert.Equal(MaintenanceEventStatus.Planned, loaded.Status);

        loaded.Status = MaintenanceEventStatus.Completed;
        loaded.Result = MaintenanceResult.Passed;
        loaded.PerformedAtUtc = Utc(2026, 5, 19);
        loaded.Notes = "OK";
        repo.UpdateMaintenanceEvent(loaded);

        var byHardware = repo.GetMaintenanceEventsByHardwareItem(hardware.Id).Single();
        Assert.Equal(MaintenanceEventStatus.Completed, byHardware.Status);
        Assert.Equal(MaintenanceResult.Passed, byHardware.Result);
        Assert.Empty(repo.GetOpenMaintenanceEventsByHardwareItem(hardware.Id));

        var due = repo.GetDueMaintenanceEvents(Utc(2026, 5, 21)).Single();
        Assert.Equal(created.Id, due.Id);
    }

    [Fact]
    public void CompletedMaintenance_SetsPerformedAtAndNextDueFromHardwareInterval()
    {
        var repo = new GrowRepository(_paths);
        var hardware = CreateHardware(repo, inspectionIntervalDays: 30);

        var created = repo.CreateMaintenanceEvent(new MaintenanceEvent
        {
            HardwareItemId = hardware.Id,
            EventType = MaintenanceEventType.Cleaning,
            Status = MaintenanceEventStatus.Completed,
            Result = MaintenanceResult.Passed,
            Title = "Reinigung"
        });

        Assert.NotNull(created.PerformedAtUtc);
        Assert.Equal(created.PerformedAtUtc!.Value.AddDays(30), created.NextDueAtUtc);
    }

    [Fact]
    public void PlannedMaintenance_WithGrowHardwareCreatesGrowTaskReminder()
    {
        var repo = new GrowRepository(_paths);
        var taskRepo = new TaskRepository(_paths);
        var tent = repo.GetTents().Single();
        var growId = repo.CreateGrow(new GrowRun
        {
            TentId = tent.Id,
            Name = "Reminder Grow",
            StartDate = Utc(2026, 5, 1),
            Status = GrowStatus.Running
        });
        var hardware = CreateHardware(repo, growId: growId, criticality: HardwareItemCriticality.Critical);
        var dueAt = Utc(2026, 5, 22);

        var created = repo.CreateMaintenanceEvent(new MaintenanceEvent
        {
            HardwareItemId = hardware.Id,
            EventType = MaintenanceEventType.Inspection,
            Status = MaintenanceEventStatus.Planned,
            Result = MaintenanceResult.Unknown,
            Title = "USV-Test",
            DueAtUtc = dueAt
        });

        Assert.NotNull(created.GrowTaskId);
        var task = taskRepo.Get(created.GrowTaskId!.Value)!;
        Assert.Equal(growId, task.GrowId);
        Assert.Equal("Wartung: USV Akku - USV-Test", task.Title);
        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Equal(GrowTaskStatus.Open, task.Status);
        Assert.Equal(dueAt, task.DueAtUtc);
    }

    [Fact]
    public void PlannedMaintenance_WithoutGrowHardwareDoesNotCreateGrowTask()
    {
        var repo = new GrowRepository(_paths);
        var hardware = CreateHardware(repo, growId: null);

        var created = repo.CreateMaintenanceEvent(new MaintenanceEvent
        {
            HardwareItemId = hardware.Id,
            EventType = MaintenanceEventType.Inspection,
            Status = MaintenanceEventStatus.Planned,
            Result = MaintenanceResult.Unknown,
            Title = "Globaler Check",
            DueAtUtc = Utc(2026, 5, 22)
        });

        Assert.Null(created.GrowTaskId);
    }

    [Fact]
    public void MaintenanceEvent_RejectsInvalidHardwareAndNextDueBeforePerformed()
    {
        var repo = new GrowRepository(_paths);

        Assert.Throws<InvalidOperationException>(() => repo.CreateMaintenanceEvent(new MaintenanceEvent
        {
            HardwareItemId = 9999,
            EventType = MaintenanceEventType.Inspection,
            Status = MaintenanceEventStatus.Planned,
            Result = MaintenanceResult.Unknown,
            Title = "Bad"
        }));

        var hardware = CreateHardware(repo);
        Assert.Throws<InvalidOperationException>(() => repo.CreateMaintenanceEvent(new MaintenanceEvent
        {
            HardwareItemId = hardware.Id,
            EventType = MaintenanceEventType.Inspection,
            Status = MaintenanceEventStatus.Completed,
            Result = MaintenanceResult.Passed,
            Title = "Bad Date",
            PerformedAtUtc = Utc(2026, 5, 20),
            NextDueAtUtc = Utc(2026, 5, 19)
        }));
    }

    private HardwareItem CreateHardware(
        GrowRepository repo,
        int? growId = null,
        int? inspectionIntervalDays = null,
        HardwareItemCriticality criticality = HardwareItemCriticality.Medium)
    {
        var tent = repo.GetTents().Single();
        return repo.CreateHardwareItem(new HardwareItem
        {
            Name = "USV Akku",
            Category = "UpsBattery",
            Status = HardwareItemStatus.Active,
            Criticality = criticality,
            TentId = tent.Id,
            GrowId = growId,
            InspectionIntervalDays = inspectionIntervalDays
        });
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath }.ToString());
        connection.Open();
        return connection;
    }

    private static DateTime Utc(int year, int month, int day)
        => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
