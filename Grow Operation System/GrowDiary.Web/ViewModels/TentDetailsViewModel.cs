using GrowDiary.Web.Models;

namespace GrowDiary.Web.ViewModels;

public sealed class TentDetailsViewModel
{
    public Tent Tent { get; set; } = new();
    public List<MetricCard> LiveMetrics { get; set; } = new();
    public ChartSeries? ClimateChart { get; set; }
    public ChartSeries? WaterChart { get; set; }
    public ChartSeries? ActivityChart { get; set; }
    public IReadOnlyList<RecommendationCard> Alerts { get; set; } = Array.Empty<RecommendationCard>();
    public List<GrowRun> ActiveGrows { get; set; } = new();
    public List<GrowRun> ArchivedGrows { get; set; } = new();
    public HomeAssistantSettings HomeAssistant { get; set; } = new();

    // Für Chart.js Sollwert-Bänder und Stats-Tabelle
    public bool HasHydroGrow { get; set; }
    public double VpdTargetMin { get; set; }
    public double VpdTargetMax { get; set; }
    public double PhTargetMin { get; set; }
    public double PhTargetMax { get; set; }
    public double EcTargetMin { get; set; }
    public double EcTargetMax { get; set; }
    public double WaterTempTargetMin { get; set; }
    public double WaterTempTargetMax { get; set; }
}
