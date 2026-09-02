using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests.Infrastructure;

/// <summary>
/// Eine gelöschte Kalibrierung lässt keine verwaiste Aufgabe zurück.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> Gefunden beim Wegräumen einer
/// Ausnahme: <c>HardwarePage.tsx</c> stand in <c>OHNE_RUNDWEG</c> mit dem
/// Grund „eine angelegte Kalibrierung erzeugt still eine Aufgabe und verändert
/// damit die Aufgabenseite". Beim Aufräumen dieser Nebenwirkung zeigte sich,
/// dass es dafür gar keinen Weg gab.</para>
///
/// <para><b>Was der Nutzer merkt.</b> Eine geplante Kalibrierung legt eine
/// Erinnerung in der Aufgabenliste an — das ist gewollt. Wer sich vertippt und
/// den Vorgang löscht, wird die Erinnerung aber nicht mehr los: sie hängt an
/// einer Kalibrierung, die es nicht mehr gibt, und ist über die Oberfläche
/// nirgends mehr erreichbar. Auf der Aufgabenseite steht dann dauerhaft
/// „Kalibrierung: pH-Sonde — …" für etwas, das nie stattfinden wird.</para>
///
/// <para>Und beim Fälligkeits-Wächter zählt sie mit: die Zahl über offenen
/// Aufgaben steigt und geht nicht mehr herunter.</para>
/// </remarks>
public sealed class WerDieKalibrierungLoeschtLoeschtDieAufgabeTests : IDisposable
{
    private readonly string _wurzel;
    private readonly AppPaths _pfade;
    private readonly HardwareRepository _geraete;
    private readonly GrowRepository _grows;
    private readonly TaskRepository _aufgaben;

