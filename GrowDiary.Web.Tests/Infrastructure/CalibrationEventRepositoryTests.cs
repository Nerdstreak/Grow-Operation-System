using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Infrastructure;

public sealed class CalibrationEventRepositoryTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly AppPaths _paths;

    public CalibrationEventRepositoryTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-calibration-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _paths = new AppPaths(_contentRoot);
        new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance).Initialize();
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Initialize_CreatesCalibrationEventsTableAndIndexes()
    {
        using var connection = OpenConnection();

        using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'CalibrationEvents';";
        Assert.Equal(1L, tableCommand.ExecuteScalar());

        foreach (var index in new[]
        {
            "IX_CalibrationEvents_HardwareItemId",
            "IX_CalibrationEvents_Status",
            "IX_CalibrationEvents_DueAtUtc",
            "IX_CalibrationEvents_NextDueAtUtc",
            "IX_CalibrationEvents_GrowTaskId",
            "IX_CalibrationEvents_CalibrationType"
        })
        {
            using var indexCommand = connection.CreateCommand();
            indexCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = $name;";
            indexCommand.Parameters.AddWithValue("$name", index);
            Assert.Equal(1L, indexCommand.ExecuteScalar());
        }
    }

    [Fact]
    public void CalibrationEvent_CreateGetUpdateListDueAndLatestCompleted()
    {
        var repo = new GrowRepository(_paths);
        var hardware = CreateHardware(repo);
        var dueAt = Utc(2026, 6, 10);

        var created = repo.CreateCalibrationEvent(new CalibrationEvent
        {
            HardwareItemId = hardware.Id,
            CalibrationType = CalibrationEventType.Ph,
            Status = CalibrationEventStatus.Planned,
            Result = CalibrationResult.Unknown,
            Title = "pH 2-Punkt",
            ReferenceSolution = "pH 7.00",
            ReferenceValue = 7.00m,
            BeforeValue = 6.86m,
            TemperatureC = 22.5m,
            DueAtUtc = dueAt,
            Notes = "Geplant"
        });

        var loaded = repo.GetCalibrationEvent(created.Id)!;
        Assert.Equal("pH 2-Punkt", loaded.Title);
        Assert.Equal(CalibrationEventStatus.Planned, loaded.Status);
        Assert.Equal(7.00m, loaded.ReferenceValue);

        loaded.Status = CalibrationEventStatus.Completed;
        loaded.Result = CalibrationResult.Passed;
        loaded.PerformedAtUtc = Utc(2026, 6, 9);
        loaded.AfterValue = 7.00m;
        repo.UpdateCalibrationEvent(loaded);

        var byHardware = repo.GetCalibrationEventsByHardwareItem(hardware.Id).Single();
        Assert.Equal(CalibrationEventStatus.Completed, byHardware.Status);
        Assert.Equal(CalibrationResult.Passed, byHardware.Result);
        Assert.Empty(repo.GetOpenCalibrationEventsByHardwareItem(hardware.Id));

        var due = repo.GetDueCalibrationEvents(Utc(2026, 6, 11)).Single();
        Assert.Equal(created.Id, due.Id);

        var latest = repo.GetLatestCompletedCalibrationEvent(hardware.Id);
        Assert.NotNull(latest);
        Assert.Equal(created.Id, latest.Id);
    }

    [Fact]
    public void CompletedAndFailedCalibration_SetPerformedAtAndDefaultNextDue()
    {
        var repo = new GrowRepository(_paths);
        var hardware = CreateHardware(repo);

        var ph = repo.CreateCalibrationEvent(new CalibrationEvent
        {
            HardwareItemId = hardware.Id,
            CalibrationType = CalibrationEventType.Ph,
            Status = CalibrationEventStatus.Completed,
            Result = CalibrationResult.Passed,
            Title = "pH Kalibrierung"
        });

        Assert.NotNull(ph.PerformedAtUtc);
        Assert.Equal(ph.PerformedAtUtc!.Value.AddDays(14), ph.NextDueAtUtc);

        foreach (var type in new[] { CalibrationEventType.Ec, CalibrationEventType.Orp, CalibrationEventType.Do })
        {
            var calibration = repo.CreateCalibrationEvent(new CalibrationEvent
            {
                HardwareItemId = hardware.Id,
                CalibrationType = type,
                Status = CalibrationEventStatus.Failed,
                Result = CalibrationResult.Failed,
                Title = $"{type} Kalibrierung"
            });

            Assert.NotNull(calibration.PerformedAtUtc);
            Assert.Equal(calibration.PerformedAtUtc!.Value.AddDays(30), calibration.NextDueAtUtc);
        }
    }

    [Fact]
    public void PlannedCalibration_WithGrowHardwareCreatesGrowTaskReminder()
    {
        var repo = new GrowRepository(_paths);
        var taskRepo = new TaskRepository(_paths);
        var tent = repo.GetTents().Single();
        var growId = repo.CreateGrow(new GrowRun
        {
            TentId = tent.Id,
            Name = "Calibration Grow",
            StartDate = Utc(2026, 6, 1),
            Status = GrowStatus.Running
        });
        var hardware = CreateHardware(repo, growId: growId, criticality: HardwareItemCriticality.High);
        var dueAt = Utc(2026, 6, 12);

        var created = repo.CreateCalibrationEvent(new CalibrationEvent
        {
            HardwareItemId = hardware.Id,
            CalibrationType = CalibrationEventType.Ph,
            Status = CalibrationEventStatus.Planned,
            Result = CalibrationResult.Unknown,
            Title = "pH 7.00 pruefen",
            DueAtUtc = dueAt
        });

        Assert.NotNull(created.GrowTaskId);
        var task = taskRepo.Get(created.GrowTaskId!.Value)!;
        Assert.Equal(growId, task.GrowId);
        Assert.Equal("Kalibrierung: pH Sonde - pH 7.00 pruefen", task.Title);
        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Equal(GrowTaskStatus.Open, task.Status);
        Assert.Equal(dueAt, task.DueAtUtc);
    }

    [Fact]
    public void PlannedCalibration_WithoutGrowHardwareDoesNotCreateGrowTask()
    {
        var repo = new GrowRepository(_paths);
        var hardware = CreateHardware(repo, growId: null);

        var created = repo.CreateCalibrationEvent(new CalibrationEvent
        {
            HardwareItemId = hardware.Id,
            CalibrationType = CalibrationEventType.Ph,
            Status = CalibrationEventStatus.Planned,
            Result = CalibrationResult.Unknown,
            Title = "Globaler pH Check",
            DueAtUtc = Utc(2026, 6, 12)
        });

        Assert.Null(created.GrowTaskId);
    }

    [Fact]
    public void CalibrationEvent_RejectsInvalidHardwareDatesAndTemperature()
    {
        var repo = new GrowRepository(_paths);

        Assert.Throws<InvalidOperationException>(() => repo.CreateCalibrationEvent(new CalibrationEvent
        {
            HardwareItemId = 9999,
            CalibrationType = CalibrationEventType.Ph,
            Status = CalibrationEventStatus.Planned,
            Result = CalibrationResult.Unknown,
            Title = "Bad"
        }));

        var hardware = CreateHardware(repo);
        Assert.Throws<InvalidOperationException>(() => repo.CreateCalibrationEvent(new CalibrationEvent
        {
            HardwareItemId = hardware.Id,
            CalibrationType = CalibrationEventType.Ph,
            Status = CalibrationEventStatus.Completed,
            Result = CalibrationResult.Passed,
            Title = "Bad Date",
            PerformedAtUtc = Utc(2026, 6, 20),
            NextDueAtUtc = Utc(2026, 6, 19)
        }));

        Assert.Throws<InvalidOperationException>(() => repo.CreateCalibrationEvent(new CalibrationEvent
        {
            HardwareItemId = hardware.Id,
            CalibrationType = CalibrationEventType.Ph,
            Status = CalibrationEventStatus.Completed,
            Result = CalibrationResult.Passed,
            Title = "Bad Temp",
            TemperatureC = 70m
        }));
    }

    private HardwareItem CreateHardware(
        GrowRepository repo,
        int? growId = null,
        HardwareItemCriticality criticality = HardwareItemCriticality.Medium)
    {
        var tent = repo.GetTents().Single();
        return repo.CreateHardwareItem(new HardwareItem
        {
            Name = "pH Sonde",
            Category = "Sensor",
            Status = HardwareItemStatus.Active,
            Criticality = criticality,
            TentId = tent.Id,
            GrowId = growId
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
