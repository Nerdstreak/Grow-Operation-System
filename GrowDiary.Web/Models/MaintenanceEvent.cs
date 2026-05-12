namespace GrowDiary.Web.Models;

public sealed class MaintenanceEvent
{
    public int Id { get; set; }
    public int HardwareItemId { get; set; }
    public MaintenanceEventType EventType { get; set; } = MaintenanceEventType.Inspection;
    public MaintenanceEventStatus Status { get; set; } = MaintenanceEventStatus.Planned;
    public MaintenanceResult Result { get; set; } = MaintenanceResult.Unknown;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? PerformedAtUtc { get; set; }
    public DateTime? NextDueAtUtc { get; set; }
    public int? GrowTaskId { get; set; }
    public int? SopInstanceId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
