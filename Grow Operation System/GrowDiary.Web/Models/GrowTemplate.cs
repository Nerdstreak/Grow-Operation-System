namespace GrowDiary.Web.Models;

public sealed class GrowTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public MediumType MediumType { get; set; } = MediumType.Hydro;
    public FeedingStyle FeedingStyle { get; set; } = FeedingStyle.None;
    public HydroStyle HydroStyle { get; set; } = HydroStyle.None;
    public GrowEnvironment Environment { get; set; } = GrowEnvironment.Indoor;
    public string? SuggestedTentKind { get; set; }
    public string? Light { get; set; }
    public string? ContainerSize { get; set; }
    public string? ReservoirSize { get; set; }
    public string? MediumDetail { get; set; }
    public string? IrrigationStyle { get; set; }
    public string? Nutrients { get; set; }
    public string? Notes { get; set; }
    public string AccentColor { get; set; } = "#79c97f";
}
