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
    public void EachPlantIsWorkedThroughCompletely_BeforeTheNextOneIsTouched()
    {
        // The block repeats, not the individual step. Lifting all six plants out first and
        // disinfecting afterwards would carry the pathogen straight across the batch — the
        // one thing SOP-S1 exists to prevent. A first version grouped by step and produced
        // exactly that, which only showed up when the steps were read in order.
        var planned = SopStepPlanner.Plan(
            RootRot(),
            new Dictionary<string, string> { ["severity"] = "severe" },
            new Dictionary<string, int> { ["plant"] = 3 });

        // The SOP has several repeat blocks (triage, treatment, follow-up inspection), each
        // cycling through the plants, so the subject legitimately returns to "1 von 3" more
        // than once. What must hold is the order inside a block.
        var titles = planned.Select(step => $"{step.Subject}|{step.Step.Title}").ToList();
        var firstDisinfect = titles.FindIndex(t => t.StartsWith("Pflanze 1 von 3") && t.Contains("desinfizieren"));
        var secondLift = titles.FindIndex(t => t.StartsWith("Pflanze 2 von 3") && t.Contains("entnehmen"));

        Assert.True(firstDisinfect >= 0 && secondLift > firstDisinfect,
            "Die Zwischendesinfektion muss vor der nächsten Pflanze liegen.");

        // Plant 1 is finished — right through to the quarantine container — before plant 2
        // is lifted out at all.
        var firstQuarantine = titles.FindIndex(t => t.StartsWith("Pflanze 1 von 3") && t.Contains("Quarantänebehälter"));
        Assert.True(firstQuarantine >= 0 && secondLift > firstQuarantine,
            "Pflanze 1 muss vollständig behandelt sein, bevor Pflanze 2 angefasst wird.");
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

    private SopDefinition Quarantine() =>
        Assert.Single(_loader.Sops, sop => sop.Id == "cuttings-quarantine");

    [Fact]
    public void BareRootCuttings_SkipTheWholeSubstrateSection()
    {
        // SOP-C1 §2 is about the carrier. A cutting from a DWC cloner has none, so telling
        // someone to dip a plug they don't have is worse than saying nothing.
        var planned = SopStepPlanner.Plan(
            Quarantine(),
            new Dictionary<string, string> { ["substrate"] = "none", ["decontamination"] = "hocl" });
        var titles = planned.Select(step => step.Step.Title).ToList();

        Assert.DoesNotContain(titles, title => title.Contains("Substrat spülen"));
        Assert.DoesNotContain(titles, title => title.Contains("Substrat dekontaminieren"));
        Assert.DoesNotContain(titles, title => title.Contains("Jiffy"));
        Assert.Contains(titles, title => title.Contains("Bad 1"));
    }

    [Fact]
    public void TheDecontaminationNeedsBothAnswers_NotJustTheAgent()
    {
        // Two conditions on one step: the agent AND there being a carrier at all. With a
        // single key the HOCl dip would have shown up for bare-root cuttings.
        var withCarrier = SopStepPlanner.Plan(
            Quarantine(),
            new Dictionary<string, string> { ["substrate"] = "rockwool", ["decontamination"] = "hocl" });

        Assert.Contains(withCarrier, step => step.Step.Title.Contains("dekontaminieren: HOCl"));
        Assert.DoesNotContain(withCarrier, step => step.Step.Title.Contains("dekontaminieren: H₂O₂"));
    }

    [Fact]
    public void OnlyJiffiesGetPressedOut()
    {
        var jiffy = SopStepPlanner.Plan(Quarantine(), new Dictionary<string, string> { ["substrate"] = "jiffy" });
        var rockwool = SopStepPlanner.Plan(Quarantine(), new Dictionary<string, string> { ["substrate"] = "rockwool" });

        Assert.Contains(jiffy, step => step.Step.Title.Contains("Jiffy sanft auspressen"));
        Assert.DoesNotContain(rockwool, step => step.Step.Title.Contains("Jiffy sanft auspressen"));
    }

    [Fact]
    public void TheThreeBathMethod_RunsAsABlockPerCutting()
    {
        // SOP-C1 §3: bath 1, 2, 3 in order for one cutting, then the next — not all of
        // bath 1 first, which would put a treated cutting back into a used insecticide bath.
        var planned = SopStepPlanner.Plan(
            Quarantine(),
            new Dictionary<string, string> { ["substrate"] = "none" },
            new Dictionary<string, int> { ["cutting"] = 2 });

        var titles = planned.Select(step => $"{step.Subject}|{step.Step.Title}").ToList();
        var firstThird = titles.FindIndex(t => t.StartsWith("Steckling 1 von 2") && t.Contains("Bad 3"));
        var secondFirst = titles.FindIndex(t => t.StartsWith("Steckling 2 von 2") && t.Contains("Bad 1"));

        Assert.True(firstThird >= 0 && secondFirst > firstThird,
            "Steckling 1 muss alle drei Bäder durchlaufen haben, bevor Steckling 2 beginnt.");
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
