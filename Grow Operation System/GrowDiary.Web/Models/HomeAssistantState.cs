namespace GrowDiary.Web.Models;

public sealed class HomeAssistantState
{
    public string EntityId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? FriendlyName { get; set; }
    public string? UnitOfMeasurement { get; set; }
    public DateTime? LastChanged { get; set; }
    public double? NumericValue { get; set; }
}
