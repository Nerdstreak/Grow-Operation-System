using System.Net;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Tests.TestFakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Der Kühler- und USV-Wächter läuft auch, wenn sich die Pumpenlage nicht ändert.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (01.09.2026).</b> <c>AnlageMeldenAsync</c> stand in
/// <c>PruefenUndMeldenAsync</c> <b>hinter</b> dem frühen Rücksprung, der greift,
/// wenn die Pumpenlage unverändert ist. Im Normalbetrieb ändert sie sich nie —
/// beide Pumpen melden seit Stunden „an". Der Kühler konnte also ausfallen,
/// ohne dass irgendetwas passierte.</para>
///
/// <para><b>Was das kostet.</b> Im RDWC ist der Kühler die Kette, die eine
/// Ernte kostet: Kühler aus, Wassertemperatur steigt, Sauerstoff fällt,
/// Wurzelfäule. Der Wächter dafür war gebaut und geprüft
/// (<c>AnlagenWatchServiceTests</c>, 16 Zusicherungen) — sein einziger
/// Aufrufer erreichte ihn nur bei einem Pumpenwechsel.</para>
///
/// <para><b>Und ein zweiter Fehler, den der erste versteckt hat.</b> Die
/// Entprellung des Anlagen-Zweigs liest <c>PumpMeldung</c>, schreibt aber nie
/// zurück — die Bedingung <c>gemeldet == lage</c> konnte nie zutreffen. Solange
/// der Zweig nie lief, war das folgenlos; ohne die zweite Reparatur hätte die
/// erste eine Push-Nachricht pro Minute erzeugt.</para>
/// </remarks>
public sealed class KuehlerWaechterLaeuftImmerTests : IDisposable
{
    private readonly string _wurzel;
    private readonly AppPaths _pfade;
    private readonly GrowRepository _grows;
    private readonly SystemHeartbeat _herzschlag = new();
    private readonly Tent _zelt;

    public KuehlerWaechterLaeuftImmerTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), "KuehlerWaechter_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_wurzel);
        _pfade = new AppPaths(_wurzel);
        TestDatabase.Initialize(_pfade);
        _grows = new GrowRepository(_pfade);
        _zelt = _grows.CreateTent(new Tent { Name = "Zelt", TentType = TentType.Production });

        // Ohne konfigurierte Benachrichtigung sendet NotificationService gar
        // nicht — dann pruefte der Entprellungs-Fall unten nichts. Der
        // Mengenwaechter dort hat genau das gefunden.
        _grows.SaveHomeAssistantSettings(new HomeAssistantSettings
        {
            BaseUrl = "http://ha.local:8123", AccessToken = "token", Enabled = true,
        });
        new NotificationSettingsRepository(_pfade).SaveNotificationSettings(
            new NotificationSettings { NotifyService = "notify.mobile_app_test" });
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    /// <summary>
    /// Pumpen laufen unverändert, der Kühler steht seit 90 Minuten.
    /// </summary>
    [Fact]
    public async Task KuehlerAusfall_WirdGemeldet_ObwohlDiePumpenlageGleichBleibt()
    {
        var jetzt = DateTime.UtcNow;
        var zustaende = Lage(pumpenAn: true, kuehlerAn: false, seit: jetzt.AddMinutes(-90));
        var waechter = Waechter();

        // ERSTER Durchgang: die Pumpenlage wird gemerkt.
        await waechter.PruefenUndMeldenAsync(_zelt, zustaende, jetzt);

        // ZWEITER Durchgang, eine Minute später, NICHTS an den Pumpen geändert —
        // genau die Lage, in der der frühe Rücksprung griff.
        await waechter.PruefenUndMeldenAsync(_zelt, zustaende, jetzt.AddMinutes(1));

        var offen = _grows.GetRiskEvents()
            .Where(e => e.EventType == RiskEventType.ChillerOffline && e.TentId == _zelt.Id)
            .Where(e => e.Status != RiskEventStatus.Resolved)
            .ToList();

        Assert.True(offen.Count > 0,
            "Der Kuehler steht seit 90 Minuten, und es entsteht kein Befund — weil sich die "
            + "Pumpenlage nicht geaendert hat. Im Normalbetrieb aendert sie sich nie.");
    }

    /// <summary>
    /// Zweimal derselbe Ausfall ergibt nicht zwei Meldungen.
    /// </summary>
    /// <remarks>
    /// Die Entprellung des Anlagen-Zweigs las bis zum 01.09.2026 die Merkstelle
    /// des Pumpen-Zweigs und schrieb nie zurück — sie konnte nie greifen.
    /// Solange der ganze Zweig hinter dem frühen Rücksprung lag, war das
    /// folgenlos. Ohne diese zweite Reparatur hätte die erste eine
    /// Push-Nachricht pro Minute erzeugt, und dann stellt der Betreiber die
    /// Benachrichtigungen ab.
    /// </remarks>
    [Fact]
    public async Task DerselbeAusfall_MeldetSichNichtJedeMinuteNeu()
    {
        var jetzt = DateTime.UtcNow;
        var zustaende = Lage(pumpenAn: true, kuehlerAn: false, seit: jetzt.AddMinutes(-90));

        var handler = new RecordingHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        var waechter = Waechter(handler);

        await waechter.PruefenUndMeldenAsync(_zelt, zustaende, jetzt);
        var nachErstem = handler.Requests.Count;

        await waechter.PruefenUndMeldenAsync(_zelt, zustaende, jetzt.AddMinutes(1));
        await waechter.PruefenUndMeldenAsync(_zelt, zustaende, jetzt.AddMinutes(2));

        // Mengenwaechter: ohne einen einzigen Aufruf pruefte der Vergleich nichts.
        Assert.True(nachErstem > 0,
            "Der erste Durchgang hat gar nicht gemeldet — dann sagt der Vergleich unten nichts.");
        Assert.True(handler.Requests.Count == nachErstem,
            $"Nach drei Durchgaengen mit derselben Lage gingen {handler.Requests.Count} Anfragen "
            + $"an Home Assistant statt {nachErstem}. Eine Warnung, die sich jede Minute "
            + "wiederholt, wird abgestellt — und dann nuetzt der beste Waechter nichts.");
    }

    /// <summary>
    /// Scheitert das Senden, wird beim nächsten Durchgang erneut gesendet.
    /// </summary>
    /// <remarks>
    /// <para><b>Der Anlass (01.09.2026, vom Prüfer gefunden).</b> Die Merkstelle
    /// wurde <b>vor</b> dem Senden geschrieben, und das Ergebnis nicht
    /// ausgewertet. Ein Home Assistant, der gerade neu startet (HTTP 503),
    /// verschluckte die Meldung damit endgültig: die Entprellung hält die
    /// Lage für gemeldet, und solange sich nichts ändert, kommt nie wieder
    /// etwas. Der Kühler steht, aufs Telefon kommt nichts.</para>
    ///
    /// <para>Der Pumpen-Zweig daneben macht es seit jeher richtig
    /// (<c>if (gesendet)</c>) — der Anlagen-Zweig war die Kopie, die es
    /// vergessen hat.</para>
    /// </remarks>
    [Fact]
    public async Task EinGescheitertesSenden_WirdBeimNaechstenDurchgangWiederholt()
    {
        var jetzt = DateTime.UtcNow;
        var zustaende = Lage(pumpenAn: true, kuehlerAn: false, seit: jetzt.AddMinutes(-90));

        // Erst faellt Home Assistant aus, danach antwortet es wieder.
        var antwortet = false;
        var handler = new RecordingHttpHandler((_, _) => new HttpResponseMessage(
            antwortet ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable));
        var waechter = Waechter(handler);

        await waechter.PruefenUndMeldenAsync(_zelt, zustaende, jetzt);
        var beimAusfall = handler.Requests.Count;

        antwortet = true;
        await waechter.PruefenUndMeldenAsync(_zelt, zustaende, jetzt.AddMinutes(1));

        // Mengenwaechter: ohne einen Versuch im ersten Durchgang prueft der
        // Vergleich unten nichts.
        Assert.True(beimAusfall > 0,
            "Im ersten Durchgang ging gar keine Anfrage raus — dann sagt der Vergleich nichts.");
        Assert.True(handler.Requests.Count > beimAusfall,
            "Der erste Versuch scheiterte (HTTP 503), der zweite haette gehen muessen — es "
            + "ging nichts raus. Die Merkstelle wurde gesetzt, BEVOR gesendet wurde: die "
            + "Entprellung haelt eine Meldung fuer zugestellt, die nie ankam.");
    }

    /// <summary>
    /// Der Waechter mit einem Home Assistant, das NICHT antwortet — der Befund
    /// muss trotzdem entstehen.
    /// </summary>
    private PumpWatchNotifier Waechter()
        => Waechter(new RecordingHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

    private PumpWatchNotifier Waechter(RecordingHttpHandler handler)
    {
        return new PumpWatchNotifier(
            new AppSettingsRepository(_pfade),
            new NotificationService(
                new NotificationSettingsRepository(_pfade),
                _grows,
                new HomeAssistantService(new StubHttpClientFactory(handler), NullLogger<HomeAssistantService>.Instance),
                NullLogger<NotificationService>.Instance),
            _herzschlag,
            new AnlagenRisikoService(_grows, NullLogger<AnlagenRisikoService>.Instance),
            NullLogger<PumpWatchNotifier>.Instance);
    }

    /// <summary>Die Zustaende, die der Waechter aus Home Assistant bekaeme.</summary>
    private static Dictionary<string, HomeAssistantState> Lage(bool pumpenAn, bool kuehlerAn, DateTime seit)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["pump-circulation"] = Zustand(pumpenAn ? "on" : "off", seit),
            ["pump-air"] = Zustand(pumpenAn ? "on" : "off", seit),
            ["chiller"] = Zustand(kuehlerAn ? "on" : "off", seit),
        };

    private static HomeAssistantState Zustand(string wert, DateTime seit)
        => new() { State = wert, LastChanged = seit, LastUpdated = seit };
}
