using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services.Knowledge;

public sealed class TreatmentCatalogTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly KnowledgeBaseLoader _loader;

    private static readonly string ProjectRoot = FindProjectRoot();

    public TreatmentCatalogTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "TreatmentCatalog_" + Guid.NewGuid().ToString("N"));
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
    public void AllTreatments_HaveValidSchema()
    {
        foreach (var t in _loader.Treatments)
        {
            Assert.False(string.IsNullOrWhiteSpace(t.SchemaVersion),
                $"{t.Id}: schemaVersion fehlt");
            Assert.True(t.SchemaVersion == "1.0",
                $"{t.Id}: schemaVersion sollte '1.0' sein");
            Assert.False(string.IsNullOrWhiteSpace(t.Id),
                $"Ein Treatment hat leere ID");
            Assert.False(string.IsNullOrWhiteSpace(t.Name),
                $"{t.Id}: name fehlt");
            Assert.False(string.IsNullOrWhiteSpace(t.Type),
                $"{t.Id}: type fehlt");
            Assert.False(string.IsNullOrWhiteSpace(t.Difficulty),
                $"{t.Id}: difficulty fehlt");
            Assert.False(string.IsNullOrWhiteSpace(t.ExpectedTimeToEffect),
                $"{t.Id}: expectedTimeToEffect fehlt");
        }
    }

    [Fact]
    public void AllTreatments_HaveUniqueIds()
    {
        var ids = _loader.Treatments.Select(t => t.Id).ToList();
        var duplicates = ids.GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void AllTreatments_HaveAtLeastOneSource()
    {
        foreach (var t in _loader.Treatments)
        {
            Assert.True(t.Sources.Count > 0, $"{t.Id}: mindestens eine Quelle erforderlich");
        }
    }

    [Fact]
    public void AllTreatments_SourceUrlsExistAsFiles_OrAreNull()
    {
        var docsPath = Path.Combine(ProjectRoot, "GrowDiary.Web", "wwwroot", "docs");

        foreach (var t in _loader.Treatments)
        {
            foreach (var source in t.Sources)
            {
                if (source.Url == null)
                    continue;

                if (!source.Url.StartsWith("/docs/"))
                    continue;

                var filename = source.Url.Replace("/docs/", "");
                var fullPath = Path.Combine(docsPath, filename);

                Assert.True(File.Exists(fullPath),
                    $"{t.Id}: source URL '{source.Url}' zeigt auf nicht-existierende Datei '{fullPath}'");
            }
        }
    }

    [Fact]
    public void ConflictReferences_PointToExistingTreatments()
    {
        var allIds = _loader.Treatments.Select(t => t.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var t in _loader.Treatments)
        {
            foreach (var conflict in t.Conflicts)
            {
                Assert.True(allIds.Contains(conflict.With),
                    $"{t.Id}: conflict referenziert unbekannte ID '{conflict.With}'");
            }
        }
    }

    [Fact]
    public void AllTreatments_HavePhaseFilterOrExplicitlyNull()
    {
        // PhaseFilter darf null sein (explizit kein Filter) oder ein valides Objekt
        // Wenn gesetzt: mindestens ein allowed-Eintrag oder ein blocked-Eintrag
        foreach (var t in _loader.Treatments)
        {
            if (t.PhaseFilter is null)
                continue;

            var hasAllowed = t.PhaseFilter.Allowed.Count > 0;
            var hasBlocked = t.PhaseFilter.Blocked.Count > 0;
            var hasWeekBlock = t.PhaseFilter.BlockAfterFlowerWeek.HasValue;

            Assert.True(hasAllowed || hasBlocked || hasWeekBlock,
                $"{t.Id}: phaseFilter ist gesetzt aber hat weder allowed, blocked noch blockAfterFlowerWeek");
        }
    }

    [Fact]
    public void KnowledgeBaseLoader_LoadsAtLeast25Treatments()
    {
        Assert.True(_loader.Treatments.Count >= 25,
            $"Erwartet mindestens 25 Treatments, geladen: {_loader.Treatments.Count}");
    }
}
