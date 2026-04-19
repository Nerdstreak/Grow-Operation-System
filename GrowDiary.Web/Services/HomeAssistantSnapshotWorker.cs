using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

public sealed class HomeAssistantSnapshotWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HomeAssistantSnapshotWorker> _logger;
    private readonly AppPaths _paths;

    // Kamera-Snapshot: einmal täglich nach 12:00
    private DateOnly? _lastCameraCaptureDateLocal;

    public HomeAssistantSnapshotWorker(
        IServiceProvider serviceProvider,
        ILogger<HomeAssistantSnapshotWorker> logger,
        AppPaths paths)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _paths = paths;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;

            await CaptureReadingsAsync(stoppingToken);

            if (now.Hour == 2 && now.Minute < 5)
            {
                await AggregateYesterdayAsync(stoppingToken);
                await CleanupOldReadingsAsync();
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task CaptureReadingsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository  = scope.ServiceProvider.GetRequiredService<GrowRepository>();
        var sensorRepo  = scope.ServiceProvider.GetRequiredService<SensorReadingRepository>();
        var haService   = scope.ServiceProvider.GetRequiredService<HomeAssistantService>();

        var settings = repository.GetHomeAssistantSettings();
        if (!settings.IsConfigured) return;

        var tents = repository.GetTents();
        foreach (var tent in tents)
        {
            try
            {
                var states      = await haService.GetStatesAsync(settings, tent, cancellationToken);
                var capturedAt  = DateTime.UtcNow;

                foreach (var (key, state) in states)
                {
                    if (state.NumericValue is not { } value) continue;
                    sensorRepo.AddReading(new TentSensorReading
                    {
                        TentId        = tent.Id,
                        MetricKey     = key,
                        Value         = value,
                        Unit          = state.UnitOfMeasurement,
                        CapturedAtUtc = capturedAt
                    });
                }

                // Kamera-Snapshot täglich nach 12:00 Uhr
                await TryCaptureCamera(haService, settings, tent, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Reading-Capture fehlgeschlagen für Zelt {TentId}", tent.Id);
            }
        }
    }

    private async Task TryCaptureCamera(
        HomeAssistantService haService,
        HomeAssistantSettings settings,
        Tent tent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tent.CameraEntityId)) return;

        var localNow = DateTime.Now;
        if (localNow.Hour < 12) return;

        var today = DateOnly.FromDateTime(localNow);
        if (_lastCameraCaptureDateLocal == today) return;

        try
        {
            var snapshot = await haService.GetCameraSnapshotAsync(
                settings, tent.CameraEntityId, cancellationToken);
            if (snapshot is not null)
            {
                var dir = Path.Combine(
                    _paths.ContentRootPath, "App_Data", "snapshots", tent.Id.ToString());
                Directory.CreateDirectory(dir);
                var filePath = Path.Combine(dir, $"{today:yyyy-MM-dd}.jpg");
                await File.WriteAllBytesAsync(filePath, snapshot.Value.Bytes, cancellationToken);
                _lastCameraCaptureDateLocal = today;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Kamera-Snapshot fehlgeschlagen für Zelt {TentId}", tent.Id);
        }
    }

    private async Task AggregateYesterdayAsync(CancellationToken cancellationToken)
    {
        var yesterday = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        using var scope    = _serviceProvider.CreateScope();
        var repository  = scope.ServiceProvider.GetRequiredService<GrowRepository>();
        var sensorRepo  = scope.ServiceProvider.GetRequiredService<SensorReadingRepository>();

        var tents = repository.GetTents();
        foreach (var tent in tents)
        {
            var metricKeys = new[]
            {
                "temperature", "humidity", "vpd",
                "reservoir-ph", "reservoir-ec",
                "reservoir-temp", "reservoir-level", "co2"
            };

            foreach (var key in metricKeys)
            {
                var readings = sensorRepo.GetReadingsForDay(tent.Id, key, yesterday);
                if (readings.Count < 3) continue;

                var values = readings.Select(r => r.Value).ToList();
                var unit   = readings[0].Unit;
                var stat   = PercentileCalculator.ComputeStats(tent.Id, key, yesterday, values, unit);
                sensorRepo.UpsertDailyStat(stat);
            }
        }

        _logger.LogInformation("Tages-Aggregation abgeschlossen für {Date}", yesterday);
        await Task.CompletedTask;
    }

    private async Task CleanupOldReadingsAsync()
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);
        using var scope   = _serviceProvider.CreateScope();
        var sensorRepo = scope.ServiceProvider.GetRequiredService<SensorReadingRepository>();
        sensorRepo.DeleteOlderThan(cutoff);
        await Task.CompletedTask;
    }
}
