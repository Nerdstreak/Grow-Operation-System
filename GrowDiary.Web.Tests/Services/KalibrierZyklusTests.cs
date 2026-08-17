using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Der Kalibrier-Zyklus muss sich nach dem Abhaken weiterdrehen.
/// </summary>
/// <remarks>
/// <para>Der Fund dahinter: das Abschliessen rechnete zwar ein
/// <c>NextDueAtUtc</c> aus, aber die Erinnerung liest ausschliesslich
/// Eintraege mit <c>DueAtUtc</c> und Status <c>Planned</c>. Nach der ersten
/// Kalibrierung waere also für immer Ruhe gewesen — und eine pH-Sonde, die
/// niemand mehr anmahnt, driftet still, bis der Nutzer wochenlang gegen
/// falsche Werte anregelt.</para>
///
/// <para>Der Fehler konnte bis jetzt gar nicht auffallen: es gab keinen Weg,
/// eine Kalibrierung abzuschliessen.</para>
/// </remarks>
public sealed class KalibrierZyklusTests : IDisposable
{
    private readonly string _temp;
    private readonly HardwareRepository _hardware;
    private readonly Tent _tent;

    public KalibrierZyklusTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "KalibrierZyklus_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
        var paths = new AppPaths(_temp);
        _tent = TestDatabase.InitializeWithDefaultTent(paths);
        _hardware = new HardwareRepository(paths);
    }

    public void Dispose()
    {
        try { Directory.Delete(_temp, recursive: true); } catch { }
    }

    private HardwareItem PhSonde(int intervallTage = 14) => _hardware.CreateHardwareItem(new HardwareItem
    {
        Name = "pH-Sonde",
        Category = "Sensor",
        Status = HardwareItemStatus.Active,
        TentId = _tent.Id,
        MetricType = SensorMetricType.ReservoirPh,
        DeviceKind = HardwareDeviceKind.FixedSensor,
        CalibrationIntervalDays = intervallTage,
    });

    [Fact]
    public void CompletingACalibrationSchedulesTheNextOne()
    {
        var sonde = PhSonde(intervallTage: 14);
        var geplant = _hardware.CreateCalibrationEvent(new CalibrationEvent
        {
            HardwareItemId = sonde.Id,
            CalibrationType = CalibrationEventType.Ph,
            Status = CalibrationEventStatus.Planned,
            Title = "pH-Sonde",
            DueAtUtc = DateTime.UtcNow.AddDays(-1),
        });

        geplant.Status = CalibrationEventStatus.Completed;
        geplant.PerformedAtUtc = DateTime.UtcNow;
        geplant.BeforeValue = 6.8m;
        geplant.AfterValue = 7.0m;
        var abgeschlossen = _hardware.CompleteCalibrationEvent(geplant);

        Assert.Equal(CalibrationEventStatus.Completed, abgeschlossen.Status);
        Assert.NotNull(abgeschlossen.NextDueAtUtc);

        // Der Kern: die Erinnerung findet wieder etwas — in 14 Tagen.
        var offen = _hardware.GetOpenCalibrationEventsByHardwareItem(sonde.Id);
        var naechste = Assert.Single(offen);
        Assert.NotNull(naechste.DueAtUtc);
        Assert.Equal(14, Math.Round((naechste.DueAtUtc!.Value - DateTime.UtcNow).TotalDays));

        // Und der Waechter sieht ihn zum Termin auch wirklich.
        var faellig = _hardware.GetDueCalibrationEvents(DateTime.UtcNow.AddDays(15));
        Assert.Contains(faellig, e => e.Id == naechste.Id && e.Status == CalibrationEventStatus.Planned);
        Assert.NotNull(CalibrationReminderService.BuildDueMessage(faellig));
    }

    [Fact]
    public void CompletingTwiceDoesNotStackUpTwoReminders()
    {
        var sonde = PhSonde();
        var geplant = _hardware.CreateCalibrationEvent(new CalibrationEvent
        {
            HardwareItemId = sonde.Id,
            CalibrationType = CalibrationEventType.Ph,
            Status = CalibrationEventStatus.Planned,
            Title = "pH-Sonde",
            DueAtUtc = DateTime.UtcNow,
        });

        geplant.Status = CalibrationEventStatus.Completed;
        _hardware.CompleteCalibrationEvent(geplant);
        // Zweiter Tipper auf denselben, schon erledigten Eintrag.
        _hardware.CompleteCalibrationEvent(geplant);

        Assert.Single(_hardware.GetOpenCalibrationEventsByHardwareItem(sonde.Id));
    }

    [Fact]
    public void AFailedCalibrationStillSchedulesTheNextAttempt()
    {
        // Die Sonde nimmt den Referenzwert nicht mehr an. Gerade dann darf die
        // Erinnerung nicht verstummen — hier steht ein Austausch an.
        var sonde = PhSonde();
        var geplant = _hardware.CreateCalibrationEvent(new CalibrationEvent
        {
            HardwareItemId = sonde.Id,
            CalibrationType = CalibrationEventType.Ph,
            Status = CalibrationEventStatus.Planned,
            Title = "pH-Sonde",
            DueAtUtc = DateTime.UtcNow,
        });

        geplant.Status = CalibrationEventStatus.Failed;
        geplant.Result = CalibrationResult.Failed;
        _hardware.CompleteCalibrationEvent(geplant);

        Assert.Single(_hardware.GetOpenCalibrationEventsByHardwareItem(sonde.Id));
    }

    [Fact]
    public void MaintenanceKeepsItsRhythmToo()
    {
        var filter = _hardware.CreateHardwareItem(new HardwareItem
        {
            Name = "Aktivkohlefilter",
            Category = "Filter",
            Status = HardwareItemStatus.Active,
            TentId = _tent.Id,
            InspectionIntervalDays = 90,
        });

        var geplant = _hardware.CreateMaintenanceEvent(new MaintenanceEvent
        {
            HardwareItemId = filter.Id,
            EventType = MaintenanceEventType.Inspection,
            Status = MaintenanceEventStatus.Planned,
            Title = "Filter prüfen",
            DueAtUtc = DateTime.UtcNow,
        });

        geplant.Status = MaintenanceEventStatus.Completed;
        geplant.PerformedAtUtc = DateTime.UtcNow;
        _hardware.CompleteMaintenanceEvent(geplant);

        var naechste = Assert.Single(_hardware.GetOpenMaintenanceEventsByHardwareItem(filter.Id));
        Assert.Equal(90, Math.Round((naechste.DueAtUtc!.Value - DateTime.UtcNow).TotalDays));
    }
}
