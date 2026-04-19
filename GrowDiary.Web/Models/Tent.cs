namespace GrowDiary.Web.Models;

public sealed class Tent
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "Grow Tent";
    public string? Notes { get; set; }
    public int DisplayOrder { get; set; }
    public string AccentColor { get; set; } = "#69b578";

    public string? TemperatureEntityId { get; set; }
    public string? HumidityEntityId { get; set; }
    public string? VpdEntityId { get; set; }
    public string? ReservoirPhEntityId { get; set; }
    public string? ReservoirEcEntityId { get; set; }
    public string? ReservoirLevelEntityId { get; set; }
    public string? ReservoirTempEntityId { get; set; }
    public string? OrpEntityId { get; set; }
    public string? DissolvedOxygenEntityId { get; set; }
    public string? Co2EntityId { get; set; }
    public string? LightEntityId { get; set; }
    public string? CameraEntityId { get; set; }
    public string? LightCycle { get; set; }
    public string? PpfdEntityId { get; set; }
    public string? PpfdTarget { get; set; }

    // Physisches Setup
    public int? WidthCm { get; set; }
    public int? DepthCm { get; set; }
    public int? TentHeightCm { get; set; }
    public string? LightType { get; set; }
    public int? LightWatt { get; set; }
    public int? ExhaustFanCount { get; set; }
    public int? ExhaustM3h { get; set; }
    public int? CirculationFanCount { get; set; }
    public string? Co2Type { get; set; }
    public int? Co2TargetPpm { get; set; }

    public int ActiveGrowCount { get; set; }
    public int ArchivedGrowCount { get; set; }
    public List<GrowRun> ActiveGrows { get; set; } = new();
}
