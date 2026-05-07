using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public enum TreatmentRecommendationConfidence
{
    Low,
    Medium,
    High
}

public sealed record GrowTreatmentRecommendationDto(
    int GrowId,
    IReadOnlyList<TreatmentRecommendationDto> Recommendations
);

public sealed record TreatmentRecommendationDto(
    string StableKey,
    string DeviationStableKey,
    DeviationMetric Metric,
    DeviationSeverity Severity,
    string? SymptomId,
    string? TreatmentId,
    string? TreatmentName,
    string? SopId,
    string? SopTitle,
    TreatmentRecommendationConfidence Confidence,
    string Reason,
    IReadOnlyList<string> SafetyNotes,
    IReadOnlyList<string> SourceDocumentIds,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> ConflictTreatmentIds,
    bool? PhaseAllowed,
    IReadOnlyList<string> HardwareRequirements
);
