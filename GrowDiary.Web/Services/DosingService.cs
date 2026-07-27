using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>Was zum Zeitpunkt der Anfrage über das Zelt bekannt ist.</summary>
public sealed record DosingContext(
    /// <summary>Der Messwert, gegen den dosiert wird; null = keiner vorhanden.</summary>
    double? Reading,
    /// <summary>Wie alt dieser Messwert ist.</summary>
    TimeSpan? ReadingAge,
    /// <summary>Wann die zugehörige Sonde zuletzt kalibriert wurde; null = nie.</summary>
    DateTime? ProbeCalibratedAtUtc,
    /// <summary>Ob die Sonde nach ihrem eigenen Plan überfällig ist.</summary>
    bool ProbeCalibrationOverdue,
    /// <summary>Bereits gelaufene Dosen dieser Pumpe seit Mitternacht.</summary>
    IReadOnlyList<DoseEvent> DosesToday,
    /// <summary>Füllstand über dem Minimum? null = unbekannt, dann kein Hindernis.</summary>
    bool? WaterLevelOk);

/// <summary>Das Urteil vor einer Dosis.</summary>
public sealed record DosingDecision(bool Allowed, double Ml, double Seconds, string Reason)
{
    public static DosingDecision No(string reason) => new(false, 0, 0, reason);
}

/// <summary>
/// Rechnet Dosen aus und prüft, ob überhaupt dosiert werden darf.
/// </summary>
/// <remarks>
/// Beide Teile sind rein und ohne Datenbank prüfbar. Das ist hier kein
/// Selbstzweck: am Ende dieser Rechnung drückt eine Pumpe Säure in ein Becken
/// mit lebenden Pflanzen. Ein Vorzeichenfehler wäre nicht „ein falscher Wert
/// auf einer Kachel", sondern ein verlorener Lauf.
/// </remarks>
public static class DosingCalculator
{
    /// <summary>Wie lange die Pumpe für diese Menge laufen muss.</summary>
    public static double SecondsFor(double ml, double mlPerMinute)
        => mlPerMinute <= 0 ? 0 : Math.Round(ml / mlPerMinute * 60.0, 2);

    /// <summary>Wie viel bei dieser Laufzeit herauskommt — die Gegenrichtung, fürs Kalibrieren.</summary>
    public static double MlFor(double seconds, double mlPerMinute)
        => Math.Round(seconds / 60.0 * mlPerMinute, 3);

    /// <summary>
    /// Fördermenge aus einem Kalibrierlauf: gemessene Milliliter auf die Minute
    /// hochgerechnet.
    /// </summary>
    public static double? MlPerMinuteFrom(double measuredMl, double seconds)
        => seconds <= 0 || measuredMl <= 0 ? null : Math.Round(measuredMl / seconds * 60.0, 2);

    /// <summary>
    /// Was aus dem Protokoll gelernt wurde: Änderung des Messwerts je Milliliter.
    /// </summary>
    /// <remarks>
    /// Aus der Konzentration allein lässt sich das nicht ausrechnen — wie stark
    /// eine Lösung gegen pH-Änderung gegenhält, hängt an Wasserhärte und Dünger.
    /// Also wird gemessen statt gerechnet: nur Dosen mit Wert davor UND danach
    /// zählen, und erst ab dreien überhaupt.
    /// </remarks>
    public static double? LearnedChangePerMl(IEnumerable<DoseEvent> history)
    {
        var brauchbar = history
            .Where(dose => dose.Outcome == DoseOutcome.Done && dose.DosedMl > 0)
            .Where(dose => dose.ValueBefore is not null && dose.ValueAfter is not null)
            .Select(dose => (dose.ValueAfter!.Value - dose.ValueBefore!.Value) / dose.DosedMl)
            .ToList();

        return brauchbar.Count < 3 ? null : Math.Round(brauchbar.Average(), 4);
    }

    /// <summary>
    /// Die Menge für den Weg vom Ist- zum Zielwert.
    /// </summary>
    /// <remarks>
    /// Ohne Erfahrung gibt es keine Zahl — geraten wird nicht. Mit Erfahrung
    /// wird bewusst nur die halbe Strecke gegangen: nach unten ist ein pH
    /// schnell, zurück fast nicht. Lieber zweimal wenig als einmal zu viel.
    /// </remarks>
    public static double? MlToReach(double current, double target, double? changePerMl)
    {
        if (changePerMl is not { } proMl || Math.Abs(proMl) < 1e-9) return null;

        var strecke = target - current;
        // Wirkt die Pumpe in die falsche Richtung, ist hier nichts zu tun.
        if (Math.Sign(strecke) != Math.Sign(proMl)) return null;

        var voll = strecke / proMl;
        return voll <= 0 ? null : Math.Round(voll * 0.5, 2);
    }
}

