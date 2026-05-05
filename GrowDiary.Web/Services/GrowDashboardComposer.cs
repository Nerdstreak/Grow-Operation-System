using GrowDiary.Web.Models;
using Microsoft.Extensions.Logging;

namespace GrowDiary.Web.Services;

public sealed class GrowDashboardComposer
{
    private readonly ChartService _chartService;
    private readonly DeviationAnalyzerService _deviationAnalyzer;
    private readonly WeekCounterService _weekCounter;
    private readonly ILogger<GrowDashboardComposer> _logger;

    public GrowDashboardComposer(ChartService chartService, DeviationAnalyzerService deviationAnalyzer, WeekCounterService weekCounter, ILogger<GrowDashboardComposer> logger)
    {
        _chartService = chartService;
        _deviationAnalyzer = deviationAnalyzer;
        _weekCounter = weekCounter;
        _logger = logger;
    }

    public List<MetricCard> BuildTentMetrics(Tent tent, Dictionary<string, HomeAssistantState> states, IReadOnlyList<Measurement> measurements)
    {
        var latest = measurements.OrderByDescending(x => x.TakenAt).FirstOrDefault();

        MetricCard Build(string label, string key, Func<Measurement?, double?> fallback, string tone = "default", string? explicitUnit = null)
        {
            if (states.TryGetValue(key, out var state))
            {
                return new MetricCard
                {
                    Key = key,
                    Label = label,
                    Value = state.NumericValue.HasValue
                        ? FormatMetricValue(key, state.NumericValue.Value)
                        : state.State,
                    Unit = explicitUnit ?? state.UnitOfMeasurement,
                    Tone = tone,
                    Hint = state.FriendlyName
                };
            }

            var value = fallback(latest);
            return new MetricCard
            {
                Key = key,
                Label = label,
                Value = value.HasValue ? FormatMetricValue(key, value.Value) : "–",
                Unit = explicitUnit,
                Tone = tone,
                Hint = states.Count == 0 ? "Noch nicht mit Home Assistant verbunden" : "Kein Entity gemappt"
            };
        }

        var cards = new List<MetricCard>
        {
            Build("Temperatur", "temperature", m => m?.AirTemperatureC, explicitUnit: "°C"),
            Build("Luftfeuchte", "humidity", m => m?.HumidityPercent, explicitUnit: "%"),
            Build("VPD", "vpd", _ => null, tone: "accent", explicitUnit: "kPa"),
            BuildLightCycleMetric(tent),
            BuildPpfdMetric(tent, states)
        };

        if (tent.Co2Available || measurements.Any(m => m.Co2Ppm.HasValue))
            cards.Add(Build("CO2", "co2", m => m?.Co2Ppm, explicitUnit: "ppm"));

        var hasActiveHydro = tent.ActiveGrows.Any(g => g.IrrigationType == IrrigationType.ActiveHydro);

        if (hasActiveHydro || measurements.Any(m => m.ReservoirPh.HasValue))
            cards.Add(Build("pH", "reservoir-ph", m => m?.ReservoirPh));

        if (hasActiveHydro || measurements.Any(m => m.ReservoirEc.HasValue))
            cards.Add(Build("EC", "reservoir-ec", m => m?.ReservoirEc, explicitUnit: "mS/cm"));

        if (hasActiveHydro || measurements.Any(m => m.OrpMv.HasValue))
            cards.Add(Build("ORP", "orp", m => m?.OrpMv, explicitUnit: "mV"));

        if (hasActiveHydro || measurements.Any(m => m.DissolvedOxygenMgL.HasValue))
            cards.Add(Build("DO", "dissolved-oxygen", m => m?.DissolvedOxygenMgL, explicitUnit: "mg/L"));

        if (measurements.Any(m => m.ReservoirLevelLiters.HasValue || m.ReservoirLevelCm.HasValue))
        {
            states.TryGetValue("reservoir-level", out var levelState);
            cards.Add(new MetricCard
            {
                Key = "reservoir-level",
                Label = "Wasserstand",
                Value = levelState is not null
                    ? levelState.NumericValue?.ToString("0.0") ?? levelState.State
                    : latest?.ReservoirLevelLiters?.ToString("0.0") ?? latest?.ReservoirLevelCm?.ToString("0.0") ?? "–",
                Unit = levelState?.UnitOfMeasurement ?? (latest?.ReservoirLevelLiters.HasValue == true ? "L" : "cm"),
                Tone = "info"
            });
        }

        if (hasActiveHydro || measurements.Any(m => m.ReservoirWaterTempC.HasValue))
            cards.Add(Build("Wassertemp.", "reservoir-temp", m => m?.ReservoirWaterTempC, explicitUnit: "°C"));

        return cards;
    }

