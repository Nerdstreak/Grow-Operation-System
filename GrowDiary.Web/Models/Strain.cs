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

    /// <summary>Feminisiert, Automatic, Regular — wie der Züchter die Samen verkauft.</summary>
    public SeedKind? SeedKind { get; set; }

    /// <summary>THC laut Züchter, in Prozent. Eine Werbeangabe — deshalb steht überall „laut Züchter" dabei.</summary>
    public double? ThcPercent { get; set; }

    public double? CbdPercent { get; set; }

    /// <summary>Sativa-Anteil laut Züchter (0–100); der Rest ist Indica.</summary>
    public int? SativaPercent { get; set; }

    /// <summary>Geschmack laut Züchter, frei: „Grapefruit, Zitrus, Melone, Banane".</summary>
    public string? Taste { get; set; }

    public string? Effect { get; set; }

    public string? Aroma { get; set; }

    /// <summary>Ertrag innen laut Züchter, g/m².</summary>
    public int? YieldIndoorGm2 { get; set; }

    /// <summary>Höhe innen laut Züchter, cm (obere Angabe).</summary>
    public int? HeightIndoorCm { get; set; }
    public double? NutrientDemandFactor { get; set; }
    public double? StretchFactor { get; set; }
    public double? VpdPreferenceShift { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
