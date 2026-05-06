namespace GrowDiary.Web.Models;

public sealed class Setup
{
    public int Id { get; set; }
    public int TentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SetupType SetupType { get; set; } = SetupType.Production;
    public SetupStatus Status { get; set; } = SetupStatus.Planning;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
