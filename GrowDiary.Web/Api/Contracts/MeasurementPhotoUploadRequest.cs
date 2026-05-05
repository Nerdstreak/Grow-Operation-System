using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Http;

namespace GrowDiary.Web.Api.Contracts;

public sealed class MeasurementPhotoUploadRequest
{
    public PhotoTag PhotoTag { get; set; } = PhotoTag.Overview;
    public string? PhotoCaption { get; set; }
    public bool UseAsReferenceShot { get; set; }
    public ValueOrigin Source { get; set; } = ValueOrigin.Manual;
    public List<IFormFile> Photos { get; set; } = new();
}
