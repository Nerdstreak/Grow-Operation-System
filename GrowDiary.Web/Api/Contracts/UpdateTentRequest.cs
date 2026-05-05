namespace GrowDiary.Web.Api.Contracts;

public sealed class UpdateTentRequest
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "Grow Tent";
    public string? TentType { get; set; }
    public string? Notes { get; set; }
    public int DisplayOrder { get; set; }
    public string AccentColor { get; set; } = "#69b578";
    public int? WidthCm { get; set; }
    public int? DepthCm { get; set; }
    public int? TentHeightCm { get; set; }
    public string? LightType { get; set; }
    public int? LightWatt { get; set; }
    public string? LightController { get; set; }
    public string? LightControllerEntityId { get; set; }
    public int? ExhaustFanCount { get; set; }
    public int? ExhaustM3h { get; set; }
    public int? CirculationFanCount { get; set; }
    public string? HvacController { get; set; }
    public string? HvacControllerEntityId { get; set; }
    public bool Co2Available { get; set; }
    public string? CameraEntityId { get; set; }
}
