using GrowDiary.Web.Models;

namespace GrowDiary.Web.ViewModels;

public sealed class HarvestFormViewModel
{
    public int GrowId { get; set; }
    public string GrowName { get; set; } = string.Empty;
    public string HarvestedAtLocal { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
    public double? WetWeightG { get; set; }
    public double? DryWeightG { get; set; }
    public int? DryDays { get; set; }
    public string? YieldNotes { get; set; }
    public double? Rating { get; set; }
    public string? FlavorNotes { get; set; }
    public string? EffectNotes { get; set; }
    public string? NugStructure { get; set; }

    public static HarvestFormViewModel FromEntry(HarvestEntry entry, string growName)
        => new()
        {
            GrowId = entry.GrowId,
            GrowName = growName,
            HarvestedAtLocal = entry.HarvestedAt.ToString("yyyy-MM-dd"),
            WetWeightG = entry.WetWeightG,
            DryWeightG = entry.DryWeightG,
            DryDays = entry.DryDays,
            YieldNotes = entry.YieldNotes,
            Rating = entry.Rating,
            FlavorNotes = entry.FlavorNotes,
            EffectNotes = entry.EffectNotes,
            NugStructure = entry.NugStructure
        };

    public HarvestEntry ToEntry()
        => new()
        {
            GrowId = GrowId,
            HarvestedAt = DateTime.TryParse(HarvestedAtLocal, out var date) ? date : DateTime.Today,
            WetWeightG = WetWeightG,
            DryWeightG = DryWeightG,
            DryDays = DryDays,
            YieldNotes = string.IsNullOrWhiteSpace(YieldNotes) ? null : YieldNotes.Trim(),
            Rating = Rating,
            FlavorNotes = string.IsNullOrWhiteSpace(FlavorNotes) ? null : FlavorNotes.Trim(),
            EffectNotes = string.IsNullOrWhiteSpace(EffectNotes) ? null : EffectNotes.Trim(),
            NugStructure = string.IsNullOrWhiteSpace(NugStructure) ? null : NugStructure.Trim()
        };
}
