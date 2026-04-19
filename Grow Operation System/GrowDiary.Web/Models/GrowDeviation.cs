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
    Ok,
    Warning,
    Critical
}

public sealed class GrowDeviation
{
    public int GrowId { get; set; }
    public string GrowName { get; set; } = string.Empty;
    public DeviationMetric Metric { get; set; }
    public double? ActualValue { get; set; }
    public double TargetMin { get; set; }
    public double TargetMax { get; set; }
    public DeviationSeverity Severity { get; set; }

    /// <summary>
    /// Konkrete Handlungsempfehlung auf Deutsch – nicht "Wert zu hoch" sondern was genau zu tun ist.
    /// </summary>
    public string Recommendation { get; set; } = string.Empty;

    /// <summary>
    /// Wie viele Messungen in Folge der Wert bereits abweicht (für "seit X Messungen"-Logik).
    /// </summary>
    public int ConsecutiveCount { get; set; }
}
