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
    SeedKind? SeedKind,
    double? ThcPercent,
    double? CbdPercent,
    int? SativaPercent,
    string? Taste,
    string? Effect,
    string? Aroma,
    int? YieldIndoorGm2,
    int? HeightIndoorCm,
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
    public SeedKind? SeedKind { get; set; }
    public double? ThcPercent { get; set; }
    public double? CbdPercent { get; set; }
    public int? SativaPercent { get; set; }
    public string? Taste { get; set; }
    public string? Effect { get; set; }
    public string? Aroma { get; set; }
    public int? YieldIndoorGm2 { get; set; }
    public int? HeightIndoorCm { get; set; }
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
    public SeedKind? SeedKind { get; set; }
    public double? ThcPercent { get; set; }
    public double? CbdPercent { get; set; }
    public int? SativaPercent { get; set; }
    public string? Taste { get; set; }
    public string? Effect { get; set; }
    public string? Aroma { get; set; }
    public int? YieldIndoorGm2 { get; set; }
    public int? HeightIndoorCm { get; set; }
}
