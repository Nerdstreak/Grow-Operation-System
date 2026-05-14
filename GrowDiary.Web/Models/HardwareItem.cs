namespace GrowDiary.Web.Models;

public sealed class HardwareItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public HardwareItemStatus Status { get; set; } = HardwareItemStatus.Active;
    public HardwareItemCriticality Criticality { get; set; } = HardwareItemCriticality.Medium;
    public int? TentId { get; set; }
    public int? SetupId { get; set; }
    public int? HydroSetupId { get; set; }
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
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