    public ChartSeries BuildTentClimateChart(
        IReadOnlyList<Measurement> measurements,
        IReadOnlyList<TentSensorReading> recentReadings,
        IReadOnlyList<TentSensorDailyStat> dailyStats,
        DateTime chartFrom)
    {
        var useRecent = chartFrom >= DateTime.Today.AddDays(-7);

        if (useRecent)
        {
            var tempPoints     = recentReadings.Where(x => x.MetricKey == "temperature").Select(x => (x.CapturedAtUtc.ToLocalTime(), (double?)x.Value)).ToList();
            var humidityPoints = recentReadings.Where(x => x.MetricKey == "humidity").Select(x => (x.CapturedAtUtc.ToLocalTime(), (double?)x.Value)).ToList();
            var vpdPoints      = recentReadings.Where(x => x.MetricKey == "vpd").Select(x => (x.CapturedAtUtc.ToLocalTime(), (double?)x.Value)).ToList();

            if (tempPoints.Count == 0)
                tempPoints = measurements.Where(x => x.AirTemperatureC.HasValue).Select(x => (x.TakenAt, x.AirTemperatureC)).ToList();
            if (humidityPoints.Count == 0)
                humidityPoints = measurements.Where(x => x.HumidityPercent.HasValue).Select(x => (x.TakenAt, x.HumidityPercent)).ToList();

            return _chartService.BuildSeries(
                "Klima-Verlauf",
                "Klima",
                ("Temperatur", "#8b5cf6", tempPoints),
                ("Luftfeuchte", "#22c55e", humidityPoints),
                ("VPD", "#f59e0b", vpdPoints));
        }
        else
        {
            // Tages-Perzentil-Bänder aus DailyStats
            var tempStats = dailyStats.Where(x => x.MetricKey == "temperature").ToList();
            var p5Points     = tempStats.Select(x => (x.Date.ToDateTime(TimeOnly.MinValue), (double?)x.P5)).ToList();
            var medianPoints = tempStats.Select(x => (x.Date.ToDateTime(TimeOnly.MinValue), (double?)x.Median)).ToList();
            var p95Points    = tempStats.Select(x => (x.Date.ToDateTime(TimeOnly.MinValue), (double?)x.P95)).ToList();

            if (medianPoints.Count == 0)
            {
                // Fallback auf manuelle Messungen
                medianPoints = measurements.Where(x => x.AirTemperatureC.HasValue).Select(x => (x.TakenAt, x.AirTemperatureC)).ToList();
            }

            return _chartService.BuildSeries(
                "Klima-Verlauf (Tage)",
                "Klima",
                ("Temp P5",     "#8b5cf6", p5Points),
                ("Temp Median", "#8b5cf6", medianPoints),
                ("Temp P95",    "#8b5cf6", p95Points));
        }
    }

