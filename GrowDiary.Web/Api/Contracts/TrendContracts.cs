namespace GrowDiary.Web.Api.Contracts;

/// <summary>A slow failure the holiday guard spotted, and the rule it rests on.</summary>
public sealed record TrendFindingDto(
    string Code,
    string Severity,
    string Headline,
    string Detail,
    string? GuidanceId);

/// <summary>One row of the SOP's diagnostic table, as the data reads it.</summary>
public sealed record StabilitySignalDto(string Key, string Label, string Verdict, string Observation);

/// <summary>
/// SOP-N1 §2.1 applied to the recent readings: the five signals together, plus the checks
/// no sensor can make.
/// </summary>
public sealed record StabilityAssessmentDto(
    string Overall,
    string Headline,
    string Detail,
    IReadOnlyList<StabilitySignalDto> Signals,
    IReadOnlyList<string> VisualChecks);
