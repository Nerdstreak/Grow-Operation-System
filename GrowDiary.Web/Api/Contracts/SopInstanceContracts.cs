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
    DateTime? DueAtUtc,
    DateTime? NextStepDueAtUtc,
    int? RecurrenceIntervalDays,
    bool IsRecurring,
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
    DateTime? DueAtUtc,
    DateTime? AvailableAtUtc,
    int? ReminderTaskId,
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

    /// <summary>
    /// Answers to the SOP's branching questions, e.g. <c>{"severity": "severe"}</c>.
    /// Missing answers keep every step rather than dropping one.
    /// </summary>
    public Dictionary<string, string>? Answers { get; set; }

    /// <summary>
    /// How often a repeated block runs, e.g. <c>{"plant": 6}</c>. Absent means once.
    /// </summary>
    public Dictionary<string, int>? RepeatCounts { get; set; }
}

/// <summary>What has to be known before an SOP can be planned.</summary>
/// <param name="Suggested">
/// The option the app would pick from what it already knows — e.g. the
/// waterSource answer derived from the grow's water source. The UI preselects
/// it; the user can still override. Null when the app has no basis to suggest.
/// </param>
public sealed record SopChoiceDto(string Key, string? Prompt, IReadOnlyList<string> Options, string? Suggested = null);

/// <summary>The questions and repeat subjects of one SOP, so the UI can ask up front.</summary>
public sealed record SopPlanQuestionsDto(
    string SopId,
    IReadOnlyList<SopChoiceDto> Choices,
    IReadOnlyList<string> RepeatSubjects);

public sealed class UpdateSopStepInstanceRequest
{
    public SopStepInstanceStatus Status { get; set; } = SopStepInstanceStatus.Pending;
    public string? Notes { get; set; }
    public int? MeasurementId { get; set; }
    public int? JournalEntryId { get; set; }
    public int? PhotoAssetId { get; set; }
}
