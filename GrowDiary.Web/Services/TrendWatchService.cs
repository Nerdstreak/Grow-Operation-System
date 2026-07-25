using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

public enum TrendSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>Something worth knowing that no single reading would have revealed.</summary>
/// <param name="Code">Stable key, so a finding can be pushed once instead of every round.</param>
/// <param name="GuidanceId">The growplan rule behind it, when there is one.</param>
public sealed record TrendFinding(
    string Code,
    TrendSeverity Severity,
    string Headline,
    string Detail,
    string? GuidanceId = null);

/// <summary>
/// The holiday guard.
///
/// A threshold alert answers "is this value wrong right now". That misses the failures that
/// actually ruin a run while nobody is looking: pH walking from 5.9 to 6.4 over five days,
/// consumption quietly collapsing because the roots are rotting, a water change that never
/// happened. Every reading along the way looks acceptable — only the shape does not.
///
/// Deliberately deterministic. This is the layer people rely on when they are away, so it
/// must not depend on a model, a provider, or a network beyond the house.
/// </summary>
public sealed class TrendWatchService
{
    /// <summary>Days of history to look at. Long enough to see a trend, short enough to stay current.</summary>
    public const int WindowDays = 7;

    /// <summary>Fewer points than this and "a trend" is just noise.</summary>
    public const int MinimumPoints = 4;

    /// <summary>The growplan calls a weekly change mandatory; this is the grace on top.</summary>
    public const int WaterChangeDueDays = 7;
    public const int WaterChangeOverdueDays = 10;

    /// <summary>SOP-N1: HOCl top-up every 2–3 days; 4 is already past the window.</summary>
    public const int OrpTopUpDueDays = 3;
    public const int OrpTopUpOverdueDays = 5;

    private static readonly System.Globalization.CultureInfo De = AppCulture.German;

    /// <summary>
    /// Pure evaluation over one grow's recent measurements, newest first or oldest first —
    /// it sorts for itself. No database, no clock beyond what is passed in.
    /// </summary>
    public static IReadOnlyList<TrendFinding> Evaluate(
        IReadOnlyList<Measurement> measurements,
        HydroTargetValues? targets,
        DateTime now)
    {
        var window = measurements
            .Where(measurement => measurement.TakenAt >= now.AddDays(-WindowDays))
            .OrderBy(measurement => measurement.TakenAt)
            .ToList();

        var findings = new List<TrendFinding>();

        // pH is judged by the growplan's comfort band, not by the narrower mixing target:
        // drifting inside 5.8–6.2 is explicitly allowed, so warning about it would nag
        // about the very thing the plan says to leave alone.
        AddDrift(findings, window, "ph", "pH", measurement => measurement.ReservoirPh, 0.25, "0.0#",
            DeviationAnalyzerService.PhComfortMin, DeviationAnalyzerService.PhComfortMax, "ph-drift-band");
        AddDrift(findings, window, "ec", "EC", measurement => measurement.ReservoirEc, 0.30, "0.0#",
            targets?.EcMin, targets?.EcMax, "ec-keep-hungry");
        AddDrift(findings, window, "orp", "ORP", measurement => measurement.OrpMv, 60, "0",
            targets?.OrpMin, targets?.OrpMax, "orp-rises-with-stage");
        AddDrift(findings, window, "watertemp", "Wassertemperatur", measurement => measurement.ReservoirWaterTempC, 2.0, "0.0",
            targets?.WaterTempNightC, targets?.WaterTempDayC, null);

        AddWaterChange(findings, measurements, now);
        AddConsumption(findings, window);
        AddOrpTopUp(findings, measurements, now);

        return findings;
    }

    /// <summary>
    /// A value moving the same way day after day. The point is that it can still be inside
    /// its band the whole time — by the time a threshold fires, five days were lost.
    /// </summary>
    private static void AddDrift(
        List<TrendFinding> findings,
        List<Measurement> window,
        string code,
        string label,
        Func<Measurement, double?> read,
        double minimumChange,
        string format,
        double? targetMin,
        double? targetMax,
        string? guidanceId)
    {
        // One value per day, so a day with six measurements doesn't outvote the rest.
        var daily = window
            .Where(measurement => read(measurement) is not null)
            .GroupBy(measurement => measurement.TakenAt.Date)
            .OrderBy(group => group.Key)
            .Select(group => (Day: group.Key, Value: read(group.OrderByDescending(m => m.TakenAt).First())!.Value))
            .ToList();

        if (daily.Count < MinimumPoints)
        {
            return;
        }

        var first = daily[0].Value;
        var last = daily[^1].Value;
        var change = last - first;
        if (Math.Abs(change) < minimumChange)
        {
            return;
        }

        // Every step has to agree with the overall direction; one wobble and it is not a drift.
        var rising = change > 0;
        for (var i = 1; i < daily.Count; i++)
        {
            var step = daily[i].Value - daily[i - 1].Value;
            if (rising ? step < 0 : step > 0)
            {
                return;
            }
        }

        var direction = rising ? "steigt" : "fällt";
        var days = (daily[^1].Day - daily[0].Day).Days + 1;
        var detail =
            $"{label} {direction} seit {days} Tagen durchgehend: " +
            $"{first.ToString(format, De)} → {last.ToString(format, De)}.";

        // Still inside the band is the interesting case — nothing else would have said a word.
        var outsideBand = (targetMin is { } min && last < min) || (targetMax is { } max && last > max);
        if (!outsideBand)
        {
            detail += " Der Wert ist noch im erlaubten Bereich — auffällig ist die Richtung, nicht die Zahl.";
        }

        findings.Add(new TrendFinding(
            $"trend.{code}.drift",
            outsideBand ? TrendSeverity.Warning : TrendSeverity.Info,
            $"{label} driftet seit {days} Tagen",
            detail,
            guidanceId));
    }

