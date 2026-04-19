using GrowDiary.Web.Models;

namespace GrowDiary.Web.ViewModels;

public sealed class ComparePageViewModel
{
    public List<GrowRun> GrowOptions { get; set; } = new();
    public int? LeftGrowId { get; set; }
    public int? RightGrowId { get; set; }
    public GrowRun? LeftGrow { get; set; }
    public GrowRun? RightGrow { get; set; }
    public Measurement? LeftMeasurement { get; set; }
    public Measurement? RightMeasurement { get; set; }
    public PhotoAsset? LeftPhoto { get; set; }
    public PhotoAsset? RightPhoto { get; set; }
    public IReadOnlyList<RecommendationCard> LeftRecommendations { get; set; } = Array.Empty<RecommendationCard>();
    public IReadOnlyList<RecommendationCard> RightRecommendations { get; set; } = Array.Empty<RecommendationCard>();
}
