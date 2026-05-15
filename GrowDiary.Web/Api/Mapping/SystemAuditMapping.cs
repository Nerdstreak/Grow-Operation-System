using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Mapping;

public static class SystemAuditMapping
{
    public static SystemAuditEventDto ToDto(this SystemAuditEvent entry)
        => new(
            Id: entry.Id,
            EventType: entry.EventType,
            Action: entry.Action,
            Summary: entry.Summary,
            Severity: entry.Severity,
            Source: entry.Source,
            RemoteAddress: entry.RemoteAddress,
            RelatedGrowId: entry.RelatedGrowId,
            RelatedFileName: entry.RelatedFileName,
            Success: entry.Success,
            CreatedAtUtc: entry.CreatedAtUtc);
}
