using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// Der AC-Test schaltet nur, was eingetragen und erlaubt ist — sonst gar nichts.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> <c>AcTestApiController</c> stand bei
/// <b>0 %</b> Abdeckung — 298 Zeilen, und der einzige Code des Projekts, der an
/// einem <b>echten</b> AC-Infinity-Controller in einem Zelt mit Pflanzen
/// stellt. Es gab <c>AcTestSchuetztDieEchteAnlageTests</c>, aber das prüft die
/// reine Funktion <c>AcTest.ZeitplanErlaubt</c>, nicht den Weg dorthin.</para>
///
/// <para><b>Was hier geprüft wird, ist nicht „gibt er 400 zurück".</b> Geprüft
/// wird, dass in den Ablehnungsfällen <b>kein einziger Ruf hinausgeht</b>. Der
/// Funk unten schreibt jeden Aufruf mit; die Zusicherung lautet „null Rufe",
/// nicht „richtige Antwort". Eine Antwort kann stimmen, während daneben schon
/// geschaltet wurde.</para>
///
/// <para>Und nichts davon erreicht ein Gerät: der <see cref="Mitschreiber"/>
/// ist eine Attrappe, <c>HomeAssistantService</c> hängt an einem
/// Stub-HttpClient.</para>
/// </remarks>
public sealed class AcTestSchaltetNurWasEingetragenIstTests : IDisposable
{
    private readonly string _wurzel;
    private readonly AppPaths _pfade;
    private readonly GrowRepository _grows;
    private readonly AppSettingsRepository _einstellungen;
    private readonly Mitschreiber _funk = new();
    private readonly int _zeltId;

    private const string Eingetragen = "number.zelt_luefter_stufe";

