using GrowDiary.Web.Models;

namespace GrowDiary.Web.ViewModels;

public sealed class SettingsViewModel
{
    public HomeAssistantSettings HomeAssistant { get; set; } = new();
    public List<Tent> Tents { get; set; } = new();
    public List<GrowTemplate> Templates { get; set; } = new();
}
