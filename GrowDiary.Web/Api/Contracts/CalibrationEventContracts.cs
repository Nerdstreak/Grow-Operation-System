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
