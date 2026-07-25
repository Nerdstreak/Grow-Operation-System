using GrowDiary.Web.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace GrowDiary.Web.Services;

/// <summary>
/// A lightweight, dedicated loop that checks every tent's threshold rules once a minute,
/// so a per-minute repeat interval actually works. Unlike the snapshot worker (which runs
/// every 5 minutes and also stores readings), this only fetches live values and evaluates
/// alerts — it does not persist readings, keeping the minute cadence cheap.
/// </summary>
public sealed class AlertWatchWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AlertWatchWorker> _logger;

    public AlertWatchWorker(IServiceProvider serviceProvider, ILogger<AlertWatchWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small offset from the snapshot worker's start so they don't hammer HA in lockstep.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateAllTentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Alarm-Wächter-Durchlauf fehlgeschlagen.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task EvaluateAllTentsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<GrowRepository>();
        var haService = scope.ServiceProvider.GetRequiredService<HomeAssistantService>();
        var alertEval = scope.ServiceProvider.GetRequiredService<AlertEvaluationService>();

        var settings = repository.GetEffectiveHomeAssistantSettings();
        if (!settings.IsConfigured)
        {
            return;
        }

        foreach (var tent in repository.GetTents())
        {
            try
            {
                var states = await haService.GetStatesAsync(settings, tent, cancellationToken);
                await alertEval.EvaluateAsync(tent, states, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Alarm-Auswertung im Wächter fehlgeschlagen: Zelt {TentId}.", tent.Id);
            }
        }
    }
}
