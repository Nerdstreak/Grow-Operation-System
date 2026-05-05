using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public sealed record PhotoAssetDto(
    int Id,
    int GrowId,
    int? MeasurementId,
    string RelativePath,
    string? Caption,
    PhotoTag Tag,
    ValueOrigin Source,
    bool IsReferenceShot,
    DateTime TakenAtUtc
);
