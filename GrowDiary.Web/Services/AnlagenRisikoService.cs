using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Legt Risiko-Ereignisse für Anlagenstörungen an — Pumpe, Kühler, USV,
/// Verbindung zu Home Assistant.
///
/// <para><b>Wozu.</b> Die Wächter schickten bisher nur eine Push-Nachricht.
/// Wer sie in der Ruhezeit verpasste, fand in der App nichts: gemessen gab es
/// 21 Risiko-Ereignisse, 20 davon aus der Abweichungs-Analyse und eines von
/// Hand. Eine ausgefallene Pumpe hinterließ keine Spur.</para>
///
/// <para><b>Und der zweite Grund.</b> Der Notfall-Empfehler verzweigt auf
/// <see cref="RiskEventType.PowerOutage"/>, <see cref="RiskEventType.UpsOnBattery"/>,
/// <see cref="RiskEventType.PumpOffline"/> und
/// <see cref="RiskEventType.HomeAssistantUnavailable"/> — vier Typen, die kein
/// Erzeuger je gesetzt hat. Gemessen bekamen 0 von 21 Ereignissen eine
/// SOP-Empfehlung, und der Ablauf „emergency-power-recovery" lag unerreichbar
/// in der Wissensbasis. Erst mit diesen Erzeugern führt der Weg irgendwohin.</para>
///
/// <para><b>Entdopplung.</b> Über den <c>DedupeKey</c>, genau wie bei den
/// Abweichungen: ein Ereignis je Lage und Zelt. Solange die Störung anhält,
/// wandert nur <c>LastSeenAtUtc</c> mit — es entsteht kein zweiter Eintrag je
/// Minutentakt.</para>
/// </summary>
public sealed class AnlagenRisikoService
{
    /// <summary>
    /// Vorsilbe der Entdopplungs-Schlüssel dieses Dienstes.
    /// </summary>
    /// <remarks>
    /// Getrennt von <c>deviation:grow:</c>, damit die Aufräumroutine der
    /// Abweichungen (die alles unter ihrer Vorsilbe schließt, was nicht mehr
    /// gemeldet wird) diese Ereignisse nicht mit abräumt.
    /// </remarks>
    public const string DedupeVorsilbe = "anlage:";

    private readonly GrowRepository _repository;
    private readonly ILogger<AnlagenRisikoService> _logger;

    public AnlagenRisikoService(GrowRepository repository, ILogger<AnlagenRisikoService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>Eine Störung melden — oder die laufende Meldung auffrischen.</summary>
    /// <param name="lage">
    /// Was gerade los ist, kurz und maschinenlesbar (etwa <c>pumpe-aus</c>).
    /// Ändert sich die Lage, entsteht ein neues Ereignis; bleibt sie gleich,
    /// wird das vorhandene aufgefrischt.
    /// </param>
    public void Melden(
        RiskEventType typ,
        RiskEventSeverity schwere,
        int? tentId,
        string titel,
        string beschreibung,
        string lage)
    {
        var schluessel = Schluessel(typ, tentId, lage);
        var jetzt = DateTime.UtcNow;

        var offen = _repository.GetRiskEvents()
            .Where(r => string.Equals(r.DedupeKey, schluessel, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(r => r.Status == RiskEventStatus.Open);

        if (offen is not null)
        {
            // Schon gemeldet. Nur den Zeitstempel nachziehen, damit man sieht,
            // dass die Stoerung anhaelt — und keinen zweiten Eintrag anlegen.
            offen.LastSeenAtUtc = jetzt;
            _repository.UpdateRiskEvent(offen);
            return;
        }

        _repository.CreateRiskEvent(new RiskEvent
        {
            EventType = typ,
            Severity = schwere,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.System,
            Title = titel,
            Description = beschreibung,
            TentId = tentId,
            StartedAtUtc = jetzt,
            LastSeenAtUtc = jetzt,
            DedupeKey = schluessel,
        });

        _logger.LogWarning("Anlagen-Risiko angelegt: {Typ} ({Lage}), Zelt {TentId}", typ, lage, tentId);
    }

    /// <summary>
    /// Entwarnung: alle offenen Ereignisse dieses Typs für dieses Zelt schließen.
    /// </summary>
    /// <remarks>
    /// Über den Typ und nicht über die Lage, weil sich die Lage geändert haben
    /// kann, während die Störung lief (erst stand die Umwälzpumpe, dann auch die
    /// Luftpumpe). Beim Entwarnen ist beides vorbei.
    /// </remarks>
    public void Entwarnen(RiskEventType typ, int? tentId)
    {
        var vorsilbe = Schluessel(typ, tentId, string.Empty);
        var jetzt = DateTime.UtcNow;

        foreach (var risk in _repository.GetRiskEvents())
        {
            if (risk.Status != RiskEventStatus.Open) continue;
            if (risk.DedupeKey is null || !risk.DedupeKey.StartsWith(vorsilbe, StringComparison.OrdinalIgnoreCase)) continue;

            risk.Status = RiskEventStatus.Resolved;
            risk.ResolvedAtUtc = jetzt;
            _repository.UpdateRiskEvent(risk);
            _logger.LogInformation("Anlagen-Risiko geschlossen: {Typ}, Zelt {TentId}", typ, tentId);
        }
    }

    private static string Schluessel(RiskEventType typ, int? tentId, string lage)
        => $"{DedupeVorsilbe}{typ.ToString().ToLowerInvariant()}:{tentId?.ToString() ?? "system"}:{lage}";
}
