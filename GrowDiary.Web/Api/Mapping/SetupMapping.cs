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
        CreatedAtUtc: setup.CreatedAtUtc,
        UpdatedAtUtc: setup.UpdatedAtUtc
    );

    public static Setup ToModel(this CreateSetupRequest request) => new()
    {
        TentId = request.TentId,
        Name = request.Name.Trim(),
        SetupType = request.SetupType,
        Status = SetupStatus.Planning,
        Notes = Normalize(request.Notes)
    };

    public static void ApplyTo(this UpdateSetupRequest request, Setup setup)
    {
        setup.Name = request.Name.Trim();
        setup.Status = request.Status;
        setup.Notes = Normalize(request.Notes);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
