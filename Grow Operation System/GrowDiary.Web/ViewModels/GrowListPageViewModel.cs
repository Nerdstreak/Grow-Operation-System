using GrowDiary.Web.Models;

namespace GrowDiary.Web.ViewModels;

public sealed class GrowListPageViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Search { get; set; } = string.Empty;
    public bool ShowArchived { get; set; }
    public List<GrowRun> Grows { get; set; } = new();
    public DashboardStats Stats { get; set; } = new();
}
