using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

public sealed class DeviationAnalyzerService
{
    private const int MaxConsecutiveLookback = 10;

    private readonly TargetValueService _targetValues;

    public DeviationAnalyzerService(TargetValueService targetValues)
    {
        _targetValues = targetValues;
    }

    public IReadOnlyList<GrowDeviation> Analyze(GrowRun grow, IReadOnlyList<Measurement> recentMeasurements)
    {
        if (grow.IrrigationType != IrrigationType.ActiveHydro || !grow.Profile.IsHydro)
        {
            return Array.Empty<GrowDeviation>();
        }

        var sorted = recentMeasurements
            .Where(measurement => measurement.GrowId == 0 || measurement.GrowId == grow.Id)
            .OrderByDescending(measurement => measurement.TakenAt)
            .ThenByDescending(measurement => measurement.Id)
            .Take(MaxConsecutiveLookback)
            .ToList();

        if (sorted.Count == 0)
        {
            return Array.Empty<GrowDeviation>();
        }

        var latest = sorted[0];
        var targets = _targetValues.GetTargets(grow.HydroStyle, latest.Stage);
        var deviations = new List<GrowDeviation>();

        CheckPh(grow, sorted, targets, deviations);
        CheckEc(grow, sorted, targets, deviations);
        CheckOrp(grow, sorted, deviations);
        CheckWaterTemp(grow, sorted, deviations);
        CheckDissolvedOxygen(grow, sorted, deviations);
        CheckPpfd(grow, sorted, targets, deviations);
        CheckCo2(grow, sorted, deviations);

        return deviations;
    }

    private static void CheckPh(GrowRun grow, List<Measurement> sorted, HydroTargetValues? targets, List<GrowDeviation> result)
    {
        sorted = sorted.Where(measurement => measurement.ReservoirPh.HasValue).ToList();
        if (sorted.Count == 0 || sorted[0].ReservoirPh is not { } actual)
        {
            return;
        }

        var outsideTarget = targets is not null && (actual < targets.PhMin || actual > targets.PhMax);
        var critical = actual < 5.5 || actual > 6.5;
        if (!outsideTarget && !critical)
        {
            return;
        }

        var targetMin = targets?.PhMin;
        var targetMax = targets?.PhMax;
        var predicate = targets is not null
            ? new Func<double, bool>(value => value < targets.PhMin || value > targets.PhMax)
            : value => value < 5.5 || value > 6.5;
        var participants = Consecutive(sorted, measurement => measurement.ReservoirPh, predicate);
        var tooHigh = actual > (targetMax ?? 6.5);

        result.Add(CreateDeviation(
            grow,
            "hydro.ph",
            DeviationMetric.Ph,
            actual,
            targetMin,
            targetMax,
            "pH",
            critical ? DeviationSeverity.Critical : DeviationSeverity.Warning,
            tooHigh
                ? $"Reservoir-pH {actual:0.00} liegt ueber dem Zielbereich."
                : $"Reservoir-pH {actual:0.00} liegt unter dem Zielbereich.",
            tooHigh ? "pH-Down pruefen." : "pH-Up pruefen.",
            tooHigh ? "ph-too-high" : "ph-too-low",
            participants));
    }

