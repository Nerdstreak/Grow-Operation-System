using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

public sealed class DeviationAnalyzerService
{
    /// <summary>
    /// Analysiert die letzten Messungen eines Hydro-Grows und gibt konkrete Handlungsempfehlungen zurück.
    /// Läuft nur für ActiveHydro (RDWC / DWC). Gibt leere Liste zurück für andere Anbauformen.
    /// </summary>
    public IReadOnlyList<GrowDeviation> Analyze(GrowRun grow, IReadOnlyList<Measurement> recentMeasurements)
    {
        if (grow.IrrigationType != IrrigationType.ActiveHydro || !grow.Profile.IsHydro)
        {
            return Array.Empty<GrowDeviation>();
        }

        if (recentMeasurements.Count == 0)
        {
            return Array.Empty<GrowDeviation>();
        }

        var sorted = recentMeasurements.OrderByDescending(m => m.TakenAt).ToList();
        var stage = sorted[0].Stage;
        var targets = TargetValueService.GetTargets(grow.HydroStyle, stage);

        if (targets is null)
        {
            return Array.Empty<GrowDeviation>();
        }

        var deviations = new List<GrowDeviation>();

        CheckPh(grow, sorted, targets, deviations);
        CheckEc(grow, sorted, targets, deviations);
        CheckOrp(grow, sorted, targets, deviations);
        CheckWaterTemp(grow, sorted, targets, deviations);
        CheckDissolvedOxygen(grow, sorted, deviations);
        CheckPpfd(grow, sorted, targets, deviations);
        CheckCo2(grow, sorted, targets, deviations);

        return deviations;
    }

    // ── pH ────────────────────────────────────────────────────────────────────

    private static void CheckPh(GrowRun grow, List<Measurement> sorted, HydroTargetValues targets, List<GrowDeviation> result)
    {
        var actual = sorted[0].ReservoirPh;
        if (!actual.HasValue) return;

        if (actual.Value > targets.PhMax)
        {
            var count = CountConsecutive(sorted, m => m.ReservoirPh, v => v > targets.PhMax);
            var severity = actual.Value > targets.PhMax + 0.3 || count >= 3
                ? DeviationSeverity.Critical
                : DeviationSeverity.Warning;
            var text = count >= 2
                ? $"pH seit {count} Messungen über {actual.Value:F1} – Calciumaufnahme blockiert, jetzt pH-Down einsetzen"
                : $"pH zu hoch ({actual.Value:F1}, Soll {targets.PhMin:F1}–{targets.PhMax:F1}) – pH-Down prüfen";

            result.Add(Deviation(grow, DeviationMetric.Ph, actual.Value, targets.PhMin, targets.PhMax, severity, text, count));
        }
        else if (actual.Value < targets.PhMin)
        {
            var count = CountConsecutive(sorted, m => m.ReservoirPh, v => v < targets.PhMin);
            var severity = actual.Value < targets.PhMin - 0.3 || count >= 3
                ? DeviationSeverity.Critical
                : DeviationSeverity.Warning;
            var text = count >= 2
                ? $"pH seit {count} Messungen unter {actual.Value:F1} – Magnesiumaufnahme gestört, jetzt pH-Up einsetzen"
                : $"pH zu niedrig ({actual.Value:F1}, Soll {targets.PhMin:F1}–{targets.PhMax:F1}) – pH-Up prüfen";

            result.Add(Deviation(grow, DeviationMetric.Ph, actual.Value, targets.PhMin, targets.PhMax, severity, text, count));
        }
    }

    // ── EC ────────────────────────────────────────────────────────────────────

    private static void CheckEc(GrowRun grow, List<Measurement> sorted, HydroTargetValues targets, List<GrowDeviation> result)
    {
        var actual = sorted[0].ReservoirEc;
        if (!actual.HasValue) return;

        // Absolute check: EC unter Sollbereich
        if (actual.Value < targets.EcMin)
        {
            var count = CountConsecutive(sorted, m => m.ReservoirEc, v => v < targets.EcMin);
            var severity = count >= 3 ? DeviationSeverity.Critical : DeviationSeverity.Warning;
            result.Add(Deviation(grow, DeviationMetric.Ec, actual.Value, targets.EcMin, targets.EcMax, severity,
                $"EC zu niedrig ({actual.Value:F2} mS/cm, Soll {targets.EcMin:F2}–{targets.EcMax:F2}) – Nährstofflösung auffrischen oder Addback erhöhen",
                count));
            return;
        }

        // Trendcheck: EC gefallen oder gestiegen (Mindestdifferenz 0.2 um Messrauschen zu ignorieren)
        if (sorted.Count >= 2 && sorted[1].ReservoirEc.HasValue)
        {
            var diff = actual.Value - sorted[1].ReservoirEc!.Value;

            if (diff < -0.2)
            {
                result.Add(Deviation(grow, DeviationMetric.Ec, actual.Value, targets.EcMin, targets.EcMax,
                    DeviationSeverity.Warning,
                    "EC gefallen – Pflanzen nehmen mehr Nährstoffe als Wasser auf, konzentrierten Addback mischen",
                    consecutiveCount: 1));
            }
            else if (diff > 0.2)
            {
                result.Add(Deviation(grow, DeviationMetric.Ec, actual.Value, targets.EcMin, targets.EcMax,
                    DeviationSeverity.Warning,
                    "EC gestiegen – Verdunstung überwiegt, reines RO-Wasser nachfüllen",
                    consecutiveCount: 1));
            }
        }
    }