/// <summary>Prüft die Anschläge — getrennt vom Rechnen, damit jeder Riegel einzeln belegt ist.</summary>
public static class DosingGuard
{
    /// <summary>Kein Lauf ist je länger, egal was die Rechnung sagt.</summary>
    public const double AbsoluteMaxSeconds = 60;

    public static DosingDecision Evaluate(DosingPump pump, double requestedMl, DosingContext context, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(pump.HaEntityId))
        {
            return DosingDecision.No("Keine Home-Assistant-Entität hinterlegt.");
        }

        if (pump.MlPerMinute is not { } mlPerMinute || mlPerMinute <= 0)
        {
            return DosingDecision.No("Pumpe ist nicht kalibriert — ohne Fördermenge sind Milliliter keine Laufzeit.");
        }

        if (requestedMl <= 0)
        {
            return DosingDecision.No("Nichts zu dosieren.");
        }

        // Deckeln statt ablehnen: die gewünschte Wirkung kommt dann eben in
        // zwei Schritten. Ablehnen hiesse, dass gar nichts passiert.
        var ml = Math.Min(requestedMl, pump.MaxSingleDoseMl);

        var gelaufen = context.DosesToday.Where(dose => dose.Outcome == DoseOutcome.Done).ToList();
        if (gelaufen.Count >= pump.MaxDosesPerDay)
        {
            return DosingDecision.No($"Tagesgrenze erreicht: schon {gelaufen.Count} Dosierungen.");
        }

        var heuteMl = gelaufen.Sum(dose => dose.DosedMl);
        if (heuteMl >= pump.MaxMlPerDay)
        {
            return DosingDecision.No($"Tagesmenge erreicht: schon {heuteMl:0.#} ml.");
        }

        // Nur so viel, wie bis zur Tagesmenge noch frei ist.
        ml = Math.Min(ml, pump.MaxMlPerDay - heuteMl);
        if (ml <= 0)
        {
            return DosingDecision.No($"Tagesmenge erreicht: schon {heuteMl:0.#} ml.");
        }

        var letzte = gelaufen.MaxBy(dose => dose.OccurredAtUtc);
        if (letzte is not null)
        {
            var seit = nowUtc - letzte.OccurredAtUtc;
            if (seit < TimeSpan.FromMinutes(pump.MinIntervalMinutes))
            {
                var rest = (int)Math.Ceiling((TimeSpan.FromMinutes(pump.MinIntervalMinutes) - seit).TotalMinutes);
                return DosingDecision.No($"Noch {rest} min mischen — erst danach sagt der Messwert etwas.");
            }
        }

        if (context.WaterLevelOk == false)
        {
            return DosingDecision.No("Wasserstand unter Minimum.");
        }

        var seconds = DosingCalculator.SecondsFor(ml, mlPerMinute);
        if (seconds <= 0)
        {
            return DosingDecision.No("Errechnete Laufzeit ist null.");
        }

        if (seconds > AbsoluteMaxSeconds)
        {
            return DosingDecision.No($"Laufzeit {seconds:0.#} s über der harten Grenze von {AbsoluteMaxSeconds:0} s.");
        }

        return new DosingDecision(true, Math.Round(ml, 2), seconds, "Freigegeben.");
    }

    /// <summary>
    /// Zusätzliche Riegel, die nur für die Automatik gelten. Von Hand darf man
    /// mehr — wer selbst drückt, steht daneben und sieht, was passiert.
    /// </summary>
    public static DosingDecision EvaluateAutomatic(DosingPump pump, double requestedMl, DosingContext context, DateTime nowUtc)
    {
        if (!pump.AutomationEnabled)
        {
            return DosingDecision.No("Automatik ist für diese Pumpe aus.");
        }

        // Ohne Abschaltung in Home Assistant läuft die Pumpe weiter, wenn Grow OS
        // zwischen Ein- und Ausschalten abstürzt. Von Hand ist das vertretbar —
        // jemand steht daneben. Unbeaufsichtigt nicht.
        if (!pump.HasHomeAssistantAutoOff)
        {
            return DosingDecision.No("Ohne Abschaltung in Home Assistant bleibt die Automatik gesperrt.");
        }

        if (context.Reading is null)
        {
            return DosingDecision.No("Kein Messwert vorhanden.");
        }

        if (context.ReadingAge is not { } age || age > TimeSpan.FromMinutes(pump.MaxReadingAgeMinutes))
        {
            var alt = context.ReadingAge is { } a ? $"{(int)a.TotalMinutes} min alt" : "unbekannt alt";
            return DosingDecision.No($"Messwert ist {alt} — es wird nicht auf alte Werte dosiert.");
        }

        if (context.ProbeCalibratedAtUtc is null)
        {
            return DosingDecision.No("Sonde wurde nie kalibriert — eine driftende Sonde dosiert blind.");
        }

        if (context.ProbeCalibrationOverdue)
        {
            return DosingDecision.No("Sonden-Kalibrierung überfällig — erst kalibrieren, dann dosieren.");
        }

        return Evaluate(pump, requestedMl, context, nowUtc);
    }
}