    private static void CheckEc(GrowRun grow, List<Measurement> sorted, HydroTargetValues? targets, List<GrowDeviation> result)
    {
        sorted = sorted.Where(measurement => measurement.ReservoirEc.HasValue).ToList();
        if (sorted.Count == 0 || sorted[0].ReservoirEc is not { } actual)
        {
            return;
        }

        var critical = actual < 0 || actual > 3.0;
        var outsideTarget = targets is not null && (actual < targets.EcMin || actual > targets.EcMax);
        var trendParticipants = GetEcTrendParticipants(sorted);

        if (!critical && !outsideTarget && trendParticipants.Count == 0)
        {
            return;
        }

        IReadOnlyList<Measurement> participants;
        string message;
        string? hint;
        if (trendParticipants.Count > 0)
        {
            participants = trendParticipants;
            var diff = sorted[0].ReservoirEc!.Value - sorted[1].ReservoirEc!.Value;
            message = diff > 0
                ? $"Reservoir-EC ist um {diff:+0.00;-0.00} mS/cm gestiegen."
                : $"Reservoir-EC ist um {diff:+0.00;-0.00} mS/cm gefallen.";
            hint = diff > 0 ? "Verdunstung/Addback pruefen." : "Naehrstoffaufnahme/Addback pruefen.";
        }
        else
        {
            var predicate = targets is not null
                ? new Func<double, bool>(value => value < targets.EcMin || value > targets.EcMax)
                : value => value < 0 || value > 3.0;
            participants = Consecutive(sorted, measurement => measurement.ReservoirEc, predicate);
            message = $"Reservoir-EC {actual:0.00} liegt ausserhalb des Zielbereichs.";
            hint = "EC-Ziel und Addback pruefen.";
        }

        result.Add(CreateDeviation(
            grow,
            "hydro.ec",
            DeviationMetric.Ec,
            actual,
            targets?.EcMin,
            targets?.EcMax,
            "mS/cm",
            critical ? DeviationSeverity.Critical : DeviationSeverity.Warning,
            message,
            hint,
            null,
            participants));
    }

    private static void CheckWaterTemp(GrowRun grow, List<Measurement> sorted, List<GrowDeviation> result)
    {
        sorted = sorted.Where(measurement => measurement.ReservoirWaterTempC.HasValue).ToList();
        if (sorted.Count == 0 || sorted[0].ReservoirWaterTempC is not { } actual)
        {
            return;
        }

        var critical = actual > 24 || actual < 14;
        var warning = actual > 22 || actual < 17;
        if (!critical && !warning)
        {
            return;
        }

        var participants = Consecutive(sorted, measurement => measurement.ReservoirWaterTempC, value => value > 22 || value < 17);
        result.Add(CreateDeviation(
            grow,
            "hydro.water-temp",
            DeviationMetric.WaterTemp,
            actual,
            17,
            22,
            "C",
            critical ? DeviationSeverity.Critical : DeviationSeverity.Warning,
            $"Reservoir-Wassertemperatur {actual:0.0} C liegt ausserhalb des Arbeitsbereichs.",
            "Wassertemperatur und Kuehlung pruefen.",
            actual > 22 ? "water-temp-rising-rapid" : null,
            participants));
    }

    private static void CheckDissolvedOxygen(GrowRun grow, List<Measurement> sorted, List<GrowDeviation> result)
    {
        sorted = sorted.Where(measurement => measurement.DissolvedOxygenMgL.HasValue).ToList();
        if (sorted.Count == 0 || sorted[0].DissolvedOxygenMgL is not { } actual || actual >= 6)
        {
            return;
        }

        var participants = Consecutive(sorted, measurement => measurement.DissolvedOxygenMgL, value => value < 6);
        result.Add(CreateDeviation(
            grow,
            "hydro.do",
            DeviationMetric.DissolvedOxygen,
            actual,
            6,
            null,
            "mg/L",
            actual < 4 ? DeviationSeverity.Critical : DeviationSeverity.Warning,
            $"Geloester Sauerstoff liegt bei {actual:0.0} mg/L.",
            "Belueftung, Umwaelzung und Wassertemperatur pruefen.",
            "do-critical",
            participants));
    }

