using System.ComponentModel.DataAnnotations;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public sealed record CalibrationEventDto(
    int Id,
    int HardwareItemId,
    CalibrationEventType CalibrationType,
    CalibrationEventStatus Status,
    CalibrationResult Result,
    string Title,
    string? ReferenceSolution,
    decimal? ReferenceValue,
    decimal? BeforeValue,
    decimal? AfterValue,
    decimal? TemperatureC,
    DateTime? DueAtUtc,
    DateTime? PerformedAtUtc,
    DateTime? NextDueAtUtc,
    int? GrowTaskId,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public sealed class CreateCalibrationEventRequest
{
    public int HardwareItemId { get; set; }
    public CalibrationEventType CalibrationType { get; set; } = CalibrationEventType.Ph;
    public CalibrationEventStatus Status { get; set; } = CalibrationEventStatus.Planned;
    public CalibrationResult Result { get; set; } = CalibrationResult.Unknown;

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? ReferenceSolution { get; set; }
    public decimal? ReferenceValue { get; set; }
    public decimal? BeforeValue { get; set; }
    public decimal? AfterValue { get; set; }
    public decimal? TemperatureC { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? PerformedAtUtc { get; set; }
    public DateTime? NextDueAtUtc { get; set; }
    public int? GrowTaskId { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdateCalibrationEventRequest
{
    public int HardwareItemId { get; set; }
    public CalibrationEventType CalibrationType { get; set; } = CalibrationEventType.Ph;
    public CalibrationEventStatus Status { get; set; } = CalibrationEventStatus.Planned;
    public CalibrationResult Result { get; set; } = CalibrationResult.Unknown;

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? ReferenceSolution { get; set; }
    public decimal? ReferenceValue { get; set; }
    public decimal? BeforeValue { get; set; }
    public decimal? AfterValue { get; set; }
    public decimal? TemperatureC { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? PerformedAtUtc { get; set; }
    public DateTime? NextDueAtUtc { get; set; }
    public int? GrowTaskId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Was am Becken wirklich passiert ist — alles ausser dem Datum optional.</summary>
/// <remarks>
/// Bewusst schmal: wer kalibriert hat, soll das in zwei Sekunden festhalten
/// koennen. Die Messwerte sind fuer die, die ihre Sonde altern sehen wollen —
/// eine Sonde, die vorher immer weiter danebenliegt, ist bald fällig für den
/// Austausch, und das sieht man nur an der Reihe der Vorher-Werte.
/// </remarks>
public sealed class CompleteCalibrationEventRequest
{
    public DateTime? PerformedAtUtc { get; set; }
    public string? ReferenceSolution { get; set; }
    public decimal? ReferenceValue { get; set; }
    public decimal? BeforeValue { get; set; }
    public decimal? AfterValue { get; set; }
    public decimal? TemperatureC { get; set; }
    public string? Notes { get; set; }

    /// <summary>Kalibrierung misslungen — die Sonde nimmt den Referenzwert nicht mehr an.</summary>
    public bool Failed { get; set; }
}
