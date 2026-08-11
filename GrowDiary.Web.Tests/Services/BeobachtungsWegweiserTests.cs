using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Der Weg von „mir gefällt was nicht" ins vorhandene Wissen.
/// </summary>
/// <remarks>
/// <para>Diese Tests laufen gegen die AUSGELIEFERTEN Symptome, nicht gegen
/// erfundene: der ganze Zweck des Wegweisers ist, dass er das echte Wissen
/// erreichbar macht. Ein Test mit Attrappen würde beweisen, dass die Sortierung
/// funktioniert, und nichts darüber sagen, ob am Ende etwas Nützliches
/// steht.</para>
/// </remarks>
public sealed class BeobachtungsWegweiserTests : IDisposable
{
    private readonly string _temp;
    private readonly BeobachtungsWegweiser _wegweiser;

    public BeobachtungsWegweiserTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "Wegweiser_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
        Kopieren(Path.Combine(Wurzel(), "GrowDiary.Web", "wwwroot", "knowledge-defaults"), _temp);

        var loader = new KnowledgeBaseLoader(
            new AppPaths(_temp), NullLogger<KnowledgeBaseLoader>.Instance);
        loader.Initialize();
        _wegweiser = new BeobachtungsWegweiser(loader);
    }

    [Fact]
    public void TheThreePlacesOneLooksAreAllThere()
    {
        var gruppen = _wegweiser.Gruppen();

        Assert.Equal(["Blatt", "Wurzel", "Lösung"], gruppen.Select(g => g.Bereich).ToArray());
        Assert.All(gruppen, gruppe => Assert.NotEmpty(gruppe.Frage));
    }

    [Fact]
    public void ARealObservationCarriesCausesAndSomethingToDo()
    {
        var blatt = _wegweiser.Gruppen().Single(g => g.Bereich == "Blatt");
        var chlorose = blatt.Beobachtungen.Single(b => b.Id == "interveinal-chlorosis");

        Assert.NotEmpty(chlorose.MoeglicheUrsachen);
        Assert.NotEmpty(chlorose.Vorschlaege);
        // Und die Vorschlaege tragen Namen, keine Kennungen.
        Assert.All(chlorose.Vorschlaege, v => Assert.NotEqual(v.Id, v.Name));
    }

    [Fact]
    public void RoutinesAreNotOfferedAsFindings()
    {
        // „Praeventive Routine-Massnahme" und „Steckling bereit fuers
        // Hauptsystem" stehen in denselben Kategorien, sind aber keine Befunde.
        // Wer ein Problem sucht, soll sie nicht durchblaettern.
        var alle = _wegweiser.Gruppen().SelectMany(g => g.Beobachtungen).Select(b => b.Id).ToList();

        Assert.DoesNotContain("routine-prevention", alle);
        Assert.DoesNotContain("calmag-baseline", alle);
        Assert.DoesNotContain("cutting-ready-for-system", alle);
    }

    [Fact]
    public void EveryObservationLeadsSomewhere()
    {
        // Eine Beobachtung ohne jeden Vorschlag waere eine Sackgasse: der
        // Nutzer klickt sich hin und steht vor „ja, und jetzt?".
        var alle = _wegweiser.Gruppen().SelectMany(g => g.Beobachtungen).ToList();

        Assert.NotEmpty(alle);
        Assert.All(alle, b => Assert.NotEmpty(b.Vorschlaege));
        Assert.All(alle, b => Assert.NotEmpty(b.MoeglicheUrsachen));
    }

    [Fact]
    public void TheKnownFindingsFromTheFieldAreReachable()
    {
        var alle = _wegweiser.Gruppen().SelectMany(g => g.Beobachtungen).Select(b => b.Id).ToList();

        // Die drei, die im RDWC wirklich vorkommen und die vorher nur ueber die
        // Volltextsuche zu finden waren.
        Assert.Contains("brown-roots-slimy", alle);
        Assert.Contains("foul-water-smell", alle);
        Assert.Contains("calmag-deficiency", alle);
    }

    private static string Wurzel()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "GrowDiary.slnx")))
        {
            dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("Projektwurzel nicht gefunden.");
        }
        return dir;
    }

    private static void Kopieren(string von, string nach)
    {
        var ziel = Path.Combine(nach, "wwwroot", "knowledge-defaults");
        foreach (var verzeichnis in Directory.GetDirectories(von, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(verzeichnis.Replace(von, ziel));
        }
        Directory.CreateDirectory(ziel);
        foreach (var datei in Directory.GetFiles(von, "*.*", SearchOption.AllDirectories))
        {
            var neu = datei.Replace(von, ziel);
            Directory.CreateDirectory(Path.GetDirectoryName(neu)!);
            File.Copy(datei, neu, overwrite: true);
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(_temp, recursive: true); } catch { /* Aufräumen darf scheitern. */ }
    }
}
