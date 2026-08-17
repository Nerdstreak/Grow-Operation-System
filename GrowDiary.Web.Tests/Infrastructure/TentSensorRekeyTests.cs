using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests.Infrastructure;

/// <summary>
/// Zelt-Mapping speichern vergibt neue Sensor-Ids — wer zeigt noch auf die alten?
/// </summary>
/// <remarks>
/// <para>Gefunden von der Rundweg-Sonde: <c>ReplaceTentSensors</c> löscht und
/// fügt neu ein. Hardware heilt der Sync-Dienst, aber ein RiskEvent mit
/// Sensor-Verweis behielt die tote Id — und jedes spätere Bestätigen oder
/// Lösen lief in die Existenzprüfung und platzte mit einem 500. Der Nutzer
/// hätte ein Ereignis vor sich gehabt, das sich nie wieder schließen lässt.</para>
/// </remarks>
public sealed class TentSensorRekeyTests : IDisposable
{
    private readonly string _temp;
    private readonly GrowRepository _repository;
    private readonly Tent _tent;

    public TentSensorRekeyTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "TentSensorRekey_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
        var paths = new AppPaths(_temp);
        _tent = TestDatabase.InitializeWithDefaultTent(paths);
        _repository = new GrowRepository(paths);
    }

    public void Dispose()
    {
        try { Directory.Delete(_temp, recursive: true); } catch { }
    }

    [Fact]
    public void ResolvingASensorRiskEventStillWorksAfterTheMappingWasSaved()
    {
        var sensor = _repository.AddTentSensor(new TentSensor
        {
            TentId = _tent.Id,
            MetricType = SensorMetricType.ReservoirPh,
            HaEntityId = "sensor.ph",
            IsActive = true,
        });

        var riskEvent = _repository.CreateRiskEvent(new RiskEvent
        {
            EventType = RiskEventType.SensorUnavailable,
            Severity = RiskEventSeverity.Warning,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.System,
            Title = "pH-Sonde driftet",
            TentId = _tent.Id,
            TentSensorId = sensor.Id,
        });

        // Der Nutzer speichert das Zelt-Mapping — gleicher Sensor, neue Zeile.
        _repository.ReplaceTentSensors(_tent.Id, new[]
        {
            new TentSensor { MetricType = SensorMetricType.ReservoirPh, HaEntityId = "sensor.ph", IsActive = true },
        });

        var geloest = _repository.ResolveRiskEvent(riskEvent.Id, DateTime.UtcNow, "Sonde kalibriert.");

        Assert.Equal(RiskEventStatus.Resolved, geloest.Status);
        Assert.Null(geloest.TentSensorId); // der tote Verweis ist weg, nicht nur versteckt
    }
}
