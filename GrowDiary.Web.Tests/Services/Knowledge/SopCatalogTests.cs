using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services.Knowledge;

public sealed class SopCatalogTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly KnowledgeBaseLoader _loader;

    private static readonly string ProjectRoot = FindProjectRoot();

    private static readonly HashSet<string> ValidStepTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Action", "Measurement", "Wait", "Confirmation", "Photo", "SubSop"
    };

    public SopCatalogTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "SopCatalog_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        CopyDefaults(
            Path.Combine(ProjectRoot, "GrowDiary.Web", "wwwroot", "knowledge-defaults"),
            _tempRoot);

        var paths = new AppPaths(_tempRoot);
        _loader = new KnowledgeBaseLoader(paths, NullLogger<KnowledgeBaseLoader>.Instance);
        _loader.Initialize();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.sln").Length > 0 ||
                Directory.Exists(Path.Combine(dir, "GrowDiary.Web")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Project root not found");
    }

    private static void CopyDefaults(string source, string tempRoot)
    {
        var dest = Path.Combine(tempRoot, "wwwroot", "knowledge-defaults");
        foreach (var file in Directory.EnumerateFiles(source, "*.json", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, file);
            var target = Path.Combine(dest, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    [Fact]
    public void AllSops_HaveValidSchema()
    {
        foreach (var sop in _loader.Sops)
        {
            Assert.False(string.IsNullOrWhiteSpace(sop.SchemaVersion),
                $"{sop.Id}: schemaVersion fehlt");
            Assert.True(sop.SchemaVersion == "1.0",
                $"{sop.Id}: schemaVersion sollte '1.0' sein");
            Assert.False(string.IsNullOrWhiteSpace(sop.Id),
                "Eine SOP hat leere ID");
            Assert.False(string.IsNullOrWhiteSpace(sop.Name),
                $"{sop.Id}: name fehlt");
            Assert.True(
                sop.Type == "Linear" || sop.Type == "MultiDay" || sop.Type == "Recurring",
                $"{sop.Id}: type muss Linear, MultiDay oder Recurring sein, ist aber '{sop.Type}'");
        }
    }

    [Fact]
    public void AllSops_HaveUniqueIds()
    {
        var ids = _loader.Sops.Select(s => s.Id).ToList();
        var duplicates = ids.GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void AllSops_HaveAtLeastOneSource()
    {
        foreach (var sop in _loader.Sops)
            Assert.True(sop.Sources.Count > 0,
                $"{sop.Id}: mindestens eine Quelle erforderlich");
    }

    [Fact]
    public void AllSops_SourceUrlsExistAsFiles_OrAreNull()
    {
        var docsPath = Path.Combine(ProjectRoot, "GrowDiary.Web", "wwwroot", "docs");

        foreach (var sop in _loader.Sops)
        {
            foreach (var source in sop.Sources)
            {
                if (source.Url == null) continue;
                if (!source.Url.StartsWith("/docs/")) continue;

                var filename = source.Url["/docs/".Length..];
                var fullPath = Path.Combine(docsPath, filename);

                Assert.True(File.Exists(fullPath),
                    $"{sop.Id}: source URL '{source.Url}' zeigt auf nicht-existierende Datei '{fullPath}'");
            }
        }
    }

    [Fact]
    public void SubSopReferences_PointToExistingSops()
    {
        var allIds = _loader.Sops.Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var sop in _loader.Sops)
        {
            foreach (var step in sop.Steps)
            {
                if (step.SubSopId == null) continue;

                Assert.True(allIds.Contains(step.SubSopId),
                    $"{sop.Id}/{step.Id}: subSopId '{step.SubSopId}' zeigt auf unbekannte SOP");
            }
        }
    }

    [Fact]
    public void AllStepsHaveValidStepType()
    {
        foreach (var sop in _loader.Sops)
        {
            foreach (var step in sop.Steps)
            {
                Assert.True(ValidStepTypes.Contains(step.StepType),
                    $"{sop.Id}/{step.Id}: unbekannter stepType '{step.StepType}'");
            }
        }
    }

    [Fact]
    public void RecurringSops_HaveScheduleTrigger()
    {
        foreach (var sop in _loader.Sops.Where(s => s.Type == "Recurring"))
        {
            Assert.True(sop.Triggers.Any(t => t.Type == "Schedule"),
                $"{sop.Id}: Recurring SOP muss mindestens einen Schedule-Trigger haben");
        }
    }

    [Fact]
    public void MultiDaySops_HaveDurationDays()
    {
        foreach (var sop in _loader.Sops.Where(s => s.Type == "MultiDay"))
        {
            Assert.True(sop.DurationDays.HasValue && sop.DurationDays > 0,
                $"{sop.Id}: MultiDay SOP muss durationDays haben");
        }
    }

    [Fact]
    public void KnowledgeBaseLoader_LoadsAllTenSops()
    {
        Assert.Equal(10, _loader.Sops.Count);
    }

    [Fact]
    public void AllSops_StepsAreOrderedSequentially()
    {
        foreach (var sop in _loader.Sops)
        {
            var orders = sop.Steps.Select(s => s.Order).OrderBy(o => o).ToList();
            for (var i = 0; i < orders.Count; i++)
            {
                Assert.True(orders[i] == i + 1,
                    $"{sop.Id}: Step-Order ist nicht sequenziell (erwartet {i + 1}, gefunden {orders[i]})");
            }
        }
    }
}
