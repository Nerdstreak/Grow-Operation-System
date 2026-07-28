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
    bool? WaterLevelOk,
    /// <summary>
    /// Die jüngste Dosis IRGENDEINER Pumpe dieses Zelts. Die Mischpause gehört
    /// dem Becken, nicht der Pumpe: nach jeder Dosis in dasselbe Wasser sagt
    /// der Messwert erst einmal nichts — egal, wer dosiert hat.
    /// </summary>
    DateTime? LastTentDoseUtc = null,
    /// <summary>
    /// Wartet im Zelt noch eine zweite Dünger-Hälfte (A gegeben, B steht aus)?
    /// Solange ja, dosiert hier NIEMAND — sonst korrigiert eine pH-Dosis einen
    /// Zustand, den B gleich wieder verschiebt.
    /// </summary>
    bool TentHasPendingDose = false,
    /// <summary>
    /// Läuft die Umwälzpumpe? null = unbekannt (kein Sensor gemappt oder Wert
    /// veraltet). In stehendes Wasser dosiert niemand: ohne Umwälzung verteilt
    /// sich nichts, ein Topf bekommt das Konzentrat ab.
    /// </summary>
    bool? CirculationOn = null);

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
    /// Wie lange die Pumpe laufen muss, um ungefähr die Zielmenge auszugeben.
    /// </summary>
    /// <remarks>
    /// Nur eine Schätzung für den Kalibrierlauf — sie muss nicht stimmen. Was
    /// zählt, ist die Menge, die danach wirklich im Becher steht: daraus wird
    /// die Fördermenge gerechnet. Die Schätzung sorgt nur dafür, dass man in
    /// einem gut ablesbaren Bereich landet.
    /// </remarks>
    public static double? SecondsForTarget(double targetMl, double? mlPerMinute)
        => mlPerMinute is { } rate && rate > 0 && targetMl > 0
            ? Math.Round(targetMl / rate * 60.0, 1)
            : null;

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
    /// <param name="sinceUtc">
    /// Schneidet das Lernfenster — in der Regel am letzten Wasserwechsel.
    /// Frisches Wasser hat frische Puffer: der pH-Down-Bedarf ist direkt nach
    /// dem Wechsel hoeher und sinkt ueber die Standzeit. Dosen von davor
    /// beschreiben ein anderes Wasser und wuerden den Schnitt verwaessern.
    /// </param>
    public static double? LearnedChangePerMl(IEnumerable<DoseEvent> history, DateTime? sinceUtc = null)
    {
        var brauchbar = history
            .Where(dose => dose.Outcome == DoseOutcome.Done && dose.DosedMl > 0)
            // Testdosen fliegen raus: es ist nichts geflossen, jede Aenderung
            // danach hat eine andere Ursache. Sonst stuende hier spaeter eine
            // Zahl, hinter der nie ein Tropfen war.
            .Where(dose => !dose.Simulated)
            .Where(dose => sinceUtc is not { } schnitt || dose.OccurredAtUtc >= schnitt)
            .Where(dose => dose.ValueBefore is not null && dose.ValueAfter is not null)
            .Select(dose => (dose.ValueAfter!.Value - dose.ValueBefore!.Value) / dose.DosedMl)
            .ToList();

        return brauchbar.Count < 3 ? null : Math.Round(brauchbar.Average(), 4);
    }

    /// <summary>
    /// Skaliert eine Dosis auf den aktuellen Fuellstand.
    /// </summary>
    /// <remarks>
    /// Die gelernte Wirkung je ml stammt aus Dosen ins volle Becken. Ist das
    /// Becken nur halb voll, wirkt dieselbe Menge fast doppelt — die Dosis muss
    /// also mit dem Fuellstand schrumpfen (ml · aktuell/voll). Nach oben wird
    /// nie skaliert: ein uebervolles Becken macht eine Dosis nur schwaecher,
    /// und schwaecher ist die sichere Richtung. Unter 30 % wird nicht weiter
    /// verkleinert, sondern beim Faktor 0,3 gedeckelt — bei so wenig Wasser
    /// stimmt meist etwas anderes nicht.
    /// </remarks>
    public static double VolumeFactor(double? currentLiters, double? referenceLiters)
    {
        if (currentLiters is not { } aktuell || referenceLiters is not { } voll) return 1;
        if (aktuell <= 0 || voll <= 0) return 1;

        return Math.Clamp(aktuell / voll, 0.3, 1.0);
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
    /// <summary>Keine DOSIS ist je länger, egal was die Rechnung sagt.</summary>
    public const double AbsoluteMaxSeconds = 60;

    /// <summary>
    /// Der Kalibrierlauf darf länger — er geht in den Messbecher, nicht ins Becken.
    /// </summary>
    /// <remarks>
    /// Für die Genauigkeit ist das entscheidend. Wer 23 ml abliest, liest sich
    /// leicht um 1 ml — das sind 4 % Fehler, die in jeder späteren Dosis
    /// stecken. Bei 100 ml ist derselbe Ablesefehler 1 %. Mit der Dosis-Grenze
    /// von 60 s käme man bei 46 ml/min nie über 46 ml hinaus.
    /// </remarks>
    public const double MaxCalibrationSeconds = 300;

    public static DosingDecision Evaluate(DosingPump pump, double requestedMl, DosingContext context, DateTime nowUtc)
    {
        // Im Testbetrieb wird nichts geschaltet — dann braucht es auch keine
        // Entität. Alles andere gilt unverändert, sonst prüfte der Test etwas
        // anderes als der Ernstfall.
        if (!pump.SimulationMode && string.IsNullOrWhiteSpace(pump.HaEntityId))
        {
            return DosingDecision.No("Keine Home-Assistant-Entität hinterlegt.");
        }

        // Wartet im Zelt noch eine zweite Dünger-Hälfte, dosiert hier niemand —
        // auch keine andere Pumpe. Sonst korrigiert pH einen Zustand, den B
        // gleich wieder verschiebt, oder A läuft doppelt, bevor B je kam.
        if (context.TentHasPendingDose)
        {
            return DosingDecision.No("Im Becken steht noch eine zweite Dünger-Hälfte aus — erst wird die Düngung vollständig.");
        }

        // In stehendes Wasser dosiert niemand: ohne Umwälzung verteilt sich
        // nichts, ein Topf bekommt das Konzentrat ab und die Wurzeln darin den
        // Schaden. Unbekannt (kein Sensor) blockt von Hand nicht — wer selbst
        // drückt, steht daneben und hört die Pumpe.
        if (context.CirculationOn == false)
        {
            return DosingDecision.No("Die Umwälzpumpe steht — ohne Umwälzung bliebe die Dosis als Konzentrat an einer Stelle.");
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

        // Was auf Tagesgrenze und Mischzeit zaehlt: nur, was in die Loesung
        // gegangen ist. Kalibrierlaeufe gehen in den Messbecher und aendern an
        // der Loesung nichts — beim ersten Durchspielen war deshalb nach dem
        // Kalibrieren 18 Minuten lang keine Dosis moeglich.
        var gelaufen = context.DosesToday
            .Where(dose => dose.Outcome == DoseOutcome.Done && dose.Trigger != DoseTrigger.Calibration)
            .ToList();
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

        // Die Mischpause gehört dem Becken, nicht der Pumpe: nach JEDER Dosis
        // in dasselbe Wasser sagt der Messwert erst einmal nichts — egal, wer
        // dosiert hat. Vorher zählte nur die eigene Historie, und eine Minute
        // nach der B-Hälfte hätte die pH-Pumpe in die Schliere gemessen.
        var letzteEigene = gelaufen.MaxBy(dose => dose.OccurredAtUtc)?.OccurredAtUtc;
        var letzteImBecken = new[] { letzteEigene, context.LastTentDoseUtc }.Max();
        if (letzteImBecken is { } zuletzt)
        {
            var seit = nowUtc - zuletzt;
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
        // jemand steht daneben. Unbeaufsichtigt nicht. Im Testbetrieb entfällt
        // die Forderung: es gibt nichts, das weiterlaufen könnte.
        if (!pump.SimulationMode && !pump.HasHomeAssistantAutoOff)
        {
            return DosingDecision.No("Ohne Abschaltung in Home Assistant bleibt die Automatik gesperrt.");
        }

        // Unbeaufsichtigt reicht „unbekannt" nicht: eine stehende Umwälzpumpe
        // ist oft genau der Grund, warum die Werte driften, die die Automatik
        // korrigieren will. Sie dosiert nur bei BESTÄTIGT laufender Umwälzung —
        // im Testbetrieb entfällt das, dort fliesst nichts.
        if (!pump.SimulationMode && context.CirculationOn != true)
        {
            return DosingDecision.No("Automatik dosiert nur bei bestätigt laufender Umwälzung — Umwälzpumpe in Home Assistant mappen.");
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
    public async Task<bool> RunForSecondsAsync(DosingPump pump, double seconds, CancellationToken cancellationToken = default, double? maxSeconds = null)
    {
        var kappt = Math.Clamp(seconds, 0, maxSeconds ?? DosingGuard.AbsoluteMaxSeconds);
        if (kappt <= 0) return false;

        // Testbetrieb: die Zeit vergeht wirklich, damit die Anzeige die echte
        // Dauer zeigt — geschaltet wird nichts. Ohne Home Assistant liesse sich
        // sonst kein einziger Schritt durchspielen.
        if (pump.SimulationMode)
        {
            _logger.LogInformation("Testbetrieb: Pumpe {Pump} laeuft {Seconds:0.#} s — es fliesst nichts.", pump.Name, kappt);
            await Task.Delay(TimeSpan.FromSeconds(kappt), CancellationToken.None);
            return true;
        }

        var settings = _repository.GetEffectiveHomeAssistantSettings();
        if (!settings.IsConfigured) return false;

        var (domain, _) = SplitEntity(pump.HaEntityId);

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
        // Eine Pumpe im Testbetrieb war nie an; „aus" ist trivial wahr.
        if (pump.SimulationMode) return true;

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
