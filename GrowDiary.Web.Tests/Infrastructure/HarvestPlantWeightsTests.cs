using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests.Infrastructure;

/// <summary>
/// Die Einzelgewichte je Pflanze liegen in <c>HarvestEntries.PlantWeightsJson</c>.
///
/// Die Spalte wird an drei Stellen angefasst — Insert, Update, Lesen — und ein
/// vergessener Parameter im Update-Zweig hätte zur Folge, dass die Gewichte beim
/// zweiten Speichern verschwinden, ohne dass irgendetwas es meldet. Genau das
/// prüfen diese Tests.
/// </summary>
public sealed class HarvestPlantWeightsTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly AppPaths _paths;
    private readonly HarvestRepository _repository;
    private readonly int _growId;

    public HarvestPlantWeightsTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "growos-harvest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", Path.Combine(_tempRoot, "test.db"));
        _paths = new AppPaths(_tempRoot);
        TestDatabase.InitializeWithDefaultTent(_paths);
        _repository = new HarvestRepository(_paths);

        // Der Ernteeintrag haengt per Fremdschluessel an einem Grow — ohne ihn
        // scheitert schon das Anlegen.
        _growId = new GrowRepository(_paths).CreateGrow(new GrowRun
        {
            Name = "Testlauf",
            StartDate = new DateTime(2026, 6, 1),
            Status = GrowStatus.Running,
        });
    }

    private const string Weights = """[{"label":"PL-01","wetG":486,"dryG":null}]""";

    [Fact]
    public void PlantWeights_SurviveTheRoundTrip()
    {
        var id = _repository.Create(new HarvestEntry { GrowId = _growId, WetWeightG = 486, PlantWeightsJson = Weights });
        Assert.True(id > 0);

        var loaded = _repository.GetForGrow(_growId);
        Assert.Equal(Weights, loaded?.PlantWeightsJson);
    }

    [Fact]
    public void PlantWeights_SurviveASecondSave()
    {
        // Der eigentliche Grund fuer diesen Test: ein fehlender Parameter im
        // Update-Zweig faellt beim ersten Speichern nicht auf.
        var id = _repository.Create(new HarvestEntry { GrowId = _growId, PlantWeightsJson = Weights });

        const string updated = """[{"label":"PL-01","wetG":486,"dryG":108},{"label":"PL-02","wetG":512,"dryG":113}]""";
        _repository.Update(new HarvestEntry { Id = id, GrowId = _growId, WetWeightG = 998, DryWeightG = 221, PlantWeightsJson = updated });

        var loaded = _repository.GetForGrow(_growId);
        Assert.Equal(updated, loaded?.PlantWeightsJson);
        Assert.Equal(998, loaded?.WetWeightG);
    }

    [Fact]
    public void AnEntryWithoutPlantWeights_StaysNull()
    {
        // Aeltere Ernten haben die Spalte nicht gefuellt; sie muessen weiter
        // lesbar sein, ohne dass irgendwo eine leere Zeichenkette entsteht.
        _repository.Create(new HarvestEntry { GrowId = _growId, WetWeightG = 700 });

        var loaded = _repository.GetForGrow(_growId);
        Assert.Null(loaded?.PlantWeightsJson);
        Assert.Equal(700, loaded?.WetWeightG);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* Aufraeumen ist Kuer */ }
    }
}
