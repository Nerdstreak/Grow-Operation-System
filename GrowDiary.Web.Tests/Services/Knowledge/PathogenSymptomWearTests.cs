using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services.Knowledge;

public sealed class PathogenSymptomWearTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly KnowledgeBaseLoader _loader;

    private static readonly string ProjectRoot = FindProjectRoot();

    public PathogenSymptomWearTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PSWTest_" + Guid.NewGuid().ToString("N"));
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

    // ─── PATHOGEN TESTS ────────────────────────────────────────────────────────

    [Fact]
    public void AllPathogens_HaveValidSchema()
    {
        foreach (var p in _loader.Pathogens)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.SchemaVersion),
                $"{p.Id}: schemaVersion fehlt");
            Assert.True(p.SchemaVersion == "1.0",
                $"{p.Id}: schemaVersion sollte '1.0' sein");
            Assert.False(string.IsNullOrWhiteSpace(p.Id),
                "Ein Pathogen hat leere ID");
            Assert.False(string.IsNullOrWhiteSpace(p.Name),
                $"{p.Id}: name fehlt");
            Assert.False(string.IsNullOrWhiteSpace(p.Category),
                $"{p.Id}: category fehlt");
            Assert.False(string.IsNullOrWhiteSpace(p.RiskLevel),
                $"{p.Id}: riskLevel fehlt");
        }
    }

    [Fact]
    public void AllPathogens_HaveUniqueIds()
    {
        var duplicates = _loader.Pathogens
            .GroupBy(p => p.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void AllPathogens_HaveAtLeastOneSource()
    {
        foreach (var p in _loader.Pathogens)
            Assert.True(p.Sources.Count > 0,
                $"{p.Id}: mindestens eine Quelle erforderlich");
    }

    [Fact]
    public void AllPathogens_TreatmentSopIds_PointToExistingSops()
    {
        var sopIds = _loader.Sops.Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var p in _loader.Pathogens)
        {
            if (!string.IsNullOrWhiteSpace(p.TreatmentSopId))
                Assert.True(sopIds.Contains(p.TreatmentSopId),
                    $"{p.Id}: treatmentSopId '{p.TreatmentSopId}' zeigt auf unbekannte SOP");

            if (!string.IsNullOrWhiteSpace(p.PreventiveSopId))
                Assert.True(sopIds.Contains(p.PreventiveSopId),
                    $"{p.Id}: preventiveSopId '{p.PreventiveSopId}' zeigt auf unbekannte SOP");
        }
    }

    // ─── SYMPTOM TESTS ─────────────────────────────────────────────────────────

    [Fact]
    public void AllSymptoms_HaveValidSchema()
    {
        foreach (var s in _loader.Symptoms)
        {
            Assert.False(string.IsNullOrWhiteSpace(s.SchemaVersion),
                $"{s.Id}: schemaVersion fehlt");
            Assert.True(s.SchemaVersion == "1.0",
                $"{s.Id}: schemaVersion sollte '1.0' sein");
            Assert.False(string.IsNullOrWhiteSpace(s.Id),
                "Ein Symptom hat leere ID");
            Assert.False(string.IsNullOrWhiteSpace(s.Name),
                $"{s.Id}: name fehlt");
            Assert.False(string.IsNullOrWhiteSpace(s.Category),
                $"{s.Id}: category fehlt");
        }
    }

    [Fact]
    public void AllSymptoms_HaveUniqueIds()
    {
        var duplicates = _loader.Symptoms
            .GroupBy(s => s.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void AllSymptoms_TreatmentReferences_PointToExistingTreatments()
    {
        var treatmentIds = _loader.Treatments
            .Select(t => t.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var sym in _loader.Symptoms)
        {
            foreach (var tid in sym.SuggestedTreatmentIds)
                Assert.True(treatmentIds.Contains(tid),
                    $"{sym.Id}: suggestedTreatmentId '{tid}' zeigt auf unbekanntes Treatment");
        }
    }

    [Fact]
    public void AllSymptoms_SopReferences_PointToExistingSops()
    {
        var sopIds = _loader.Sops
            .Select(s => s.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var sym in _loader.Symptoms)
        {
            foreach (var sid in sym.SuggestedSopIds)
                Assert.True(sopIds.Contains(sid),
                    $"{sym.Id}: suggestedSopId '{sid}' zeigt auf unbekannte SOP");
        }
    }

    // ─── WEAR TEMPLATE TESTS ───────────────────────────────────────────────────

    [Fact]
    public void AllWearTemplates_HaveValidSchema()
    {
        foreach (var w in _loader.WearTemplates)
        {
            Assert.False(string.IsNullOrWhiteSpace(w.SchemaVersion),
                $"{w.Id}: schemaVersion fehlt");
            Assert.True(w.SchemaVersion == "1.0",
                $"{w.Id}: schemaVersion sollte '1.0' sein");
            Assert.False(string.IsNullOrWhiteSpace(w.Id),
                "Ein Wear-Template hat leere ID");
            Assert.False(string.IsNullOrWhiteSpace(w.Name),
                $"{w.Id}: name fehlt");
            Assert.False(string.IsNullOrWhiteSpace(w.Category),
                $"{w.Id}: category fehlt");
        }
    }

    [Fact]
    public void AllWearTemplates_HaveUniqueIds()
    {
        var duplicates = _loader.WearTemplates
            .GroupBy(w => w.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void AllWearTemplates_HaveLifespanGreaterZero()
    {
        foreach (var w in _loader.WearTemplates)
            Assert.True(w.ExpectedLifespanDays > 0,
                $"{w.Id}: expectedLifespanDays muss > 0 sein");
    }

    // ─── COUNT TESTS ───────────────────────────────────────────────────────────

    [Fact]
    public void KnowledgeBaseLoader_LoadsAllPathogens()
    {
        Assert.Equal(8, _loader.Pathogens.Count);
    }

    [Fact]
    public void KnowledgeBaseLoader_LoadsAllSymptoms()
    {
        Assert.True(_loader.Symptoms.Count >= 20,
            $"Erwartet mindestens 20 Symptome, geladen: {_loader.Symptoms.Count}");
    }

    [Fact]
    public void KnowledgeBaseLoader_LoadsAllWearTemplates()
    {
        Assert.Equal(12, _loader.WearTemplates.Count);
    }
}