    // ── ORP ───────────────────────────────────────────────────────────────────

    private static void CheckOrp(GrowRun grow, List<Measurement> sorted, HydroTargetValues targets, List<GrowDeviation> result)
    {
        var actual = sorted[0].OrpMv;
        if (!actual.HasValue) return;

        if (actual.Value < targets.OrpMin)
        {
            var count = CountConsecutive(sorted, m => m.OrpMv, v => v < targets.OrpMin);
            var severity = actual.Value < 200 ? DeviationSeverity.Critical : DeviationSeverity.Warning;
            result.Add(Deviation(grow, DeviationMetric.Orp, actual.Value, targets.OrpMin, targets.OrpMax, severity,
                "ORP niedrig – System möglicherweise kontaminiert, H₂O₂ Behandlung prüfen",
                count));
        }
    }

    // ── Wassertemperatur ──────────────────────────────────────────────────────

    private static void CheckWaterTemp(GrowRun grow, List<Measurement> sorted, HydroTargetValues targets, List<GrowDeviation> result)
    {
        var actual = sorted[0].ReservoirWaterTempC;
        if (!actual.HasValue) return;

        if (actual.Value > targets.WaterTempDayC + 2)
        {
            var count = CountConsecutive(sorted, m => m.ReservoirWaterTempC, v => v > targets.WaterTempDayC + 2);
            var severity = actual.Value > 24 ? DeviationSeverity.Critical : DeviationSeverity.Warning;
            result.Add(Deviation(grow, DeviationMetric.WaterTemp, actual.Value, targets.WaterTempNightC, targets.WaterTempDayC, severity,
                "Wassertemperatur kritisch – Wurzelfäule-Risiko steigt, sofort kühlen",
                count));
        }
    }

    // ── Dissolved Oxygen ──────────────────────────────────────────────────────

    private static void CheckDissolvedOxygen(GrowRun grow, List<Measurement> sorted, List<GrowDeviation> result)
    {
        var actual = sorted[0].DissolvedOxygenMgL;
        if (!actual.HasValue) return;

        const double doWarning = 7.0;
        const double doCritical = 5.0;

        if (actual.Value < doWarning)
        {
            var count = CountConsecutive(sorted, m => m.DissolvedOxygenMgL, v => v < doWarning);
            var severity = actual.Value < doCritical ? DeviationSeverity.Critical : DeviationSeverity.Warning;
            result.Add(Deviation(grow, DeviationMetric.DissolvedOxygen, actual.Value, doCritical, doWarning, severity,
                "Sauerstoff niedrig – Luftstein prüfen, Wassertemp senken",
                count));
        }
    }

    // ── PPFD ──────────────────────────────────────────────────────────────────

    private static void CheckPpfd(GrowRun grow, List<Measurement> sorted, HydroTargetValues targets, List<GrowDeviation> result)
    {
        var actual = sorted[0].PpfdMol;
        if (!actual.HasValue) return;

        if (actual.Value < targets.PpfdMin)
        {
            var count = CountConsecutive(sorted, m => m.PpfdMol, v => v < targets.PpfdMin);
            result.Add(Deviation(grow, DeviationMetric.Ppfd, actual.Value, targets.PpfdMin, targets.PpfdMax,
                DeviationSeverity.Warning,
                $"PPFD zu niedrig ({actual.Value:F0} µmol/m²/s, Soll {targets.PpfdMin:F0}–{targets.PpfdMax:F0}) – Lichtintensität oder Abstand prüfen",
                count));
        }
        else if (actual.Value > targets.PpfdMax * 1.2)
        {
            result.Add(Deviation(grow, DeviationMetric.Ppfd, actual.Value, targets.PpfdMin, targets.PpfdMax,
                DeviationSeverity.Warning,
                $"PPFD sehr hoch ({actual.Value:F0} µmol/m²/s) – Lichtintensität reduzieren oder VPD kontrollieren",
                consecutiveCount: 1));
        }
    }