    private static void CheckOrp(GrowRun grow, List<Measurement> sorted, List<GrowDeviation> result)
    {
        sorted = sorted.Where(measurement => measurement.OrpMv.HasValue).ToList();
        if (sorted.Count == 0 || sorted[0].OrpMv is not { } actual)
        {
            return;
        }

        var critical = actual < 250 || actual > 650;
        var warning = actual < 300 || actual > 500;
        if (!critical && !warning)
        {
            return;
        }

        var participants = Consecutive(sorted, measurement => measurement.OrpMv, value => value < 300 || value > 500);
        result.Add(CreateDeviation(
            grow,
            "hydro.orp",
            DeviationMetric.Orp,
            actual,
            300,
            500,
            "mV",
            critical ? DeviationSeverity.Critical : DeviationSeverity.Warning,
            $"ORP {actual:0} mV liegt ausserhalb des Arbeitsbereichs.",
            "Wasserhygiene und Sensor plausibilisieren.",
            actual < 300 ? "orp-low-mild" : null,
            participants));
    }

    private static void CheckPpfd(GrowRun grow, List<Measurement> sorted, HydroTargetValues? targets, List<GrowDeviation> result)
    {
        sorted = sorted.Where(measurement => measurement.PpfdMol.HasValue).ToList();
        if (sorted.Count == 0 || sorted[0].PpfdMol is not { } actual)
        {
            return;
        }

        var critical = actual > 1500;
        var warning = targets is not null && actual > targets.PpfdMax * 1.2;
        if (!critical && !warning)
        {
            return;
        }

        var participants = Consecutive(sorted, measurement => measurement.PpfdMol, value => value > 1500 || (targets is not null && value > targets.PpfdMax * 1.2));
        result.Add(CreateDeviation(
            grow,
            "hydro.ppfd",
            DeviationMetric.Ppfd,
            actual,
            targets?.PpfdMin,
            targets?.PpfdMax,
            "umol/m2/s",
            critical ? DeviationSeverity.Critical : DeviationSeverity.Warning,
            $"PPFD {actual:0} liegt deutlich ueber dem Zielbereich.",
            "Lichtintensitaet oder Abstand pruefen.",
            "led-bleaching-mild",
            participants));
    }

    private static void CheckCo2(GrowRun grow, List<Measurement> sorted, List<GrowDeviation> result)
    {
        sorted = sorted.Where(measurement => measurement.Co2Ppm.HasValue).ToList();
        if (sorted.Count == 0 || sorted[0].Co2Ppm is not { } actual || actual <= 1600)
        {
            return;
        }

        var participants = Consecutive(sorted, measurement => measurement.Co2Ppm, value => value > 1600);
        result.Add(CreateDeviation(
            grow,
            "hydro.co2",
            DeviationMetric.Co2,
            actual,
            null,
            1600,
            "ppm",
            actual > 2500 ? DeviationSeverity.Critical : DeviationSeverity.Warning,
            $"CO2 {actual:0} ppm liegt ueber dem Arbeitsbereich.",
            "CO2-Zufuhr und Lueftung pruefen.",
            null,
            participants));
    }

    public IReadOnlyList<GrowDeviation> CheckGerminationAndRooting(GrowRun grow, GrowWeekInfo weekInfo)
    {
        var deviations = new List<GrowDeviation>();

        if (weekInfo.State == GrowCounterState.WaitingForGermination && weekInfo.DaysGerminating.HasValue)
        {
            var days = weekInfo.DaysGerminating.Value;
            if (days >= 14)
            {
                deviations.Add(LifecycleDeviation(grow, DeviationSeverity.Critical, "Keimung nach 14 Tagen nicht bestaetigt.", days));
            }
            else if (days >= 7)
            {
                deviations.Add(LifecycleDeviation(grow, DeviationSeverity.Warning, "Samen keimt seit 7 Tagen noch nicht.", days));
            }
        }

        if (weekInfo.State == GrowCounterState.WaitingForRooting && weekInfo.DaysRooting.HasValue)
        {
            var days = weekInfo.DaysRooting.Value;
            if (days >= 14)
            {
                deviations.Add(LifecycleDeviation(grow, DeviationSeverity.Critical, "Bewurzelung nach 14 Tagen nicht bestaetigt.", days));
            }
            else if (days >= 7)
            {
                deviations.Add(LifecycleDeviation(grow, DeviationSeverity.Warning, "Steckling bewurzelt noch nicht nach 7 Tagen.", days));
            }
        }

        return deviations;
    }

