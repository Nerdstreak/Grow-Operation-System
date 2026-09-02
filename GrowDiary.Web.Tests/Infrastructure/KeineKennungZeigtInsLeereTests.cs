using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests.Infrastructure;

/// <summary>
/// Eine gelöschte Aufgabe lässt keine Kennung zurück, die ins Leere zeigt.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> Der Prüfer meldete als Hinweis, zwei
/// Vorgänge könnten über die Schnittstelle auf dieselbe <c>GrowTaskId</c>
/// zeigen. Beim Nachgehen kam der schwerere Fall heraus — und der ist über die
/// Oberfläche erreichbar.</para>
///
/// <para><b>Was der Nutzer merkt.</b> Eine geplante Kalibrierung legt eine
/// Erinnerung in der Aufgabenliste an. Wer sie dort löscht
/// (<c>MobileActionPage.tsx</c>, „Aufgaben"), behält eine Kalibrierung, deren
/// <c>GrowTaskId</c> auf eine Aufgabe zeigt, die es nicht mehr gibt. Und weil
/// die Erinnerung nur beim <b>Anlegen</b> entsteht
/// (<c>GrowTaskId ??= TryCreate…</c>), bekommt dieser Vorgang <b>nie wieder</b>
/// eine — auch nicht beim Bearbeiten. Die Kalibrierung steht weiter als
/// geplant da und erinnert an nichts mehr.</para>
///
/// <para>Die tote Kennung wird deshalb beim Löschen der Aufgabe gelöst. Danach
/// ist der Vorgang wieder in dem Zustand, aus dem eine neue Erinnerung
/// entstehen kann.</para>
/// </remarks>
public sealed class KeineKennungZeigtInsLeereTests : IDisposable
{
    private readonly string _wurzel;
    private readonly AppPaths _pfade;
    private readonly HardwareRepository _geraete;
    private readonly GrowRepository _grows;
    private readonly TaskRepository _aufgaben;

    public KeineKennungZeigtInsLeereTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), "ToteKennung_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_wurzel);
        _pfade = new AppPaths(_wurzel);
        TestDatabase.Initialize(_pfade);
        _geraete = new HardwareRepository(_pfade);
        _grows = new GrowRepository(_pfade);
        _aufgaben = new TaskRepository(_pfade);
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    /// <summary>Wer die Erinnerung löscht, hängt die Kalibrierung ab.</summary>
    [Fact]
    public void EineGeloeschteAufgabe_LaesstKeineToteKennung()
    {
        var kalibrierung = GeplanteKalibrierung();

        // Mengenwaechter: ohne Erinnerung prueft der Rest nichts.
        Assert.True(kalibrierung.GrowTaskId is not null,
            "Der Aufbau erzeugt gar keine Erinnerung — dann ist die Pruefung darunter wertlos.");

        _aufgaben.Delete(kalibrierung.GrowTaskId!.Value);

        var danach = _geraete.GetCalibrationEvent(kalibrierung.Id);
        Assert.True(danach is not null, "Die Kalibrierung selbst wurde mitgeloescht.");
        Assert.True(danach!.GrowTaskId is null,
            $"Die Kalibrierung zeigt weiter auf Aufgabe {danach.GrowTaskId}, die es nicht mehr "
            + "gibt. Eine Erinnerung entsteht nur beim ANLEGEN (GrowTaskId ??= TryCreate…) — "
            + "dieser Vorgang bekommt also nie wieder eine und erinnert an nichts mehr.");
    }

    /// <summary>Dasselbe für einen Wartungsvorgang.</summary>
    [Fact]
    public void EineGeloeschteAufgabe_HaengtAuchDieWartungAb()
    {
        var wartung = GeplanteWartung();
        Assert.True(wartung.GrowTaskId is not null, "Der Aufbau erzeugt gar keine Erinnerung.");

        _aufgaben.Delete(wartung.GrowTaskId!.Value);

        Assert.True(_geraete.GetMaintenanceEvent(wartung.Id)!.GrowTaskId is null,
            "Der Wartungsvorgang zeigt weiter auf eine Aufgabe, die es nicht mehr gibt.");
    }

    /// <summary>
    /// Eine <b>fremde</b> Aufgabe zu löschen hängt nichts ab.
    /// </summary>
    /// <remarks>
    /// Die Gegenrichtung: würde beim Löschen pauschal alles abgehängt, verlöre
    /// jede noch gültige Kalibrierung ihre Erinnerung — schlimmer als der
    /// Fehler selbst.
    /// </remarks>
    [Fact]
    public void EineFremdeAufgabe_LaesstDieVerknuepfungInRuhe()
    {
        var kalibrierung = GeplanteKalibrierung();
        var geraet = _geraete.GetHardwareItem(kalibrierung.HardwareItemId)!;
        var fremde = _aufgaben.Create(new GrowTask
        {
            GrowId = geraet.GrowId!.Value,
            Title = "Etwas ganz anderes",
            Status = GrowTaskStatus.Open,
            DueAtUtc = DateTime.UtcNow.AddDays(1),
        });

        _aufgaben.Delete(fremde);

        Assert.True(_geraete.GetCalibrationEvent(kalibrierung.Id)!.GrowTaskId == kalibrierung.GrowTaskId,
            "Das Loeschen einer fremden Aufgabe hat die Verknuepfung der Kalibrierung geloest.");
    }

    // ------------------------------------------------------------------ Hilfe

    private int GrowMitGeraet()
    {
        var zelt = _grows.CreateTent(new Tent { Name = "Zelt", TentType = TentType.Production });
        return _grows.CreateGrow(new GrowRun
        {
            Name = "Lauf", TentId = zelt.Id, HydroStyle = HydroStyle.RDWC,
            Status = GrowStatus.Running, StartDate = DateTime.Today.AddDays(-10),
        });
    }

    private HardwareItem Geraet(int growId)
        => _geraete.CreateHardwareItem(new HardwareItem
        {
            Name = "pH-Sonde", Category = "Sensor", GrowId = growId,
            Criticality = HardwareItemCriticality.High,
        });

    private CalibrationEvent GeplanteKalibrierung()
        => _geraete.CreateCalibrationEvent(new CalibrationEvent
        {
            HardwareItemId = Geraet(GrowMitGeraet()).Id,
            CalibrationType = CalibrationEventType.Ph,
            Status = CalibrationEventStatus.Planned,
            Title = "Zweipunkt 4,01 / 7,00",
            DueAtUtc = DateTime.UtcNow.AddDays(7),
        });

    private MaintenanceEvent GeplanteWartung()
        => _geraete.CreateMaintenanceEvent(new MaintenanceEvent
        {
            HardwareItemId = Geraet(GrowMitGeraet()).Id,
            EventType = MaintenanceEventType.Cleaning,
            Status = MaintenanceEventStatus.Planned,
            Title = "Filter wechseln",
            DueAtUtc = DateTime.UtcNow.AddDays(14),
        });
}
