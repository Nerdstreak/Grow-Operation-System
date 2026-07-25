using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

public sealed class PhenoScoreCalculatorTests
{
    private static PhenoEvaluation Sheet(int plantId, Action<PhenoEvaluation>? configure = null)
    {
        var sheet = new PhenoEvaluation { PlantInstanceId = plantId };
        configure?.Invoke(sheet);
        return sheet;
    }

    [Fact]
    public void EmptySheet_HasNoScore()
    {
        var scores = PhenoScoreCalculator.Score([Sheet(1)], PhenoWeights.Default);

        Assert.Null(scores.Single().Total);
    }

    [Fact]
    public void BestYieldInTheHunt_ScoresAboveTheWeakest()
    {
        // Yield is meaningless in isolation — it is ranked against the siblings.
        var hunt = new[]
        {
            Sheet(1, s => s.DryYieldG = 40),
            Sheet(2, s => s.DryYieldG = 120),
        };

        var scores = PhenoScoreCalculator.Score(hunt, PhenoWeights.Default);

        Assert.Equal(0, scores.First(s => s.PlantInstanceId == 1).Yield);
        Assert.Equal(10, scores.First(s => s.PlantInstanceId == 2).Yield);
    }

    [Fact]
    public void SingleMeasuredYield_CountsAsFullMarks()
    {
        // Nothing to compare against, so the value must not drag the plant down.
        var scores = PhenoScoreCalculator.Score([Sheet(1, s => s.DryYieldG = 80)], PhenoWeights.Default);

        Assert.Equal(10, scores.Single().Yield);
    }

    [Fact]
    public void MissingBuckets_DoNotPunishThePlant()
    {
        // Only resilience is filled in — the total must reflect that bucket, not average
        // in a pile of zeros for the lab test that was never ordered.
        var scores = PhenoScoreCalculator.Score(
            [Sheet(1, s => { s.StressToleranceScore = 10; s.TrainingResponseScore = 10; s.PestResistanceScore = 5; })],
            PhenoWeights.Default);

        Assert.Equal(10, scores.Single().Total);
    }

    [Fact]
    public void WeightsShiftTheOutcome()
    {
        var hunt = new[]
        {
            // Strong yield, poor aroma.
            Sheet(1, s => { s.DryYieldG = 150; s.BudDensityScore = 10; s.AromaScore = 2; s.FlavorScore = 2; }),
            // Weak yield, superb aroma.
            Sheet(2, s => { s.DryYieldG = 50; s.BudDensityScore = 3; s.AromaScore = 10; s.FlavorScore = 10; }),
        };

        var yieldFocused = PhenoScoreCalculator.Score(hunt, new PhenoWeights(Yield: 80, Quality: 20, Potency: 0, Resilience: 0, Structure: 0));
        var aromaFocused = PhenoScoreCalculator.Score(hunt, new PhenoWeights(Yield: 20, Quality: 80, Potency: 0, Resilience: 0, Structure: 0));

        Assert.True(yieldFocused.First(s => s.PlantInstanceId == 1).Total > yieldFocused.First(s => s.PlantInstanceId == 2).Total);
        Assert.True(aromaFocused.First(s => s.PlantInstanceId == 2).Total > aromaFocused.First(s => s.PlantInstanceId == 1).Total);
    }

    [Fact]
    public void ManualScore_WinsAndIsFlagged()
    {
        var scores = PhenoScoreCalculator.Score(
            [Sheet(1, s => { s.VigorScore = 1; s.ManualOverallScore = 9.5; })],
            PhenoWeights.Default);

        Assert.Equal(9.5, scores.Single().Total);
        Assert.True(scores.Single().IsManual);
    }

    [Fact]
    public void PestScale_IsNormalisedFromFive()
    {
        Assert.Equal(0, PhenoScoreCalculator.FromFive(1));
        Assert.Equal(1, PhenoScoreCalculator.FromFive(5));
        Assert.Null(PhenoScoreCalculator.FromFive(9));
    }

    [Fact]
    public void RatingScale_IsNormalisedFromTen()
    {
        Assert.Equal(0, PhenoScoreCalculator.FromTen(1));
        Assert.Equal(1, PhenoScoreCalculator.FromTen(10));
        Assert.Null(PhenoScoreCalculator.FromTen(0));
    }

    [Fact]
    public void StretchFactor_IsDerivedFromHeights()
    {
        var sheet = Sheet(1, s => { s.HeightAtFlipCm = 40; s.HeightAtHarvestCm = 100; });

        Assert.Equal(2.5, sheet.StretchFactor);
    }
}
