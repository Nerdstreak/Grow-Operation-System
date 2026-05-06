namespace GrowDiary.Web.Models;

public sealed class Strain
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Breeder { get; set; }
    public StrainDominance Dominance { get; set; } = StrainDominance.Unknown;
    public int? FlowerWeeksMin { get; set; }
    public int? FlowerWeeksMax { get; set; }
    public string? Notes { get; set; }
    public double? NutrientDemandFactor { get; set; }
    public double? StretchFactor { get; set; }
    public double? VpdPreferenceShift { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
