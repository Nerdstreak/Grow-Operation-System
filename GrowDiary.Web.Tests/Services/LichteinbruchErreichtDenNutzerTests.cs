using System.Net;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Tests.TestFakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Ein Lichteinbruch in der Dunkelphase erreicht den Nutzer — nachts.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (01.09.2026).</b> Zwei Befunde am selben Wächter, beide
/// mit derselben Wirkung: der Alarm kommt nicht.</para>
///
/// <list type="number">
///   <item><b>Die Ruhezeit legte ihn still.</b> <c>CheckIntrusionAsync</c>
///   sendete über den gewöhnlichen Weg, und der prüft
///   <c>settings.IsQuietHour</c>. Ein Blütezelt fährt 12/12 mit Licht aus um
///   20:00 — die übliche Ruhezeit 22–07 überdeckt <b>neun der zwölf</b>
///   Dunkelstunden. Genau in der Zeit, für die der Wächter gebaut ist, war er
///   stumm.</item>
///   <item><b>Ein zweiter Grow schaltete ihn ganz ab.</b>
///   <c>ActiveGrows.FirstOrDefault()</c> liess <b>einen</b> Grow entscheiden.
///   Steht neben dem Photoperioden-Grow in Blütewoche 6 eine später gesteckte
///   Autoflower, liefert die Liste womöglich die Autoflower — und für die ist
///   Licht in der Nacht kein Einbruch. Der Alarm fiel für das <i>ganze Zelt</i>
///   aus.</item>
/// </list>
///
/// <para><b>Warum das zählt.</b> Licht in der Dunkelphase ist in der Blüte
/// keine Unannehmlichkeit, sondern der Weg zu Zwittern und einem verlorenen
/// Lauf. Der Kommentar an der Methode sagt es selbst: „ein Push jetzt kann die
/// Nacht noch retten, ein Blick ins Protokoll übermorgen nicht."</para>
/// </remarks>
public sealed class LichteinbruchErreichtDenNutzerTests : IDisposable
{
    private readonly string _wurzel;
    private readonly AppPaths _pfade;
    private readonly GrowRepository _grows;
    private readonly Tent _zelt;

    public LichteinbruchErreichtDenNutzerTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), "Lichteinbruch_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_wurzel);
        _pfade = new AppPaths(_wurzel);
        TestDatabase.Initialize(_pfade);
        _grows = new GrowRepository(_pfade);

        _zelt = _grows.CreateTent(new Tent { Name = "Bluetezelt", TentType = TentType.Production });

        _grows.SaveHomeAssistantSettings(new HomeAssistantSettings
        {
            BaseUrl = "http://ha.local:8123", AccessToken = "token", Enabled = true,
        });
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    /// <summary>
    /// Der Kontrollfall: ohne Ruhezeit und mit einem Grow meldet der Wächter.
    /// </summary>
    /// <remarks>
    /// <b>Ohne diesen Fall belegen die beiden darunter nichts.</b> Sie sind rot,
    /// wenn kein Alarm rausgeht — aber das könnte jede Ursache haben: kein
    /// gelernter Zyklus, falsche Phase, kein Benachrichtigungsdienst. Erst
    /// wenn dieselbe Anordnung <i>ohne</i> die beiden gesuchten Umstände
    /// <b>grün</b> ist, sagt ihr Rot etwas über die Umstände aus.
    /// </remarks>
    [Fact]
    public async Task Kontrollfall_OhneRuhezeitUndMitEinemGrow_MeldetDerWaechter()
    {
        SaeeZyklus();
        _zelt.ActiveGrows.Add(BlueteGrow());

        var handler = new RecordingHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        await Waechter(handler).CheckIntrusionAsync(_zelt, Einschaltflanke(), CancellationToken.None);

        Assert.True(handler.Requests.Count > 0,
            "Selbst ohne Ruhezeit und mit einem einzigen Bluete-Grow geht kein Alarm raus. "
            + "Dann liegt es nicht an den beiden gesuchten Fehlern, sondern am Aufbau dieser "
            + "Datei — und die Faelle darunter belegen nichts.");
    }

    /// <summary>
    /// Mitten in der Ruhezeit geht der Alarm trotzdem raus.
    /// </summary>
    /// <remarks>
    /// Die Ruhezeit ist dafür da, dass niemand um drei Uhr wegen eines
    /// EC-Trends geweckt wird. Ein Lichteinbruch ist das Gegenteil: er
    /// <i>passiert</i> nachts, und wer ihn morgens erfährt, kann nichts mehr
    /// tun.
    /// </remarks>
    [Fact]
    public async Task InDerRuhezeit_GehtDerAlarmTrotzdemRaus()
    {
        // Ruhezeit ueber die ganze Uhr: was jetzt nicht rausgeht, geht nie raus.
        RuhezeitUeberall();
        SaeeZyklus();
        var grow = BlueteGrow();
        _zelt.ActiveGrows.Add(grow);

        var handler = new RecordingHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        await Waechter(handler).CheckIntrusionAsync(_zelt, Einschaltflanke(), CancellationToken.None);

        Assert.True(handler.Requests.Count > 0,
            "Waehrend der Ruhezeit ging kein Lichteinbruch-Alarm raus. Die uebliche Ruhezeit "
            + "22-07 ueberdeckt neun der zwoelf Dunkelstunden eines 12/12-Zelts — der Waechter "
            + "waere damit genau dann stumm, wofuer es ihn gibt.");
    }

    /// <summary>
    /// Ein zweiter Grow im Zelt schaltet den Wächter nicht ab.
    /// </summary>
    /// <remarks>
    /// Gemeldet wird, sobald <b>irgendein</b> Grow im Zelt Dunkelphase hat.
    /// Die Lampe leuchtet für alle.
    /// </remarks>
    [Fact]
    public async Task EinZweiterGrowImZelt_SchaltetDenWaechterNichtAb()
    {
        var handler = new RecordingHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));

        SaeeZyklus();

        // Die Autoflower steht VORNE — genau der Fall, den FirstOrDefault traf.
        _zelt.ActiveGrows.Add(AutoflowerGrow());
        _zelt.ActiveGrows.Add(BlueteGrow());

        await Waechter(handler).CheckIntrusionAsync(_zelt, Einschaltflanke(), CancellationToken.None);

        Assert.True(handler.Requests.Count > 0,
            "Im Zelt steht ein Photoperioden-Grow in der Bluete und daneben eine Autoflower. "
            + "Der Waechter hat nur den ERSTEN Grow gefragt und geschwiegen — die Lampe "
            + "leuchtet aber auf beide.");
    }

    /// <summary>
    /// Und ohne Einbruch bleibt es still.
    /// </summary>
    /// <remarks>
    /// Mengenwächter gegen die Gegenrichtung: ein Wächter, der immer meldet,
    /// besteht die beiden Fälle oben ebenfalls — und wäre wertlos.
    /// </remarks>
    [Fact]
    public async Task OhneEinbruch_BleibtEsStill()
    {
        SaeeZyklus();
        var grow = BlueteGrow();
        _zelt.ActiveGrows.Add(grow);

        var handler = new RecordingHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));

        // Eine AUS-Flanke ist nie ein Einbruch.
        await Waechter(handler).CheckIntrusionAsync(
            _zelt,
            new LightTransitionEvent
            {
                TentId = _zelt.Id,
                Kind = LightTransitionKind.LightOff,
                OccurredAtUtc = DateTime.UtcNow,
            },
            CancellationToken.None);

        Assert.True(handler.Requests.Count == 0,
            "Eine Ausschaltflanke hat einen Lichteinbruch-Alarm ausgeloest — der Waechter "
            + "meldet dann bei allem, und die beiden Faelle darueber sagen nichts mehr.");
    }

    // ------------------------------------------------------------------ Hilfe

    private void RuhezeitUeberall()
        => new NotificationSettingsRepository(_pfade).SaveNotificationSettings(new NotificationSettings
        {
            NotifyService = "notify.mobile_app_test",
            QuietHoursStartHour = 0,
            QuietHoursEndHour = 23,
        });

    private void NurDienstEingerichtet()
        => new NotificationSettingsRepository(_pfade).SaveNotificationSettings(
            new NotificationSettings { NotifyService = "notify.mobile_app_test" });

    private GrowRun BlueteGrow()
    {
        var id = _grows.CreateGrow(new GrowRun
        {
            Name = "Photoperiode", TentId = _zelt.Id, HydroStyle = HydroStyle.RDWC,
            Status = GrowStatus.Running, SeedType = SeedType.Feminized,
            StartDate = DateTime.Today.AddDays(-100), FlipDate = DateTime.Today.AddDays(-42),
        });
        return _grows.GetGrow(id)!;
    }

    private GrowRun AutoflowerGrow()
    {
        var id = _grows.CreateGrow(new GrowRun
        {
            Name = "Autoflower", TentId = _zelt.Id, HydroStyle = HydroStyle.RDWC,
            Status = GrowStatus.Running, SeedType = SeedType.Autoflower,
            StartDate = DateTime.Today.AddDays(-28),
        });
        return _grows.GetGrow(id)!;
    }

    /// <summary>
    /// Sät vier Tage 12/12 — sonst gibt es keinen gelernten Zyklus.
    /// </summary>
    /// <remarks>
    /// <para><b>Ohne das misst diese Datei nichts.</b>
    /// <c>LightIntrusionGuard.IsIntrusion</c> beginnt mit
    /// <c>if (cycle is null) return false</c>, und <c>CycleFor</c> lernt den
    /// Zyklus aus den Flanken der letzten fünf Tage. Auf einer leeren
    /// Datenbank schweigt der Wächter also <b>immer</b> — die Fälle oben
    /// wären rot gewesen, ohne dass einer der beiden gesuchten Fehler daran
    /// beteiligt ist.</para>
    ///
    /// <para>Genau die Falle aus <c>CLAUDE.md</c>: „Zeigen, dass die Prüfung
    /// beißt." Eine rote Prüfung ist kein Beleg, solange nicht feststeht,
    /// <i>woran</i> sie rot ist.</para>
    ///
    /// <para>12/12 mit Licht an um 08:00 und aus um 20:00 (Ortszeit des
    /// Servers) — der Normalfall in der Blüte.</para>
    /// </remarks>
    private void SaeeZyklus()
    {
        var lights = new LightRepository(_pfade);
        var versatz = new LightCycleReader(lights).LocalOffset(_zelt.Id);
        var heute = DateTime.UtcNow.Date;

        for (var tag = 4; tag >= 1; tag -= 1)
        {
            var basis = heute.AddDays(-tag);
            foreach (var (stunde, art) in new[]
                     {
                         (8, LightTransitionKind.LightOn),
                         (20, LightTransitionKind.LightOff),
                     })
            {
                lights.CreateLightTransitionIfNotDuplicate(new LightTransitionEvent
                {
                    TentId = _zelt.Id,
                    Kind = art,
                    OccurredAtUtc = basis.AddHours(stunde) - versatz,
                });
            }
        }
    }

    /// <summary>Eine Einschaltflanke mitten in der Nacht.</summary>
    private LightTransitionEvent Einschaltflanke()
        => new()
        {
            TentId = _zelt.Id,
            Kind = LightTransitionKind.LightOn,
            OccurredAtUtc = DateTime.UtcNow.Date.AddHours(2),
        };

    private LightWatchService Waechter(RecordingHttpHandler handler)
    {
        if (new NotificationSettingsRepository(_pfade).GetNotificationSettings().NotifyService is null
            or { Length: 0 })
        {
            NurDienstEingerichtet();
        }

        var ha = new HomeAssistantService(
            new StubHttpClientFactory(handler), NullLogger<HomeAssistantService>.Instance);

        return new LightWatchService(
            new LightCycleReader(new LightRepository(_pfade)),
            new NotificationService(
                new NotificationSettingsRepository(_pfade), _grows, ha,
                NullLogger<NotificationService>.Instance),
            NullLogger<LightWatchService>.Instance);
    }
}
