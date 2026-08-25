using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// Was der Nutzer anlegt, kann er auch wieder entfernen.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (25.08.2026).</b> „CRUD ist grundlegend und das
/// befolgst du nicht." <see cref="CrudVollstaendigTests"/> hat daraufhin
/// gezählt: <b>neun</b> Controller legten etwas an, das niemand wieder
/// loswird. Diese Klasse hält je Fall fest, dass es geht — und woran es
/// <i>nicht</i> gehen darf.</para>
///
/// <para><b>Ein Löschweg ohne Wächter ist ein neuer Fehler.</b> Die Datenbank
/// räumt beim Löschen still auf (<c>ON DELETE SET NULL</c> bzw.
/// <c>CASCADE</c>). Eine Sorte zu entfernen, die vier Pflanzen führen, nähme
/// diesen Pflanzen wortlos ihre Sorte — genau die Klasse „stiller
/// Datenverlust", die hier schon zweimal zugeschlagen hat. Deshalb steht zu
/// jedem Löschweg auch der Fall, in dem er sich weigert.</para>
/// </remarks>
public sealed class LoeschwegeTests : IDisposable
{
    private readonly string _temp;
    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;
    private readonly Tent _tent;

    public LoeschwegeTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "Loeschwege_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
        _paths = new AppPaths(_temp);
        _tent = TestDatabase.InitializeWithDefaultTent(_paths);
        _repository = new GrowRepository(_paths);
    }

    public void Dispose()
    {
        try { Directory.Delete(_temp, recursive: true); } catch { }
    }

    // ---------------------------------------------------------------- Sorten

    [Fact]
    public void EineSorteLaesstSichEntfernen()
    {
        var controller = new StrainsApiController(_repository);
        var sorte = _repository.CreateStrain(new Strain { Name = "Falsch getippt" });

        Assert.IsType<NoContentResult>(controller.Delete(sorte.Id));
        Assert.Null(_repository.GetStrain(sorte.Id));
    }

    [Fact]
    public void EineSorteInBenutzungBleibtStehen()
    {
        // Sonst nimmt ON DELETE SET NULL vier Pflanzen wortlos ihre Sorte.
        var controller = new StrainsApiController(_repository);
        var sorte = _repository.CreateStrain(new Strain { Name = "In Benutzung" });
        _repository.CreatePlant(new PlantInstance { Label = "Pflanze 1", StrainId = sorte.Id });

        var antwort = controller.Delete(sorte.Id);

        Assert.IsType<BadRequestObjectResult>(antwort);
        Assert.NotNull(_repository.GetStrain(sorte.Id));
    }

    // ---------------------------------------------------------------- Setups

    [Fact]
    public void EinSetupLaesstSichEntfernen()
    {
        var controller = new SetupsApiController(_repository);
        var setup = _repository.CreateSetup(new Setup
        {
            TentId = _tent.Id, Name = "Leerer Bereich", SetupType = SetupType.Production,
        });

        Assert.IsType<NoContentResult>(controller.Delete(setup.Id));
        Assert.Null(_repository.GetSetup(setup.Id));
    }

    [Fact]
    public void EinSetupMitPflanzenBleibtStehen()
    {
        var controller = new SetupsApiController(_repository);
        var setup = _repository.CreateSetup(new Setup
        {
            TentId = _tent.Id, Name = "Bewohnt", SetupType = SetupType.Mother,
        });
        _repository.CreatePlant(new PlantInstance { Label = "Mutter", SetupId = setup.Id });

        Assert.IsType<BadRequestObjectResult>(controller.Delete(setup.Id));
        Assert.NotNull(_repository.GetSetup(setup.Id));
    }

    // ----------------------------------------------------------- Lichtplaene

    [Fact]
    public void EinLichtplanLaesstSichEntfernen()
    {
        var controller = new LightSchedulesApiController(_repository);
        var erster = _repository.CreateLightSchedule(new LightSchedule
        {
            TentId = _tent.Id, Name = "Veg 18/6", LightsOnTime = "06:00", LightsOffTime = "00:00",
        });
        var zweiter = _repository.CreateLightSchedule(new LightSchedule
        {
            TentId = _tent.Id, Name = "Versehentlich", LightsOnTime = "07:00", LightsOffTime = "19:00",
        });

        Assert.IsType<NoContentResult>(controller.Delete(zweiter.Id));
        Assert.Single(_repository.GetLightSchedulesByTent(_tent.Id));
        Assert.Equal(erster.Id, _repository.GetLightSchedulesByTent(_tent.Id)[0].Id);
    }

    [Fact]
    public void DerLetzteLichtplanEinesZeltsBleibtStehen()
    {
        // Am Lichtplan haengen die Nachtabsenkung, der Waechter gegen
        // Lichteinbruch und die Auto-Messungen. Ein Zelt ohne Plan verliert
        // alle drei still.
        var controller = new LightSchedulesApiController(_repository);
        var einziger = _repository.CreateLightSchedule(new LightSchedule
        {
            TentId = _tent.Id, Name = "Bluete 12/12", LightsOnTime = "06:00", LightsOffTime = "18:00",
        });

        Assert.IsType<BadRequestObjectResult>(controller.Delete(einziger.Id));
        Assert.NotNull(_repository.GetLightSchedule(einziger.Id));
    }

    // -------------------------------------------------------- Wartung & Kalibrierung

    [Fact]
    public void EineFalschEingetrageneKalibrierungLaesstSichEntfernen()
    {
        var controller = new CalibrationEventsApiController(_repository);
        var geraet = _repository.CreateHardwareItem(new HardwareItem
        {
            TentId = _tent.Id, Name = "pH-Sonde", Category = "Sensor",
        });
        var ereignis = _repository.CreateCalibrationEvent(new CalibrationEvent
        {
            HardwareItemId = geraet.Id, Title = "pH-Kalibrierung", CalibrationType = CalibrationEventType.Ph,
        });

        Assert.IsType<NoContentResult>(controller.Delete(ereignis.Id));
        Assert.Null(_repository.GetCalibrationEvent(ereignis.Id));
    }

    [Fact]
    public void EinFalschEingetragenerWartungseintragLaesstSichEntfernen()
    {
        var controller = new MaintenanceEventsApiController(_repository);
        var geraet = _repository.CreateHardwareItem(new HardwareItem
        {
            TentId = _tent.Id, Name = "Umwaelzpumpe", Category = "Pump",
        });
        var ereignis = _repository.CreateMaintenanceEvent(new MaintenanceEvent
        {
            HardwareItemId = geraet.Id, Title = "Filter wechseln", EventType = MaintenanceEventType.Inspection,
        });

        Assert.IsType<NoContentResult>(controller.Delete(ereignis.Id));
        Assert.Null(_repository.GetMaintenanceEvent(ereignis.Id));
    }
    // -------------------------------------------------------------- Journal

    [Fact]
    public void EinJournaleintragLaesstSichEntfernen()
    {
        // Ein Journal ist ein Tagebuch, kein Gesetzblatt: wer den falschen
        // Grow erwischt oder sich vertippt, muss den Eintrag loswerden.
        var journal = new JournalRepository(_paths);
        var controller = new JournalApiController(
            _repository, journal, new AuditRepository(_paths));

        var growId = _repository.CreateGrow(new GrowRun
        {
            TentId = _tent.Id, Name = "Lauf", StartDate = new DateTime(2026, 5, 1),
        });
        var eintragId = journal.Create(new JournalEntry
        {
            GrowId = growId, Title = "Versehentlich", Body = "gehoert hier nicht hin",
        });

        Assert.IsType<NoContentResult>(controller.DeleteEntry(eintragId));
        Assert.Null(journal.Get(eintragId));
    }

    // ------------------------------------------------------ Auto-Messungen

    [Fact]
    public void EineAutoMessungLaesstSichEntfernen()
    {
        var controller = new AutoMeasurementsApiController(_repository);
        var growId = _repository.CreateGrow(new GrowRun
        {
            TentId = _tent.Id, Name = "Lauf", StartDate = new DateTime(2026, 5, 1),
        });
        var config = _repository.CreateAutoMeasurementConfig(new AutoMeasurementConfig
        {
            GrowId = growId,
            TentId = _tent.Id,
            Name = "Versehentlich",
            TriggerKind = AutoMeasurementTriggerKind.LightOnDelay,
            DelayMinutes = 30,
        });

        Assert.IsType<NoContentResult>(controller.DeleteConfig(config.Id));
        Assert.Null(_repository.GetAutoMeasurementConfig(config.Id));
    }


    // ------------------------------------- Verweise ohne Fremdschluessel

    /// <summary>
    /// Ein Waechter muss ALLE Verweise sehen, nicht nur die mit Fremdschluessel.
    /// </summary>
    /// <remarks>
    /// <para><b>Gefunden vom Pruefer (25.08.2026), am laufenden Stand
    /// nachgestellt.</b> Die erste Fassung der Waechter zaehlte nur, was die
    /// Datenbank ohnehin absichert. <c>HardwareItems.SetupId</c>,
    /// <c>Grows.SetupId</c> und <c>Grows.StrainId</c> haben aber
    /// <b>gar keinen</b> Fremdschluessel — dort greift nicht einmal
    /// <c>ON DELETE SET NULL</c>. Der Verweis blieb stehen und zeigte ins
    /// Leere.</para>
    ///
    /// <para><b>Die Folge war schlimmer als ein leeres Feld:</b>
    /// <c>GrowsApiController</c> lehnt beim Speichern mit
    /// <c>Setup mit Id X existiert nicht</c> ab — ein Grow, dessen Setup
    /// geloescht wurde, liess sich danach gar nicht mehr speichern.</para>
    /// </remarks>
    [Fact]
    public void EinSetupMitGeraetenBleibtStehen()
    {
        var controller = new SetupsApiController(_repository);
        var setup = _repository.CreateSetup(new Setup
        {
            TentId = _tent.Id, Name = "Mit Geraet", SetupType = SetupType.Production,
        });
        _repository.CreateHardwareItem(new HardwareItem
        {
            TentId = _tent.Id, SetupId = setup.Id, Name = "pH-Sonde", Category = "Sensor",
        });

        Assert.IsType<BadRequestObjectResult>(controller.Delete(setup.Id));
        Assert.NotNull(_repository.GetSetup(setup.Id));
    }

    [Fact]
    public void EinSetupAnDemEinGrowHaengtBleibtStehen()
    {
        var controller = new SetupsApiController(_repository);
        var setup = _repository.CreateSetup(new Setup
        {
            TentId = _tent.Id, Name = "Mit Grow", SetupType = SetupType.Production,
        });
        _repository.CreateGrow(new GrowRun
        {
            TentId = _tent.Id, SetupId = setup.Id, Name = "Lauf",
            StartDate = new DateTime(2026, 5, 1),
        });

        Assert.IsType<BadRequestObjectResult>(controller.Delete(setup.Id));
        Assert.NotNull(_repository.GetSetup(setup.Id));
    }

    [Fact]
    public void EineSorteAnDerEinGrowHaengtBleibtStehen()
    {
        var controller = new StrainsApiController(_repository);
        var sorte = _repository.CreateStrain(new Strain { Name = "Am Grow" });
        _repository.CreateGrow(new GrowRun
        {
            TentId = _tent.Id, StrainId = sorte.Id, Name = "Lauf",
            StartDate = new DateTime(2026, 5, 1),
        });

        Assert.IsType<BadRequestObjectResult>(controller.Delete(sorte.Id));
        Assert.NotNull(_repository.GetStrain(sorte.Id));
    }

}
