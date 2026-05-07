namespace GrowDiary.Web.Models;

public sealed class SopInstance
{
    public int Id { get; set; }
    public int GrowId { get; set; }
    public string SopId { get; set; } = string.Empty;
    public string SopName { get; set; } = string.Empty;
    public string SopType { get; set; } = string.Empty;
    public SopInstanceStatus Status { get; set; } = SopInstanceStatus.Active;
    public SopStartSource Source { get; set; } = SopStartSource.Manual;
    public string? SourceRecommendationKey { get; set; }
    public string? TreatmentRecommendationStableKey { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public int StepCount { get; set; }
}
