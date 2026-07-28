using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Was Grow OS aus den beobachteten Lichtflanken macht.
/// </summary>
/// <remarks>
/// Zwei Aufgaben: den Zyklus lernen (fuer die Kachel und den Abgleich mit der
/// Phase) und den Einbruch in die Dunkelphase melden. Beides aus Daten, die
/// ohnehin schon aufgezeichnet wurden — es hat sie nur nie jemand gelesen.
/// </remarks>
public sealed class LightWatchService
{
    private readonly LightCycleReader _cycles;
    private readonly NotificationService _notifications;
    private readonly ILogger<LightWatchService> _logger;

    public LightWatchService(
        LightCycleReader cycles,
        NotificationService notifications,
        ILogger<LightWatchService> logger)
    {
        _cycles = cycles;
        _notifications = notifications;
        _logger = logger;
    }

    /// <summary>
    /// Passt der Zyklus nicht zur Phase, kommt hier der Klartext-Hinweis.
    /// </summary>
    public string? MismatchFor(Tent tent, DateTime nowUtc)
    {
        if (_cycles.CycleFor(tent.Id, nowUtc) is not { } cycle) return null;
        if (tent.ActiveGrows.FirstOrDefault() is not { } grow) return null;

        var stage = GrowStageResolver.Resolve(grow, DateTime.Today);
        return LightCycleLearner.Mismatch(cycle, stage, grow.SeedType);
    }

    /// <summary>
    /// Eine frische Einschaltflanke pruefen — und bei Einbruch sofort pushen.
    /// </summary>
    /// <remarks>
    /// Sofort heisst sofort: ein Push jetzt kann die Nacht noch retten, ein
    /// Blick ins Protokoll uebermorgen nicht.
    /// </remarks>
    public async Task CheckIntrusionAsync(Tent tent, LightTransitionEvent transition, CancellationToken cancellationToken)
    {
        if (transition.Kind != LightTransitionKind.LightOn) return;
        if (tent.ActiveGrows.FirstOrDefault() is not { } grow) return;

        var cycle = _cycles.CycleFor(tent.Id, transition.OccurredAtUtc);
        var stage = GrowStageResolver.Resolve(grow, DateTime.Today);
        var lokal = TimeOnly.FromDateTime(transition.OccurredAtUtc + _cycles.LocalOffset(tent.Id));

        if (!LightIntrusionGuard.IsIntrusion(cycle, lokal, stage, grow.SeedType)) return;

        _logger.LogWarning(
            "Lichteinbruch in der Dunkelphase: Zelt {TentId} um {Zeit}.", tent.Id, lokal);

        await _notifications.SendAsync(
            NotificationCategory.Risk,
            "Licht in der Dunkelphase",
            LightIntrusionGuard.Message(tent.Name, cycle!, lokal),
            cancellationToken);
    }

}
