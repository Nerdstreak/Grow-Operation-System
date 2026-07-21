using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Behavior tests for the HA-mapping → hardware sync: mapping a pH entity must make the
/// sensor appear under "Sensoren" with a calibration cycle armed, survive repeated mapping
/// saves (TentSensor ids regenerate on every save), and never overwrite user edits.
/// </summary>
public sealed class TentSensorHardwareSyncTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly AppPaths _paths;
    private readonly Tent _tent;
    private readonly GrowRepository _growRepository;
    private readonly HardwareRepository _hardware;
    private readonly TentSensorHardwareSyncService _sync;

    public TentSensorHardwareSyncTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _paths = new AppPaths(_contentRoot);
        _tent = TestDatabase.InitializeWithDefaultTent(_paths);

        _growRepository = new GrowRepository(_paths);
        _hardware = new HardwareRepository(_paths);
        _sync = new TentSensorHardwareSyncService(_hardware, NullLogger<TentSensorHardwareSyncService>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    /// <summary>Saves the mapping the same way the settings endpoint does, then syncs.</summary>
    private Tent MapAndSync(params (SensorMetricType Type, string EntityId)[] sensors)
    {
        _growRepository.ReplaceTentSensors(_tent.Id, sensors
            .Select(s => new TentSensor { TentId = _tent.Id, MetricType = s.Type, HaEntityId = s.EntityId, IsActive = true })
            .ToList());
        var tent = _growRepository.GetTent(_tent.Id)!;
        _sync.SyncForTent(tent);
        return tent;
    }

    [Fact]
    public void MappedPhEntity_AppearsAsHardwareSensor_WithCalibrationArmed()
    {
        MapAndSync((SensorMetricType.ReservoirPh, "sensor.bluelab_guardian_ph"));

        var item = Assert.Single(_hardware.GetHardwareItemsByTent(_tent.Id));
        Assert.Equal("pH-Sonde", item.Name);
        Assert.Equal("Sensor", item.Category);
        Assert.Equal(SensorMetricType.ReservoirPh, item.MetricType);
        Assert.Equal("sensor.bluelab_guardian_ph", item.HaEntityId);
        Assert.Equal(14, item.CalibrationIntervalDays);
        Assert.Equal(HardwareItemCriticality.High, item.Criticality);

        // Calibration cycle is armed: a planned event due in ~14 days exists, so the
        // daily calibration push reminder fires without a first manual calibration.
        var calibration = Assert.Single(_hardware.GetOpenCalibrationEventsByHardwareItem(item.Id));
        Assert.Equal(CalibrationEventStatus.Planned, calibration.Status);
        Assert.Equal(CalibrationEventType.Ph, calibration.CalibrationType);
        Assert.NotNull(calibration.DueAtUtc);
        Assert.InRange(calibration.DueAtUtc!.Value, DateTime.UtcNow.AddDays(13), DateTime.UtcNow.AddDays(15));
    }

    [Fact]
    public void ClimateSensor_IsCreated_WithoutCalibrationCycle()
    {
        MapAndSync((SensorMetricType.Humidity, "sensor.zelt_luftfeuchte"));

        var item = Assert.Single(_hardware.GetHardwareItemsByTent(_tent.Id));
        Assert.Equal("Luftfeuchte-Sensor", item.Name);
        Assert.Null(item.CalibrationIntervalDays);
        Assert.Empty(_hardware.GetOpenCalibrationEventsByHardwareItem(item.Id));
    }

    [Fact]
    public void NonMeasurementMetrics_AreNotSynced()
    {
        MapAndSync(
            (SensorMetricType.LightStatus, "switch.licht"),
            (SensorMetricType.PumpCirculation, "switch.pumpe"));

        Assert.Empty(_hardware.GetHardwareItemsByTent(_tent.Id));
    }

    [Fact]
    public void RepeatedMappingSaves_DoNotDuplicate_AndRefreshTentSensorId()
    {
        MapAndSync((SensorMetricType.ReservoirPh, "sensor.ph"));
        var firstSensorId = Assert.Single(_hardware.GetHardwareItemsByTent(_tent.Id)).TentSensorId;

        // Saving the mapping again deletes + re-inserts TentSensors (new ids).
        var tent = MapAndSync((SensorMetricType.ReservoirPh, "sensor.ph"));

        var item = Assert.Single(_hardware.GetHardwareItemsByTent(_tent.Id));
        Assert.NotEqual(firstSensorId, item.TentSensorId);
        Assert.Equal(tent.Sensors.Single().Id, item.TentSensorId);
    }

    [Fact]
    public void EntitySwap_UpdatesLinkedEntity()
    {
        MapAndSync((SensorMetricType.ReservoirPh, "sensor.old_ph"));
        MapAndSync((SensorMetricType.ReservoirPh, "sensor.new_ph"));

        var item = Assert.Single(_hardware.GetHardwareItemsByTent(_tent.Id));
        Assert.Equal("sensor.new_ph", item.HaEntityId);
    }

    [Fact]
    public void UserEdits_SurviveResync()
    {
        MapAndSync((SensorMetricType.ReservoirPh, "sensor.ph"));
        var item = Assert.Single(_hardware.GetHardwareItemsByTent(_tent.Id));
        item.Name = "Meine Bluelab Sonde";
        item.CalibrationIntervalDays = 7;
        _hardware.UpdateHardwareItem(item);

        MapAndSync((SensorMetricType.ReservoirPh, "sensor.ph"));

        var resynced = Assert.Single(_hardware.GetHardwareItemsByTent(_tent.Id));
        Assert.Equal("Meine Bluelab Sonde", resynced.Name);
        Assert.Equal(7, resynced.CalibrationIntervalDays);
    }

    [Fact]
    public void Unmapping_KeepsThePhysicalItem()
    {
        MapAndSync((SensorMetricType.ReservoirPh, "sensor.ph"));
        MapAndSync(); // mapping cleared

        Assert.Single(_hardware.GetHardwareItemsByTent(_tent.Id));
    }

    [Fact]
    public void DisplayLabel_BecomesTheItemName()
    {
        _growRepository.ReplaceTentSensors(_tent.Id, new List<TentSensor>
        {
            new() { TentId = _tent.Id, MetricType = SensorMetricType.ReservoirEc, HaEntityId = "sensor.ec", DisplayLabel = "Guardian EC", IsActive = true },
        });
        _sync.SyncForTent(_growRepository.GetTent(_tent.Id)!);

        Assert.Equal("Guardian EC", Assert.Single(_hardware.GetHardwareItemsByTent(_tent.Id)).Name);
    }

    [Fact]
    public void MultipleMetrics_CreateOneItemEach()
    {
        MapAndSync(
            (SensorMetricType.ReservoirPh, "sensor.ph"),
            (SensorMetricType.ReservoirEc, "sensor.ec"),
            (SensorMetricType.ReservoirWaterTemp, "sensor.wtemp"),
            (SensorMetricType.AirTemperature, "sensor.air"));

        var items = _hardware.GetHardwareItemsByTent(_tent.Id);
        Assert.Equal(4, items.Count);
        Assert.Equal(4, items.Select(i => i.MetricType).Distinct().Count());
    }
}
