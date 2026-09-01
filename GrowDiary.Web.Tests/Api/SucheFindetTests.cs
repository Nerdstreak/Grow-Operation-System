using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// Die Suche findet, was der Nutzer eintippt.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (01.09.2026).</b> Die Abdeckungsmessung zeigte
/// <c>SearchApiController</c> bei <b>0 %</b> — und in beta.60 war genau dieser
/// Controller geändert worden, damit die Suche auch Sorten findet, die nur an
/// einer Pflanze hängen. Eine Änderung an einer Stelle ohne jede Prüfung.</para>
///
/// <para>Die Suche ist der Weg, den die App selbst für sich vorsieht, seit das
/// Menü nicht mehr durchblätterbar ist: „zwanzig Ziele plus jeder Grow, jedes
/// Zelt, jedes System, jede Sorte". Findet sie nichts, ist die Funktion
/// dahinter unerreichbar.</para>
/// </remarks>
public sealed class SucheFindetTests : IDisposable
{
    private readonly string _wurzel;
    private readonly GrowRepository _grows;
    private readonly SetupRepository _setups;
    private readonly SearchApiController _controller;

    public SucheFindetTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), "Suche_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_wurzel);
        var pfade = new AppPaths(_wurzel);
        TestDatabase.Initialize(pfade);
        _grows = new GrowRepository(pfade);
        _setups = new SetupRepository(pfade);

        var wissen = new KnowledgeBaseLoader(pfade, NullLogger<KnowledgeBaseLoader>.Instance);
        wissen.Initialize();
        _controller = new SearchApiController(_grows, wissen);
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    [Fact]
    public void EinGrowWirdUeberSeinenNamenGefunden()
    {
        var grow = GrowAnlegen("Purple Lemonade RDWC", "Purple Lemonade");

        var treffer = Suche("Lemonade");

        Assert.Contains(treffer, t => t.Route == $"/grows/{grow}");
    }

    /// <summary>
    /// Eine Sorte, die nur an einer PFLANZE hängt, wird gefunden.
    /// </summary>
    /// <remarks>
    /// <para>Das war die Änderung aus beta.60. Ein Grow kann N Sorten führen;
    /// gesucht wurde vorher nur über Name, Hauptsorte und Züchter des Grows.
    /// „Wo steht meine Gorilla Glue" ist aber genau die Frage, die jemand in
    /// ein Suchfeld tippt — und die Antwort war „nichts gefunden".</para>
    /// </remarks>
    [Fact]
    public void EineSorteAnEinerPflanzeWirdGefunden()
    {
        var grow = GrowAnlegen("Mischbecken", "White Widow");
        var gorilla = _setups.CreateStrain(new Strain { Name = "Gorilla Glue #4" });
        _setups.CreatePlant(new PlantInstance
        {
            GrowId = grow,
            StrainId = gorilla.Id,
            SiteIndex = 3,
            Label = "Pflanze 3",
            PlantRole = PlantRole.Production,
            PlantStatus = PlantStatus.Active,
        });

        var treffer = Suche("Gorilla");

        Assert.True(treffer.Any(t => t.Route == $"/grows/{grow}"),
            "Die Sorte haengt an einer Pflanze dieses Grows, die Suche findet ihn aber nicht. "
            + "Wer „wo steht meine Gorilla Glue\" tippt, bekommt „nichts gefunden\".");
    }

    /// <summary>
    /// Und der Untertitel nennt, was wirklich im Becken steht.
    /// </summary>
    [Fact]
    public void DerTrefferNenntAlleSortenDesGrows()
    {
        var grow = GrowAnlegen("Mischbecken", "White Widow");
        var widow = _setups.CreateStrain(new Strain { Name = "White Widow" });
        var gorilla = _setups.CreateStrain(new Strain { Name = "Gorilla Glue #4" });
        Pflanze(grow, widow.Id, 1);
        Pflanze(grow, gorilla.Id, 2);

        var treffer = Suche("Mischbecken").Single(t => t.Route == $"/grows/{grow}");

        Assert.Contains("White Widow", treffer.Subtitle ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("Gorilla Glue", treffer.Subtitle ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void EinZeltWirdGefunden()
    {
        var zelt = _grows.CreateTent(new Tent { Name = "Blütezelt Nord", TentType = TentType.Production });

        Assert.Contains(Suche("Blütezelt"), t => t.Route == $"/zelte/{zelt.Id}");
    }

    /// <summary>
    /// Ein zu kurzer Suchbegriff liefert nichts — statt die halbe App.
    /// </summary>
    /// <remarks>
    /// Bei einem Zeichen träfe „a" auf fast jeden Namen; die Liste wäre wertlos
    /// und die Abfrage teuer.
    /// </remarks>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    [InlineData(null)]
    public void EinZuKurzerBegriffLiefertNichts(string? begriff)
    {
        GrowAnlegen("Alpha", "Amnesia");

        Assert.Empty(Suche(begriff));
    }

    [Fact]
    public void GrossUndKleinschreibungIstEgal()
    {
        var grow = GrowAnlegen("Purple Lemonade RDWC", "Purple Lemonade");

        Assert.Contains(Suche("PURPLE"), t => t.Route == $"/grows/{grow}");
        Assert.Contains(Suche("purple"), t => t.Route == $"/grows/{grow}");
    }

    private IReadOnlyList<SearchHitDto> Suche(string? begriff)
    {
        var antwort = _controller.Search(begriff);
        var wert = (antwort.Result as ObjectResult)?.Value ?? antwort.Value;
        return Assert.IsAssignableFrom<IEnumerable<SearchHitDto>>(wert).ToList();
    }

    private int GrowAnlegen(string name, string sorte)
        => _grows.CreateGrow(new GrowRun
        {
            Name = name,
            Strain = sorte,
            HydroStyle = HydroStyle.RDWC,
            Status = GrowStatus.Running,
            StartDate = DateTime.Today,
        });

    private void Pflanze(int growId, int sorteId, int topf)
        => _setups.CreatePlant(new PlantInstance
        {
            GrowId = growId,
            StrainId = sorteId,
            SiteIndex = topf,
            Label = $"Pflanze {topf}",
            PlantRole = PlantRole.Production,
            PlantStatus = PlantStatus.Active,
        });
}
