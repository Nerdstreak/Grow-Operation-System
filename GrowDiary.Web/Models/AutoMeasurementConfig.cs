namespace GrowDiary.Web.Models;

public sealed class AutoMeasurementConfig
{
    public int Id { get; set; }
    public int GrowId { get; set; }
    public int? TentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AutoMeasurementStatus Status { get; set; } = AutoMeasurementStatus.Enabled;
    public AutoMeasurementTriggerKind TriggerKind { get; set; } = AutoMeasurementTriggerKind.Manual;
    public int? DelayMinutes { get; set; }
    public int WindowMinutes { get; set; } = 20;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
