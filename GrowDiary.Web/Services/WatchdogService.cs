using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>What the watchdog looked at.</summary>
public sealed record WatchdogInput(
    bool HomeAssistantConfigured,
    int MappedSensorCount,
    DateTime? LastSnapshotRunUtc,
    DateTime? LastHomeAssistantSuccessUtc,
    DateTime? NewestReadingUtc,
    string? LastHomeAssistantError);

/// <summary>What it concluded.</summary>
public sealed record WatchdogVerdict(string Code, string Headline, string Detail, bool IsProblem);

/// <summary>
/// The dead-man's switch: a normal alert says "this value is wrong", the watchdog says
/// "I cannot see anything at all right now". Without it, silence is ambiguous — it could
/// mean everything is fine, or that monitoring itself has stopped.
/// </summary>
public sealed class WatchdogService
{
    public const string Ok = "ok";
    public const string Idle = "idle";
    public const string WorkerStalled = "worker_stalled";
    public const string HaUnreachable = "ha_unreachable";
    public const string NoData = "no_data";

    // The snapshot worker loops every 5 minutes; three missed rounds is a real problem,
    // not a hiccup.
    public const int StalledMinutes = 16;
    public const int NoDataMinutes = 21;

    private readonly GrowRepository _repository;
    private readonly SensorReadingRepository _readings;
    private readonly SystemHeartbeat _heartbeat;
    private readonly NotificationService _notifications;
    private readonly ILogger<WatchdogService> _logger;

    public WatchdogService(
        GrowRepository repository,
        SensorReadingRepository readings,
        SystemHeartbeat heartbeat,
        NotificationService notifications,
        ILogger<WatchdogService> logger)
    {
        _repository = repository;
        _readings = readings;
        _heartbeat = heartbeat;
        _notifications = notifications;
        _logger = logger;
    }

    /// <summary>Pure verdict logic, so every branch can be tested without a database.</summary>
    public static WatchdogVerdict Evaluate(WatchdogInput input, DateTime nowUtc)
    {
        if (!input.HomeAssistantConfigured)
        {
            return new WatchdogVerdict(Idle, "Nicht überwacht",
                "Home Assistant ist noch nicht verbunden — es gibt nichts zu überwachen.", false);
        }

        if (input.MappedSensorCount == 0)
        {
            return new WatchdogVerdict(Idle, "Nicht überwacht",
                "Noch kein Sensor zugeordnet — ordne auf der Home-Assistant-Seite Entitäten zu.", false);
        }

        var snapshotAge = Age(input.LastSnapshotRunUtc, nowUtc);
        if (snapshotAge is null || snapshotAge > TimeSpan.FromMinutes(StalledMinutes))
        {
            return new WatchdogVerdict(WorkerStalled, "Überwachung steht",
                "Grow OS hat seit über 15 Minuten keine Runde mehr gedreht. Ein Neustart des Add-ons behebt das meist.", true);
        }

        var haAge = Age(input.LastHomeAssistantSuccessUtc, nowUtc);
        if (haAge is null || haAge > TimeSpan.FromMinutes(StalledMinutes))
        {
            var reason = string.IsNullOrWhiteSpace(input.LastHomeAssistantError) ? string.Empty : $" ({input.LastHomeAssistantError})";
            return new WatchdogVerdict(HaUnreachable, "Home Assistant antwortet nicht",
                $"Seit über 15 Minuten kommen keine Werte aus Home Assistant{reason}. Solange sind Grenzwert-Alarme blind.", true);
        }

        var readingAge = Age(input.NewestReadingUtc, nowUtc);
        if (readingAge is null || readingAge > TimeSpan.FromMinutes(NoDataMinutes))
        {
            return new WatchdogVerdict(NoData, "Keine neuen Messwerte",
                "Die Verbindung steht, aber deine Sensoren liefern seit über 20 Minuten nichts Neues. Prüfe die Geräte in Home Assistant.", true);
        }

        var minutes = (int)Math.Round(readingAge.Value.TotalMinutes);
        return new WatchdogVerdict(Ok, "Alles wach",
            minutes <= 1 ? "Letzte Sensordaten gerade eben." : $"Letzte Sensordaten vor {minutes} Minuten.", false);
    }

    private static TimeSpan? Age(DateTime? timestampUtc, DateTime nowUtc)
        => timestampUtc is { } value ? nowUtc - value : null;

    /// <summary>Collects the current state without notifying — used by the status endpoint.</summary>
    public WatchdogVerdict Inspect(DateTime nowUtc)
    {
        var settings = _repository.GetEffectiveHomeAssistantSettings();
        var tents = _repository.GetTents();
        var mappedSensors = tents.Sum(tent => tent.Sensors.Count(sensor => sensor.IsActive && !string.IsNullOrWhiteSpace(sensor.HaEntityId)));
        var newest = tents
            .Select(tent => _readings.GetNewestReadingUtc(tent.Id))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty()
            .Max();

        var (snapshotRun, haSuccess, haError) = _heartbeat.Read();
        return Evaluate(
            new WatchdogInput(
                settings.IsConfigured,
                mappedSensors,
                snapshotRun,
                haSuccess,
                newest == default ? null : newest,
                haError),
            nowUtc);
    }

    /// <summary>
    /// Checks and pushes once per state change: one message when something breaks, one when
    /// it recovers. Never repeats the same complaint.
    /// </summary>
    public async Task<WatchdogVerdict> CheckAndNotifyAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var verdict = Inspect(nowUtc);
        var previous = _heartbeat.NotifiedCode;

        if (verdict.IsProblem && previous != verdict.Code)
        {
            var sent = await _notifications.SendAsync(
                NotificationCategory.System, "🌱 Grow OS · Systemwarnung", $"{verdict.Headline}: {verdict.Detail}", cancellationToken);
            if (sent)
            {
                _heartbeat.NotifiedCode = verdict.Code;
                _logger.LogWarning("Watchdog: {Code} — {Detail}", verdict.Code, verdict.Detail);
            }
        }
        else if (!verdict.IsProblem && previous is not null)
        {
            await _notifications.SendAsync(
                NotificationCategory.System, "🌱 Grow OS · Entwarnung", "Die Überwachung läuft wieder — Sensordaten kommen an.", cancellationToken);
            _heartbeat.NotifiedCode = null;
            _logger.LogInformation("Watchdog: wieder normal.");
        }

        return verdict;
    }
}
