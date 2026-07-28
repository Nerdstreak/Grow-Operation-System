using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Trägt zusammen, was vor einer Dosis bekannt ist.
/// </summary>
/// <remarks>
/// Vorher lag das im Controller und war für Stufe 1 absichtlich dünn: Messwert
/// nur aus der letzten Handmessung, Zielwert nur aus einem selbst eingetragenen
/// Grenzwert, Sondenkalibrierung gar nicht. Für „von Hand dosieren" ging das —
/// die Zahl kam ja vom Menschen.
///
/// Für den Vorschlag reicht es nicht. Ein Vorschlag, der gegen eine drei Tage
/// alte Handmessung rechnet, während der Sensor seit gestern etwas anderes sagt,
/// ist schlimmer als kein Vorschlag: er sieht genauso aus wie ein guter. Und wer
/// keinen eigenen Grenzwert eingetragen hat, bekam gar keinen — obwohl das
/// Phasen-Profil längst einen Sollwert kennt.
///
/// Deshalb steht das jetzt an einer Stelle, mit der Herkunft im Ergebnis, und
/// die Entscheidungen darin sind rein (<see cref="DosingSituationRules"/>).
/// </remarks>
public sealed class DosingContextBuilder
{
    private readonly GrowRepository _repository;
    private readonly DosingRepository _dosing;
    private readonly SensorReadingRepository _readings;
    private readonly AlertRuleRepository _alertRules;
    private readonly TargetValueService _targetValues;
    private readonly HydroSetupRepository _hydroSetups;
    private readonly AddbackRepository? _addback;

    public DosingContextBuilder(
        GrowRepository repository,
        DosingRepository dosing,
        SensorReadingRepository readings,
        AlertRuleRepository alertRules,
        TargetValueService targetValues,
        HydroSetupRepository hydroSetups,
        AddbackRepository? addback = null)
    {
        _repository = repository;
        _dosing = dosing;
        _readings = readings;
        _alertRules = alertRules;
        _targetValues = targetValues;
        _hydroSetups = hydroSetups;
        _addback = addback;
    }

    /// <param name="liveStates">
    /// Live-Zustände aus Home Assistant, wenn der Aufrufer sie hat. Daraus
    /// kommt die Umwälzung: an/aus-Zustände landen nicht im Messwert-Speicher
    /// (der hält nur Zahlen), also gibt es sie nur live. Ohne sie bleibt die
    /// Umwälzung unbekannt.
    /// </param>
    public DosingSituation Build(
        DosingPump pump,
        DateTime nowUtc,
        IReadOnlyDictionary<string, HomeAssistantState>? liveStates = null)
    {
        var heute = _dosing.GetDosesSince(pump.Id, nowUtc.Date);

        var tent = _repository.GetTent(pump.TentId);
        if (tent is null || pump.MetricKey is not { } key)
        {
            return DosingSituation.Empty(heute);
        }

        var (wert, alter, herkunft) = ReadingFor(tent.Id, key, nowUtc);
        var (ziel, zielHerkunft) = TargetFor(tent, key);
        var (kalibriert, ueberfaellig) = ProbeFor(tent.Id, key, nowUtc);

        return new DosingSituation(
            new DosingContext(wert, alter, kalibriert, ueberfaellig, heute, WaterLevelOk: null,
                LastTentDoseUtc: LastTentDose(tent.Id, pump.Id),
                TentHasPendingDose: TentHasPending(tent.Id),
                CirculationOn: CirculationFrom(liveStates)),
            ziel, zielHerkunft, herkunft,
            VolumeFactor: VolumeFactorFor(tent),
            LearnSinceUtc: LastSolutionChangeUtc(tent));
    }

    /// <summary>
    /// Halb leeres Becken, halbe Dosis: die gelernte Wirkung je ml stammt aus
    /// dem vollen Becken. Ohne frischen Fuellstand in Litern bleibt es bei 1.
    /// </summary>
    private double VolumeFactorFor(Tent tent)
    {
        var pegel = _readings.GetNewestReading(tent.Id, "reservoir-level");
        if (pegel is null || DateTime.UtcNow - pegel.CapturedAtUtc > TimeSpan.FromHours(2))
        {
            return 1;
        }

        var voll = tent.ActiveGrows.FirstOrDefault()?.SystemId is { } systemId
            ? _hydroSetups.GetSystem(systemId)?.ReservoirLiters
            : null;

        return DosingCalculator.VolumeFactor(pegel.Value, voll);
    }

    /// <summary>
    /// Der letzte Wasserwechsel — dort wird das Lernfenster geschnitten.
    /// Frisches Wasser puffert anders; Dosen von davor beschreiben ein anderes
    /// Becken. Beide Quellen zaehlen: die Messung mit Haken „Loesungswechsel"
    /// und der Wasserwechsel-Assistent.
    /// </summary>
    private DateTime? LastSolutionChangeUtc(Tent tent)
    {
        if (tent.ActiveGrows.FirstOrDefault() is not { } grow) return null;

        var ausMessung = _repository.GetMeasurementsForGrow(grow.Id)
            .Where(measurement => measurement.SolutionChange)
            .Select(measurement => (DateTime?)measurement.TakenAt.ToUniversalTime())
            .FirstOrDefault();

        var ausWechsel = _addback?.GetChangeoutsForGrow(grow.Id)
            .Select(entry => (DateTime?)entry.PerformedAtUtc)
            .Max();

        return new[] { ausMessung, ausWechsel }.Max();
    }

