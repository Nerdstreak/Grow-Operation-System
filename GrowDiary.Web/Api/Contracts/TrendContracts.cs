namespace GrowDiary.Web.Api.Contracts;

/// <summary>A slow failure the holiday guard spotted, and the rule it rests on.</summary>
public sealed record TrendFindingDto(
    string Code,
    string Severity,
    string Headline,
    string Detail,
    string? GuidanceId);
