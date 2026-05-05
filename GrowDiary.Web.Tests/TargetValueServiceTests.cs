using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests;

public sealed class TargetValueServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly TargetValueService _svc;

    public TargetValueServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "TVSvcTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        // Copy the real rdwc-default.json from the project defaults
        var projectRoot = FindProjectRoot();
        var defaultsSource = Path.Combine(projectRoot, "GrowDiary.Web", "wwwroot", "knowledge-defaults");
        CopyDefaults(defaultsSource, _tempRoot);

        var paths = new AppPaths(_tempRoot);
        var loader = new KnowledgeBaseLoader(paths, NullLogger<KnowledgeBaseLoader>.Instance);
        loader.Initialize();

        _svc = new TargetValueService(loader);
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
    public void RDWC_Seedling_KorrekteSollwerte()
    {
        var result = _svc.GetTargets(HydroStyle.RDWC, GrowStage.Seedling);

        Assert.NotNull(result);
        Assert.Equal(6.0, result.PhMin);
        Assert.Equal(6.2, result.PhMax);
        Assert.Equal(0.2, result.EcMin);
        Assert.Equal(0.4, result.EcMax);
    }

    [Fact]
    public void DWC_Seedling_ECHoeherAlsRDWC()
    {
        var result = _svc.GetTargets(HydroStyle.DWC, GrowStage.Seedling);

        Assert.NotNull(result);
        Assert.True(result.EcMin > 0.2);
        Assert.Equal(Math.Round(0.2 * TargetValueService.DwcEcMultiplier, 2), result.EcMin);
        Assert.Equal(Math.Round(0.4 * TargetValueService.DwcEcMultiplier, 2), result.EcMax);
    }

    [Fact]
    public void DWC_AndereWerteGleichWieRDWC()
    {
        var rdwc = _svc.GetTargets(HydroStyle.RDWC, GrowStage.Flower)!;
        var dwc  = _svc.GetTargets(HydroStyle.DWC,  GrowStage.Flower)!;

        Assert.Equal(rdwc.PhMin,  dwc.PhMin);
        Assert.Equal(rdwc.PhMax,  dwc.PhMax);
        Assert.Equal(rdwc.VpdMin, dwc.VpdMin);
        Assert.Equal(rdwc.VpdMax, dwc.VpdMax);
        Assert.NotEqual(rdwc.EcMin, dwc.EcMin);
    }

    [Fact]
    public void Dry_Cure_GibtNull()
    {
        Assert.Null(_svc.GetTargets(HydroStyle.RDWC, GrowStage.Dry));
        Assert.Null(_svc.GetTargets(HydroStyle.RDWC, GrowStage.Cure));
    }

    [Fact]
    public void Flower_PhNiedrigerAlsVeg()
    {
        var veg    = _svc.GetTargets(HydroStyle.RDWC, GrowStage.Veg)!;
        var flower = _svc.GetTargets(HydroStyle.RDWC, GrowStage.Flower)!;

        Assert.True(flower.PhMax < veg.PhMax);
    }

    [Fact]
    public void Flower_ECHoeherAlsSeedling()
    {
        var seedling = _svc.GetTargets(HydroStyle.RDWC, GrowStage.Seedling)!;
        var flower   = _svc.GetTargets(HydroStyle.RDWC, GrowStage.Flower)!;

        Assert.True(flower.EcMin > seedling.EcMax);
    }

    [Fact]
    public void RDWC_Flower_PhNiedrigerAlsVeg()
    {
        var veg    = _svc.GetTargets(HydroStyle.RDWC, GrowStage.Veg)!;
        var flower = _svc.GetTargets(HydroStyle.RDWC, GrowStage.Flower)!;

        Assert.True(flower.PhMax <= veg.PhMax);
    }

    [Fact]
    public void RDWC_Finish_ECMaxGroesserGleichFlowerECMax()
    {
        var flower = _svc.GetTargets(HydroStyle.RDWC, GrowStage.Flower)!;
        var finish = _svc.GetTargets(HydroStyle.RDWC, GrowStage.Finish)!;

        Assert.True(finish.EcMax >= flower.EcMax);
    }

    [Fact]
    public void DWC_EcMultiplikatorKorrektFuerAlleStages()
    {
        var stages = new[] { GrowStage.Seedling, GrowStage.Veg, GrowStage.Flower, GrowStage.Finish };

        foreach (var stage in stages)
        {
            var rdwc = _svc.GetTargets(HydroStyle.RDWC, stage)!;
            var dwc  = _svc.GetTargets(HydroStyle.DWC,  stage)!;

            Assert.Equal(Math.Round(rdwc.EcMin * TargetValueService.DwcEcMultiplier, 2), dwc.EcMin);
            Assert.Equal(Math.Round(rdwc.EcMax * TargetValueService.DwcEcMultiplier, 2), dwc.EcMax);
        }
    }
}
