namespace GrowDiary.Web.Models;

public sealed class LightTransitionEvent
{
    public int Id { get; set; }
    public int TentId { get; set; }
    public LightTransitionKind Kind { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public LightSource Source { get; set; } = LightSource.HomeAssistant;
    public string? RawState { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
