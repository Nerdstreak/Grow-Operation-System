namespace GrowDiary.Web.ViewModels;

public sealed class TimelineItemViewModel
{
    public DateTime Timestamp { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string KindLabel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? SourceLabel { get; set; }
    public List<string> Badges { get; set; } = new();
    public string? PhotoPath { get; set; }
    public string? ActionUrl { get; set; }
    public string? ActionLabel { get; set; }
}
