using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Xunit;

namespace GrowDiary.Web.Tests;

/// <summary>
/// Die geplante Veg-Dauer muss die Runde durch die Datenbank überstehen.
///
/// Sie ist die Absicht des Growers („ich will vier Wochen vegetativ fahren"),
/// aus der der Zeitstrahl Flip- und Erntetermin ableitet. Ginge sie beim
/// Speichern verloren, stünde der Strahl wieder ohne Ziel da — und zwar
/// unauffällig, weil alles andere weiter funktioniert.
/// </summary>
public sealed class PlannedVegDaysTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _dbPath;
    private readonly GrowRepository _repository;
    private readonly Tent _tent;

    public PlannedVegDaysTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"planned-veg-{Guid.NewGuid():N}.db");
        _tempRoot = Path.Combine(Path.GetTempPath(), "PlannedVegDaysTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", _dbPath);

        var paths = new AppPaths(_tempRoot);
        _tent = TestDatabase.InitializeWithDefaultTent(paths);
        _repository = new GrowRepository(paths);
    }

    private GrowRun NeuerGrow(string name) => new()
    {
        Name = name,
        TentId = _tent.Id,
        HydroStyle = HydroStyle.RDWC,
        StartDate = DateTime.Today.AddDays(-20),
    };

    [Fact]
    public void Speichert_und_liest_die_geplante_Veg_Dauer()
    {
        var grow = NeuerGrow("Veg-Plan");
        grow.PlannedVegDays = 28;

        var id = _repository.CreateGrow(grow);
        var gelesen = _repository.GetGrow(id);

        Assert.NotNull(gelesen);
        Assert.Equal(28, gelesen!.PlannedVegDays);
    }

    [Fact]
    public void Laesst_die_Dauer_leer_wenn_nicht_geplant_wird()
    {
        var id = _repository.CreateGrow(NeuerGrow("Nach Augenmass"));

        Assert.Null(_repository.GetGrow(id)!.PlannedVegDays);
    }

    [Fact]
    public void Aendert_die_Dauer_beim_Aktualisieren()
    {
        var grow = NeuerGrow("Umgeplant");
        grow.PlannedVegDays = 21;
        var id = _repository.CreateGrow(grow);

        var gespeichert = _repository.GetGrow(id)!;
        gespeichert.PlannedVegDays = 35;
        _repository.UpdateGrow(gespeichert);

        Assert.Equal(35, _repository.GetGrow(id)!.PlannedVegDays);
    }

    [Fact]
    public void Behaelt_die_Planzahlen_in_der_Liste_aller_Grows()
    {
        // Der Zeitstrahl auf Live liest die Übersichtsliste. Fehlt dort die
        // Planung, rechnet er wieder mit dem 8-Wochen-Richtwert und kann keinen
        // geplanten Flip zeigen.
        var grow = NeuerGrow("Uebersicht");
        grow.PlannedVegDays = 30;
        grow.BreederFlowerWeeksMin = 8;
        grow.BreederFlowerWeeksMax = 9;
        var id = _repository.CreateGrow(grow);

        var ausListe = _repository.GetAllGrows().Single(item => item.Id == id);

        Assert.Equal(30, ausListe.PlannedVegDays);
        Assert.Equal(9, ausListe.BreederFlowerWeeksMax);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* Aufräumen ist Kür. */ }
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { /* dito */ }
    }
}
