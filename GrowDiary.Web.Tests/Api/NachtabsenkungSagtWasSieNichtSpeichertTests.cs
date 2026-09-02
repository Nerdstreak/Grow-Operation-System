using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// Die Nachtabsenkung meldet keinen Erfolg für etwas, das sie wegwirft.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> Zwei Befunde am selben Endpunkt.</para>
///
/// <list type="number">
///   <item><b>Ohne Zelt still verworfen.</b> Zielgerät und Kühler-Einstellungen
///   hängen am Zelt. Hat der Grow keines — nach einem Import ein ganz normaler
///   Zustand —, fiel der ganze Block aus, und die Antwort war trotzdem 200. Die
///   Oberfläche schreibt die Antwort ins Formular zurück: das Feld läuft leer,
///   daneben steht „Gespeichert.", und die Voraussetzungskette fordert genau
///   das wieder an, was der Nutzer eben eingetragen hat. Eine geschlossene
///   Schleife, aus der er nicht herausfindet.</item>
///   <item><b>Jede Kennung angenommen.</b> Der Kühler-Worker schaltet über
///   <c>switch.turn_on</c>. Wer <c>light.zelt_kuehler</c> oder
///   <c>input_boolean.kuehler</c> einträgt — beides Kennungen, die es in einer
///   Home-Assistant-Installation gibt —, bekam „Gespeichert" und eine
///   Steuerung, die nie schaltet.</item>
/// </list>
/// </remarks>
public sealed class NachtabsenkungSagtWasSieNichtSpeichertTests : IDisposable
{
    private readonly string _wurzel;
    private readonly AppPaths _pfade;
    private readonly GrowRepository _grows;

