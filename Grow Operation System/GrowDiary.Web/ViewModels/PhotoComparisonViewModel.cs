using GrowDiary.Web.Models;

namespace GrowDiary.Web.ViewModels;

public sealed class PhotoComparisonViewModel
{
    public PhotoAsset? Latest { get; set; }
    public PhotoAsset? Previous { get; set; }
    public PhotoAsset? WeekBack { get; set; }
    public PhotoAsset? ReferenceShot { get; set; }
}
