using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public sealed record SetupDto(
    int Id,
    int TentId,
    string Name,
    SetupType SetupType,
    SetupStatus Status,
    string? Notes,
    int? CloneCounterTotal,
    DateTime? LastCloneCutAt,
    string? MotherHealthStatus,
    DateTime? QuarantineStartedAt,
    DateTime? QuarantinePlannedEndAt,
    string? QuarantineResult,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);
