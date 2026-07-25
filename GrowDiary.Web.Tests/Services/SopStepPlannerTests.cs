using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using GrowDiary.Web.Services.Knowledge.Schema;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// The source procedures branch and repeat; a flat list cannot express them. These tests
/// pin the two things that make SOP-S1 and SOP-C1 followable rather than readable.
/// </summary>
public sealed class SopStepPlannerTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly KnowledgeBaseLoader _loader;

    public SopStepPlannerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "SopPlan_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        CopyDefaults(Path.Combine(FindProjectRoot(), "GrowDiary.Web", "wwwroot", "knowledge-defaults"), _tempRoot);
        _loader = new KnowledgeBaseLoader(new AppPaths(_tempRoot), NullLogger<KnowledgeBaseLoader>.Instance);
        _loader.Initialize();
    }

    private SopDefinition RootRot() =>
        Assert.Single(_loader.Sops, sop => sop.Id == "root-rot-treatment");

    [Fact]
    public void TheRootRotSop_AsksForTheSeverityBeforeStarting()
    {
        var choices = SopStepPlanner.RequiredChoices(RootRot());

        var severity = Assert.Single(choices, choice => choice.Key == "severity");
        Assert.Contains("light", severity.Options);
        Assert.Contains("severe", severity.Options);
        Assert.False(string.IsNullOrWhiteSpace(severity.Prompt));
    }

    [Fact]
    public void ALightlyAffectedPlant_SkipsTheRootCutAndGetsTheShortRinse()
    {
        // SOP-S1 §5.1: no cutting, 1–2 minutes in the second bath.
        var planned = SopStepPlanner.Plan(RootRot(), new Dictionary<string, string> { ["severity"] = "light" });
        var titles = planned.Select(step => step.Step.Title).ToList();

        Assert.DoesNotContain(titles, title => title.Contains("Faule Wurzeln entfernen"));
        Assert.Contains(titles, title => title.Contains("passiv, 1–2 Minuten"));
        Assert.DoesNotContain(titles, title => title.Contains("aktiv, 180 Sekunden"));
    }

    [Fact]
    public void ASeverelyAffectedPlant_GetsTheCutAndTheLongRinse()
    {
        // SOP-S1 §4.3 + §5.2: cut first, then 180 seconds because the ends are open.
        var planned = SopStepPlanner.Plan(RootRot(), new Dictionary<string, string> { ["severity"] = "severe" });
        var titles = planned.Select(step => step.Step.Title).ToList();

        Assert.Contains(titles, title => title.Contains("Faule Wurzeln entfernen"));
        Assert.Contains(titles, title => title.Contains("aktiv, 180 Sekunden"));
        Assert.DoesNotContain(titles, title => title.Contains("passiv, 1–2 Minuten"));
    }

    [Fact]
    public void TheDisinfectionBetweenPlants_IsRepeatedOncePerPlant()
    {
        // §4.4 is the step that actually stops the pathogen travelling. As one line it reads
        // like advice; it has to be ticked off per plant.
        var planned = SopStepPlanner.Plan(
            RootRot(),
            new Dictionary<string, string> { ["severity"] = "severe" },
            new Dictionary<string, int> { ["plant"] = 6 });

        var disinfect = planned
            .Where(step => step.Step.Title.Contains("Werkzeug und Arbeitsfläche desinfizieren"))
            .ToList();

        Assert.Equal(6, disinfect.Count);
        Assert.Equal("Pflanze 1 von 6", disinfect[0].Subject);
        Assert.Equal("Pflanze 6 von 6", disinfect[5].Subject);
        Assert.All(disinfect, step => Assert.True(step.IsRepeated));
    }

    [Fact]
    public void StepsThatDoNotRepeat_StayASingleOccurrence()
    {
        var planned = SopStepPlanner.Plan(
            RootRot(),
            new Dictionary<string, string> { ["severity"] = "light" },
            new Dictionary<string, int> { ["plant"] = 4 });

        var cleaning = Assert.Single(planned, step => step.Step.StepType == "SubSop");

        Assert.False(cleaning.IsRepeated);
        Assert.Null(cleaning.Subject);
    }

    [Fact]
    public void AnUnansweredQuestion_KeepsEveryStep()
    {
        // Dropping a treatment step because a question was skipped is the worst way to be
        // wrong here — better to show one step too many.
        var planned = SopStepPlanner.Plan(RootRot());
        var titles = planned.Select(step => step.Step.Title).ToList();

        Assert.Contains(titles, title => title.Contains("passiv, 1–2 Minuten"));
        Assert.Contains(titles, title => title.Contains("aktiv, 180 Sekunden"));
    }

    [Fact]
    public void WithoutAPlantCount_TheRepeatedBlockRunsOnce()
    {
        var planned = SopStepPlanner.Plan(RootRot(), new Dictionary<string, string> { ["severity"] = "light" });

        Assert.All(planned, step => Assert.Equal(1, step.OccurrenceCount));
    }

    [Fact]
    public void TheOrderOfTheSourceProcedureIsKept()
    {
        // Rinsing before cutting would defeat the point.
        var planned = SopStepPlanner.Plan(RootRot(), new Dictionary<string, string> { ["severity"] = "severe" });
        var orders = planned.Select(step => step.Step.Order).ToList();

        Assert.Equal(orders.OrderBy(order => order), orders);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* temp dir */ }
    }

    private static void CopyDefaults(string source, string tempRoot)
    {
        var destination = Path.Combine(tempRoot, "wwwroot", "knowledge-defaults");
        foreach (var file in Directory.EnumerateFiles(source, "*.json", SearchOption.AllDirectories))
        {
            var target = file.Replace(source, destination);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string FindProjectRoot()
    {
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
