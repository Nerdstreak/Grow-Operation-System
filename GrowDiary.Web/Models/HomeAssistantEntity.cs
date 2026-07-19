namespace GrowDiary.Web.Models;

/// <summary>
/// A single Home Assistant entity as returned by <c>GET /api/states</c>, reduced
/// to the fields Grow OS needs to offer a searchable sensor picker (friendly
/// name, current value, unit and device class for filtering).
/// </summary>
public sealed class HomeAssistantEntity
{
    public required string EntityId { get; init; }
    public string? FriendlyName { get; init; }
    public string? State { get; init; }
    public string? UnitOfMeasurement { get; init; }
    public string? DeviceClass { get; init; }

    /// <summary>The entity domain (the part before the first dot, e.g. "sensor").</summary>
    public string Domain { get; init; } = string.Empty;
}