    public AcTestSchaltetNurWasEingetragenIstTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), "AcTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_wurzel);
        _pfade = new AppPaths(_wurzel);
        TestDatabase.Initialize(_pfade);
        _grows = new GrowRepository(_pfade);
        _einstellungen = new AppSettingsRepository(_pfade);

        _zeltId = _grows.CreateTent(new Tent { Name = "Hauptzelt", TentType = TentType.Production }).Id;
        AcTest.Speichern(_einstellungen, _zeltId, [
            new AcGeraet("Abluft", Eingetragen, null, null, null),
        ]);
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    /// <summary>Ein Zelt, das es nicht gibt: nichts geht hinaus.</summary>
    [Fact]
    public async Task EinUnbekanntesZelt_SchaltetNichts()
    {
        var antwort = await Endpunkt().Stufe(
            999, new AcTestApiController.StufeRequest { EntityId = Eingetragen, Stufe = 5 }, default);

        Assert.IsType<NotFoundObjectResult>(antwort);
        NichtsGeschaltet("Fuer ein Zelt, das es nicht gibt, ging ein Ruf an die Anlage.");
    }

    /// <summary>
    /// Eine Entität, die nicht eingetragen ist: nichts geht hinaus.
    /// </summary>
    /// <remarks>
    /// Das ist die wichtigste Sperre dieses Controllers. Ohne sie könnte ein
    /// Aufruf jede beliebige Entität in Home Assistant stellen — die
    /// Heizung, das Garagentor, das Licht im Wohnzimmer.
    /// </remarks>
    [Theory]
    [InlineData("switch.wohnzimmer_licht")]
    [InlineData("number.heizung_soll")]
    [InlineData("cover.garage")]
    public async Task EineFremdeEntitaet_WirdNichtGestellt(string fremd)
    {
        var antwort = await Endpunkt().Stufe(
            _zeltId, new AcTestApiController.StufeRequest { EntityId = fremd, Stufe = 5 }, default);

        Assert.IsType<BadRequestObjectResult>(antwort);
        NichtsGeschaltet(
            $"„{fremd}\" ist fuer dieses Zelt nicht eingetragen, und trotzdem ging ein Ruf "
            + "hinaus. Ohne diese Sperre stellt ein Aufruf JEDE Entitaet in Home Assistant.");
    }

    /// <summary>Eine Stufe ausserhalb der Skala: nichts geht hinaus.</summary>
    /// <remarks>
    /// Der Controller kennt die Skala des Geräts (<c>AcTest.StufeMin</c> bis
    /// <c>StufeMax</c>). Was darüber liegt, nimmt der Controller gar nicht erst
    /// an — was das Gerät daraus machen würde, weiss niemand.
    /// </remarks>
    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(99)]
    public async Task EineStufeAusserhalbDerSkala_WirdNichtGestellt(double stufe)
    {
        var antwort = await Endpunkt().Stufe(
            _zeltId, new AcTestApiController.StufeRequest { EntityId = Eingetragen, Stufe = stufe }, default);

        Assert.IsType<BadRequestObjectResult>(antwort);
        NichtsGeschaltet($"Stufe {stufe} liegt ausserhalb der Skala und ging trotzdem hinaus.");
    }

    /// <summary>
    /// Ein Zeitplan mit gleicher Ein- und Aus-Zeit: nichts geht hinaus.
    /// </summary>
    /// <remarks>
    /// <c>AcTestSchuetztDieEchteAnlageTests</c> prüft, dass
    /// <c>AcTest.ZeitplanErlaubt</c> das ablehnt. Hier wird geprüft, dass die
    /// Ablehnung auch <b>ankommt</b> — eine richtige Rechnung nützt nichts,
    /// wenn der Controller sie nicht fragt.
    /// </remarks>
    [Fact]
    public async Task EinZeitplanOhneDauer_WirdNichtGestellt()
    {
        var antwort = await Endpunkt().Zeitplan(
            _zeltId,
            new AcTestApiController.ZeitplanRequest { EntityId = Eingetragen, Ein = "20:00", Aus = "20:00" },
            default);

        Assert.IsType<BadRequestObjectResult>(antwort);
        NichtsGeschaltet(
            "20:00 bis 20:00 ging an die Anlage. Danach zwingt derselbe Aufruf den Modus auf "
            + "„Schedule\", und das Geraet faehrt einen Plan ohne Dauer.");
    }

    /// <summary>
    /// Und die Gegenrichtung: eine gültige Stufe geht wirklich hinaus.
    /// </summary>
    /// <remarks>
    /// Der Mengenwächter für alles darüber. Ginge nie etwas hinaus, bestünden
    /// alle Fälle oben — und die Seite wäre unbrauchbar.
    /// </remarks>
    [Fact]
    public async Task EineGueltigeStufe_GehtHinaus()
    {
        await Endpunkt().Stufe(
            _zeltId, new AcTestApiController.StufeRequest { EntityId = Eingetragen, Stufe = 5 }, default);

        Assert.True(_funk.Rufe.Count > 0,
            "Eine gueltige Stufe an einem eingetragenen Geraet ging NICHT hinaus — dann laesst "
            + "sich ueber diese Seite gar nichts mehr stellen.");
        Assert.True(_funk.Rufe.All(r => r.EntityId == Eingetragen),
            "Es ging ein Ruf an eine andere Entitaet als die eingetragene: "
            + string.Join(", ", _funk.Rufe.Select(r => r.EntityId).Distinct()));
    }

    /// <summary>
    /// Ein Gerät, das nicht antwortet, ergibt eine Zeile — keinen Serverfehler.
    /// </summary>
    /// <remarks>
    /// Bis zum 02.09.2026 lief jeder Aufruf ungeschützt: eine Basisadresse ohne
    /// Schema liess <c>GetEntityStateAsync</c> werfen, und <c>GET</c> brach mit
    /// HTTP 500 ab — obwohl der erklärende Satz daneben bereitstand. Die ganze
    /// Seite war weg statt einer Zeile.
    /// </remarks>
    [Fact]
    public async Task EinGeraetDasNichtAntwortet_ErgibtEineZeileStattEinesAbsturzes()
    {
        var antwort = await Endpunkt(werfen: true).Stand(_zeltId, default);

        var ok = Assert.IsType<OkObjectResult>(antwort.Result);
        var stand = Assert.IsType<AcTestStand>(ok.Value);

        Assert.True(stand.Geraete.Count == 1,
            $"Statt einer Zeile kamen {stand.Geraete.Count} — der Ausfall EINES Geraets hat die "
            + "ganze Seite mitgenommen.");
        Assert.True(!string.IsNullOrWhiteSpace(stand.Geraete[0].Fehler),
            "Das Geraet antwortet nicht, und daneben steht kein Wort dazu.");
    }

    // ------------------------------------------------------------------ Hilfe

    private void NichtsGeschaltet(string warum)
        => Assert.True(_funk.Rufe.Count == 0,
            warum + $" Es gingen {_funk.Rufe.Count} Rufe hinaus: "
            + string.Join(", ", _funk.Rufe.Select(r => $"{r.Domain}.{r.Dienst} auf {r.EntityId}")));

    private AcTestApiController Endpunkt(bool werfen = false)
    {
        var ha = new HomeAssistantService(
            new TestFakes.StubHttpClientFactory(new TestFakes.RecordingHttpHandler((_, _) =>
                werfen
                    ? throw new InvalidOperationException("Basisadresse ohne Schema")
                    : new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK))),
            NullLogger<HomeAssistantService>.Instance);

        return new AcTestApiController(
            _grows,
            _einstellungen,
            ha,
            new SystemAuditRepository(_pfade),
            new AcSchreiber(_funk, NullLogger<AcSchreiber>.Instance),
            NullLogger<AcTestApiController>.Instance);
    }

    /// <summary>Ein Funk, der jeden Ruf mitschreibt und keinen weitergibt.</summary>
    /// <remarks>
    /// Die Zusicherung dieser Datei ist „null Rufe" — dafür braucht es eine
    /// Stelle, die zählt. Ein Fake, der einfach <c>true</c> zurückgibt, würde
    /// die Ablehnungsfälle bestehen lassen, ohne etwas zu belegen.
    /// </remarks>
    private sealed class Mitschreiber : IAcFunk
    {
        /* Ein Zustand je Entitaet, damit sich die Attrappe wie ein Geraet
           verhaelt. Der erste Anlauf hatte hier eine feste "5" — und weil
           AcSchreiber nichts sendet, was schon so steht ("jeder Aufruf ist eine
           Gelegenheit zu scheitern"), ging bei Stufe 5 gar nichts hinaus. Der
           Mengenwaechter hat das gefunden: die Attrappe war falsch, nicht der
           Code. */
        private readonly Dictionary<string, string> _zustand = new(StringComparer.Ordinal);

        public List<(string Domain, string Dienst, string EntityId)> Rufe { get; } = [];

        public Task<HomeAssistantState?> ZustandAsync(
            HomeAssistantSettings einstellungen, string entityId, CancellationToken ct)
            => Task.FromResult<HomeAssistantState?>(new HomeAssistantState
            {
                EntityId = entityId,
                State = _zustand.TryGetValue(entityId, out var wert) ? wert : "0",
            });

        public Task<bool> SchickenAsync(
            HomeAssistantSettings einstellungen, string domain, string dienst, string entityId,
            IReadOnlyDictionary<string, object> daten, CancellationToken ct)
        {
            Rufe.Add((domain, dienst, entityId));

            // Wie ein echtes Geraet: was gestellt wurde, meldet es danach.
            if (daten.Count > 0)
            {
                _zustand[entityId] = Convert.ToString(
                    daten.Values.First(), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            }

            return Task.FromResult(true);
        }
    }
}
