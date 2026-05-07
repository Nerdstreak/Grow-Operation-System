namespace GrowDiary.Web.Models;

public sealed class SopStepInstance
{
    public int Id { get; set; }
    public int SopInstanceId { get; set; }
    public string StepId { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string StepType { get; set; } = string.Empty;
    public SopStepInstanceStatus Status { get; set; } = SopStepInstanceStatus.Pending;
    public int? WaitMinutes { get; set; }
    public string? SubSopId { get; set; }
    public string? ExpectedInputsJson { get; set; }
    public bool PhotoRequired { get; set; }
    public bool PhotoRecommended { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? SkippedAtUtc { get; set; }
    public string? Notes { get; set; }
    public int? MeasurementId { get; set; }
    public int? JournalEntryId { get; set; }
    public int? PhotoAssetId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
