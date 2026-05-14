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
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
