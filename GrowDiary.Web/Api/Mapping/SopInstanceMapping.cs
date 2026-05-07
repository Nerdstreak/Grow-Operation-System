using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Mapping;

public static class SopInstanceMapping
{
    public static SopInstanceDto ToDto(this SopInstance instance) => new(
        Id: instance.Id,
        GrowId: instance.GrowId,
        SopId: instance.SopId,
        SopName: instance.SopName,
        SopType: instance.SopType,
        Status: instance.Status,
        Source: instance.Source,
        SourceRecommendationKey: instance.SourceRecommendationKey,
        TreatmentRecommendationStableKey: instance.TreatmentRecommendationStableKey,
        StartedAtUtc: instance.StartedAtUtc,
        CompletedAtUtc: instance.CompletedAtUtc,
        CancelledAtUtc: instance.CancelledAtUtc,
        DueAtUtc: instance.DueAtUtc,
        NextStepDueAtUtc: instance.NextStepDueAtUtc,
        RecurrenceIntervalDays: instance.RecurrenceIntervalDays,
        IsRecurring: instance.IsRecurring,
        Notes: instance.Notes,
        CreatedAtUtc: instance.CreatedAtUtc,
        UpdatedAtUtc: instance.UpdatedAtUtc,
        StepCount: instance.StepCount
    );

    public static SopStepInstanceDto ToDto(this SopStepInstance step) => new(
        Id: step.Id,
        SopInstanceId: step.SopInstanceId,
        StepId: step.StepId,
        Order: step.Order,
        Title: step.Title,
        Description: step.Description,
        StepType: step.StepType,
        Status: step.Status,
        WaitMinutes: step.WaitMinutes,
        SubSopId: step.SubSopId,
        ExpectedInputsJson: step.ExpectedInputsJson,
        PhotoRequired: step.PhotoRequired,
        PhotoRecommended: step.PhotoRecommended,
        DueAtUtc: step.DueAtUtc,
        AvailableAtUtc: step.AvailableAtUtc,
        ReminderTaskId: step.ReminderTaskId,
        StartedAtUtc: step.StartedAtUtc,
        CompletedAtUtc: step.CompletedAtUtc,
        SkippedAtUtc: step.SkippedAtUtc,
        Notes: step.Notes,
        MeasurementId: step.MeasurementId,
        JournalEntryId: step.JournalEntryId,
        PhotoAssetId: step.PhotoAssetId,
        CreatedAtUtc: step.CreatedAtUtc,
        UpdatedAtUtc: step.UpdatedAtUtc
    );
}
