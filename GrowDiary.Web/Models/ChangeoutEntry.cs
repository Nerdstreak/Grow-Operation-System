namespace GrowDiary.Web.Models;

public sealed class ChangeoutEntry
{
    public int Id { get; set; }
    public int GrowId { get; set; }
    public int? HydroSetupId { get; set; }
    public ChangeoutKind Kind { get; set; } = ChangeoutKind.Partial;
    public DateTime PerformedAtUtc { get; set; } = DateTime.UtcNow;
    public double? VolumeChangedLiters { get; set; }
    public double? PercentChanged { get; set; }
    public double? EcBefore { get; set; }
    public double? EcAfter { get; set; }
    public double? PhBefore { get; set; }
    public double? PhAfter { get; set; }

    /// <summary>Womit aufgefuellt wurde — null heisst „nicht festgehalten".</summary>
    /// <remarks>
    /// Der Grow traegt eine Wasserquelle, aber die gilt fuer den ganzen Lauf.
    /// Wer einmal mit Leitungswasser nachfuellt, weil der Osmose-Tank leer war,
    /// soll genau das hier stehen haben — sonst erklaert spaeter niemand mehr
    /// den EC-Sprung.
    /// </remarks>
    public WaterSource? WaterUsed { get; set; }

    /// <summary>EC des verwendeten Wassers in mS/cm, vor dem Duenger.</summary>
    public double? WaterEcMsCm { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
