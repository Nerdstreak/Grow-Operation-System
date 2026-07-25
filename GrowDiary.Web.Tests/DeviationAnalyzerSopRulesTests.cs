using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using GrowDiary.Web.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests;

/// <summary>
/// Rules taken straight from the SOPs, asserted with the SOP's own numbers.
///
/// These were missing or wrong until the documents were checked against the code rather
/// than only against the knowledge-base files: the pH drift rate did not exist at all, and
/// the dissolved-oxygen threshold sat below the level at which SOP-N1 already calls for
/// action.
/// </summary>
public sealed class DeviationAnalyzerSopRulesTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly DeviationAnalyzerService _service;

    public DeviationAnalyzerSopRulesTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "SopRules_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        CopyDefaults(Path.Combine(FindProjectRoot(), "GrowDiary.Web", "wwwroot", "knowledge-defaults"), _tempRoot);

        var loader = new KnowledgeBaseLoader(new AppPaths(_tempRoot), NullLogger<KnowledgeBaseLoader>.Instance);
        loader.Initialize();
        _service = new DeviationAnalyzerService(new TargetValueService(loader));
    }

    private static GrowRun Grow() => new()
    {
        Id = 1,
        Name = "Testlauf",
        MediumType = MediumType.Hydro,
        HydroStyle = HydroStyle.RDWC,
        StartDate = DateTime.Now.AddDays(-40),
    };

    private static Measurement At(DateTime takenAt, double? ph = null, double? doMgL = null, double? ec = null) => new()
    {
        Id = (int)(takenAt.Ticks % 100000),
        Stage = GrowStage.Flower,
        TakenAt = takenAt,
        ReservoirPh = ph,
        DissolvedOxygenMgL = doMgL,
        ReservoirEc = ec,
    };

    // --- SOP-RDWC-CAN-N1 §2.1: drift is defined by speed, not by position ---

    [Fact]
    public void PhJumpingHalfAPointOvernight_IsCritical_EvenThoughItNeverLeftTheBand()
    {
        // 5,8 -> 6,3 in 14 hours. Both readings sit inside the comfort band, so no absolute
        // check would say a word — but the SOP calls this acute and lists immediate steps.
        var now = DateTime.Now;
        var measurements = new List<Measurement>
        {
            At(now, ph: 6.30),
            At(now.AddHours(-14), ph: 5.80),
        };

        var drift = Assert.Single(_service.Analyze(Grow(), measurements), d => d.StableKey == "hydro.ph-drift");

        Assert.Equal(DeviationSeverity.Critical, drift.Severity);
        // Trennzeichen haengt an der Host-Kultur — nicht mitpruefen.
        Assert.Contains($"{0.50:0.00}", drift.Message);
        Assert.Contains("Wurzeln", drift.RecommendationHint ?? string.Empty);
    }

    [Fact]
    public void ADriftOfTwoTenths_IsOnlyAGentleNote()
    {
        var now = DateTime.Now;
        var measurements = new List<Measurement>
        {
            At(now, ph: 6.05),
            At(now.AddHours(-20), ph: 5.80),
        };

        var drift = Assert.Single(_service.Analyze(Grow(), measurements), d => d.StableKey == "hydro.ph-drift");

        Assert.Equal(DeviationSeverity.Info, drift.Severity);
        Assert.Contains("0,1-0,2", drift.RecommendationHint ?? string.Empty);
    }

    [Fact]
    public void TheNormalDailySwing_IsNotReported()
    {
        // 0,1 a day is the plant feeding — SOP-N1 calls this not merely harmless but useful.
        var now = DateTime.Now;
        var measurements = new List<Measurement>
        {
            At(now, ph: 6.00),
            At(now.AddHours(-24), ph: 5.90),
        };

        Assert.DoesNotContain(_service.Analyze(Grow(), measurements), d => d.StableKey == "hydro.ph-drift");
    }

    [Fact]
    public void TheSameChangeSpreadOverAWeek_IsNotAnAcuteDrift()
    {
        // The rule is explicitly about 12–24 h. Slow movement is the trend guard's job.
        var now = DateTime.Now;
        var measurements = new List<Measurement>
        {
            At(now, ph: 6.30),
            At(now.AddDays(-7), ph: 5.80),
        };

        Assert.DoesNotContain(_service.Analyze(Grow(), measurements), d => d.StableKey == "hydro.ph-drift");
    }

    // --- SOP-RDWC-CAN-N1 §2.2 and SOP-RDWC-CAN-S1 §2.2: dissolved oxygen ---

    [Theory]
    [InlineData(6.2)]
    [InlineData(6.4)]
    public void OxygenBelowSixPointFive_IsReported_AsMicrobialActivity(double value)
    {
        // Used to be silent: the old threshold was 6,0, but SOP-N1 already calls for action
        // at 6,5 — the band where root rot starts and nothing looks wrong yet.
        var deviation = Assert.Single(
            _service.Analyze(Grow(), new List<Measurement> { At(DateTime.Now, doMgL: value) }),
            d => d.StableKey == "hydro.do");

        Assert.Equal(DeviationSeverity.Warning, deviation.Severity);
        Assert.Contains("mikrobiologische", deviation.RecommendationHint ?? string.Empty);
    }

    [Fact]
    public void OxygenBelowSix_CountsAsConfirmedRootRot()
    {
        var deviation = Assert.Single(
            _service.Analyze(Grow(), new List<Measurement> { At(DateTime.Now, doMgL: 5.5) }),
            d => d.StableKey == "hydro.do");

        Assert.Equal(DeviationSeverity.Critical, deviation.Severity);
        Assert.Contains("Wurzelfaeule", deviation.RecommendationHint ?? string.Empty);
    }

    [Fact]
    public void HealthyOxygen_IsSilent()
    {
        Assert.DoesNotContain(
            _service.Analyze(Grow(), new List<Measurement> { At(DateTime.Now, doMgL: 7.8) }),
            d => d.StableKey == "hydro.do");
    }

    // --- Growplan: the flush ends at EC 0,4 ---

    [Fact]
    public void FlushingCorrectlyInFinish_IsNotReportedAsOutOfRange()
    {
        // The setpoint used to say 1,1–1,6 for Finish, so a grower following the plan down
        // to 0,4 was told the value was wrong.
        var grow = Grow();
        var measurement = At(DateTime.Now, ec: 0.4);
        measurement.Stage = GrowStage.Finish;

        var deviations = _service.Analyze(grow, new List<Measurement> { measurement });

        Assert.DoesNotContain(deviations, d => d.StableKey == "hydro.ec");
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
