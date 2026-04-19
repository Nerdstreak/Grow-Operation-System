using GrowDiary.Web.Models;

namespace GrowDiary.Web.ViewModels;

public sealed class KnowledgeIndexViewModel
{
    public List<MediumPlaybook> MediumPlaybooks { get; set; } = new();
    public List<NutrientProgram> NutrientPrograms { get; set; } = new();
    public string? SelectedKey { get; set; }
}
