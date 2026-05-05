using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services.Knowledge;

public sealed class NutrientProgramMigrationTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly KnowledgeBaseLoader _loader;
    private readonly CultivationKnowledgeService _svc;

    public NutrientProgramMigrationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "NutrientMigTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        var projectRoot = FindProjectRoot();
        var defaultsSource = Path.Combine(projectRoot, "GrowDiary.Web", "wwwroot", "knowledge-defaults");
        CopyDefaults(defaultsSource, _tempRoot);

        var paths = new AppPaths(_tempRoot);
        _loader = new KnowledgeBaseLoader(paths, NullLogger<KnowledgeBaseLoader>.Instance);
        _loader.Initialize();

        _svc = new CultivationKnowledgeService(_loader);
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
    public void Loader_LoadsAllThreeNutrientPrograms()
    {
        Assert.Equal(3, _loader.NutrientPrograms.Count);
    }

    [Fact]
    public void CultivationKnowledgeService_ExposesAllPrograms()
    {
        Assert.Equal(3, _svc.GetPrograms().Count);
    }

    [Fact]
    public void CultivationKnowledgeService_AthenaProgram_HasSevenStages()
    {
        var athena = _svc.GetPrograms().Single(p => p.Key == "athena");
        Assert.Equal(7, athena.Stages.Count);
    }

    [Fact]
    public void CultivationKnowledgeService_VbxProgram_HasSixStages()
    {
        var vbx = _svc.GetPrograms().Single(p => p.Key == "hydro-research-vbx");
        Assert.Equal(6, vbx.Stages.Count);
    }

    [Fact]
    public void CultivationKnowledgeService_CannaAquaProgram_HasSixStages()
    {
        var canna = _svc.GetPrograms().Single(p => p.Key == "canna-aqua");
        Assert.Equal(6, canna.Stages.Count);
    }

    [Fact]
    public void CultivationKnowledgeService_MatchProgram_FindsAthenaByKeyword()
    {
        var match = _svc.MatchProgram("athena blended grow a");
        Assert.NotNull(match);
        Assert.Equal("athena", match.Key);
    }

    [Fact]
    public void CultivationKnowledgeService_MatchProgram_FindsCannaByKeyword()
    {
        var match = _svc.MatchProgram("canna aqua vega");
        Assert.NotNull(match);
        Assert.Equal("canna-aqua", match.Key);
    }
}
