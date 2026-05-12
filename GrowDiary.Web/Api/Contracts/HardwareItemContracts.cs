using System.ComponentModel.DataAnnotations;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public sealed record HardwareItemDto(
    int Id,
    string Name,
    string Category,
    HardwareItemStatus Status,
    HardwareItemCriticality Criticality,
    int? TentId,
    int? SetupId,
    int? GrowId,
    string? WearTemplateId,
    int? TentSensorId,
    string? HaEntityId,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    DateTime? InstalledAtUtc,
    DateTime? RetiredAtUtc,
    int? ExpectedLifespanDays,
    int? InspectionIntervalDays,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public sealed class CreateHardwareItemRequest
{
    public string? Name { get; set; }
    public string? Category { get; set; }
    public HardwareItemStatus Status { get; set; } = HardwareItemStatus.Active;
    public HardwareItemCriticality Criticality { get; set; } = HardwareItemCriticality.Medium;
    public int? TentId { get; set; }
    public int? SetupId { get; set; }
    public int? GrowId { get; set; }
    public string? WearTemplateId { get; set; }
    public int? TentSensorId { get; set; }
    public string? HaEntityId { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? InstalledAtUtc { get; set; }
    public DateTime? RetiredAtUtc { get; set; }
    public int? ExpectedLifespanDays { get; set; }
    public int? InspectionIntervalDays { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdateHardwareItemRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Category { get; set; } = string.Empty;

    public HardwareItemStatus Status { get; set; } = HardwareItemStatus.Active;
    public HardwareItemCriticality Criticality { get; set; } = HardwareItemCriticality.Medium;
    public int? TentId { get; set; }
    public int? SetupId { get; set; }
    public int? GrowId { get; set; }
    public string? WearTemplateId { get; set; }
    public int? TentSensorId { get; set; }
    public string? HaEntityId { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? InstalledAtUtc { get; set; }
    public DateTime? RetiredAtUtc { get; set; }
    public int? ExpectedLifespanDays { get; set; }
    public int? InspectionIntervalDays { get; set; }
    public string? Notes { get; set; }
}
