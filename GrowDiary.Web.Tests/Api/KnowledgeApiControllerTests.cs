using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using GrowDiary.Web.Services.Knowledge.Schema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

public sealed class KnowledgeApiControllerTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly KnowledgeApiController _controller;

    private static readonly string ProjectRoot = FindProjectRoot();

    public KnowledgeApiControllerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "KnowledgeApiCtrl_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        CopyDefaults(
            Path.Combine(ProjectRoot, "GrowDiary.Web", "wwwroot", "knowledge-defaults"),
            _tempRoot);

        var paths = new AppPaths(_tempRoot);
        var loader = new KnowledgeBaseLoader(paths, NullLogger<KnowledgeBaseLoader>.Instance);
        loader.Initialize();

        var knowledgeService = new CultivationKnowledgeService(loader);
        _controller = new KnowledgeApiController(knowledgeService, loader);
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
    public void GetTreatments_Returns30Items()
    {
        var result = _controller.GetTreatments();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<TreatmentDefinition>>(ok.Value);
        Assert.Equal(30, items.Count);
    }

    [Fact]
    public void GetTreatment_ExistingId_ReturnsItem()
    {
        var result = _controller.GetTreatment("beneficial-nematodes");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var item = Assert.IsType<TreatmentDefinition>(ok.Value);
        Assert.Equal("beneficial-nematodes", item.Id);
    }

    [Fact]
    public void GetTreatment_UnknownId_Returns404()
    {
        var result = _controller.GetTreatment("does-not-exist-xyz");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void GetSops_Returns10Items()
    {
        var result = _controller.GetSops();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<SopDefinition>>(ok.Value);
        Assert.Equal(10, items.Count);
    }

    [Fact]
    public void GetPathogens_Returns8Items()
    {
        var result = _controller.GetPathogens();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<PathogenDefinition>>(ok.Value);
        Assert.Equal(8, items.Count);
    }

    [Fact]
    public void GetSymptoms_Returns20Items()
    {
        var result = _controller.GetSymptoms();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<SymptomDefinition>>(ok.Value);
        Assert.Equal(20, items.Count);
    }

    [Fact]
    public void GetWearTemplates_Returns12Items()
    {
        var result = _controller.GetWearTemplates();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<WearTemplateDefinition>>(ok.Value);
        Assert.Equal(12, items.Count);
    }
}
