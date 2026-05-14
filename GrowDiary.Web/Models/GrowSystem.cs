namespace GrowDiary.Web.Models;

public sealed class GrowSystem
{
    public int Id { get; set; }
    public int? TentId { get; set; }
    public string? TentName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string HydroStyle { get; set; } = string.Empty;
    public int? PotCount { get; set; }
    public double? PotSizeLiters { get; set; }
    public double? ReservoirLiters { get; set; }
    public HydroSetupStatus Status { get; set; } = HydroSetupStatus.Active;
    public HydroSetupLayoutType LayoutType { get; set; } = HydroSetupLayoutType.SingleBucket;
    public ReservoirPosition ReservoirPosition { get; set; } = ReservoirPosition.None;
    public bool HasCirculationPump { get; set; }
    public string? CirculationPumpNotes { get; set; }
    public bool HasAirPump { get; set; }
    public string? AirPumpNotes { get; set; }
    public int? AirStoneCount { get; set; }
    public bool HasChiller { get; set; }
    public bool HasUvSterilizer { get; set; }
    public string? Notes { get; set; }
    public int DisplayOrder { get; set; } = 99;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int ActiveGrowCount { get; set; }
}
