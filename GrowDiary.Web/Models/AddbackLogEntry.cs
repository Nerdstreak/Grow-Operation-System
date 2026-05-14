namespace GrowDiary.Web.Models;

public sealed class AddbackLogEntry
{
    public int Id { get; set; }
    public int GrowId { get; set; }
    public int? HydroSetupId { get; set; }
    public AddbackLogKind Kind { get; set; } = AddbackLogKind.Addback;
    public DateTime PerformedAtUtc { get; set; } = DateTime.UtcNow;
    public double? ReservoirLiters { get; set; }
    public double? EcBefore { get; set; }
    public double? EcTarget { get; set; }
    public double? EcStock { get; set; }
    public double? EcAfter { get; set; }
    public double? PhBefore { get; set; }
    public double? PhAfter { get; set; }
    public double? LitersAdded { get; set; }
    public double? NewReservoirVolumeLiters { get; set; }
    public bool UsedHydroSetupVolume { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