    public ChartSeries BuildTentWaterChart(
        IReadOnlyList<Measurement> measurements,
        IReadOnlyList<TentSensorReading> recentReadings,
        IReadOnlyList<TentSensorDailyStat> dailyStats,
        DateTime chartFrom)
    {
        var useRecent = chartFrom >= DateTime.Today.AddDays(-7);

        if (useRecent)
        {
            var phPoints        = recentReadings.Where(x => x.MetricKey == "reservoir-ph").Select(x => (x.CapturedAtUtc.ToLocalTime(), (double?)x.Value)).ToList();
            var ecPoints        = recentReadings.Where(x => x.MetricKey == "reservoir-ec").Select(x => (x.CapturedAtUtc.ToLocalTime(), (double?)x.Value)).ToList();
            var levelPoints     = recentReadings.Where(x => x.MetricKey == "reservoir-level").Select(x => (x.CapturedAtUtc.ToLocalTime(), (double?)x.Value)).ToList();
            var waterTempPoints = recentReadings.Where(x => x.MetricKey == "reservoir-temp").Select(x => (x.CapturedAtUtc.ToLocalTime(), (double?)x.Value)).ToList();

            if (phPoints.Count == 0)
                phPoints = measurements.Where(x => x.ReservoirPh.HasValue).Select(x => (x.TakenAt, x.ReservoirPh)).ToList();
            if (ecPoints.Count == 0)
                ecPoints = measurements.Where(x => x.ReservoirEc.HasValue).Select(x => (x.TakenAt, x.ReservoirEc)).ToList();
            if (levelPoints.Count == 0)
                levelPoints = measurements
                    .Where(x => x.ReservoirLevelLiters.HasValue || x.ReservoirLevelCm.HasValue)
                    .Select(x => (x.TakenAt, x.ReservoirLevelLiters ?? x.ReservoirLevelCm))
                    .ToList();
            if (waterTempPoints.Count == 0)
                waterTempPoints = measurements.Where(x => x.ReservoirWaterTempC.HasValue).Select(x => (x.TakenAt, x.ReservoirWaterTempC)).ToList();

            return _chartService.BuildSeries(
                "Wasser / Reservoir",
                "Reservoir",
                ("pH", "#38bdf8", phPoints),
                ("EC", "#22c55e", ecPoints),
                ("Level", "#f97316", levelPoints),
                ("Wassertemp.", "#ef4444", waterTempPoints));
        }
        else
        {
            // Tages-Perzentil-Bänder für pH
            var phStats = dailyStats.Where(x => x.MetricKey == "reservoir-ph").ToList();
            var p5Points     = phStats.Select(x => (x.Date.ToDateTime(TimeOnly.MinValue), (double?)x.P5)).ToList();
            var medianPoints = phStats.Select(x => (x.Date.ToDateTime(TimeOnly.MinValue), (double?)x.Median)).ToList();
            var p95Points    = phStats.Select(x => (x.Date.ToDateTime(TimeOnly.MinValue), (double?)x.P95)).ToList();

            if (medianPoints.Count == 0)
            {
                medianPoints = measurements.Where(x => x.ReservoirPh.HasValue).Select(x => (x.TakenAt, x.ReservoirPh)).ToList();
            }

            return _chartService.BuildSeries(
                "Reservoir (Tage)",
                "Reservoir",
                ("pH P5",     "#38bdf8", p5Points),
                ("pH Median", "#38bdf8", medianPoints),
                ("pH P95",    "#38bdf8", p95Points));
        }
    }

    public ChartSeries BuildActivityChart(IReadOnlyList<Measurement> measurements)
    {
        var waterPoints = measurements.Where(x => x.WaterAmountMl.HasValue).Select(x => (x.TakenAt, x.WaterAmountMl)).ToList();
        var runoffPoints = measurements.Where(x => x.RunoffAmountMl.HasValue).Select(x => (x.TakenAt, x.RunoffAmountMl)).ToList();
        var heightPoints = measurements.Where(x => x.HeightCm.HasValue).Select(x => (x.TakenAt, x.HeightCm)).ToList();

        return _chartService.BuildSeries(
            "Aktivität & Entwicklung",
            "Aktivität",
            ("Wasser", "#60a5fa", waterPoints),
            ("Runoff", "#f59e0b", runoffPoints),
            ("Höhe", "#34d399", heightPoints));
    }



