namespace GrowDiary.Web.Models;

public sealed class LightSchedule
{
    public int Id { get; set; }
    public int TentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string LightsOnTime { get; set; } = "08:00";
    public string LightsOffTime { get; set; } = "20:00";
    public string? TimeZoneId { get; set; }
    public LightSource Source { get; set; } = LightSource.Manual;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
