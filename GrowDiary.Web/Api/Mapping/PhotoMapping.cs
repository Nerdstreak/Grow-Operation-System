using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Mapping;

public static class PhotoMapping
{
    public static PhotoAssetDto ToDto(this PhotoAsset photo) => new(
        Id: photo.Id,
        GrowId: photo.GrowId,
        MeasurementId: photo.MeasurementId,
        RelativePath: photo.RelativePath,
        Caption: photo.Caption,
        Tag: photo.Tag,
        Source: photo.Source,
        IsReferenceShot: photo.IsReferenceShot,
        TakenAtUtc: photo.TakenAtUtc
    );
}
