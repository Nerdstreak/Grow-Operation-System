using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services;

public sealed class AutoMeasurementExecutionServiceTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;
    private readonly SensorReadingRepository _sensorReadings;
    private readonly AutoMeasurementExecutionService _service;

    public AutoMeasurementExecutionServiceTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _paths = new AppPaths(_contentRoot);
        GrowDiary.Web.Tests.TestDatabase.InitializeWithDefaultTent(_paths);
        _repository = new GrowRepository(_paths);
        _sensorReadings = new SensorReadingRepository(_paths);
        _service = new AutoMeasurementExecutionService(_repository, _sensorReadings, new AutoMeasurementValueGuard());
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    [Fact]
    public void ExecuteDue_CreatesOneMeasurementForLightOnDelay()
    {
        var context = CreateContext(AutoMeasurementTriggerKind.LightOnDelay, AutoMeasurementField.AirTemperatureC, "temperature", delayMinutes: 5);
        var occurredAt = Utc(2026, 5, 7, 8, 0);
        AddTransition(context.TentId, LightTransitionKind.LightOn, occurredAt);
        AddReading(context.TentId, "temperature", 24.2, occurredAt.AddMinutes(4));

        _service.ExecuteDue(occurredAt.AddMinutes(5));

        var measurement = Assert.Single(_repository.GetMeasurementsForGrow(context.GrowId));
        Assert.Equal(ValueOrigin.HomeAssistant, measurement.Source);
        Assert.Equal(24.2, measurement.AirTemperatureC);
        var run = Assert.Single(_repository.GetAutoMeasurementRunsByConfig(context.ConfigId));
        Assert.Equal(AutoMeasurementRunStatus.Created, run.Status);
        Assert.Equal(measurement.Id, run.MeasurementId);
    }

    [Fact]
    public void ExecuteDue_CreatesOneMeasurementForLightOffDelay()
    {
        var context = CreateContext(AutoMeasurementTriggerKind.LightOffDelay, AutoMeasurementField.HumidityPercent, "humidity");
        var occurredAt = Utc(2026, 5, 7, 20, 0);
        AddTransition(context.TentId, LightTransitionKind.LightOff, occurredAt);
        AddReading(context.TentId, "humidity", 58, occurredAt);

        _service.ExecuteDue(occurredAt);

        Assert.Equal(58, Assert.Single(_repository.GetMeasurementsForGrow(context.GrowId)).HumidityPercent);
        Assert.Equal(AutoMeasurementRunStatus.Created, Assert.Single(_repository.GetAutoMeasurementRunsByConfig(context.ConfigId)).Status);
    }

    [Fact]
    public void ExecuteDue_RepeatedRunDoesNotCreateDuplicateMeasurement()
    {
        var context = CreateContext(AutoMeasurementTriggerKind.LightOnDelay, AutoMeasurementField.ReservoirPh, "reservoir-ph");
        var occurredAt = Utc(2026, 5, 7, 8, 0);
        AddTransition(context.TentId, LightTransitionKind.LightOn, occurredAt);
        AddReading(context.TentId, "reservoir-ph", 5.8, occurredAt);

        _service.ExecuteDue(occurredAt);
        _service.ExecuteDue(occurredAt.AddMinutes(10));

        Assert.Single(_repository.GetMeasurementsForGrow(context.GrowId));
        Assert.Single(_repository.GetAutoMeasurementRunsByConfig(context.ConfigId));
    }

    [Fact]
    public void ExecuteDue_RespectsDelayMinutes()
    {
        var context = CreateContext(AutoMeasurementTriggerKind.LightOnDelay, AutoMeasurementField.ReservoirEc, "reservoir-ec", delayMinutes: 15);
        var occurredAt = Utc(2026, 5, 7, 8, 0);
        AddTransition(context.TentId, LightTransitionKind.LightOn, occurredAt);
        AddReading(context.TentId, "reservoir-ec", 1.4, occurredAt.AddMinutes(10));

        _service.ExecuteDue(occurredAt.AddMinutes(14));
        Assert.Empty(_repository.GetMeasurementsForGrow(context.GrowId));
        Assert.Empty(_repository.GetAutoMeasurementRunsByConfig(context.ConfigId));

        _service.ExecuteDue(occurredAt.AddMinutes(15));
        Assert.Equal(1.4, Assert.Single(_repository.GetMeasurementsForGrow(context.GrowId)).ReservoirEc);
    }

    [Fact]
    public void ExecuteDue_RequiredMappingWithoutValueSkipsRunAndCreatesNoMeasurement()
    {
        var context = CreateContext(AutoMeasurementTriggerKind.LightOnDelay, AutoMeasurementField.ReservoirWaterTempC, "reservoir-temp", isRequired: true);
        var occurredAt = Utc(2026, 5, 7, 8, 0);
        AddTransition(context.TentId, LightTransitionKind.LightOn, occurredAt);

        _service.ExecuteDue(occurredAt);

        Assert.Empty(_repository.GetMeasurementsForGrow(context.GrowId));
        var run = Assert.Single(_repository.GetAutoMeasurementRunsByConfig(context.ConfigId));
        Assert.Equal(AutoMeasurementRunStatus.Skipped, run.Status);
        Assert.Contains("reservoir-temp", run.ErrorMessage);
    }

    [Fact]
    public void ExecuteDue_OptionalMappingWithoutValueDoesNotBlockCreatedMeasurement()
    {
        var context = CreateContext(AutoMeasurementTriggerKind.LightOnDelay, AutoMeasurementField.ReservoirLevelLiters, "reservoir-level");
        _repository.ReplaceAutoMeasurementFieldMappings(context.ConfigId, new[]
        {
            new AutoMeasurementFieldMapping
            {
                MeasurementField = AutoMeasurementField.ReservoirLevelLiters,
                MetricKey = "reservoir-level",
                Aggregation = AutoMeasurementAggregation.Latest,
                IsRequired = true
            },
            new AutoMeasurementFieldMapping
            {
                MeasurementField = AutoMeasurementField.Co2Ppm,
                MetricKey = "co2",
                Aggregation = AutoMeasurementAggregation.Latest,
                IsRequired = false
            }
        });
        var occurredAt = Utc(2026, 5, 7, 8, 0);
        AddTransition(context.TentId, LightTransitionKind.LightOn, occurredAt);
        AddReading(context.TentId, "reservoir-level", 42, occurredAt);

        _service.ExecuteDue(occurredAt);

        var measurement = Assert.Single(_repository.GetMeasurementsForGrow(context.GrowId));
        Assert.Equal(42, measurement.ReservoirLevelLiters);
        Assert.Null(measurement.Co2Ppm);
        Assert.Equal(AutoMeasurementRunStatus.Created, Assert.Single(_repository.GetAutoMeasurementRunsByConfig(context.ConfigId)).Status);
    }

    [Fact]
    public void ExecuteDue_SkipsWhenNoMappedFieldCanBeSet()
    {
        var context = CreateContext(AutoMeasurementTriggerKind.LightOnDelay, AutoMeasurementField.PpfdMol, "ppfd", isRequired: false);
        var occurredAt = Utc(2026, 5, 7, 8, 0);
        AddTransition(context.TentId, LightTransitionKind.LightOn, occurredAt);

        _service.ExecuteDue(occurredAt);

        Assert.Empty(_repository.GetMeasurementsForGrow(context.GrowId));
        Assert.Equal(AutoMeasurementRunStatus.Skipped, Assert.Single(_repository.GetAutoMeasurementRunsByConfig(context.ConfigId)).Status);
    }

    [Fact]
    public void ExecuteDue_LatestUsesLastValueInsideWindow()
    {
        var context = CreateContext(AutoMeasurementTriggerKind.LightOnDelay, AutoMeasurementField.OrpMv, "orp", aggregation: AutoMeasurementAggregation.Latest);
        var occurredAt = Utc(2026, 5, 7, 8, 0);
        AddTransition(context.TentId, LightTransitionKind.LightOn, occurredAt);
        AddReading(context.TentId, "orp", 280, occurredAt.AddMinutes(-2));
        AddReading(context.TentId, "orp", 300, occurredAt);

        _service.ExecuteDue(occurredAt);

        Assert.Equal(300, Assert.Single(_repository.GetMeasurementsForGrow(context.GrowId)).OrpMv);
    }

    [Fact]
    public void ExecuteDue_AverageCalculatesMean()
    {
        var context = CreateContext(AutoMeasurementTriggerKind.LightOnDelay, AutoMeasurementField.DissolvedOxygenMgL, "dissolved-oxygen", aggregation: AutoMeasurementAggregation.Average);
        var occurredAt = Utc(2026, 5, 7, 8, 0);
        AddTransition(context.TentId, LightTransitionKind.LightOn, occurredAt);
        AddReading(context.TentId, "dissolved-oxygen", 7, occurredAt.AddMinutes(-1));
        AddReading(context.TentId, "dissolved-oxygen", 9, occurredAt);

        _service.ExecuteDue(occurredAt);

        Assert.Equal(8, Assert.Single(_repository.GetMeasurementsForGrow(context.GrowId)).DissolvedOxygenMgL);
    }

    [Fact]
    public void ExecuteDue_MedianCalculatesMedian()
    {
        var context = CreateContext(AutoMeasurementTriggerKind.LightOnDelay, AutoMeasurementField.PpfdMol, "ppfd", aggregation: AutoMeasurementAggregation.Median);
        var occurredAt = Utc(2026, 5, 7, 8, 0);
        AddTransition(context.TentId, LightTransitionKind.LightOn, occurredAt);
        AddReading(context.TentId, "ppfd", 500, occurredAt.AddMinutes(-2));
        AddReading(context.TentId, "ppfd", 700, occurredAt.AddMinutes(-1));
        AddReading(context.TentId, "ppfd", 900, occurredAt);

        _service.ExecuteDue(occurredAt);

        Assert.Equal(700, Assert.Single(_repository.GetMeasurementsForGrow(context.GrowId)).PpfdMol);
    }

    [Fact]
    public void ExecuteDue_NoTentSkipsRunAndCreatesNoMeasurement()
    {
        var growId = _repository.CreateGrow(new GrowRun { TentId = null, Name = "No Tent", StartDate = new DateTime(2026, 5, 1), Status = GrowStatus.Running });
        var config = _repository.CreateAutoMeasurementConfig(new AutoMeasurementConfig
        {
            GrowId = growId,
            TentId = null,
            Name = "No Tent Config",
            Status = AutoMeasurementStatus.Enabled,
            TriggerKind = AutoMeasurementTriggerKind.LightOnDelay,
            DelayMinutes = 0,
            WindowMinutes = 20
        });

        _service.ExecuteDue(Utc(2026, 5, 7, 8, 0));

        Assert.Empty(_repository.GetMeasurementsForGrow(growId));
        var run = Assert.Single(_repository.GetAutoMeasurementRunsByConfig(config.Id));
        Assert.Equal(AutoMeasurementRunStatus.Skipped, run.Status);
        Assert.Contains("Tent", run.ErrorMessage);
    }

    [Fact]
    public void ExecuteDue_DisabledConfigIsIgnored()
    {
        var context = CreateContext(AutoMeasurementTriggerKind.LightOnDelay, AutoMeasurementField.AirTemperatureC, "temperature", status: AutoMeasurementStatus.Disabled);
        var occurredAt = Utc(2026, 5, 7, 8, 0);
        AddTransition(context.TentId, LightTransitionKind.LightOn, occurredAt);
        AddReading(context.TentId, "temperature", 24, occurredAt);

        _service.ExecuteDue(occurredAt);

        Assert.Empty(_repository.GetMeasurementsForGrow(context.GrowId));
        Assert.Empty(_repository.GetAutoMeasurementRunsByConfig(context.ConfigId));
    }

    [Fact]
    public void ExecuteDue_ManualConfigIsIgnored()
    {
        var context = CreateContext(AutoMeasurementTriggerKind.Manual, AutoMeasurementField.AirTemperatureC, "temperature");
        var occurredAt = Utc(2026, 5, 7, 8, 0);
        AddTransition(context.TentId, LightTransitionKind.LightOn, occurredAt);
        AddReading(context.TentId, "temperature", 24, occurredAt);

        _service.ExecuteDue(occurredAt);

        Assert.Empty(_repository.GetMeasurementsForGrow(context.GrowId));
        Assert.Empty(_repository.GetAutoMeasurementRunsByConfig(context.ConfigId));
    }

    [Fact]
    public void ExecuteDue_RequiredRejectedValueSkipsRunAndCreatesNoMeasurement()
    {
        var context = CreateContext(AutoMeasurementTriggerKind.LightOnDelay, AutoMeasurementField.ReservoirPh, "reservoir-ph");
        var occurredAt = Utc(2026, 5, 7, 8, 0);
        AddTransition(context.TentId, LightTransitionKind.LightOn, occurredAt);
        AddReading(context.TentId, "reservoir-ph", 0, occurredAt);

        _service.ExecuteDue(occurredAt);

        Assert.Empty(_repository.GetMeasurementsForGrow(context.GrowId));
        var run = Assert.Single(_repository.GetAutoMeasurementRunsByConfig(context.ConfigId));
        Assert.Equal(AutoMeasurementRunStatus.Skipped, run.Status);
        Assert.Contains(nameof(AutoMeasurementField.ReservoirPh), run.ErrorMessage);
        Assert.Contains("0", run.ErrorMessage);
    }

    [Fact]
    public void ExecuteDue_OptionalRejectedValueIsOmittedWhenAnotherFieldIsValid()
    {
        var context = CreateContext(AutoMeasurementTriggerKind.LightOnDelay, AutoMeasurementField.AirTemperatureC, "temperature");
        _repository.ReplaceAutoMeasurementFieldMappings(context.ConfigId, new[]
        {
            new AutoMeasurementFieldMapping
            {
                MeasurementField = AutoMeasurementField.AirTemperatureC,
                MetricKey = "temperature",
                Aggregation = AutoMeasurementAggregation.Latest,
                IsRequired = true
            },
            new AutoMeasurementFieldMapping
            {
                MeasurementField = AutoMeasurementField.ReservoirPh,
                MetricKey = "reservoir-ph",
                Aggregation = AutoMeasurementAggregation.Latest,
                IsRequired = false
            }
        });
        var occurredAt = Utc(2026, 5, 7, 8, 0);
        AddTransition(context.TentId, LightTransitionKind.LightOn, occurredAt);
        AddReading(context.TentId, "temperature", 24, occurredAt);
        AddReading(context.TentId, "reservoir-ph", 0, occurredAt);

        _service.ExecuteDue(occurredAt);

        var measurement = Assert.Single(_repository.GetMeasurementsForGrow(context.GrowId));
        Assert.Equal(24, measurement.AirTemperatureC);
        Assert.Null(measurement.ReservoirPh);
        var run = Assert.Single(_repository.GetAutoMeasurementRunsByConfig(context.ConfigId));
        Assert.Equal(AutoMeasurementRunStatus.Created, run.Status);
        Assert.Contains(nameof(AutoMeasurementField.ReservoirPh), run.ErrorMessage);
    }

    [Fact]
    public void ExecuteDue_WarningValueCreatesMeasurementAndStoresWarning()
    {
        var context = CreateContext(AutoMeasurementTriggerKind.LightOnDelay, AutoMeasurementField.DissolvedOxygenMgL, "dissolved-oxygen");
        var occurredAt = Utc(2026, 5, 7, 8, 0);
        AddTransition(context.TentId, LightTransitionKind.LightOn, occurredAt);
        AddReading(context.TentId, "dissolved-oxygen", 2, occurredAt);

        _service.ExecuteDue(occurredAt);

        var measurement = Assert.Single(_repository.GetMeasurementsForGrow(context.GrowId));
        Assert.Equal(2, measurement.DissolvedOxygenMgL);
        var run = Assert.Single(_repository.GetAutoMeasurementRunsByConfig(context.ConfigId));
        Assert.Equal(AutoMeasurementRunStatus.Created, run.Status);
        Assert.Contains("Warnung", run.ErrorMessage);
        Assert.Contains(nameof(AutoMeasurementField.DissolvedOxygenMgL), run.ErrorMessage);
    }

    [Fact]
    public void ExecuteDue_AllValuesRejectedOrMissingSkipsRunAndCreatesNoMeasurement()
    {
        var context = CreateContext(AutoMeasurementTriggerKind.LightOnDelay, AutoMeasurementField.ReservoirPh, "reservoir-ph", isRequired: false);
        _repository.ReplaceAutoMeasurementFieldMappings(context.ConfigId, new[]
        {
            new AutoMeasurementFieldMapping
            {
                MeasurementField = AutoMeasurementField.ReservoirPh,
                MetricKey = "reservoir-ph",
                Aggregation = AutoMeasurementAggregation.Latest,
                IsRequired = false
            },
            new AutoMeasurementFieldMapping
            {
                MeasurementField = AutoMeasurementField.Co2Ppm,
                MetricKey = "co2",
                Aggregation = AutoMeasurementAggregation.Latest,
                IsRequired = false
            }
        });
        var occurredAt = Utc(2026, 5, 7, 8, 0);
        AddTransition(context.TentId, LightTransitionKind.LightOn, occurredAt);
        AddReading(context.TentId, "reservoir-ph", 0, occurredAt);

        _service.ExecuteDue(occurredAt);

        Assert.Empty(_repository.GetMeasurementsForGrow(context.GrowId));
        var run = Assert.Single(_repository.GetAutoMeasurementRunsByConfig(context.ConfigId));
        Assert.Equal(AutoMeasurementRunStatus.Skipped, run.Status);
        Assert.Contains(nameof(AutoMeasurementField.ReservoirPh), run.ErrorMessage);
        Assert.Contains("Keine", run.ErrorMessage);
    }

    private TestContext CreateContext(
        AutoMeasurementTriggerKind triggerKind,
        AutoMeasurementField field,
        string metricKey,
        int? delayMinutes = 0,
        int windowMinutes = 20,
        bool isRequired = true,
        AutoMeasurementAggregation aggregation = AutoMeasurementAggregation.Latest,
        AutoMeasurementStatus status = AutoMeasurementStatus.Enabled)
    {
        var tent = _repository.GetTents().Single();
        var growId = _repository.CreateGrow(new GrowRun
        {
            TentId = tent.Id,
            Name = $"Auto {Guid.NewGuid():N}",
            StartDate = new DateTime(2026, 5, 1),
            Status = GrowStatus.Running
        });
        var config = _repository.CreateAutoMeasurementConfig(new AutoMeasurementConfig
        {
            GrowId = growId,
            TentId = tent.Id,
            Name = "Auto",
            Status = status,
            TriggerKind = triggerKind,
            DelayMinutes = delayMinutes,
            WindowMinutes = windowMinutes
        });
        _repository.ReplaceAutoMeasurementFieldMappings(config.Id, new[]
        {
            new AutoMeasurementFieldMapping
            {
                MeasurementField = field,
                MetricKey = metricKey,
                Aggregation = aggregation,
                IsRequired = isRequired
            }
        });

        return new TestContext(tent.Id, growId, config.Id);
    }

    private void AddTransition(int tentId, LightTransitionKind kind, DateTime occurredAtUtc)
    {
        _repository.CreateLightTransitionIfNotDuplicate(new LightTransitionEvent
        {
            TentId = tentId,
            Kind = kind,
            OccurredAtUtc = occurredAtUtc,
            Source = LightSource.HomeAssistant,
            RawState = kind == LightTransitionKind.LightOn ? "on" : "off"
        });
    }

    private void AddReading(int tentId, string metricKey, double value, DateTime capturedAtUtc)
    {
        _sensorReadings.AddReading(new TentSensorReading
        {
            TentId = tentId,
            MetricKey = metricKey,
            Value = value,
            CapturedAtUtc = capturedAtUtc
        });
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute)
        => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private sealed record TestContext(int TentId, int GrowId, int ConfigId);
}
