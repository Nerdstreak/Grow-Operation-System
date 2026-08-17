using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// Ein Pheno-Hunt vergleicht Phänotypen, keine Genetiken.
/// </summary>
/// <remarks>
/// <para>Der Fund kam vom Tester: sechs Pflanzen aus drei Sorten in einem
/// Zelt, und der Hunt warf sie zusammen. Das ist nicht nur eine schiefe
/// Beschriftung — Ertrag und Wirkstoff werden RELATIV gerechnet (beste
/// Pflanze 1, schwächste 0). Über Sortengrenzen hinweg bewertet das die
/// Genetik statt den Phänotyp: eine Sorte mit von Haus aus weniger THC
/// bekäme die 0, ohne dass ihr bester Phänotyp etwas dafür kann. Wer danach
/// seinen Keeper wählt, wirft womöglich genau die Pflanze weg, die er
/// behalten wollte.</para>
/// </remarks>
public sealed class PhenoHuntPerStrainTests : IDisposable
{
    private readonly string _temp;
    private readonly GrowRepository _repository;
    private readonly PhenoRepository _pheno;
    private readonly PhenoApiController _controller;
    private readonly int _growId;

    public PhenoHuntPerStrainTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "PhenoHunt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
        var paths = new AppPaths(_temp);
        var tent = TestDatabase.InitializeWithDefaultTent(paths);
        _repository = new GrowRepository(paths);
        _pheno = new PhenoRepository(paths);
        _controller = new PhenoApiController(_repository, _pheno);

        _growId = _repository.CreateGrow(new GrowRun
        {
            TentId = tent.Id,
            Name = "Mehrsorten-Zelt",
            StartDate = new DateTime(2026, 6, 26),
            Status = GrowStatus.Running,
        });
    }

    public void Dispose()
    {
        try { Directory.Delete(_temp, recursive: true); } catch { }
    }

    private int Pflanze(string label, int? strainId, double dryYieldG)
    {
        var plant = _repository.CreatePlant(new PlantInstance
        {
            GrowId = _growId,
            StrainId = strainId,
            Label = label,
            PlantRole = PlantRole.Production,
            PlantStatus = PlantStatus.Active,
        });
        _pheno.Save(new PhenoEvaluation { PlantInstanceId = plant.Id, DryYieldG = dryYieldG });
        return plant.Id;
    }

    [Fact]
    public void EachStrainIsScoredAgainstItsOwnSiblings()
    {
        var starkeSorte = _repository.CreateStrain(new Strain { Name = "RS11 x Banana OG" }).Id;
        var zarteSorte = _repository.CreateStrain(new Strain { Name = "Pineapple Express" }).Id;

        // Die zarte Sorte traegt weniger — aber innerhalb ihrer Sorte gibt es
        // eine klar bessere und eine klar schlechtere Pflanze.
        _ = Pflanze("Stark 1", starkeSorte, 180);
        _ = Pflanze("Stark 2", starkeSorte, 120);
        var zartBesser = Pflanze("Zart 1", zarteSorte, 90);
        var zartSchwach = Pflanze("Zart 2", zarteSorte, 60);

        var ok = Assert.IsType<OkObjectResult>(_controller.Hunt(_growId).Result);
        var hunt = Assert.IsType<PhenoHuntDto>(ok.Value);

        var besser = hunt.Plants.Single(p => p.PlantInstanceId == zartBesser);
        var schwach = hunt.Plants.Single(p => p.PlantInstanceId == zartSchwach);

        // Der Kern: die beste Pflanze IHRER Sorte bekommt die volle Ertragsnote
        // (die Noten laufen von 0 bis 10), obwohl sie weniger traegt als jede
        // Pflanze der starken Sorte. Vorher wurde sie gegen 180 g normiert und
        // landete im Mittelfeld.
        Assert.Equal(10.0, besser.Score.Yield);
        Assert.Equal(0.0, schwach.Score.Yield);
    }

    [Fact]
    public void PlantsWithoutAStrainStayAmongThemselves()
    {
        var sorte = _repository.CreateStrain(new Strain { Name = "Mimosa EVO" }).Id;
        _ = Pflanze("Benannt", sorte, 200);
        var ohneA = Pflanze("Ohne A", null, 100);
        var ohneB = Pflanze("Ohne B", null, 50);

        var ok = Assert.IsType<OkObjectResult>(_controller.Hunt(_growId).Result);
        var hunt = Assert.IsType<PhenoHuntDto>(ok.Value);

        // Auch „ohne Sorte" ist eine eigene Gruppe — sonst waeren sie an der
        // benannten Sorte gemessen, und das ist dieselbe Vermischung.
        Assert.Equal(10.0, hunt.Plants.Single(p => p.PlantInstanceId == ohneA).Score.Yield);
        Assert.Equal(0.0, hunt.Plants.Single(p => p.PlantInstanceId == ohneB).Score.Yield);
    }

    [Fact]
    public void ASingleSortedGrowIsUnchanged()
    {
        // Der Normalfall darf sich nicht aendern: eine Sorte, alle Pflanzen
        // Geschwister — genau wie vorher.
        var sorte = _repository.CreateStrain(new Strain { Name = "Nur eine" }).Id;
        var beste = Pflanze("A", sorte, 150);
        var schwaechste = Pflanze("B", sorte, 50);

        var ok = Assert.IsType<OkObjectResult>(_controller.Hunt(_growId).Result);
        var hunt = Assert.IsType<PhenoHuntDto>(ok.Value);

        Assert.Equal(10.0, hunt.Plants.Single(p => p.PlantInstanceId == beste).Score.Yield);
        Assert.Equal(0.0, hunt.Plants.Single(p => p.PlantInstanceId == schwaechste).Score.Yield);
    }
}
