namespace GrowDiary.Web.Models;

public sealed class AutoMeasurementFieldMapping
{
    public int Id { get; set; }
    public int ConfigId { get; set; }
    public AutoMeasurementField MeasurementField { get; set; }
    public string MetricKey { get; set; } = string.Empty;
    public AutoMeasurementAggregation Aggregation { get; set; } = AutoMeasurementAggregation.Latest;
    public bool IsRequired { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
