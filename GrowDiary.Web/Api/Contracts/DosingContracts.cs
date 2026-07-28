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
    /// <summary>Testbetrieb: rechnet und protokolliert, schaltet aber nichts.</summary>
    bool SimulationMode,
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
    /// <summary>Testbetrieb — ohne Hardware durchspielen.</summary>
    public bool SimulationMode { get; set; }
    /// <summary>Setzt das Schlauchdatum auf jetzt — „Schlauch gewechselt".</summary>
    public bool TubeChangedNow { get; set; }
}

/// <summary>
/// Der Kalibrierlauf — entweder eine Zielmenge oder eine feste Zeit.
/// </summary>
/// <remarks>
/// Die Zielmenge ist der genauere Weg: wer 23 ml abliest, liest sich leicht um
/// 1 ml, das sind 4 % Fehler in jeder späteren Dosis. Bei 100 ml ist derselbe
/// Ablesefehler 1 %. Sie setzt aber eine grobe Fördermenge voraus — beim
/// allerersten Mal weiss niemand, wie lange 100 ml dauern. Dann läuft es über
/// die Zeit, und ab der zweiten Runde über die Menge.
/// </remarks>
public sealed class CalibrationRunRequest
{
    public double Seconds { get; set; } = 30;

    /// <summary>Wenn gesetzt und die Fördermenge grob bekannt ist: so lange laufen, bis ungefähr so viel heraus ist.</summary>
    public double? TargetMl { get; set; }
}

/// <summary>Was im Messbecher stand, nach einem Lauf über <see cref="Seconds"/>.</summary>
public sealed class CalibrationResultRequest
{
    public double Seconds { get; set; } = 30;

    /// <summary>Was wirklich im Becher stand — daraus wird gerechnet, nicht aus der Zielmenge.</summary>
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
    string? Reason,
    bool Simulated);

/// <summary>Antwort auf eine Dosieranfrage — auch auf eine abgelehnte.</summary>
public sealed record DoseResultDto(
    bool Dosed,
    double Ml,
    double Seconds,
    string Reason);
