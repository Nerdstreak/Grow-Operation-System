namespace GrowDiary.Web.Models;

public enum DeviationMetric
{
    Ph,
    Ec,
    Orp,
    WaterTemp,
    Vpd,
    Ppfd,
    Co2,
    DissolvedOxygen,
    GerminationStatus
}

public enum DeviationSeverity
{
    Info,
    Warning,
    Critical
}

public enum DeviationSource
{
    Manual,
    HomeAssistant,
    Mixed,
    Unknown
}

public sealed class GrowDeviation
{
    public int GrowId { get; set; }
    public string GrowName { get; set; } = string.Empty;
    public string StableKey { get; set; } = string.Empty;
    public DeviationMetric Metric { get; set; }
    public double? ActualValue { get; set; }
    public double? TargetMin { get; set; }
    public double? TargetMax { get; set; }
    public string? Unit { get; set; }
    public DeviationSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? RecommendationHint { get; set; }
    public string? SymptomId { get; set; }
    public List<int> SourceMeasurementIds { get; set; } = [];

    public string Recommendation { get; set; } = string.Empty;
    public int ConsecutiveCount { get; set; }
    public DateTime? FirstDetectedAtUtc { get; set; }
    public DateTime? LastDetectedAtUtc { get; set; }
    public DeviationSource Source { get; set; } = DeviationSource.Unknown;
}
