namespace GrowDiary.Web.Models;

public sealed class TentSensorReading
{
    public int Id { get; set; }
    public int TentId { get; set; }
    public string MetricKey { get; set; } = string.Empty;
    public double Value { get; set; }
    public string? Unit { get; set; }
    public DateTime CapturedAtUtc { get; set; }
}