    /// <summary>
    /// Die jüngste Dosis einer ANDEREN Pumpe dieses Zelts — für die Mischpause,
    /// die dem Becken gehört, nicht der Pumpe.
    /// </summary>
    private DateTime? LastTentDose(int tentId, int ownPumpId)
        => _dosing.GetEvents(tentId: tentId, limit: 10)
            .Where(dose => dose.PumpId != ownPumpId
                && dose.Outcome == DoseOutcome.Done
                && dose.Trigger != DoseTrigger.Calibration)
            .Select(dose => (DateTime?)dose.OccurredAtUtc)
            .FirstOrDefault();

    /// <summary>Wartet für irgendeine Pumpe dieses Zelts noch eine zweite Hälfte?</summary>
    private bool TentHasPending(int tentId)
        => _dosing.GetPumps(tentId).Any(pump => _dosing.GetPendingForPump(pump.Id).Count > 0);

    /// <summary>
    /// Läuft die Umwälzung? Nur aus Live-Zuständen zu beantworten: an/aus wird
    /// nicht als Messwert gespeichert. Kein Zustand da → unbekannt.
    /// </summary>
    private static bool? CirculationFrom(IReadOnlyDictionary<string, HomeAssistantState>? states)
    {
        if (states is null || !states.TryGetValue("pump-circulation", out var state))
        {
            return null;
        }

        if (state.NumericValue is { } zahl)
        {
            return zahl > 0.5;
        }

        return LightStateNormalizer.Normalize(state.State) switch
        {
            LightState.On => true,
            LightState.Off => false,
            _ => null,
        };
    }

    private (double? Value, TimeSpan? Age, ReadingSource From) ReadingFor(int tentId, string key, DateTime nowUtc)
    {
        var sensor = _readings.GetNewestReading(tentId, key);

        double? handWert = null;
        DateTime? handWann = null;
        if (_repository.GetTent(tentId)?.ActiveGrows.FirstOrDefault() is { } grow
            && _repository.GetMeasurementsForGrow(grow.Id).FirstOrDefault() is { } letzte)
        {
            handWert = key switch
            {
                "reservoir-ph" => letzte.ReservoirPh,
                "reservoir-ec" => letzte.ReservoirEc,
                _ => null,
            };
            if (handWert is not null) handWann = letzte.TakenAt.ToUniversalTime();
        }

        return DosingSituationRules.PickReading(
            sensor?.Value, sensor?.CapturedAtUtc, handWert, handWann, nowUtc);
    }

    private (double? Target, TargetSource From) TargetFor(Tent tent, string key)
    {
        var eigene = UserTargets.For(key, _alertRules.GetForTent(tent.Id));

        // Dieselbe Kette wie auf den Live-Kacheln: Grow → Hydro-System →
        // Anbaustil, und die Phase kommt aus dem Grow, nicht aus der letzten
        // Messung. Ohne das bekäme jeder ohne eigenen Grenzwert keinen Vorschlag.
        (double Min, double Max)? ausProfil = null;
        if (tent.ActiveGrows.FirstOrDefault() is { } grow)
        {
            var stage = GrowStageResolver.Resolve(grow, DateTime.Today);
            var resolved = SetpointProfileResolver.Resolve(
                grow.SetpointProfileId, SystemProfileFor(grow), grow.HydroStyle);
            if (_targetValues.GetTargets(resolved.ProfileId, stage) is { } targets)
            {
                ausProfil = key switch
                {
                    "reservoir-ph" => (targets.PhMin, targets.PhMax),
                    "reservoir-ec" => (targets.EcMin, targets.EcMax),
                    _ => null,
                };
            }
        }

        return DosingSituationRules.PickTarget(eigene, ausProfil);
    }

    private string? SystemProfileFor(GrowRun grow)
        => grow.SystemId is { } systemId ? _hydroSetups.GetSystem(systemId)?.SetpointProfileId : null;

    /// <summary>
    /// Wann die zuständige Sonde zuletzt kalibriert wurde — und ob sie überfällig ist.
    /// </summary>
    /// <remarks>
    /// Eine driftende pH-Sonde meldet 6,0, während 5,4 im Becken steht. Die
    /// Automatik dosiert dann in die falsche Richtung, und zwar überzeugt. Die
    /// Sonde wird über ihre Metrik gefunden, nicht über ihren Namen.
    /// </remarks>
    private (DateTime? CalibratedAtUtc, bool Overdue) ProbeFor(int tentId, string key, DateTime nowUtc)
    {
        var sonde = _repository.GetHardwareItemsByTent(tentId)
            .Where(item => item.MetricType is { } metric && TentSensorMetricKeyMap.Resolve(metric) == key)
            .FirstOrDefault(item => item.Status == HardwareItemStatus.Active);
        if (sonde is null) return (null, false);

        var events = _repository.GetCalibrationEventsByHardwareItem(sonde.Id)
            .Where(item => item.Status == CalibrationEventStatus.Completed && item.PerformedAtUtc is not null)
            .OrderByDescending(item => item.PerformedAtUtc)
            .ToList();
        if (events.FirstOrDefault() is not { } letzte) return (null, false);

        var faellig = letzte.NextDueAtUtc
            ?? (sonde.CalibrationIntervalDays is { } tage ? letzte.PerformedAtUtc!.Value.AddDays(tage) : null);

        return (letzte.PerformedAtUtc, faellig is { } termin && termin < nowUtc);
    }
}
