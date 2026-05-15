namespace GrowDiary.Web.Models;

public sealed class SystemAuditEvent
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public string Source { get; set; } = "backend";
    public string? RemoteAddress { get; set; }
    public int? RelatedGrowId { get; set; }
    public string? RelatedFileName { get; set; }
    public bool Success { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
