using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services.Knowledge;

public sealed class SetpointMigrationTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly KnowledgeBaseLoader _loader;
    private readonly TargetValueService _svc;

    public SetpointMigrationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "SetpointMigTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        var projectRoot = FindProjectRoot();
        var defaultsSource = Path.Combine(projectRoot, "GrowDiary.Web", "wwwroot", "knowledge-defaults");
        CopyDefaults(defaultsSource, _tempRoot);

        var paths = new AppPaths(_tempRoot);
        _loader = new KnowledgeBaseLoader(paths, NullLogger<KnowledgeBaseLoader>.Instance);
        _loader.Initialize();

        _svc = new TargetValueService(_loader);
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
    public void Loader_LoadsRdwcDefaultSetpoint()
    {
        Assert.Single(_loader.Setpoints);
        Assert.Equal("rdwc-default", _loader.Setpoints[0].Id);
    }

    [Fact]
    public void TargetValueService_GetTargets_RdwcSeedling_ReturnsPh6_0to6_2()
    {
        var result = _svc.GetTargets(HydroStyle.RDWC, GrowStage.Seedling);

        Assert.NotNull(result);
        Assert.Equal(6.0, result.PhMin);
        Assert.Equal(6.2, result.PhMax);
    }

    [Fact]
    public void TargetValueService_GetTargets_RdwcFlower_ReturnsEc1_0to1_2()
    {
        var result = _svc.GetTargets(HydroStyle.RDWC, GrowStage.Flower);

        Assert.NotNull(result);
        Assert.Equal(1.0, result.EcMin);
        Assert.Equal(1.2, result.EcMax);
    }

    [Fact]
    public void TargetValueService_GetTargets_DwcFlower_ReturnsEcMultiplied_1_3()
    {
        var rdwc = _svc.GetTargets(HydroStyle.RDWC, GrowStage.Flower)!;
        var dwc  = _svc.GetTargets(HydroStyle.DWC,  GrowStage.Flower)!;

        Assert.Equal(Math.Round(rdwc.EcMin * TargetValueService.DwcEcMultiplier, 2), dwc.EcMin);
        Assert.Equal(Math.Round(rdwc.EcMax * TargetValueService.DwcEcMultiplier, 2), dwc.EcMax);
    }

    [Fact]
    public void TargetValueService_GetTargets_UnknownStage_ReturnsNull()
    {
        Assert.Null(_svc.GetTargets(HydroStyle.RDWC, GrowStage.Dry));
        Assert.Null(_svc.GetTargets(HydroStyle.RDWC, GrowStage.Cure));
    }
}