    public ChartSeries BuildGrowMainChart(GrowRun grow, IReadOnlyList<Measurement> measurements)
    {
        if (grow.Profile.IsHydro)
        {
            return _chartService.BuildSeries(
                grow.HydroStyle == HydroStyle.RDWC ? "RDWC Reservoir" : "Hydro Reservoir",
                "Hydro",
                ("pH", "#38bdf8", measurements.Where(x => x.ReservoirPh.HasValue).Select(x => (x.TakenAt, x.ReservoirPh))),
                ("EC", "#22c55e", measurements.Where(x => x.ReservoirEc.HasValue).Select(x => (x.TakenAt, x.ReservoirEc))),
                ("Wassertemp.", "#ef4444", measurements.Where(x => x.ReservoirWaterTempC.HasValue).Select(x => (x.TakenAt, x.ReservoirWaterTempC))));
        }

        if (grow.Profile.IsAutopot)
        {
            return _chartService.BuildSeries(
                "Autopot Reservoir",
                "Reservoir",
                ("Reservoir pH", "#38bdf8", measurements.Where(x => x.ReservoirPh.HasValue).Select(x => (x.TakenAt, x.ReservoirPh))),
                ("Reservoir EC", "#22c55e", measurements.Where(x => x.ReservoirEc.HasValue).Select(x => (x.TakenAt, x.ReservoirEc))),
                ("Wasserstand", "#f59e0b", measurements.Where(x => x.ReservoirLevelLiters.HasValue).Select(x => (x.TakenAt, x.ReservoirLevelLiters))));
        }

        return _chartService.BuildSeries(
            grow.Profile.IsCoco ? "Coco Klima & Wuchs" : "Klima & Wuchs",
            "Pflanze",
            ("Temperatur", "#8b5cf6", measurements.Where(x => x.AirTemperatureC.HasValue).Select(x => (x.TakenAt, x.AirTemperatureC))),
            ("Luftfeuchte", "#22c55e", measurements.Where(x => x.HumidityPercent.HasValue).Select(x => (x.TakenAt, x.HumidityPercent))),
            ("Höhe", "#f59e0b", measurements.Where(x => x.HeightCm.HasValue).Select(x => (x.TakenAt, x.HeightCm))));
    }

    public ChartSeries BuildGrowSecondaryChart(GrowRun grow, IReadOnlyList<Measurement> measurements)
    {
        if (grow.Profile.IsHydro)
        {
            return _chartService.BuildSeries(
                "Wasserstand & Addback",
                "Hydro",
                ("Wasserstand (L)", "#f59e0b", measurements.Where(x => x.ReservoirLevelLiters.HasValue).Select(x => (x.TakenAt, x.ReservoirLevelLiters))),
                ("Top-Off (L)", "#14b8a6", measurements.Where(x => x.TopOffLiters.HasValue).Select(x => (x.TakenAt, x.TopOffLiters))),
                ("Addback EC", "#fb7185", measurements.Where(x => x.AddbackEc.HasValue).Select(x => (x.TakenAt, x.AddbackEc))));
        }

        if (grow.Profile.IsAutopot)
        {
            return _chartService.BuildSeries(
                "Autopot Feed",
                "Reservoir",
                ("Top-Off", "#14b8a6", measurements.Where(x => x.TopOffLiters.HasValue).Select(x => (x.TakenAt, x.TopOffLiters))),
                ("Wassertemp.", "#ef4444", measurements.Where(x => x.ReservoirWaterTempC.HasValue).Select(x => (x.TakenAt, x.ReservoirWaterTempC))),
                ("Höhe", "#f59e0b", measurements.Where(x => x.HeightCm.HasValue).Select(x => (x.TakenAt, x.HeightCm))));
        }

        return _chartService.BuildSeries(
            grow.Profile.IsSoilOrganic ? "Bewässerung & pH" : "Input vs. Drain",
            "Medium",
            ("Gießmenge", "#14b8a6", measurements.Where(x => x.WaterAmountMl.HasValue).Select(x => (x.TakenAt, x.WaterAmountMl))),
            ("Input pH", "#38bdf8", measurements.Where(x => x.IrrigationPh.HasValue).Select(x => (x.TakenAt, x.IrrigationPh))),
            ("Drain pH", "#fb7185", measurements.Where(x => x.DrainPh.HasValue).Select(x => (x.TakenAt, x.DrainPh))),
            ("Drain EC", "#22c55e", measurements.Where(x => x.DrainEc.HasValue).Select(x => (x.TakenAt, x.DrainEc))));
    }

