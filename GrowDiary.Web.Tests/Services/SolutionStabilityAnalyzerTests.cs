using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// SOP-N1 §2.1 asks for five signals to be read together, because the same pH movement
/// means "the plant is feeding" in one combination and "biofilm" in another. These tests
/// use the table's own pairings.
/// </summary>
public sealed class SolutionStabilityAnalyzerTests
{
    private static readonly DateTime Now = new(2026, 7, 26, 9, 0, 0, DateTimeKind.Unspecified);
    private readonly SolutionStabilityAnalyzer _analyzer = new();

    private static Measurement At(double hoursAgo, double? ph = null, double? ec = null,
                                  double? doMgL = null, double? orp = null) => new()
    {
        Id = (int)(hoursAgo * 10) + 1,
        Stage = GrowStage.Flower,
        TakenAt = Now.AddHours(-hoursAgo),
        ReservoirPh = ph,
        ReservoirEc = ec,
        DissolvedOxygenMgL = doMgL,
        OrpMv = orp,
    };

    [Fact]
    public void FallingPhWithStableEcAndGoodOxygen_ReadsAsAPlantThatIsFeeding()
    {
        // The table's "normal" column, top to bottom.
        var measurements = new List<Measurement>
        {
            At(0, ph: 5.95, ec: 1.18, doMgL: 8.1, orp: 420),
            At(24, ph: 6.05, ec: 1.24, doMgL: 8.0, orp: 435),
        };

        var result = _analyzer.Assess(measurements, Now);

        Assert.Equal(StabilitySignalVerdict.Normal, result.Overall);
        Assert.Contains("Nährstoffaufnahme", result.Headline);
        Assert.Equal(0, result.InstabilityCount);
    }

    [Fact]
    public void FastPhMoveWithRisingEcAndLowOxygen_ReadsAsInstability()
    {
        // Same direction of pH as a healthy run, but the company it keeps is different —
        // which is the entire point of reading the table rather than one value.
        var measurements = new List<Measurement>
        {
            At(0, ph: 6.45, ec: 1.55, doMgL: 6.1, orp: 310),
            At(20, ph: 5.85, ec: 1.20, doMgL: 7.2, orp: 430),
        };

        var result = _analyzer.Assess(measurements, Now);

        Assert.Equal(StabilitySignalVerdict.Instability, result.Overall);
        Assert.True(result.InstabilityCount >= 2);
        Assert.Contains("bevor am pH nachgeregelt wird", result.Detail);
    }

    [Fact]
    public void ASingleOddReading_IsNotYetADiagnosis()
    {
        // One low value has many harmless explanations. Calling that instability would
        // train the user to ignore the panel.
        var measurements = new List<Measurement>
        {
            At(0, ph: 6.00, ec: 1.18, doMgL: 6.2, orp: 420),
            At(24, ph: 6.05, ec: 1.22, doMgL: 6.2, orp: 430),
        };

        var result = _analyzer.Assess(measurements, Now);

        Assert.Equal(StabilitySignalVerdict.Unknown, result.Overall);
        Assert.Equal(1, result.InstabilityCount);
        Assert.Contains("Ein Merkmal", result.Headline);
    }

    [Fact]
    public void RapidOrpDecay_CountsAsASignal_EvenWhileTheValueIsStillAcceptable()
    {
        // The table talks about how fast it decays, not only where it stands: 380 mV is
        // fine on its own, but not if it was 480 yesterday.
        var measurements = new List<Measurement>
        {
            At(0, ph: 6.40, ec: 1.30, doMgL: 8.0, orp: 380),
            At(24, ph: 5.85, ec: 1.28, doMgL: 8.0, orp: 480),
        };

        var result = _analyzer.Assess(measurements, Now);

        var orp = Assert.Single(result.Signals, signal => signal.Key == "orp");
        Assert.Equal(StabilitySignalVerdict.Instability, orp.Verdict);
        Assert.Contains("rascher Abbau", orp.Observation);
    }

    [Fact]
    public void OxygenBetweenSixPointFiveAndSevenPointFive_IsExplicitlyAGreyZone()
    {
        var measurements = new List<Measurement>
        {
            At(0, ph: 6.00, ec: 1.20, doMgL: 7.0, orp: 420),
            At(24, ph: 6.02, ec: 1.22, doMgL: 7.0, orp: 425),
        };

        var oxygen = Assert.Single(_analyzer.Assess(measurements, Now).Signals, s => s.Key == "do");

        Assert.Equal(StabilitySignalVerdict.Unknown, oxygen.Verdict);
        Assert.Contains("Graubereich", oxygen.Observation);
    }

    [Fact]
    public void WithoutData_ItSaysSoRatherThanGuessing()
    {
        var result = _analyzer.Assess([], Now);

        Assert.Equal(StabilitySignalVerdict.Unknown, result.Overall);
        Assert.Contains("Zu wenig Daten", result.Headline);
        Assert.All(result.Signals, signal => Assert.Equal(StabilitySignalVerdict.Unknown, signal.Verdict));
    }

    [Fact]
    public void TheVisualChecksAreAlwaysReturned_BecauseNoSensorCoversThem()
    {
        // The surface of the water and the smell are two rows of the SOP's table. Dropping
        // them because there is no sensor would quietly shrink the diagnosis.
        var result = _analyzer.Assess([At(0, ph: 6.0)], Now);

        Assert.Equal(2, result.VisualChecks.Count);
        Assert.Contains(result.VisualChecks, check => check.Contains("Wasseroberfläche"));
        Assert.Contains(result.VisualChecks, check => check.Contains("Bohnensprossen"));
    }

    [Fact]
    public void ReadingsOlderThanFourDays_AreIgnored()
    {
        var measurements = new List<Measurement>
        {
            At(24 * 10, ph: 5.0, ec: 3.0, doMgL: 2.0, orp: 100),
        };

        Assert.Equal(StabilitySignalVerdict.Unknown, _analyzer.Assess(measurements, Now).Overall);
    }
}
