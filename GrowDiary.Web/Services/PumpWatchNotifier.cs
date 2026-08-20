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
    private readonly AnlagenRisikoService _risiken;
    private readonly ILogger<PumpWatchNotifier> _logger;

    public PumpWatchNotifier(
        AppSettingsRepository settings,
        NotificationService notifications,
        SystemHeartbeat heartbeat,
        AnlagenRisikoService risiken,
        ILogger<PumpWatchNotifier> logger)
    {
        _settings = settings;
        _notifications = notifications;
        _heartbeat = heartbeat;
        _risiken = risiken;
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

            // Auch in die App, nicht nur aufs Telefon.
            //
            // Vorher blieb von einer stehenden Pumpe nur eine Push-Nachricht.
            // Wer sie in der Ruhezeit verpasste, fand hinterher nichts — und der
            // Notfall-Ablauf, der genau an diesem Ereignistyp haengt, konnte nie
            // vorgeschlagen werden.
            _risiken.Melden(
                RiskEventType.PumpOffline,
                kritisch ? RiskEventSeverity.Critical : RiskEventSeverity.Warning,
                tent.Id,
                kritisch ? $"Pumpe steht ({tent.Name})" : $"Pumpe prüfen ({tent.Name})",
                text,
                lage);
        }
        else
        {
            await _notifications.SendAsync(
                NotificationCategory.System,
                $"🌱 Grow OS · Entwarnung ({tent.Name})",
                "Die Pumpen laufen wieder.",
                cancellationToken);
            _heartbeat.SetPumpMeldung(tent.Id, null);
            _risiken.Entwarnen(RiskEventType.PumpOffline, tent.Id);
            _logger.LogInformation("Pumpen-Wächter, Zelt {TentId}: wieder normal.", tent.Id);
        }

        // Kuehler und USV im selben Takt. Sie haengen an derselben Ursache wie
        // die Pumpen — faellt der Strom, faellt alles — und es waere ein zweiter
        // Waechter mit demselben Zeitplan und derselben Quelle.
        await AnlageMeldenAsync(tent, zustaende, nowUtc, cancellationToken);

        return befunde;
    }

    /// <summary>
    /// Kühler und USV: melden, wenn etwas steht, und entwarnen, wenn nicht.
    /// </summary>
    /// <remarks>
    /// Beide Größen waren seit jeher mappbar und wurden von keinem Dienst
    /// gelesen. Im RDWC ist der Kühler die Kette, die eine Ernte kostet: Kühler
    /// aus, Wassertemperatur steigt, Sauerstoff fällt, Wurzelfäule.
    /// </remarks>
    private async Task AnlageMeldenAsync(
        Tent tent,
        IReadOnlyDictionary<string, HomeAssistantState> zustaende,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        // Hat die Kuehler-Steuerung ihn selbst abgeschaltet? Dann ist „aus" der
        // Regler bei der Arbeit und kein Ausfall.
        //
        // ENTSCHEIDEND ist der letzte BEFEHL, nicht die letzte Schaltzeit. Eine
        // erste Fassung gab hier ein Zeitfenster von 20 Minuten (Schonfrist +
        // Mindestpause) — ab Minute 21 kam die kritische Meldung samt Push doch,
        // und eine kuehle Nacht ist genau dieser Fall. Ein Regler, der ein Geraet
        // besitzt, darf es beliebig lange ausgeschaltet lassen.
        //
        // Umgekehrt bleibt der echte Ausfall sichtbar: hat der Regler EIN
        // befohlen und die Steckdose meldet trotzdem aus, ist das keine
        // Regelpause, sondern ein Defekt — dann greift die alte Beurteilung.
        var absichtlich = KuehlerService.IstAbsichtlichAus(tent, KuehlerWorker.LetzterBefehl(_settings, tent.Id));

        var befunde = AnlagenWatchService.Beurteilen(zustaende, nowUtc, SchonfristMinuten, absichtlich);

        foreach (var (schluessel, typ) in new[]
                 {
                     ("chiller", RiskEventType.ChillerOffline),
                     ("ups-status", RiskEventType.UpsOnBattery),
                     ("ups-battery", RiskEventType.UpsOnBattery),
                 })
        {
            var befund = befunde.FirstOrDefault(b => b.Schluessel == schluessel);

            // Nichts gemappt heisst nichts zu sagen — und ausdruecklich AUCH
            // keine Entwarnung: sonst raeumte ein Zelt ohne Kuehler die Meldung
            // eines anderen mit weg.
            if (befund is null) continue;

            if (befund.Stufe == "ok")
            {
                _risiken.Entwarnen(typ, tent.Id);
                continue;
            }

            _risiken.Melden(
                typ,
                befund.Stufe == "kritisch" ? RiskEventSeverity.Critical : RiskEventSeverity.Warning,
                tent.Id,
                $"{befund.Name}: {(befund.Stufe == "kritisch" ? "Störung" : "prüfen")} ({tent.Name})",
                $"{befund.Meldung} {befund.Herkunft}",
                befund.Schluessel + ":" + befund.Stufe);

            var gemeldet = _heartbeat.PumpMeldung(tent.Id);
            var lage = $"anlage:{befund.Schluessel}:{befund.Stufe}";
            if (gemeldet == lage) continue;

            await _notifications.SendAsync(
                NotificationCategory.System,
                $"🌱 Grow OS · {befund.Name} ({tent.Name})",
                befund.Meldung,
                cancellationToken);
        }
    }
}
