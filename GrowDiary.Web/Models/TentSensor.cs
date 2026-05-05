namespace GrowDiary.Web.Models;

public sealed class TentSensor
{
    public int Id { get; set; }
    public int TentId { get; set; }
    public SensorMetricType MetricType { get; set; }
    public string HaEntityId { get; set; } = string.Empty;
    public string? DisplayLabel { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
