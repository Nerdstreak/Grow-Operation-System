namespace GrowDiary.Web.Models;

public sealed class MetricCard
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = "-";
    public string? Unit { get; set; }
    public string Tone { get; set; } = "default";
    public string? Hint { get; set; }
    public string? Target { get; set; }   // z. B. "5.8-6.2" fuer Sollwert-Anzeige
}
