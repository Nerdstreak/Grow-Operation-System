using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public sealed record SopInstanceDto(
    int Id,
    int GrowId,
    string SopId,
    string SopName,
    string SopType,
    SopInstanceStatus Status,
    SopStartSource Source,
    string? SourceRecommendationKey,
    string? TreatmentRecommendationStableKey,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int StepCount
);

public sealed record SopStepInstanceDto(
    int Id,
    int SopInstanceId,
    string StepId,
    int Order,
    string Title,
    string? Description,
    string StepType,
    SopStepInstanceStatus Status,
    int? WaitMinutes,
    string? SubSopId,
    string? ExpectedInputsJson,
    bool PhotoRequired,
    bool PhotoRecommended,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? SkippedAtUtc,
    string? Notes,
    int? MeasurementId,
    int? JournalEntryId,
    int? PhotoAssetId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public sealed class StartSopInstanceRequest
{
    public int GrowId { get; set; }
    public string SopId { get; set; } = string.Empty;
    public SopStartSource Source { get; set; } = SopStartSource.Manual;
    public string? SourceRecommendationKey { get; set; }
    public string? TreatmentRecommendationStableKey { get; set; }
    public string? Notes { get; set; }
}
