namespace GrowDiary.Web.Models;

public sealed class Tent
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "Grow Tent";
    public TentType TentType { get; set; } = TentType.MultiPurpose;
    public string? Notes { get; set; }
    public int DisplayOrder { get; set; }
    public string AccentColor { get; set; } = "#69b578";

    public int? WidthCm { get; set; }
    public int? DepthCm { get; set; }
    public int? TentHeightCm { get; set; }
    public string? LightType { get; set; }
    public int? LightWatt { get; set; }
    public LightControllerType? LightController { get; set; }
    public string? LightControllerEntityId { get; set; }
    public int? ExhaustFanCount { get; set; }
    public int? ExhaustM3h { get; set; }
    public int? CirculationFanCount { get; set; }
    public HvacControllerType? HvacController { get; set; }
    public string? HvacControllerEntityId { get; set; }
    public bool Co2Available { get; set; }
    public string? CameraEntityId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public int ActiveGrowCount { get; set; }
    public int ArchivedGrowCount { get; set; }
    public List<GrowRun> ActiveGrows { get; set; } = new();
    public List<TentSensor> Sensors { get; set; } = new();
}