    public NachtabsenkungSagtWasSieNichtSpeichertTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), "Nachtabsenkung_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_wurzel);
        KopiereWissen(Path.Combine(ProjektWurzel(), "GrowDiary.Web", "wwwroot", "knowledge-defaults"), _wurzel);
        _pfade = new AppPaths(_wurzel);
        TestDatabase.Initialize(_pfade);
        _grows = new GrowRepository(_pfade);
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    /// <summary>Ein Grow ohne Zelt bekommt eine Ansage, keinen falschen Erfolg.</summary>
    [Fact]
    public void OhneZelt_WirdDasZielgeraetNichtStillVerworfen()
    {
        var grow = GrowOhneZelt();

        var antwort = Endpunkt().Put(grow.Id, new NightRampApiController.NightRampRequest
        {
            Enabled = true,
            FloorC = 18,
            TargetEntityId = "number.wassertemp_soll",
        });

        Assert.True(antwort.Result is BadRequestObjectResult,
            $"Der Endpunkt antwortete mit {antwort.Result?.GetType().Name ?? "Ok"} — also mit Erfolg. "
            + "Das Zielgeraet haengt aber am Zelt, und dieser Grow hat keines: es wurde still "
            + "weggeworfen. Die Oberflaeche schreibt die Antwort zurueck ins Formular, das Feld "
            + "laeuft leer, und die Kette fordert genau das wieder an, was gerade eingetragen wurde.");
    }

    /// <summary>Dasselbe für den Kühler.</summary>
    [Fact]
    public void OhneZelt_WirdDerKuehlerNichtStillVerworfen()
    {
        var grow = GrowOhneZelt();

        var antwort = Endpunkt().Put(grow.Id, new NightRampApiController.NightRampRequest
        {
            Enabled = true,
            FloorC = 18,
            Chiller = new NightRampApiController.KuehlerRequest { Enabled = true, SwitchEntityId = "switch.kuehler" },
        });

        Assert.True(antwort.Result is BadRequestObjectResult,
            "Die Kuehler-Steuerung wurde ohne Zelt still verworfen und der Endpunkt meldete Erfolg.");
    }

    /// <summary>
    /// Eine Kennung aus der falschen Domäne wird abgelehnt.
    /// </summary>
    /// <remarks>
    /// Der Worker ruft <c>switch.turn_on</c>. Alles andere wird angenommen und
    /// schaltet nie — der Nutzer sucht den Fehler dann in Home Assistant.
    /// </remarks>
    [Theory]
    [InlineData("light.zelt_kuehler_led")]
    [InlineData("input_boolean.kuehler")]
    [InlineData("sensor.kuehler_leistung")]
    [InlineData("kuehler")]
    public void EineKennungAusDerFalschenDomaene_WirdAbgelehnt(string kennung)
    {
        var grow = GrowMitZelt();

        var antwort = Endpunkt().Put(grow.Id, new NightRampApiController.NightRampRequest
        {
            Enabled = true,
            Chiller = new NightRampApiController.KuehlerRequest { Enabled = true, SwitchEntityId = kennung },
        });

        Assert.True(antwort.Result is BadRequestObjectResult,
            $"„{kennung}\" wurde als Kuehler-Steckdose angenommen. Der Worker schaltet ueber "
            + "switch.turn_on — diese Steuerung wuerde nie schalten, und der Nutzer sucht den "
            + "Fehler in Home Assistant.");
    }

    /// <summary>
    /// Und die richtige Kennung geht durch.
    /// </summary>
    /// <remarks>
    /// Die Gegenrichtung: eine Prüfung, die alles ablehnt, besteht die Fälle
    /// darüber ebenfalls — und macht die Kühler-Steuerung unbenutzbar.
    /// </remarks>
    [Fact]
    public void EineSteckdose_GehtDurch()
    {
        var grow = GrowMitZelt();

        var antwort = Endpunkt().Put(grow.Id, new NightRampApiController.NightRampRequest
        {
            Enabled = true,
            FloorC = 18,
            TargetEntityId = "number.wassertemp_soll",
            Chiller = new NightRampApiController.KuehlerRequest { Enabled = true, SwitchEntityId = "switch.zelt_kuehler" },
        });

        Assert.True(antwort.Result is not BadRequestObjectResult,
            "Eine gewoehnliche Steckdose an einem Grow MIT Zelt wurde abgelehnt — dann ist die "
            + "Kuehler-Steuerung gar nicht mehr einzurichten.");

        var zelt = _grows.GetTents(includeArchived: true).Single();
        Assert.True(zelt.ChillerSwitchEntityId == "switch.zelt_kuehler",
            $"Gespeichert wurde „{zelt.ChillerSwitchEntityId}\".");
        Assert.True(zelt.WaterTargetEntityId == "number.wassertemp_soll",
            $"Als Zielgeraet steht „{zelt.WaterTargetEntityId}\" da.");
    }

    // ------------------------------------------------------------------ Hilfe

    private GrowRun GrowOhneZelt()
    {
        var id = _grows.CreateGrow(new GrowRun
        {
            Name = "Importiert", TentId = null, HydroStyle = HydroStyle.RDWC,
            Status = GrowStatus.Running, StartDate = DateTime.Today.AddDays(-30),
        });
        return _grows.GetGrow(id)!;
    }

    private GrowRun GrowMitZelt()
    {
        var zelt = _grows.CreateTent(new Tent { Name = "Zelt", TentType = TentType.Production });
        var id = _grows.CreateGrow(new GrowRun
        {
            Name = "Mit Zelt", TentId = zelt.Id, HydroStyle = HydroStyle.RDWC,
            Status = GrowStatus.Running, StartDate = DateTime.Today.AddDays(-30),
        });
        return _grows.GetGrow(id)!;
    }

    private NightRampApiController Endpunkt()
    {
        var wissen = new KnowledgeBaseLoader(_pfade, NullLogger<KnowledgeBaseLoader>.Instance);
        wissen.Initialize();

        var hydro = new HydroSetupRepository(_pfade, new TentRepository(_pfade));
        var protokoll = new SystemAuditRepository(_pfade);

        return new NightRampApiController(
            _grows,
            new NachtabsenkungWriter(
                _grows,
                new TargetValueService(wissen),
                new HomeAssistantService(
                    new TestFakes.StubHttpClientFactory(
                        new TestFakes.RecordingHttpHandler((_, _) =>
                            new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK))),
                    NullLogger<HomeAssistantService>.Instance),
                new SetpointProfileRepository(_pfade),
                hydro,
                protokoll,
                NullLogger<NachtabsenkungWriter>.Instance),
            new AppSettingsRepository(_pfade),
            protokoll);
    }

    private static string ProjektWurzel()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "GrowDiary.Web"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Projektwurzel nicht gefunden.");
    }

    private static void KopiereWissen(string quelle, string ziel)
    {
        var nach = Path.Combine(ziel, "wwwroot", "knowledge-defaults");
        foreach (var datei in Directory.EnumerateFiles(quelle, "*.json", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(quelle, datei);
            var pfad = Path.Combine(nach, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
            File.Copy(datei, pfad);
        }
    }
}
