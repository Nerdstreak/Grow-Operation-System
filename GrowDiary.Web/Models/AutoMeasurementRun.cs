namespace GrowDiary.Web.Models;

public sealed class AutoMeasurementRun
{
    public int Id { get; set; }
    public int ConfigId { get; set; }
    public int GrowId { get; set; }
    public AutoMeasurementTriggerKind TriggerKind { get; set; } = AutoMeasurementTriggerKind.Manual;
    public DateTime ScheduledForUtc { get; set; }
    public int? MeasurementId { get; set; }
    public AutoMeasurementRunStatus Status { get; set; } = AutoMeasurementRunStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