    public ChartSeries BuildGrowWateringChart(IReadOnlyList<Measurement> measurements)
    {
        return _chartService.BuildSeries(
            "Events & Aufwand",
            "Pflege",
            ("Runoff", "#a78bfa", measurements.Where(x => x.RunoffAmountMl.HasValue).Select(x => (x.TakenAt, x.RunoffAmountMl))),
            ("Wasser", "#14b8a6", measurements.Where(x => x.WaterAmountMl.HasValue).Select(x => (x.TakenAt, x.WaterAmountMl))),
            ("Höhe", "#f59e0b", measurements.Where(x => x.HeightCm.HasValue).Select(x => (x.TakenAt, x.HeightCm))));
    }

    private static MetricCard BuildLightCycleMetric(Tent tent)
    {
        var cycle = ResolveLightCycle(tent);
        return new MetricCard
        {
            Key = "light-cycle",
            Label = "Lichtzyklus",
            Value = cycle ?? "–",
            Tone = "info",
            Hint = "Kein Lichtzyklus konfiguriert"
        };
    }

    private static MetricCard BuildPpfdMetric(Tent tent, IReadOnlyDictionary<string, HomeAssistantState> states)
    {
        if (states.TryGetValue("ppfd", out var state))
        {
            return new MetricCard
            {
                Key = "ppfd",
                Label = "PPFD",
                Value = state.NumericValue.HasValue
                    ? FormatMetricValue("ppfd", state.NumericValue.Value)
                    : state.State,
                Unit = state.UnitOfMeasurement ?? "µmol/m²/s",
                Tone = "accent",
                Hint = state.FriendlyName
            };
        }

        return new MetricCard
        {
            Key = "ppfd",
            Label = "PPFD",
            Value = "–",
            Unit = null,
            Tone = "accent",
            Hint = "Kein Sensor konfiguriert"
        };
    }

    public IReadOnlyList<GrowDeviation> BuildDeviationsForGrow(GrowRun grow, IReadOnlyList<Measurement> measurements)
    {
        try
        {
            var recent = measurements
                .Where(m => m.GrowId == grow.Id)
                .OrderByDescending(m => m.TakenAt)
                .Take(3)
                .ToList();

            var weekInfo = _weekCounter.Calculate(grow);
            var hydroDeviations = _deviationAnalyzer.Analyze(grow, recent);
            var germinationDeviations = _deviationAnalyzer.CheckGerminationAndRooting(grow, weekInfo);

            return hydroDeviations.Concat(germinationDeviations).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei Deviations-Berechnung für Grow {GrowId} ({GrowName})", grow.Id, grow.Name);
            return Array.Empty<GrowDeviation>();
        }
    }

    private static string FormatMetricValue(string key, double value)
    {
        return key switch
        {
            "temperature"     => value.ToString("0.0"),
            "humidity"        => value.ToString("0"),
            "vpd"             => value.ToString("0.00"),
            "co2"             => value.ToString("0"),
            "reservoir-ph"    => value.ToString("0.00"),
            "reservoir-ec"    => value.ToString("0.00"),
            "orp"             => value.ToString("0"),
            "dissolved-oxygen" => value.ToString("0.0"),
            "reservoir-temp"  => value.ToString("0.0"),
            "reservoir-level" => value.ToString("0.0"),
            "ppfd"            => value.ToString("0"),
            "ups-battery"     => value.ToString("0"),
            _                 => value.ToString("0.#")
        };
    }

    private static string? ResolveLightCycle(Tent tent)
    {
        // TODO Sprint B1b: LightCycle aus Setup/Phase laden
        return null;
    }
}
