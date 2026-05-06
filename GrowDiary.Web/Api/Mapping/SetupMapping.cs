using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Mapping;

public static class SetupMapping
{
    public static SetupDto ToDto(this Setup setup) => new(
        Id: setup.Id,
        TentId: setup.TentId,
        Name: setup.Name,
        SetupType: setup.SetupType,
        Status: setup.Status,
        Notes: setup.Notes,
        CloneCounterTotal: setup.CloneCounterTotal,
        LastCloneCutAt: setup.LastCloneCutAt,
        MotherHealthStatus: setup.MotherHealthStatus,
        QuarantineStartedAt: setup.QuarantineStartedAt,
        QuarantinePlannedEndAt: setup.QuarantinePlannedEndAt,
        QuarantineResult: setup.QuarantineResult,
        CreatedAtUtc: setup.CreatedAtUtc,
        UpdatedAtUtc: setup.UpdatedAtUtc
    );

    public static Setup ToModel(this CreateSetupRequest request) => new()
    {
        TentId = request.TentId,
        Name = request.Name.Trim(),
        SetupType = request.SetupType,
        Status = SetupStatus.Planning,
        Notes = Normalize(request.Notes),
        CloneCounterTotal = request.CloneCounterTotal,
        LastCloneCutAt = request.LastCloneCutAt,
        MotherHealthStatus = Normalize(request.MotherHealthStatus),
        QuarantineStartedAt = request.QuarantineStartedAt,
        QuarantinePlannedEndAt = request.QuarantinePlannedEndAt,
        QuarantineResult = Normalize(request.QuarantineResult)
    };

    public static void ApplyTo(this UpdateSetupRequest request, Setup setup)
    {
        setup.Name = request.Name.Trim();
        setup.Status = request.Status;
        setup.Notes = Normalize(request.Notes);
        setup.CloneCounterTotal = request.CloneCounterTotal;
        setup.LastCloneCutAt = request.LastCloneCutAt;
        setup.MotherHealthStatus = Normalize(request.MotherHealthStatus);
        setup.QuarantineStartedAt = request.QuarantineStartedAt;
        setup.QuarantinePlannedEndAt = request.QuarantinePlannedEndAt;
        setup.QuarantineResult = Normalize(request.QuarantineResult);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