/// <summary>Schaltet die Pumpe tatsächlich — und schaltet sie garantiert wieder aus.</summary>
public sealed class DosingService
{
    private readonly GrowRepository _repository;
    private readonly DosingRepository _dosing;
    private readonly HomeAssistantService _homeAssistant;
    private readonly ILogger<DosingService> _logger;

    public DosingService(
        GrowRepository repository,
        DosingRepository dosing,
        HomeAssistantService homeAssistant,
        ILogger<DosingService> logger)
    {
        _repository = repository;
        _dosing = dosing;
        _homeAssistant = homeAssistant;
        _logger = logger;
    }

    /// <summary>
    /// Lässt die Pumpe für die angegebene Zeit laufen.
    /// </summary>
    /// <remarks>
    /// Das Ausschalten steht in einem <c>finally</c> und läuft auch dann, wenn
    /// der Aufruf abgebrochen wird. Was es NICHT übersteht, ist ein Absturz des
    /// ganzen Add-ons — dagegen hilft nur die Abschaltung in Home Assistant und
    /// der Auswurf beim Start (<see cref="TurnAllOffAsync"/>).
    /// </remarks>
    public async Task<bool> RunForSecondsAsync(DosingPump pump, double seconds, CancellationToken cancellationToken = default)
    {
        var settings = _repository.GetEffectiveHomeAssistantSettings();
        if (!settings.IsConfigured) return false;

        var (domain, _) = SplitEntity(pump.HaEntityId);
        var kappt = Math.Clamp(seconds, 0, DosingGuard.AbsoluteMaxSeconds);
        if (kappt <= 0) return false;

        var an = await _homeAssistant.CallEntityServiceAsync(settings, domain, "turn_on", pump.HaEntityId, cancellationToken);
        if (!an)
        {
            _logger.LogWarning("Pumpe {Pump} liess sich nicht einschalten.", pump.Name);
            return false;
        }

        try
        {
            // Bewusst ohne den uebergebenen Token: bricht der Aufrufer ab, soll
            // trotzdem die volle Zeit gewartet und danach ausgeschaltet werden.
            // Ein Abbruch mitten im Lauf darf die Pumpe nicht laufen lassen.
            await Task.Delay(TimeSpan.FromSeconds(kappt), CancellationToken.None);
        }
        finally
        {
            var aus = await _homeAssistant.CallEntityServiceAsync(settings, domain, "turn_off", pump.HaEntityId, CancellationToken.None);
            if (!aus)
            {
                _logger.LogError(
                    "Pumpe {Pump} ({Entity}) liess sich NICHT ausschalten — in Home Assistant prüfen.",
                    pump.Name, pump.HaEntityId);
            }
        }

        return true;
    }

    /// <summary>Schaltet eine einzelne Pumpe aus — ohne Bedingungen.</summary>
    public async Task<bool> TurnOffAsync(DosingPump pump, CancellationToken cancellationToken = default)
    {
        var settings = _repository.GetEffectiveHomeAssistantSettings();
        if (!settings.IsConfigured || string.IsNullOrWhiteSpace(pump.HaEntityId)) return false;

        var (domain, _) = SplitEntity(pump.HaEntityId);
        return await _homeAssistant.CallEntityServiceAsync(settings, domain, "turn_off", pump.HaEntityId, cancellationToken);
    }

    /// <summary>
    /// Wirft beim Start jede eingerichtete Pumpe einmal aus.
    /// </summary>
    /// <remarks>
    /// Der Totmann: Ist Grow OS mitten in einer Dosis abgestürzt, läuft die
    /// Pumpe seither. Der erste Handgriff nach dem Hochfahren ist deshalb, alle
    /// abzuschalten — das kostet nichts, wenn ohnehin alles aus war.
    /// </remarks>
    public async Task TurnAllOffAsync(CancellationToken cancellationToken = default)
    {
        var settings = _repository.GetEffectiveHomeAssistantSettings();
        if (!settings.IsConfigured) return;

        foreach (var pump in _dosing.GetPumps())
        {
            if (string.IsNullOrWhiteSpace(pump.HaEntityId)) continue;
            var (domain, _) = SplitEntity(pump.HaEntityId);
            await _homeAssistant.CallEntityServiceAsync(settings, domain, "turn_off", pump.HaEntityId, cancellationToken);
        }
    }

    /// <summary>„switch.dosier_ph_minus" → („switch", "dosier_ph_minus").</summary>
    public static (string Domain, string Name) SplitEntity(string entityId)
    {
        var index = entityId.IndexOf('.');
        return index <= 0
            ? ("switch", entityId)
            : (entityId[..index], entityId[(index + 1)..]);
    }
}
