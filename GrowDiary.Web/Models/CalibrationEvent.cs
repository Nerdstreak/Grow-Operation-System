namespace GrowDiary.Web.Models;

public sealed class CalibrationEvent
{
    public int Id { get; set; }
    public int HardwareItemId { get; set; }
    public CalibrationEventType CalibrationType { get; set; } = CalibrationEventType.Ph;
    public CalibrationEventStatus Status { get; set; } = CalibrationEventStatus.Planned;
    public CalibrationResult Result { get; set; } = CalibrationResult.Unknown;
    public string Title { get; set; } = string.Empty;
    public string? ReferenceSolution { get; set; }
    public decimal? ReferenceValue { get; set; }
    public decimal? BeforeValue { get; set; }
    public decimal? AfterValue { get; set; }
    public decimal? TemperatureC { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? PerformedAtUtc { get; set; }
    public DateTime? NextDueAtUtc { get; set; }
    public int? GrowTaskId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
