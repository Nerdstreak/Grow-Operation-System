using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Bringt das Urteil des <see cref="PumpWatchService"/> zum Betreiber.
/// </summary>
/// <remarks>
/// <para>Getrennt vom Urteil selbst, damit die Entscheidung „ist das ein
/// Ausfall?" ohne Home Assistant, ohne Datenbank und ohne Push prüfbar bleibt.
/// Hier steht nur, wann jemand geweckt wird.</para>
///
/// <para>Gemeldet wird bei Wechsel der Lage, nicht im Minutentakt — sonst
/// stellt der Betreiber die Benachrichtigungen ab, und dann nützt der beste
/// Wächter nichts. Wird es wieder gut, kommt eine Entwarnung: eine Warnung,
/// die man nie zurücknimmt, lernt man zu ignorieren.</para>
/// </remarks>
public sealed class PumpWatchNotifier
{
    private readonly AppSettingsRepository _settings;
    private readonly NotificationService _notifications;
    private readonly SystemHeartbeat _heartbeat;
    private readonly ILogger<PumpWatchNotifier> _logger;

    public PumpWatchNotifier(
        AppSettingsRepository settings,
        NotificationService notifications,
        SystemHeartbeat heartbeat,
        ILogger<PumpWatchNotifier> logger)
    {
        _settings = settings;
        _notifications = notifications;
        _heartbeat = heartbeat;
        _logger = logger;
    }

    /// <summary>Die eingestellte Schonfrist, bevor ein Aus als Ausfall zählt.</summary>
    public int SchonfristMinuten
    {
        get
        {
            var wert = _settings.GetValue(PumpWatchService.SchonfristKey);
            return int.TryParse(wert, out var minuten) && minuten is > 0 and <= 720
                ? minuten
                : PumpWatchService.StandardSchonfristMinuten;
        }
        set => _settings.SetValue(PumpWatchService.SchonfristKey, Math.Clamp(value, 1, 720).ToString());
    }

    public IReadOnlyList<PumpBefund> Pruefen(IReadOnlyDictionary<string, HomeAssistantState> zustaende, DateTime nowUtc)
        => PumpWatchService.Beurteilen(zustaende, nowUtc, SchonfristMinuten);

    public async Task<IReadOnlyList<PumpBefund>> PruefenUndMeldenAsync(
        Tent tent,
        IReadOnlyDictionary<string, HomeAssistantState> zustaende,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var befunde = Pruefen(zustaende, nowUtc);
        var schlimm = befunde.Where(b => b.Stufe != "ok").ToList();

        // Der Schluessel traegt WELCHE Pumpe in welcher Stufe steht: faellt zur
        // Luftpumpe auch die Umwaelzung aus, ist das eine neue Lage.
        var lage = schlimm.Count == 0
            ? null
            : string.Join("|", schlimm.Select(b => $"{b.Schluessel}:{b.Stufe}").OrderBy(x => x));

        var zuletzt = _heartbeat.PumpMeldung(tent.Id);
        if (lage == zuletzt) return befunde;

        if (lage is not null)
        {
            var kritisch = schlimm.Any(b => b.Stufe == "kritisch");
            var text = string.Join(" ", schlimm.Select(b => b.Meldung));
            var gesendet = await _notifications.SendAsync(
                NotificationCategory.System,
                kritisch ? $"🌱 Grow OS · Pumpe steht ({tent.Name})" : $"🌱 Grow OS · Pumpe prüfen ({tent.Name})",
                text,
                cancellationToken);

            if (gesendet)
            {
                _heartbeat.SetPumpMeldung(tent.Id, lage);
                _logger.LogWarning("Pumpen-Wächter, Zelt {TentId}: {Text}", tent.Id, text);
            }
        }
        else
        {
            await _notifications.SendAsync(
                NotificationCategory.System,
                $"🌱 Grow OS · Entwarnung ({tent.Name})",
                "Die Pumpen laufen wieder.",
                cancellationToken);
            _heartbeat.SetPumpMeldung(tent.Id, null);
            _logger.LogInformation("Pumpen-Wächter, Zelt {TentId}: wieder normal.", tent.Id);
        }

        return befunde;
    }
}
