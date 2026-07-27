namespace GrowDiary.Web.Api.Contracts;

public sealed record DosingPumpDto(
    int Id,
    int TentId,
    string Name,
    string Purpose,
    string? Agent,
    double? ConcentrationPercent,
    string HaEntityId,
    double? MlPerMinute,
    DateTime? CalibratedAtUtc,
    DateTime? TubeChangedAtUtc,
    int? CalibrationIntervalDays,
    int? TubeIntervalDays,
    double MaxSingleDoseMl,
    int MinIntervalMinutes,
    int MaxDosesPerDay,
    double MaxMlPerDay,
    int MaxReadingAgeMinutes,
    bool AutomationEnabled,
    bool HasHomeAssistantAutoOff,
    /// <summary>Der Messwert, gegen den die Pumpe arbeitet; null bei „frei".</summary>
    string? MetricKey,
    /// <summary>Was aus dem Protokoll gelernt wurde — Änderung je ml; null bis genug Daten da sind.</summary>
    double? LearnedChangePerMl,
    /// <summary>Wie viele ausgewertete Dosen dahinterstehen.</summary>
    int LearnedFromDoses,
    /// <summary>Was gerade dagegen spricht zu dosieren; null = frei.</summary>
    string? BlockedReason);

public sealed class DosingPumpUpsertRequest
{
    public int TentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Purpose { get; set; } = "Custom";
    public string? Agent { get; set; }
    public double? ConcentrationPercent { get; set; }
    public string HaEntityId { get; set; } = string.Empty;
    public int? CalibrationIntervalDays { get; set; }
    public int? TubeIntervalDays { get; set; }
    public double? MaxSingleDoseMl { get; set; }
    public int? MinIntervalMinutes { get; set; }
    public int? MaxDosesPerDay { get; set; }
    public double? MaxMlPerDay { get; set; }
    public int? MaxReadingAgeMinutes { get; set; }
    public bool AutomationEnabled { get; set; }
    public bool HasHomeAssistantAutoOff { get; set; }
    /// <summary>Setzt das Schlauchdatum auf jetzt — „Schlauch gewechselt".</summary>
    public bool TubeChangedNow { get; set; }
}

/// <summary>Der Kalibrierlauf: so viele Sekunden laufen lassen.</summary>
public sealed class CalibrationRunRequest
{
    public double Seconds { get; set; } = 30;
}

/// <summary>Was im Messbecher stand, nach einem Lauf über <see cref="Seconds"/>.</summary>
public sealed class CalibrationResultRequest
{
    public double Seconds { get; set; } = 30;
    public double MeasuredMl { get; set; }
}

public sealed class ManualDoseRequest
{
    public double Ml { get; set; }
}

public sealed record DoseEventDto(
    int Id,
    int PumpId,
    string PumpName,
    DateTime OccurredAtUtc,
    string Trigger,
    string Outcome,
    double RequestedMl,
    double DosedMl,
    double SecondsRun,
    double? ValueBefore,
    double? ValueAfter,
    double? TargetValue,
    string? Reason);

/// <summary>Antwort auf eine Dosieranfrage — auch auf eine abgelehnte.</summary>
public sealed record DoseResultDto(
    bool Dosed,
    double Ml,
    double Seconds,
    string Reason);
