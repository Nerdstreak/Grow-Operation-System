using System.ComponentModel.DataAnnotations;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public sealed record StrainDto(
    int Id,
    string Name,
    string? Breeder,
    StrainDominance Dominance,
    int? FlowerWeeksMin,
    int? FlowerWeeksMax,
    string? Notes,
    double? NutrientDemandFactor,
    double? StretchFactor,
    double? VpdPreferenceShift,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public sealed class CreateStrainRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Breeder { get; set; }
    public StrainDominance Dominance { get; set; } = StrainDominance.Unknown;
    public int? FlowerWeeksMin { get; set; }
    public int? FlowerWeeksMax { get; set; }
    public string? Notes { get; set; }
    public double? NutrientDemandFactor { get; set; }
    public double? StretchFactor { get; set; }
    public double? VpdPreferenceShift { get; set; }
}

public sealed class UpdateStrainRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Breeder { get; set; }
    public StrainDominance Dominance { get; set; } = StrainDominance.Unknown;
    public int? FlowerWeeksMin { get; set; }
    public int? FlowerWeeksMax { get; set; }
    public string? Notes { get; set; }
    public double? NutrientDemandFactor { get; set; }
    public double? StretchFactor { get; set; }
    public double? VpdPreferenceShift { get; set; }
}
