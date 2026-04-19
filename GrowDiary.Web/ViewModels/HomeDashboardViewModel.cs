using GrowDiary.Web.Models;

namespace GrowDiary.Web.ViewModels;

public sealed class HomeDashboardViewModel
{
    public bool HomeAssistantConfigured { get; set; }
    public DashboardStats Stats { get; set; } = new();
    public List<TentDashboardCardViewModel> Tents { get; set; } = new();
    public List<GrowRun> NeedsAttention { get; set; } = new();
    public List<GrowTask> DueSoonTasks { get; set; } = new();
    public List<PhotoAsset> RecentPhotos { get; set; } = new();
    public List<GrowDeviation> ActiveDeviations { get; set; } = new();
}
