using GrowDiary.Web.Models;

namespace GrowDiary.Web.ViewModels;

public sealed class TentDashboardCardViewModel
{
    public Tent Tent { get; set; } = new();
    public List<MetricCard> LiveMetrics { get; set; } = new();
    public IReadOnlyList<RecommendationCard> Alerts { get; set; } = Array.Empty<RecommendationCard>();
    public ChartSeries? ClimateSparkline { get; set; }
    public ChartSeries? WaterSparkline { get; set; }
    public DateTime? LastMeasurementAt { get; set; }
}
