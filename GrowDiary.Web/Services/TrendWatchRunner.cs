using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Runs the holiday guard over every active grow and pushes what it finds.
///
/// Edge-triggered on purpose: a drift that lasts a week is one message, not ten thousand.
/// The state lives in AppSettings rather than memory so a restart doesn't re-announce
/// everything that was already reported.
/// </summary>
public sealed class TrendWatchRunner
{
    private const string StateKeyPrefix = "trendwatch:seen:";

    private readonly GrowRepository _repository;
    private readonly TargetValueService _targets;
    private readonly NotificationService _notifications;
    private readonly AppSettingsRepository _settings;
    private readonly ILogger<TrendWatchRunner> _logger;

    public TrendWatchRunner(
        GrowRepository repository,
        TargetValueService targets,
        NotificationService notifications,
        AppSettingsRepository settings,
        ILogger<TrendWatchRunner> logger)
    {
        _repository = repository;
        _targets = targets;
        _notifications = notifications;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>The current findings for one grow, without notifying — used by the API.</summary>
    public IReadOnlyList<TrendFinding> Inspect(int growId, DateTime now)
    {
        var grow = _repository.GetGrow(growId);
        if (grow is null)
        {
            return [];
        }

        var measurements = _repository.GetMeasurementsForGrow(growId);
        var stage = measurements.OrderByDescending(measurement => measurement.TakenAt).FirstOrDefault()?.Stage ?? GrowStage.Veg;
        // Die Profil-Kette Grow -> System -> Anbaustil, nicht die Abkuerzung.
        //
        // `GetTargets(HydroStyle, stage)` landet immer beim Standardprofil und
        // uebergeht damit das eigene Profil des Nutzers. Genau dieser Fehler
        // stand in der Diagnose und hat dort EC 0,6-0,8 gemeldet, waehrend die
        // Live-Kachel fuer denselben Grow 0,9-1,1 sagte.
        return TrendWatchService.Evaluate(
            measurements, ZieleFuer(grow, stage), now, _repository.GetChangeoutsForGrow(growId));
    }

    /// <summary>Die Sollwerte über die volle Profil-Kette.</summary>
    private HydroTargetValues? ZieleFuer(GrowRun grow, GrowStage stage)
    {
        var profil = SetpointProfileResolver.Resolve(
            grow.SetpointProfileId,
            grow.SystemId is { } systemId ? _repository.GetSystem(systemId)?.SetpointProfileId : null,
            grow.HydroStyle);
        return _targets.GetTargets(profil.ProfileId, stage);
    }

    public async Task RunAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        foreach (var grow in _repository.GetActiveGrows())
        {
            try
            {
                await RunForGrowAsync(grow, now, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Trend-Wächter fehlgeschlagen: Grow {GrowId}.", grow.Id);
            }
        }
    }

    private async Task RunForGrowAsync(GrowRun grow, DateTime now, CancellationToken cancellationToken)
    {
        var findings = Inspect(grow.Id, now);
        var key = StateKeyPrefix + grow.Id;
        var previous = (_settings.GetValue(key) ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        // Info-level findings are for the screen; only something worth acting on is worth
        // interrupting someone's holiday for.
        var pushWorthy = findings.Where(finding => finding.Severity >= TrendSeverity.Warning).ToList();

        foreach (var finding in pushWorthy.Where(finding => !previous.Contains(finding.Code)))
        {
            await _notifications.SendAsync(
                NotificationCategory.Risk,
                $"{grow.Name}: {finding.Headline}",
                finding.Detail,
                cancellationToken);
        }

        var current = pushWorthy.Select(finding => finding.Code).ToHashSet(StringComparer.Ordinal);
        if (!current.SetEquals(previous))
        {
            _settings.SetValue(key, string.Join(',', current));
        }
    }
}
