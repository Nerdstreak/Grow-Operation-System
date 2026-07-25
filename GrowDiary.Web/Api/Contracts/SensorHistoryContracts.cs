namespace GrowDiary.Web.Api.Contracts;

/// <summary>One point on a history curve. Min/Max are only set for daily resolution (the band).</summary>
public sealed record HistoryPointDto(DateTime T, double V, double? Min, double? Max);

/// <summary>One metric's curve over the requested window.</summary>
public sealed record HistorySeriesDto(
    string MetricKey,
    string Label,
    string? Unit,
    IReadOnlyList<HistoryPointDto> Points);

/// <summary>
/// A tent's sensor history. Several metrics come back in one response so a dashboard can
/// draw all its sparklines from a single request.
/// </summary>
public sealed record TentHistoryDto(
    int TentId,
    string Resolution,
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<HistorySeriesDto> Series);
