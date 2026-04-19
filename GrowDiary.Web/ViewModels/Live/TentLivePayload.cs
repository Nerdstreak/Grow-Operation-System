namespace GrowDiary.Web.ViewModels.Live;

public sealed class TentLivePayload
{
    public int TentId { get; set; }
    public string StateTone { get; set; } = "neutral";
    public string StateLabel { get; set; } = "neutral";
    public List<MetricPayload> Metrics { get; set; } = new();
    public string? CameraUrl { get; set; }
    public DateTime RefreshedAtUtc { get; set; }
}

public sealed class MetricPayload
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = "–";
    public string? Unit { get; set; }
    public string Tone { get; set; } = "default";
    public string? Hint { get; set; }
}
