using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>How much each trait bucket counts toward a plant's overall score.</summary>
public sealed record PhenoWeights(
    double Yield = 25,
    double Quality = 25,
    double Potency = 15,
    double Resilience = 20,
    double Structure = 15)
{
    public static PhenoWeights Default => new();
}

/// <summary>A plant's score, broken down so the total is explainable.</summary>
public sealed record PhenoScore(
    int PlantInstanceId,
    double? Total,
    double? Yield,
    double? Quality,
    double? Potency,
    double? Resilience,
    double? Structure,
    bool IsManual);

/// <summary>
/// Turns a pheno-hunt score sheet into one comparable number.
/// <para>
/// Ratings are absolute (1–10), but yield and potency are not: 90 g is only good or bad
/// next to its siblings. Those are therefore scored <em>relative to the other plants in
/// the same hunt</em> — which is exactly what a pheno hunt compares.
/// </para>
/// <para>
/// Buckets without any data are left out and the weights are renormalised over the rest,
/// so a plant is never punished for a lab test you didn't order.
/// </para>
/// </summary>
public static class PhenoScoreCalculator
{
    /// <summary>Normalises a 1–10 rating to 0..1.</summary>
    public static double? FromTen(int? value) => value is { } v && v is >= 1 and <= 10 ? (v - 1) / 9.0 : null;

    /// <summary>Normalises a 1–5 severity rating to 0..1.</summary>
    public static double? FromFive(int? value) => value is { } v && v is >= 1 and <= 5 ? (v - 1) / 4.0 : null;

    private static double? Average(params double?[] parts)
    {
        var present = parts.Where(part => part.HasValue).Select(part => part!.Value).ToList();
        return present.Count == 0 ? null : present.Average();
    }

    /// <summary>
    /// Scores an absolute measure against its siblings: the best value in the hunt gets 1,
    /// the weakest 0. A single value (or all-equal values) counts as full marks — there is
    /// nothing to distinguish.
    /// </summary>
    public static double? Relative(double? value, IReadOnlyCollection<double> peers)
    {
        if (value is not { } v || peers.Count == 0) return null;
        var min = peers.Min();
        var max = peers.Max();
        if (max - min < 1e-9) return 1.0;
        return Math.Clamp((v - min) / (max - min), 0, 1);
    }

    /// <summary>Scores every sheet in a hunt together, because the relative parts need the field.</summary>
    public static IReadOnlyList<PhenoScore> Score(IReadOnlyList<PhenoEvaluation> hunt, PhenoWeights weights)
    {
        var dryYields = hunt.Where(e => e.DryYieldG is > 0).Select(e => e.DryYieldG!.Value).ToList();
        var thcValues = hunt.Where(e => e.ThcPercent is > 0).Select(e => e.ThcPercent!.Value).ToList();

        return hunt.Select(evaluation =>
        {
            var yieldScore = Average(
                Relative(evaluation.DryYieldG, dryYields),
                FromTen(evaluation.BudDensityScore));

            var qualityScore = Average(
                FromTen(evaluation.AromaScore),
                FromTen(evaluation.FlavorScore),
                FromTen(evaluation.EffectScore),
                FromTen(evaluation.ResinScore));

            var potencyScore = Relative(evaluation.ThcPercent, thcValues);

            var resilienceScore = Average(
                FromTen(evaluation.StressToleranceScore),
                FromTen(evaluation.TrainingResponseScore),
                FromFive(evaluation.PestResistanceScore));

            var structureScore = Average(
                FromTen(evaluation.VigorScore),
                FromTen(evaluation.BranchingScore),
                FromTen(evaluation.LeafToBudScore),
                FromTen(evaluation.TrimEaseScore));

            var buckets = new (double? Value, double Weight)[]
            {
                (yieldScore, weights.Yield),
                (qualityScore, weights.Quality),
                (potencyScore, weights.Potency),
                (resilienceScore, weights.Resilience),
                (structureScore, weights.Structure),
            };

            var present = buckets.Where(bucket => bucket.Value.HasValue && bucket.Weight > 0).ToList();
            var weightSum = present.Sum(bucket => bucket.Weight);
            double? total = present.Count == 0 || weightSum <= 0
                ? null
                : Math.Round(present.Sum(bucket => bucket.Value!.Value * bucket.Weight) / weightSum * 10, 1);

            var manual = evaluation.ManualOverallScore.HasValue;
            return new PhenoScore(
                evaluation.PlantInstanceId,
                manual ? Math.Round(evaluation.ManualOverallScore!.Value, 1) : total,
                Round(yieldScore),
                Round(qualityScore),
                Round(potencyScore),
                Round(resilienceScore),
                Round(structureScore),
                manual);
        }).ToList();
    }

    private static double? Round(double? normalized) => normalized is { } value ? Math.Round(value * 10, 1) : null;
}
