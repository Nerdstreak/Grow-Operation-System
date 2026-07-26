using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Die Messwert-Kacheln zeichnen eine Skala mit Zielband. Dafür muss die Karte
/// den Zielbereich der aktuellen Phase mitbringen — vorher trug sie nur die Zahl.
///
/// Der Zielbereich hing an <c>Tent.ActiveGrows</c>, und diese Liste hatte
/// niemand befüllt: sie war überall leer. Damit blieb nicht nur das Band aus,
/// auch die Alarmzeile des Zelts konnte nie etwas melden. Diese Tests halten
/// beides fest.
/// </summary>
public sealed class GrowDashboardComposerTargetTests
{
    private static GrowDashboardComposer CreateComposer()
    {
        // Die Wissensbasis in ein Temp-Verzeichnis spiegeln, damit der Testlauf
        // nicht ins App_Data des Repos schreibt — dasselbe Vorgehen wie in den
        // uebrigen Tests, die den Loader brauchen.
        var tempRoot = Path.Combine(Path.GetTempPath(), "growos-target-" + Guid.NewGuid().ToString("N"));
        CopyDefaults(Path.Combine(FindProjectRoot(), "GrowDiary.Web", "wwwroot", "knowledge-defaults"), tempRoot);

        var loader = new KnowledgeBaseLoader(new AppPaths(tempRoot), NullLogger<KnowledgeBaseLoader>.Instance);
        loader.Initialize();
        return new GrowDashboardComposer(
            null!, null!, null!, new TargetValueService(loader),
            NullLogger<GrowDashboardComposer>.Instance);
    }

    private static void CopyDefaults(string source, string tempRoot)
    {
        var destination = Path.Combine(tempRoot, "wwwroot", "knowledge-defaults");
        foreach (var file in Directory.EnumerateFiles(source, "*.json", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static Tent TentWithGrow(GrowStyleFixture fixture) => new()
    {
        Id = 1,
        Name = "Zelt",
        ActiveGrows =
        [
            new GrowRun
            {
                Id = 1,
                Name = "Testlauf",
                HydroStyle = fixture.Style,
                Status = GrowStatus.Running,
            },
        ],
    };

    private static Measurement MeasurementAt(GrowStage stage) => new()
    {
        Id = 1,
        GrowId = 1,
        TakenAt = new DateTime(2026, 7, 26, 9, 0, 0, DateTimeKind.Utc),
        Stage = stage,
        ReservoirPh = 6.0,
        ReservoirEc = 1.6,
        AirTemperatureC = 24.0,
        HumidityPercent = 60,
    };

    public sealed record GrowStyleFixture(HydroStyle Style);

    [Fact]
    public void ReservoirTiles_CarryTheTargetRangeOfTheCurrentStage()
    {
        var composer = CreateComposer();
        var tent = TentWithGrow(new GrowStyleFixture(HydroStyle.RDWC));

        var cards = composer.BuildTentMetrics(tent, [], [MeasurementAt(GrowStage.Flower)]);

        var ph = cards.Single(card => card.Key == "reservoir-ph");
        Assert.NotNull(ph.TargetMin);
        Assert.NotNull(ph.TargetMax);
        Assert.True(ph.TargetMin < ph.TargetMax, "Der Zielbereich muss eine Spanne sein.");
        Assert.Equal(6.0, ph.NumericValue);
    }

    [Fact]
    public void WithoutAnActiveGrow_ThereIsNoTarget()
    {
        // Ein leeres Zelt hat kein Ziel — die Kachel zeigt dann nur den Wert,
        // statt einen Bereich zu erfinden.
        var composer = CreateComposer();
        var tent = new Tent { Id = 1, Name = "Leer", ActiveGrows = [] };

        var cards = composer.BuildTentMetrics(tent, [], [MeasurementAt(GrowStage.Flower)]);

        var ph = cards.Single(card => card.Key == "reservoir-ph");
        Assert.Null(ph.TargetMin);
        Assert.Null(ph.TargetMax);
    }

    [Fact]
    public void WithoutAMeasurement_ThereIsNoTarget()
    {
        // Ohne Messung ist die Phase unbekannt, und ein Zielbereich ohne Phase
        // wäre geraten. Dieselbe Regel benutzt die Abweichungsanalyse.
        var composer = CreateComposer();
        var tent = TentWithGrow(new GrowStyleFixture(HydroStyle.RDWC));

        var cards = composer.BuildTentMetrics(tent, [], []);

        var ph = cards.SingleOrDefault(card => card.Key == "reservoir-ph");
        if (ph is not null)
        {
            Assert.Null(ph.TargetMin);
        }
    }

    [Fact]
    public void DwcGetsAHigherEcTargetThanRdwc()
    {
        // DWC hat weniger Puffervolumen; der Aufschlag steht in TargetValueService
        // und muss auch auf der Kachel ankommen, nicht nur in der Analyse.
        var composer = CreateComposer();
        var measurement = MeasurementAt(GrowStage.Flower);

        var rdwc = composer.BuildTentMetrics(TentWithGrow(new GrowStyleFixture(HydroStyle.RDWC)), [], [measurement])
            .Single(card => card.Key == "reservoir-ec");
        var dwc = composer.BuildTentMetrics(TentWithGrow(new GrowStyleFixture(HydroStyle.DWC)), [], [measurement])
            .Single(card => card.Key == "reservoir-ec");

        Assert.NotNull(rdwc.TargetMax);
        Assert.NotNull(dwc.TargetMax);
        Assert.True(dwc.TargetMax > rdwc.TargetMax);
    }

    [Fact]
    public void ReservoirTiles_AppearForAnActiveGrow_EvenWithoutSensorOrMeasurement()
    {
        // Diese Bedingung war jahrelang tot, weil Tent.ActiveGrows nie gefuellt
        // wurde. Seit sie es ist, entscheidet sie ueber fuenf Kacheln — also
        // gehoert sie festgehalten, statt sie beim naechsten Umbau erneut
        // stillzulegen.
        var composer = CreateComposer();
        var tent = TentWithGrow(new GrowStyleFixture(HydroStyle.RDWC));

        var keys = composer.BuildTentMetrics(tent, [], []).Select(card => card.Key).ToList();

        Assert.Contains("reservoir-ph", keys);
        Assert.Contains("reservoir-ec", keys);
        Assert.Contains("orp", keys);
        Assert.Contains("dissolved-oxygen", keys);
    }

    [Fact]
    public void WithoutAnyGrow_TheReservoirTilesStayAway()
    {
        // Ein leeres Zelt zeigt keine Reservoirwerte — sonst staenden auf jedem
        // frisch angelegten Zelt fuenf Kacheln mit „–".
        var composer = CreateComposer();
        var tent = new Tent { Id = 1, Name = "Leer", ActiveGrows = [] };

        var keys = composer.BuildTentMetrics(tent, [], []).Select(card => card.Key).ToList();

        Assert.DoesNotContain("reservoir-ph", keys);
        Assert.DoesNotContain("dissolved-oxygen", keys);
    }

    private static string FindProjectRoot()
    {
        // Die Projektmappe heisst GrowDiary.slnx — auf "*.sln" zu pruefen findet
        // hier nichts. Deshalb am Projektordner festmachen.
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory, "GrowDiary.Web")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("Projektwurzel nicht gefunden.");
    }
}
