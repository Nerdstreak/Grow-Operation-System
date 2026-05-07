using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

public sealed class AutoMeasurementExecutionService
{
    private static readonly DateTime MissingTentRunScheduleUtc = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly GrowRepository _repository;
    private readonly SensorReadingRepository _sensorReadings;
    private readonly AutoMeasurementValueGuard _valueGuard;

    public AutoMeasurementExecutionService(
        GrowRepository repository,
        SensorReadingRepository sensorReadings,
        AutoMeasurementValueGuard valueGuard)
    {
        _repository = repository;
        _sensorReadings = sensorReadings;
        _valueGuard = valueGuard;
    }

    public int ExecuteDue(DateTime nowUtc)
    {
        nowUtc = nowUtc.ToUniversalTime();
        var processed = 0;

        foreach (var config in _repository.GetEnabledAutoMeasurementConfigs())
        {
            if (config.TriggerKind == AutoMeasurementTriggerKind.Manual)
            {
                continue;
            }

            var grow = _repository.GetGrow(config.GrowId);
            var tentId = config.TentId ?? grow?.TentId;
            if (!tentId.HasValue)
            {
                if (TryCreateMissingTentRun(config) is { } missingTentRun)
                {
                    MarkSkipped(missingTentRun, "Kein Tent fuer AutoMeasurementConfig ermittelbar.");
                    processed++;
                }
                continue;
            }

            var transitionKind = ToTransitionKind(config.TriggerKind);
            if (!transitionKind.HasValue)
            {
                continue;
            }

            var transitions = _repository.GetLightTransitionsByTentAndKindSince(
                tentId.Value,
                transitionKind.Value,
                DateTime.MinValue);

            foreach (var transition in transitions)
            {
                var scheduledForUtc = transition.OccurredAtUtc
                    .ToUniversalTime()
                    .AddMinutes(config.DelayMinutes ?? 0);

                if (scheduledForUtc > nowUtc)
                {
                    continue;
                }

                if (_repository.GetAutoMeasurementRun(config.Id, config.TriggerKind, scheduledForUtc) is not null)
                {
                    continue;
                }

                var run = _repository.CreateAutoMeasurementRunIfNotExists(new AutoMeasurementRun
                {
                    ConfigId = config.Id,
                    GrowId = config.GrowId,
                    TriggerKind = config.TriggerKind,
                    ScheduledForUtc = scheduledForUtc,
                    Status = AutoMeasurementRunStatus.Pending
                });

                ProcessRun(config, grow, tentId.Value, scheduledForUtc, run);
                processed++;
            }
        }

        return processed;
    }

    private AutoMeasurementRun? TryCreateMissingTentRun(AutoMeasurementConfig config)
    {
        if (_repository.GetAutoMeasurementRun(config.Id, config.TriggerKind, MissingTentRunScheduleUtc) is not null)
        {
            return null;
        }

        return _repository.CreateAutoMeasurementRunIfNotExists(new AutoMeasurementRun
        {
            ConfigId = config.Id,
            GrowId = config.GrowId,
            TriggerKind = config.TriggerKind,
            ScheduledForUtc = MissingTentRunScheduleUtc,
            Status = AutoMeasurementRunStatus.Pending
        });
    }

