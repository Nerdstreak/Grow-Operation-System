namespace GrowDiary.Web.Api.Contracts;

public sealed record SystemAuditEventDto(
    int Id,
    string EventType,
    string Action,
    string Summary,
    string Severity,
    string Source,
    string? RemoteAddress,
    int? RelatedGrowId,
    string? RelatedFileName,
    bool Success,
    DateTime CreatedAtUtc);
