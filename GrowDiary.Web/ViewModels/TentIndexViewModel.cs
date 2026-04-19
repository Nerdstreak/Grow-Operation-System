using GrowDiary.Web.Models;

namespace GrowDiary.Web.ViewModels;

public sealed class TentIndexViewModel
{
    public bool HomeAssistantConfigured { get; set; }
    public HomeAssistantSettings HomeAssistant { get; set; } = new();
    public List<TentDashboardCardViewModel> Cards { get; set; } = new();
    public TentDashboardCardViewModel? SelectedCard { get; set; }
    public int? SelectedTentId { get; set; }
}
