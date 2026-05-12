using System.ComponentModel.DataAnnotations;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public sealed record MaintenanceEventDto(
    int Id,
    int HardwareItemId,
    MaintenanceEventType EventType,
    MaintenanceEventStatus Status,
    MaintenanceResult Result,
    string Title,
    string? Description,
    DateTime? DueAtUtc,
    DateTime? PerformedAtUtc,
    DateTime? NextDueAtUtc,
    int? GrowTaskId,
    int? SopInstanceId,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public sealed class CreateMaintenanceEventRequest
{
    public int HardwareItemId { get; set; }
    public MaintenanceEventType EventType { get; set; } = MaintenanceEventType.Inspection;
    public MaintenanceEventStatus Status { get; set; } = MaintenanceEventStatus.Planned;
    public MaintenanceResult Result { get; set; } = MaintenanceResult.Unknown;

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? PerformedAtUtc { get; set; }
    public DateTime? NextDueAtUtc { get; set; }
    public int? GrowTaskId { get; set; }
    public int? SopInstanceId { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdateMaintenanceEventRequest
{
    public int HardwareItemId { get; set; }
    public MaintenanceEventType EventType { get; set; } = MaintenanceEventType.Inspection;
    public MaintenanceEventStatus Status { get; set; } = MaintenanceEventStatus.Planned;
    public MaintenanceResult Result { get; set; } = MaintenanceResult.Unknown;

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? PerformedAtUtc { get; set; }
    public DateTime? NextDueAtUtc { get; set; }
    public int? GrowTaskId { get; set; }
    public int? SopInstanceId { get; set; }
    public string? Notes { get; set; }
}