    public WerDieKalibrierungLoeschtLoeschtDieAufgabeTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), "KalibAufgabe_" + Guid.NewGuid().ToString("N"));
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

    /// <summary>Mit der Kalibrierung geht ihre Erinnerung.</summary>
    [Fact]
    public void EineGeloeschteKalibrierung_LaesstKeineAufgabeZurueck()
    {
        var (kalibrierung, growId) = GeplanteKalibrierung();

        // Mengenwaechter: ohne erzeugte Aufgabe prueft der Rest nichts.
        Assert.True(kalibrierung.GrowTaskId is not null,
            "Der Aufbau erzeugt gar keine Erinnerung — dann ist die Pruefung darunter wertlos. "
            + "Eine GEPLANTE Kalibrierung mit Faelligkeit an einem Geraet MIT Grow legt eine an.");

        _geraete.DeleteCalibrationEvent(kalibrierung.Id);

        var uebrig = _aufgaben.GetForGrow(growId)
            .Where(a => a.Id == kalibrierung.GrowTaskId)
            .ToList();

        Assert.True(uebrig.Count == 0,
            $"Die Erinnerung „{uebrig.FirstOrDefault()?.Title}\" steht noch in der Aufgabenliste, "
            + "obwohl die Kalibrierung geloescht ist. Sie haengt an nichts mehr und ist ueber die "
            + "Oberflaeche nicht mehr erreichbar — wer sich vertippt hat, wird sie nie los.");
    }

    /// <summary>
    /// Eine <b>erledigte</b> Aufgabe bleibt stehen.
    /// </summary>
    /// <remarks>
    /// Die Gegenrichtung. Wer die Kalibrierung tatsächlich durchgeführt und
    /// abgehakt hat, hat einen Eintrag in seiner Historie — den darf ein
    /// späteres Löschen des Vorgangs nicht stillschweigend wegnehmen. Gelöscht
    /// wird nur, was noch offen ist.
    /// </remarks>
    [Fact]
    public void EineBereitsErledigteAufgabe_BleibtStehen()
    {
        var (kalibrierung, growId) = GeplanteKalibrierung();
        var aufgabeId = kalibrierung.GrowTaskId!.Value;

        _aufgaben.SetStatus(aufgabeId, GrowTaskStatus.Done);

        _geraete.DeleteCalibrationEvent(kalibrierung.Id);

        Assert.True(_aufgaben.GetForGrow(growId).Any(a => a.Id == aufgabeId),
            "Die abgehakte Aufgabe wurde mitgeloescht. Was der Nutzer erledigt hat, gehoert in "
            + "seine Historie und verschwindet nicht, weil der Vorgang darunter aufgeraeumt wird.");
    }

    // ------------------------------------------------------------------ Hilfe

    private (CalibrationEvent Kalibrierung, int GrowId) GeplanteKalibrierung()
    {
        var zelt = _grows.CreateTent(new Tent { Name = "Zelt", TentType = TentType.Production });
        var growId = _grows.CreateGrow(new GrowRun
        {
            Name = "Lauf",
            TentId = zelt.Id,
            HydroStyle = HydroStyle.RDWC,
            Status = GrowStatus.Running,
            StartDate = DateTime.Today.AddDays(-10),
        });

        var geraet = _geraete.CreateHardwareItem(new HardwareItem
        {
            Name = "pH-Sonde",
            Category = "Sensor",
            GrowId = growId,
            Criticality = HardwareItemCriticality.High,
        });

        var kalibrierung = _geraete.CreateCalibrationEvent(new CalibrationEvent
        {
            HardwareItemId = geraet.Id,
            CalibrationType = CalibrationEventType.Ph,
            Status = CalibrationEventStatus.Planned,
            Title = "Zweipunkt 4,01 / 7,00",
            DueAtUtc = DateTime.UtcNow.AddDays(7),
        });

        return (kalibrierung, growId);
    }

    /// <summary>Und beim Löschen des ganzen <b>Geräts</b> ebenso.</summary>
    /// <remarks>
    /// Der schwerste Fall: ein Gerät nimmt <i>alle</i> seine Vorgänge mit, und
    /// bis zum 02.09.2026 blieb jede einzelne ihrer Erinnerungen stehen. Wer
    /// eine ausgetauschte pH-Sonde entfernte, hatte danach ihre Kalibrier- und
    /// Wartungsaufgaben dauerhaft in der Liste — für ein Gerät, das es nicht
    /// mehr gibt.
    /// </remarks>
    [Fact]
    public void EinGeloeschtesGeraet_LaesstKeineAufgabenZurueck()
    {
        var (kalibrierung, growId) = GeplanteKalibrierung();
        var geraetId = kalibrierung.HardwareItemId;

        var wartung = _geraete.CreateMaintenanceEvent(new MaintenanceEvent
        {
            HardwareItemId = geraetId,
            EventType = MaintenanceEventType.Cleaning,
            Status = MaintenanceEventStatus.Planned,
            Title = "Filter wechseln",
            DueAtUtc = DateTime.UtcNow.AddDays(14),
        });

        // Mengenwaechter: beide Erinnerungen muessen entstanden sein.
        Assert.True(kalibrierung.GrowTaskId is not null && wartung.GrowTaskId is not null,
            "Der Aufbau erzeugt nicht beide Erinnerungen — dann prueft der Rest nur die Haelfte.");

        _geraete.DeleteHardwareItem(geraetId);

        var uebrig = _aufgaben.GetForGrow(growId)
            .Where(a => a.Id == kalibrierung.GrowTaskId || a.Id == wartung.GrowTaskId)
            .Select(a => a.Title)
            .ToList();

        Assert.True(uebrig.Count == 0,
            "Nach dem Loeschen des Geraets stehen diese Erinnerungen noch in der Liste: "
            + string.Join(" | ", uebrig)
            + " — sie zeigen auf ein Geraet, das es nicht mehr gibt.");
    }

    /// <summary>Und beim einzelnen <b>Wartungs</b>vorgang genauso.</summary>
    [Fact]
    public void EinGeloeschterWartungsvorgang_LaesstKeineAufgabeZurueck()
    {
        var (kalibrierung, growId) = GeplanteKalibrierung();

        var wartung = _geraete.CreateMaintenanceEvent(new MaintenanceEvent
        {
            HardwareItemId = kalibrierung.HardwareItemId,
            EventType = MaintenanceEventType.Cleaning,
            Status = MaintenanceEventStatus.Planned,
            Title = "Filter wechseln",
            DueAtUtc = DateTime.UtcNow.AddDays(14),
        });

        Assert.True(wartung.GrowTaskId is not null,
            "Der Aufbau erzeugt keine Erinnerung — dann prueft der Rest nichts.");

        _geraete.DeleteMaintenanceEvent(wartung.Id);

        Assert.True(_aufgaben.GetForGrow(growId).All(a => a.Id != wartung.GrowTaskId),
            "Die Wartungs-Erinnerung steht noch in der Liste, obwohl der Vorgang geloescht ist.");

        // Gegenprobe: die Kalibrier-Erinnerung darf NICHT mitgegangen sein.
        Assert.True(_aufgaben.GetForGrow(growId).Any(a => a.Id == kalibrierung.GrowTaskId),
            "Mit dem Wartungsvorgang ist auch die Erinnerung der KALIBRIERUNG verschwunden — "
            + "die Bedingung raeumt zu breit ab.");
    }
}
