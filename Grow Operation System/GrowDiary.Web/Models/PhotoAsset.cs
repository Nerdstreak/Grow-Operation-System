namespace GrowDiary.Web.Models;

public sealed class PhotoAsset
{
    public int Id { get; set; }
    public int GrowId { get; set; }
    public int? MeasurementId { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public PhotoTag Tag { get; set; } = PhotoTag.Overview;
    public ValueOrigin Source { get; set; } = ValueOrigin.Manual;
    public bool IsReferenceShot { get; set; }
    public DateTime TakenAtUtc { get; set; } = DateTime.UtcNow;
}
