namespace GrowDiary.Web.Models;

public sealed class RiskEvent
{
    public int Id { get; set; }
    public RiskEventType EventType { get; set; } = RiskEventType.Other;
    public RiskEventSeverity Severity { get; set; } = RiskEventSeverity.Warning;
    public RiskEventStatus Status { get; set; } = RiskEventStatus.Open;
    public RiskEventSource Source { get; set; } = RiskEventSource.Manual;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? HardwareItemId { get; set; }
    public int? TentId { get; set; }
    public int? GrowId { get; set; }
    public int? TentSensorId { get; set; }
    public string? HaEntityId { get; set; }
    public int? SopInstanceId { get; set; }
    public int? GrowTaskId { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public string? DedupeKey { get; set; }
    public string? RawValue { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
