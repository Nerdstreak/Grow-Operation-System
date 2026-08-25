using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// Ein Topf trägt eine Pflanze — und es gibt nicht mehr Pflanzen als Töpfe.
/// </summary>
/// <remarks>
/// <para><b>Der gemeldete Fehler (25.08.2026).</b> „Du kannst mehr Sorten
/// angeben, die du anbaust, als es Töpfe gibt." Am laufenden Stand belegt: in
/// ein Vier-Topf-System liessen sich acht Pflanzen legen, zwei davon in
/// denselben Topf 1, eine in einen Topf 999 — jedes Mal HTTP 201. Die
/// Prüfung sah den <c>SiteIndex</c> überhaupt nicht an.</para>
///
/// <para><b>Und es gab keinen Weg zurück:</b> Pflanzen liessen sich anlegen
/// und ändern, aber nirgends entfernen — kein <c>HttpDelete</c>, keine
/// Löschung im Repository, kein Knopf. Wer eine zu viel anlegte, behielt sie.
/// Deshalb prüft dieser Fall beides zusammen.</para>
/// </remarks>
public sealed class ToepfeReichenNichtTests : IDisposable
{
    private readonly string _temp;
    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;
    private readonly PlantsApiController _controller;
    private readonly int _growId;
    private const int Toepfe = 4;

    public ToepfeReichenNichtTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "ToepfeReichenNicht_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
        _paths = new AppPaths(_temp);
        var tent = TestDatabase.InitializeWithDefaultTent(_paths);
        _repository = new GrowRepository(_paths);

        var system = _repository.CreateSystem(new GrowSystem
        {
            TentId = tent.Id,
            Name = "RDWC 4er",
            HydroStyle = "RDWC",
            PotCount = Toepfe,
            PotSizeLiters = 20,
            ReservoirLiters = 60,
        });

        _growId = _repository.CreateGrow(new GrowRun
        {
            TentId = tent.Id,
            SystemId = system.Id,
            Name = "Mischgrow",
            StartDate = new DateTime(2026, 5, 1),
            Status = GrowStatus.Running,
        });

