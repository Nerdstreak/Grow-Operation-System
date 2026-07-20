using GrowDiary.Web.Infrastructure;

namespace GrowDiary.Web.Services;

public sealed class AutoMeasurementWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutoMeasurementWorker> _logger;

    public AutoMeasurementWorker(IServiceProvider serviceProvider, ILogger<AutoMeasurementWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<AutoMeasurementExecutionService>();
                var snapshotRequests = service.ExecuteDue(DateTime.UtcNow);
                await CaptureSnapshotsAsync(scope, snapshotRequests, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AutoMeasurement-Job fehlgeschlagen.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    // Captures the tent camera snapshot(s) that a fired trigger requested and saves each
    // as a grow photo. Kept out of the (synchronous) execution service because the HA
    // camera fetch is async.
    private static async Task CaptureSnapshotsAsync(IServiceScope scope, IReadOnlyList<AutoSnapshotRequest> requests, CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            return;
        }

        var repository = scope.ServiceProvider.GetRequiredService<GrowRepository>();
        var haService = scope.ServiceProvider.GetRequiredService<HomeAssistantService>();
        var photos = scope.ServiceProvider.GetRequiredService<PhotoStorageService>();
        var settings = repository.GetEffectiveHomeAssistantSettings();
        if (!settings.IsConfigured)
        {
            return;
        }

        foreach (var request in requests)
        {
            var tent = repository.GetTent(request.TentId);
            if (tent is null || string.IsNullOrWhiteSpace(tent.CameraEntityId))
            {
                continue;
            }

            var grow = repository.GetGrow(request.GrowId);
            if (grow is null)
            {
                continue;
            }

            var snapshot = await haService.GetCameraSnapshotAsync(settings, tent.CameraEntityId, cancellationToken);
            if (snapshot is null)
            {
                continue;
            }

            await photos.SaveSnapshotAsync(grow, null, snapshot.Value.Bytes, cancellationToken);
        }
    }
}
