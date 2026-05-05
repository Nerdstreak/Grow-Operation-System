using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GrowDiary.Web.Tests.Infrastructure;

public sealed class TentRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppPaths _paths;

    public TentRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"grow-test-{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", _dbPath);
        _paths = new AppPaths(Path.GetTempPath());
        var initializer = new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance);
        initializer.Initialize();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
    }

    private GrowRepository Repo() => new(_paths);

    [Fact]
    public void DefaultTent_IsCreatedOnFirstStart()
    {
        var tents = Repo().GetTents();
        Assert.Single(tents);
        Assert.Equal("Hauptzelt", tents[0].Name);
        Assert.Equal(TentType.MultiPurpose, tents[0].TentType);
    }

    [Fact]
    public void CreateTent_PersistsAllFields()
    {
        var repo = Repo();
        var created = repo.CreateTent("Testzelt");
        Assert.True(created.Id > 0);
        Assert.Equal("Testzelt", created.Name);

        var loaded = repo.GetTent(created.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Testzelt", loaded!.Name);
        Assert.Equal(TentType.MultiPurpose, loaded.TentType);
    }

    [Fact]
    public void GetTent_LoadsSensorsCorrectly()
    {
        var repo = Repo();
        var tent = repo.GetTents().First();

        repo.AddTentSensor(new TentSensor
        {
            TentId     = tent.Id,
            MetricType = SensorMetricType.AirTemperature,
            HaEntityId = "sensor.temp_test",
            IsActive   = true
        });

        var loaded = repo.GetTent(tent.Id);
        Assert.NotNull(loaded);
        Assert.Single(loaded!.Sensors);
        Assert.Equal(SensorMetricType.AirTemperature, loaded.Sensors[0].MetricType);
        Assert.Equal("sensor.temp_test", loaded.Sensors[0].HaEntityId);
    }

    [Fact]
    public void AddTentSensor_PersistsCorrectly()
    {
        var repo = Repo();
        var tent = repo.GetTents().First();

        var sensor = repo.AddTentSensor(new TentSensor
        {
            TentId       = tent.Id,
            MetricType   = SensorMetricType.Humidity,
            HaEntityId   = "sensor.humidity_test",
            DisplayLabel = "Luftfeuchte Haupt",
            IsActive     = true
        });

        Assert.True(sensor.Id > 0);
        var sensors = repo.GetTentSensors(tent.Id);
        Assert.Single(sensors);
        Assert.Equal("sensor.humidity_test", sensors[0].HaEntityId);
        Assert.Equal("Luftfeuchte Haupt", sensors[0].DisplayLabel);
    }

    [Fact]
    public void UpdateTentSensor_UpdatesValue()
    {
        var repo = Repo();
        var tent = repo.GetTents().First();

        var sensor = repo.AddTentSensor(new TentSensor
        {
            TentId     = tent.Id,
            MetricType = SensorMetricType.Co2,
            HaEntityId = "sensor.co2_old",
            IsActive   = true
        });

        sensor.HaEntityId = "sensor.co2_new";
        repo.UpdateTentSensor(sensor);

        var sensors = repo.GetTentSensors(tent.Id);
        Assert.Equal("sensor.co2_new", sensors[0].HaEntityId);
    }

    [Fact]
    public void DeleteTentSensor_RemovesEntry()
    {
        var repo = Repo();
        var tent = repo.GetTents().First();

        var sensor = repo.AddTentSensor(new TentSensor
        {
            TentId     = tent.Id,
            MetricType = SensorMetricType.Vpd,
            HaEntityId = "sensor.vpd_test",
            IsActive   = true
        });

        repo.DeleteTentSensor(sensor.Id);
        var sensors = repo.GetTentSensors(tent.Id);
        Assert.Empty(sensors);
    }

    [Fact]
    public void ReplaceTentSensors_ReplacesExistingSet()
    {
        var repo = Repo();
        var tent = repo.GetTents().First();

        repo.AddTentSensor(new TentSensor
        {
            TentId = tent.Id,
            MetricType = SensorMetricType.AirTemperature,
            HaEntityId = "sensor.temp_old",
            IsActive = true
        });

        repo.ReplaceTentSensors(tent.Id, new[]
        {
            new TentSensor
            {
                TentId = tent.Id,
                MetricType = SensorMetricType.Humidity,
                HaEntityId = "sensor.humidity_new",
                DisplayLabel = "RH",
                IsActive = true
            }
        });

        var sensors = repo.GetTentSensors(tent.Id);
        Assert.Single(sensors);
        Assert.Equal(SensorMetricType.Humidity, sensors[0].MetricType);
        Assert.Equal("sensor.humidity_new", sensors[0].HaEntityId);
        Assert.Equal("RH", sensors[0].DisplayLabel);
    }

    [Fact]
    public void GetTentSensorByMetric_ReturnsNullWhenMissing()
    {
        var repo = Repo();
        var tent = repo.GetTents().First();

        var result = repo.GetTentSensorByMetric(tent.Id, SensorMetricType.ReservoirPh);
        Assert.Null(result);
    }

    [Fact]
    public void GetTentSensorByMetric_ReturnsCorrectEntry()
    {
        var repo = Repo();
        var tent = repo.GetTents().First();

        repo.AddTentSensor(new TentSensor
        {
            TentId     = tent.Id,
            MetricType = SensorMetricType.ReservoirEc,
            HaEntityId = "sensor.ec_test",
            IsActive   = true
        });

        var result = repo.GetTentSensorByMetric(tent.Id, SensorMetricType.ReservoirEc);
        Assert.NotNull(result);
        Assert.Equal("sensor.ec_test", result!.HaEntityId);
    }

    [Fact]
    public void DeleteTent_CascadeDeletesSensors()
    {
        var repo = Repo();
        var tent = repo.CreateTent("ZeltZumLöschen");

        repo.AddTentSensor(new TentSensor
        {
            TentId     = tent.Id,
            MetricType = SensorMetricType.AirTemperature,
            HaEntityId = "sensor.temp_delete_test",
            IsActive   = true
        });

        repo.DeleteTent(tent.Id);

        var sensors = repo.GetTentSensors(tent.Id);
        Assert.Empty(sensors);
    }
}