    private static void AddWaterChange(List<TrendFinding> findings, IReadOnlyList<Measurement> measurements, DateTime now)
    {
        var lastChange = measurements
            .Where(measurement => measurement.SolutionChange)
            .OrderByDescending(measurement => measurement.TakenAt)
            .FirstOrDefault();

        // Without a recorded change there is nothing to count from; saying "overdue" to
        // someone who simply never logged one would be noise, not a warning.
        if (lastChange is null)
        {
            return;
        }

        var days = (int)(now.Date - lastChange.TakenAt.Date).TotalDays;
        if (days < WaterChangeDueDays)
        {
            return;
        }

        findings.Add(new TrendFinding(
            "trend.waterchange.overdue",
            days >= WaterChangeOverdueDays ? TrendSeverity.Warning : TrendSeverity.Info,
            $"Wasserwechsel seit {days} Tagen offen",
            $"Der letzte dokumentierte Wechsel war am {lastChange.TakenAt.ToString("dd.MM.", De)}. "
            + "Der Growplan sieht wöchentlich vor.",
            "weekly-water-change"));
    }

    /// <summary>
    /// SOP-N1: the ORP has to be brought back up with HOCl every two to three days. It is
    /// a consumable, not a setting — it decays as it does its job, and the moment it is
    /// forgotten is the moment the reservoir turns anaerobic without a single value moving
    /// out of range that day.
    /// </summary>
    private static void AddOrpTopUp(List<TrendFinding> findings, IReadOnlyList<Measurement> measurements, DateTime now)
    {
        var lastOrp = measurements
            .Where(measurement => measurement.OrpMv is not null)
            .OrderByDescending(measurement => measurement.TakenAt)
            .FirstOrDefault();

        // Never measured means the user isn't tracking ORP at all — nagging about a value
        // they don't collect would be noise, not a reminder.
        if (lastOrp is null)
        {
            return;
        }

        var days = (int)(now.Date - lastOrp.TakenAt.Date).TotalDays;
        if (days < OrpTopUpDueDays)
        {
            return;
        }

        findings.Add(new TrendFinding(
            "trend.orp.topup-due",
            days >= OrpTopUpOverdueDays ? TrendSeverity.Warning : TrendSeverity.Info,
            $"ORP seit {days} Tagen nicht geprüft",
            $"Der letzte ORP-Wert stammt vom {lastOrp.TakenAt.ToString("dd.MM.", De)} "
            + $"({lastOrp.OrpMv:0} mV). Laut SOP wird alle 2–3 Tage per HOCl nachjustiert — "
            + "der Wert baut sich im Betrieb laufend ab.",
            "orp-optimal-band"));
    }

    /// <summary>
    /// Consumption is the plant's own report. A collapse means it stopped drinking — roots
    /// or a blockage; a jump usually means the water went somewhere it shouldn't.
    /// </summary>
    private static void AddConsumption(List<TrendFinding> findings, List<Measurement> window)
    {
        var daily = window
            .Where(measurement => measurement.TopOffLiters is > 0)
            .GroupBy(measurement => measurement.TakenAt.Date)
            .OrderBy(group => group.Key)
            .Select(group => group.Sum(measurement => measurement.TopOffLiters!.Value))
            .ToList();

        if (daily.Count < MinimumPoints)
        {
            return;
        }

        var recent = daily.TakeLast(2).Average();
        var earlier = daily.Take(daily.Count - 2).Average();
        if (earlier <= 0)
        {
            return;
        }

        var ratio = recent / earlier;
        if (ratio <= 0.5)
        {
            findings.Add(new TrendFinding(
                "trend.consumption.drop",
                TrendSeverity.Warning,
                "Verbrauch eingebrochen",
                $"Zuletzt {recent.ToString("0.#", De)} L/Tag gegenüber {earlier.ToString("0.#", De)} L/Tag davor. "
                + "Wenn die Pflanze aufhört zu trinken, sind meist die Wurzeln oder eine Verstopfung die Ursache.",
                "daily-consumption-plausibility"));
        }
        else if (ratio >= 2.0)
        {
            findings.Add(new TrendFinding(
                "trend.consumption.spike",
                TrendSeverity.Warning,
                "Verbrauch stark gestiegen",
                $"Zuletzt {recent.ToString("0.#", De)} L/Tag gegenüber {earlier.ToString("0.#", De)} L/Tag davor. "
                + "Prüfe zuerst auf ein Leck, bevor du es als Wachstum verbuchst.",
                "daily-consumption-plausibility"));
        }
    }
}