        _controller = new PlantsApiController(_repository);
    }

    public void Dispose()
    {
        try { Directory.Delete(_temp, recursive: true); } catch { }
    }

    private ActionResult<PlantInstanceDto> Anlegen(string label, int? topf)
        => _controller.Create(new CreatePlantInstanceRequest
        {
            GrowId = _growId,
            Label = label,
            SiteIndex = topf,
            PlantRole = PlantRole.Production,
            PlantStatus = PlantStatus.Active,
        });

    private void ToepfeFuellen()
    {
        for (var topf = 1; topf <= Toepfe; topf++)
        {
            Assert.IsType<CreatedAtActionResult>(Anlegen($"Pflanze {topf}", topf).Result);
        }
    }

    /// <summary>Der Mengenwächter: das System hat wirklich Töpfe.</summary>
    [Fact]
    public void DasSystemKenntSeineToepfe()
    {
        var grow = _repository.GetGrow(_growId)!;
        Assert.NotNull(grow.SystemId);
        Assert.Equal(Toepfe, _repository.GetSystem(grow.SystemId!.Value)!.PotCount);
    }

    [Fact]
    public void KeineNeunteWurzelInEinemVierTopfSystem()
    {
        ToepfeFuellen();

        var antwort = Anlegen("eine zu viel", Toepfe + 1);

        Assert.IsType<BadRequestObjectResult>(antwort.Result);
        Assert.Equal(Toepfe, _repository.GetPlantsByGrow(_growId).Count);
    }

    [Fact]
    public void AuchOhneTopfangabeIstBeiVierSchluss()
    {
        // Sonst waere die Regel mit einem leeren Feld zu umgehen.
        ToepfeFuellen();

        Assert.IsType<BadRequestObjectResult>(Anlegen("ohne Topf", null).Result);
        Assert.Equal(Toepfe, _repository.GetPlantsByGrow(_growId).Count);
    }

    [Fact]
    public void ZweiPflanzenTeilenSichKeinenTopf()
    {
        Assert.IsType<CreatedAtActionResult>(Anlegen("Erste", 1).Result);

        var antwort = Anlegen("Zweite", 1);

        var abgelehnt = Assert.IsType<BadRequestObjectResult>(antwort.Result);
        var fehler = Assert.IsType<ApiError>(abgelehnt.Value);
        // Die Meldung muss den Topf UND den Bewohner nennen — sonst steht der
        // Nutzer vor „Eingaben konnten nicht validiert werden".
        Assert.Contains("Topf 1", fehler.Message, StringComparison.Ordinal);
        Assert.Contains("Erste", fehler.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EinenTopfDenEsNichtGibtNimmtNiemandAn()
    {
        var abgelehnt = Assert.IsType<BadRequestObjectResult>(Anlegen("Geisterpflanze", 999).Result);
        var fehler = Assert.IsType<ApiError>(abgelehnt.Value);
        Assert.Contains("999", fehler.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UmziehenInEinenBelegtenTopfWirdAbgelehnt()
    {
        ToepfeFuellen();
        var pflanzen = _repository.GetPlantsByGrow(_growId);
        var zweite = pflanzen.Single(p => p.SiteIndex == 2);

        var antwort = _controller.Update(zweite.Id, new UpdatePlantInstanceRequest
        {
            GrowId = _growId,
            Label = zweite.Label,
            SiteIndex = 1,
            PlantRole = zweite.PlantRole,
            PlantStatus = zweite.PlantStatus,
        });

        Assert.IsType<BadRequestObjectResult>(antwort.Result);
        Assert.Equal(2, _repository.GetPlant(zweite.Id)!.SiteIndex);
    }

    [Fact]
    public void EinAltbestandLaesstSichNochInOrdnungBringen()
    {
        // Wer den Fehler schon hat, muss ihn aufräumen können. Ein PUT, das an
        // einem unveränderten (und regelwidrigen) Topf scheitert, würde den
        // Nutzer aus seinen eigenen Daten aussperren.
        ToepfeFuellen();
        var pflanzen = _repository.GetPlantsByGrow(_growId);
        var vierte = pflanzen.Single(p => p.SiteIndex == 4);

        // Direkt am Repository, wie es ein Bestand von vor dieser Prüfung wäre:
        vierte.SiteIndex = 99;
        _repository.UpdatePlant(vierte);

        // Nur die Sorte ändern — der regelwidrige Topf bleibt, wie er ist.
        var antwort = _controller.Update(vierte.Id, new UpdatePlantInstanceRequest
        {
            GrowId = _growId,
            Label = "umbenannt",
            SiteIndex = 99,
            PlantRole = vierte.PlantRole,
            PlantStatus = vierte.PlantStatus,
        });
        Assert.IsType<OkObjectResult>(antwort.Result);

        // Und der Weg zurück in einen freien Topf steht offen.
        _controller.Update(vierte.Id, new UpdatePlantInstanceRequest
        {
            GrowId = _growId,
            Label = "umbenannt",
            SiteIndex = null,
            PlantRole = vierte.PlantRole,
            PlantStatus = vierte.PlantStatus,
        });
        var frei = _controller.Update(vierte.Id, new UpdatePlantInstanceRequest
        {
            GrowId = _growId,
            Label = "umbenannt",
            SiteIndex = 4,
            PlantRole = vierte.PlantRole,
            PlantStatus = vierte.PlantStatus,
        });
        Assert.IsType<OkObjectResult>(frei.Result);
        Assert.Equal(4, _repository.GetPlant(vierte.Id)!.SiteIndex);
    }

    [Fact]
    public void EinePflanzeLaesstSichEntfernen()
    {
        ToepfeFuellen();
        var zweite = _repository.GetPlantsByGrow(_growId).Single(p => p.SiteIndex == 2);

        Assert.IsType<NoContentResult>(_controller.Delete(zweite.Id));

        Assert.Null(_repository.GetPlant(zweite.Id));
        Assert.Equal(Toepfe - 1, _repository.GetPlantsByGrow(_growId).Count);

        // Und danach ist der Topf wieder frei — sonst waere das Entfernen
        // nur die halbe Reparatur.
        Assert.IsType<CreatedAtActionResult>(Anlegen("Nachrückerin", 2).Result);
    }

    /// <summary>
    /// Ein Umzug in einen anderen Grow ist ein Einzug — mit derselben Nummer.
    /// </summary>
    /// <remarks>
    /// Die erste Fassung fragte nur „ist die Topfnummer anders?". Eine Pflanze
    /// mit Topf 1 konnte damit in einen Grow wandern, dessen Topf 1 belegt war:
    /// die Nummer war unveraendert, also prüfte niemand. Gefunden vom Prüfer,
    /// am laufenden Stand nachgestellt.
    /// </remarks>
    [Fact]
    public void EinUmzugInEinenAnderenGrowZaehltAlsEinzug()
    {
        ToepfeFuellen();

        var zweiterGrow = _repository.CreateGrow(new GrowRun
        {
            TentId = _repository.GetTents().First().Id,
            Name = "Nachbarlauf",
            StartDate = new DateTime(2026, 5, 1),
            Status = GrowStatus.Running,
        });
        var wanderin = Assert.IsType<CreatedAtActionResult>(
            _controller.Create(new CreatePlantInstanceRequest
            {
                GrowId = zweiterGrow,
                Label = "Wanderin",
                SiteIndex = 1,
                PlantRole = PlantRole.Production,
                PlantStatus = PlantStatus.Active,
            }).Result);
        var wanderinId = Assert.IsType<PlantInstanceDto>(wanderin.Value).Id;

        // Derselbe Topf 1 — im Zielgrow steht dort schon jemand.
        var antwort = _controller.Update(wanderinId, new UpdatePlantInstanceRequest
        {
            GrowId = _growId,
            Label = "Wanderin",
            SiteIndex = 1,
            PlantRole = PlantRole.Production,
            PlantStatus = PlantStatus.Active,
        });

        Assert.IsType<BadRequestObjectResult>(antwort.Result);
        Assert.Equal(Toepfe, _repository.GetPlantsByGrow(_growId).Count);
    }

    /// <summary>
    /// Die Freigabe aus der Quarantäne war der zweite Weg an der Prüfung vorbei.
    /// </summary>
    [Fact]
    public void EineFreigabeInEinenVollenGrowWirdAbgelehnt()
    {
        ToepfeFuellen();

        var quarantaene = _repository.CreateSetup(new Setup
        {
            TentId = _repository.GetTents().First().Id,
            Name = "Quarantäne",
            SetupType = SetupType.Quarantine,
        });
        var klon = Assert.IsType<CreatedAtActionResult>(
            _controller.Create(new CreatePlantInstanceRequest
            {
                SetupId = quarantaene.Id,
                Label = "Klon",
                PlantRole = PlantRole.Clone,
                PlantStatus = PlantStatus.Active,
            }).Result);
        var klonId = Assert.IsType<PlantInstanceDto>(klon.Value).Id;

        var antwort = _controller.DecideQuarantine(new DecideQuarantinePlantRequest
        {
            PlantId = klonId,
            Decision = "Cleared",
            TargetGrowId = _growId,
        });

        Assert.IsType<BadRequestObjectResult>(antwort.Result);
        Assert.Equal(Toepfe, _repository.GetPlantsByGrow(_growId).Count);
    }

    /// <summary>
    /// Die Zahl am Grow folgt den erfassten Pflanzen — sonst gibt es zwei.
    /// </summary>
    [Fact]
    public void DiePflanzenzahlAmGrowFolgtDenErfasstenPflanzen()
    {
        // Der gemeldete Screenshot: Kachel „Pflanzen 6", darunter acht Zeilen.
        var grow = _repository.GetGrow(_growId)!;
        grow.PlantCount = 6;
        _repository.UpdateGrow(grow);

        ToepfeFuellen();
        Assert.Equal(Toepfe, _repository.GetGrow(_growId)!.PlantCount);

        var eine = _repository.GetPlantsByGrow(_growId).First();
        _controller.Delete(eine.Id);
        Assert.Equal(Toepfe - 1, _repository.GetGrow(_growId)!.PlantCount);
    }

    [Fact]
    public void EineMutterMitStecklingenBleibtStehen()
    {
        var mutter = Assert.IsType<CreatedAtActionResult>(Anlegen("Mutter", 1).Result);
        var mutterId = Assert.IsType<PlantInstanceDto>(mutter.Value).Id;

        _controller.Create(new CreatePlantInstanceRequest
        {
            GrowId = _growId,
            Label = "Steckling",
            SiteIndex = 2,
            ParentPlantId = mutterId,
            PlantRole = PlantRole.Clone,
            PlantStatus = PlantStatus.Active,
        });

        var antwort = _controller.Delete(mutterId);

        Assert.IsType<BadRequestObjectResult>(antwort);
        Assert.NotNull(_repository.GetPlant(mutterId));
    }
}