    // ── CO₂ ───────────────────────────────────────────────────────────────────

    private static void CheckCo2(GrowRun grow, List<Measurement> sorted, HydroTargetValues targets, List<GrowDeviation> result)
    {
        var actual = sorted[0].Co2Ppm;
        if (!actual.HasValue) return;

        if (actual.Value < targets.Co2Min)
        {
            var count = CountConsecutive(sorted, m => m.Co2Ppm, v => v < targets.Co2Min);
            var severity = count >= 3 ? DeviationSeverity.Critical : DeviationSeverity.Warning;
            result.Add(Deviation(grow, DeviationMetric.Co2, actual.Value, targets.Co2Min, targets.Co2Max, severity,
                $"CO₂ zu niedrig ({actual.Value:F0} ppm, Soll {targets.Co2Min:F0}–{targets.Co2Max:F0}) – CO₂-Zufuhr prüfen oder Belüftung reduzieren",
                count));
        }
    }

    // ── Keimung / Bewurzelung ─────────────────────────────────────────────────

    public IReadOnlyList<GrowDeviation> CheckGerminationAndRooting(GrowRun grow, GrowWeekInfo weekInfo)
    {
        var deviations = new List<GrowDeviation>();

        if (weekInfo.State == GrowCounterState.WaitingForGermination && weekInfo.DaysGerminating.HasValue)
        {
            var days = weekInfo.DaysGerminating.Value;
            if (days >= 14)
            {
                deviations.Add(new GrowDeviation
                {
                    GrowId = grow.Id,
                    GrowName = grow.Name,
                    Metric = DeviationMetric.GerminationStatus,
                    Severity = DeviationSeverity.Critical,
                    Recommendation = "Keimung nach 14 Tagen nicht bestätigt – Samen wahrscheinlich nicht lebensfähig. Neuen Samen erwägen.",
                    ConsecutiveCount = days
                });
            }
            else if (days >= 7)
            {
                deviations.Add(new GrowDeviation
                {
                    GrowId = grow.Id,
                    GrowName = grow.Name,
                    Metric = DeviationMetric.GerminationStatus,
                    Severity = DeviationSeverity.Warning,
                    Recommendation = "Samen keimt seit 7 Tagen – prüfe Feuchtigkeit (70-90%), Temperatur (22-28°C) und Lichtabschirmung.",
                    ConsecutiveCount = days
                });
            }
        }

        if (weekInfo.State == GrowCounterState.WaitingForRooting && weekInfo.DaysRooting.HasValue)
        {
            var days = weekInfo.DaysRooting.Value;
            if (days >= 14)
            {
                deviations.Add(new GrowDeviation
                {
                    GrowId = grow.Id,
                    GrowName = grow.Name,
                    Metric = DeviationMetric.GerminationStatus,
                    Severity = DeviationSeverity.Critical,
                    Recommendation = "Bewurzelung nach 14 Tagen nicht bestätigt – Temperatur (22-26°C), Luftfeuchte (75-85%) und Stecklingsgesundheit prüfen.",
                    ConsecutiveCount = days
                });
            }
            else if (days >= 7)
            {
                deviations.Add(new GrowDeviation
                {
                    GrowId = grow.Id,
                    GrowName = grow.Name,
                    Metric = DeviationMetric.GerminationStatus,
                    Severity = DeviationSeverity.Warning,
                    Recommendation = "Steckling bewurzelt noch nicht nach 7 Tagen – prüfe Temperatur und Luftfeuchte unter der Dome.",
                    ConsecutiveCount = days
                });
            }
        }

        return deviations;
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private static int CountConsecutive(List<Measurement> sorted, Func<Measurement, double?> getValue, Func<double, bool> matches)
    {
        var count = 0;
        foreach (var m in sorted)
        {
            var v = getValue(m);
            if (v.HasValue && matches(v.Value)) count++;
            else break;
        }
        return count;
    }

    private static GrowDeviation Deviation(
        GrowRun grow,
        DeviationMetric metric,
        double? actual,
        double targetMin,
        double targetMax,
        DeviationSeverity severity,
        string recommendation,
        int consecutiveCount)
    {
        return new GrowDeviation
        {
            GrowId = grow.Id,
            GrowName = grow.Name,
            Metric = metric,
            ActualValue = actual,
            TargetMin = targetMin,
            TargetMax = targetMax,
            Severity = severity,
            Recommendation = recommendation,
            ConsecutiveCount = consecutiveCount
        };
    }
}