    private static GrowDeviation LifecycleDeviation(GrowRun grow, DeviationSeverity severity, string message, int days)
        => new()
        {
            GrowId = grow.Id,
            GrowName = grow.Name,
            StableKey = "lifecycle.germination-rooting",
            Metric = DeviationMetric.GerminationStatus,
            Severity = severity,
            Message = message,
            Recommendation = message,
            RecommendationHint = message,
            ConsecutiveCount = days,
            Source = DeviationSource.Unknown
        };

    private static IReadOnlyList<Measurement> Consecutive(
        List<Measurement> sorted,
        Func<Measurement, double?> getValue,
        Func<double, bool> matches)
    {
        var result = new List<Measurement>();
        foreach (var measurement in sorted)
        {
            var value = getValue(measurement);
            if (!value.HasValue || !matches(value.Value))
            {
                break;
            }

            result.Add(measurement);
        }

        return result;
    }

    private static IReadOnlyList<Measurement> GetEcTrendParticipants(List<Measurement> sorted)
    {
        if (sorted.Count < 2 || !sorted[1].ReservoirEc.HasValue || !sorted[0].ReservoirEc.HasValue)
        {
            return Array.Empty<Measurement>();
        }

        var diff = sorted[0].ReservoirEc.GetValueOrDefault() - sorted[1].ReservoirEc.GetValueOrDefault();
        return Math.Abs(diff) > 0.2
            ? new[] { sorted[0], sorted[1] }
            : Array.Empty<Measurement>();
    }

    private static GrowDeviation CreateDeviation(
        GrowRun grow,
        string stableKey,
        DeviationMetric metric,
        double? actual,
        double? targetMin,
        double? targetMax,
        string? unit,
        DeviationSeverity severity,
        string message,
        string? recommendationHint,
        string? symptomId,
        IReadOnlyList<Measurement> sourceMeasurements)
    {
        var sourceIds = sourceMeasurements
            .Where(measurement => measurement.Id > 0)
            .Select(measurement => measurement.Id)
            .ToList();
        var firstDetected = sourceMeasurements.Count > 0
            ? sourceMeasurements.Min(measurement => measurement.TakenAt).ToUniversalTime()
            : (DateTime?)null;
        var lastDetected = sourceMeasurements.Count > 0
            ? sourceMeasurements.Max(measurement => measurement.TakenAt).ToUniversalTime()
            : (DateTime?)null;

        return new GrowDeviation
        {
            GrowId = grow.Id,
            GrowName = grow.Name,
            StableKey = stableKey,
            Metric = metric,
            ActualValue = actual,
            TargetMin = targetMin,
            TargetMax = targetMax,
            Unit = unit,
            Severity = severity,
            Message = message,
            RecommendationHint = recommendationHint,
            SymptomId = symptomId,
            SourceMeasurementIds = sourceIds,
            Recommendation = message,
            ConsecutiveCount = Math.Max(1, sourceMeasurements.Count),
            FirstDetectedAtUtc = firstDetected,
            LastDetectedAtUtc = lastDetected,
            Source = ResolveSource(sourceMeasurements)
        };
    }

    private static DeviationSource ResolveSource(IReadOnlyList<Measurement> measurements)
    {
        if (measurements.Count == 0)
        {
            return DeviationSource.Unknown;
        }

        if (measurements.All(measurement => measurement.Source == ValueOrigin.HomeAssistant))
        {
            return DeviationSource.HomeAssistant;
        }

        if (measurements.All(measurement => measurement.Source == ValueOrigin.Manual))
        {
            return DeviationSource.Manual;
        }

        if (measurements.All(measurement => measurement.Source is ValueOrigin.Manual or ValueOrigin.HomeAssistant))
        {
            return DeviationSource.Mixed;
        }

        return DeviationSource.Unknown;
    }
}