    private void ProcessRun(
        AutoMeasurementConfig config,
        GrowRun? grow,
        int tentId,
        DateTime scheduledForUtc,
        AutoMeasurementRun run)
    {
        try
        {
            var mappings = _repository.GetAutoMeasurementFieldMappings(config.Id);
            var measurement = new Measurement
            {
                GrowId = config.GrowId,
                TakenAt = scheduledForUtc,
                Stage = _repository.GetLatestMeasurement(config.GrowId)?.Stage ?? GrowStage.Veg,
                Source = ValueOrigin.HomeAssistant,
                Notes = $"AutoMeasurement {config.TriggerKind}"
            };

            var anyValueSet = false;
            var runMessages = new List<string>();
            foreach (var mapping in mappings)
            {
                var readings = _sensorReadings.GetReadings(
                    tentId,
                    mapping.MetricKey,
                    scheduledForUtc.AddMinutes(-config.WindowMinutes),
                    scheduledForUtc);

                if (readings.Count == 0)
                {
                    if (mapping.IsRequired)
                    {
                        MarkSkipped(run, $"Pflicht-Metrik '{mapping.MetricKey}' hat keine Sensorwerte im Zeitfenster.");
                        return;
                    }

                    continue;
                }

                var value = Aggregate(readings, mapping.Aggregation);
                var guardResult = _valueGuard.Check(mapping.MeasurementField, value);
                if (guardResult.Severity == AutoMeasurementValueSeverity.Reject)
                {
                    var message = guardResult.Message ?? $"{mapping.MeasurementField} Wert {value} wurde verworfen.";
                    if (mapping.IsRequired)
                    {
                        MarkSkipped(run, $"Pflichtfeld abgelehnt: {message}");
                        return;
                    }

                    runMessages.Add($"Optionaler Wert verworfen: {message}");
                    continue;
                }

                if (guardResult.Severity == AutoMeasurementValueSeverity.Warning && !string.IsNullOrWhiteSpace(guardResult.Message))
                {
                    runMessages.Add($"Warnung: {guardResult.Message}");
                }

                if (ApplyValue(measurement, mapping.MeasurementField, value))
                {
                    anyValueSet = true;
                }
            }

            if (!anyValueSet)
            {
                var message = runMessages.Count > 0
                    ? $"Keine AutoMeasurement-Felder konnten gesetzt werden. {string.Join(" | ", runMessages)}"
                    : "Keine AutoMeasurement-Felder konnten aus Sensorwerten gesetzt werden.";
                MarkSkipped(run, message);
                return;
            }

            var measurementId = _repository.CreateMeasurement(measurement);
            run.MeasurementId = measurementId;
            run.Status = AutoMeasurementRunStatus.Created;
            run.ErrorMessage = runMessages.Count > 0 ? string.Join(" | ", runMessages) : null;
            _repository.UpdateAutoMeasurementRun(run);
        }
        catch (Exception ex)
        {
            run.Status = AutoMeasurementRunStatus.Failed;
            run.ErrorMessage = ex.Message;
            _repository.UpdateAutoMeasurementRun(run);
        }
    }

    private static LightTransitionKind? ToTransitionKind(AutoMeasurementTriggerKind triggerKind)
        => triggerKind switch
        {
            AutoMeasurementTriggerKind.LightOnDelay => LightTransitionKind.LightOn,
            AutoMeasurementTriggerKind.LightOffDelay => LightTransitionKind.LightOff,
            _ => null
        };

    private static double Aggregate(IReadOnlyList<TentSensorReading> readings, AutoMeasurementAggregation aggregation)
        => aggregation switch
        {
            AutoMeasurementAggregation.Average => readings.Average(reading => reading.Value),
            AutoMeasurementAggregation.Median => Median(readings.Select(reading => reading.Value).ToList()),
            _ => readings.OrderBy(reading => reading.CapturedAtUtc).Last().Value
        };

    private static double Median(List<double> values)
    {
        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 1
            ? values[middle]
            : (values[middle - 1] + values[middle]) / 2;
    }

    private static bool ApplyValue(Measurement measurement, AutoMeasurementField field, double value)
    {
        switch (field)
        {
            case AutoMeasurementField.AirTemperatureC:
                measurement.AirTemperatureC = value;
                return true;
            case AutoMeasurementField.HumidityPercent:
                measurement.HumidityPercent = value;
                return true;
            case AutoMeasurementField.ReservoirPh:
                measurement.ReservoirPh = value;
                return true;
            case AutoMeasurementField.ReservoirEc:
                measurement.ReservoirEc = value;
                return true;
            case AutoMeasurementField.ReservoirWaterTempC:
                measurement.ReservoirWaterTempC = value;
                return true;
            case AutoMeasurementField.ReservoirLevelLiters:
                measurement.ReservoirLevelLiters = value;
                return true;
            case AutoMeasurementField.ReservoirLevelCm:
                measurement.ReservoirLevelCm = value;
                return true;
            case AutoMeasurementField.DissolvedOxygenMgL:
                measurement.DissolvedOxygenMgL = value;
                return true;
            case AutoMeasurementField.OrpMv:
                measurement.OrpMv = value;
                return true;
            case AutoMeasurementField.PpfdMol:
                measurement.PpfdMol = value;
                return true;
            case AutoMeasurementField.Co2Ppm:
                measurement.Co2Ppm = value;
                return true;
            default:
                return false;
        }
    }

    private void MarkSkipped(AutoMeasurementRun run, string errorMessage)
    {
        run.Status = AutoMeasurementRunStatus.Skipped;
        run.ErrorMessage = errorMessage;
        _repository.UpdateAutoMeasurementRun(run);
    }
}
