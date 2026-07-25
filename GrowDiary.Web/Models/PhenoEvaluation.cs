namespace GrowDiary.Web.Models;

/// <summary>How tightly the nodes sit — the classic first read on a phenotype.</summary>
public enum InternodeSpacing
{
    Unknown,
    Tight,
    Medium,
    Wide
}

/// <summary>
/// The pheno-hunt score sheet for one plant. Every field is optional: it fills up as the
/// run progresses (structure while growing, yield at harvest, aroma after the cure).
/// </summary>
public sealed class PhenoEvaluation
{
    public int Id { get; set; }
    public int PlantInstanceId { get; set; }

    // --- Wuchs & Struktur (during veg) ---
    public int? VigorScore { get; set; }
    public InternodeSpacing InternodeSpacing { get; set; } = InternodeSpacing.Unknown;
    public int? BranchingScore { get; set; }
    public int? LeafToBudScore { get; set; }
    public double? HeightAtFlipCm { get; set; }

    // --- Stress & Training ---
    /// <summary>Applied techniques, newline separated (LST, Topping, Supercropping, …).</summary>
    public string? TrainingMethods { get; set; }
    public int? TrainingResponseScore { get; set; }
    public int? StressToleranceScore { get; set; }
    /// <summary>Pest/disease pressure on the usual 1–5 severity scale (1 = untroubled).</summary>
    public int? PestResistanceScore { get; set; }

    // --- Blüte & Ernte ---
    public int? FloweringDays { get; set; }
    public double? HeightAtHarvestCm { get; set; }
    public double? WetYieldG { get; set; }
    public double? DryYieldG { get; set; }
    public int? BudDensityScore { get; set; }
    public int? ResinScore { get; set; }
    public int? TrimEaseScore { get; set; }

    // --- Qualität (after drying/curing) ---
    public int? AromaScore { get; set; }
    public string? AromaNotes { get; set; }
    public int? FlavorScore { get; set; }
    public int? EffectScore { get; set; }
    public string? EffectNotes { get; set; }
    public double? ThcPercent { get; set; }
    public double? CbdPercent { get; set; }
    public string? TerpeneNotes { get; set; }

    // --- Entscheidung ---
    /// <summary>Set to override the computed score; null lets the weighted score stand.</summary>
    public double? ManualOverallScore { get; set; }
    public bool IsKeeper { get; set; }
    public bool ConfirmedInSecondRun { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Stretch factor from flip to harvest — the number growers actually plan around.</summary>
    public double? StretchFactor => HeightAtFlipCm is > 0 && HeightAtHarvestCm is > 0
        ? Math.Round(HeightAtHarvestCm.Value / HeightAtFlipCm.Value, 2)
        : null;
}
