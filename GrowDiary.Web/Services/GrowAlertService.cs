using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

public sealed class GrowAlertService
{
    private readonly GrowRepository _repository;
    private readonly RecommendationEngine _recommendationEngine;
    private readonly DeviationAnalyzerService _deviationAnalyzer;
    private readonly TreatmentRecommender _treatmentRecommender;

    public GrowAlertService(
        GrowRepository repository,
        RecommendationEngine recommendationEngine,
        DeviationAnalyzerService deviationAnalyzer,
        TreatmentRecommender treatmentRecommender)
    {
        _repository = repository;
        _recommendationEngine = recommendationEngine;
        _deviationAnalyzer = deviationAnalyzer;
        _treatmentRecommender = treatmentRecommender;
    }

    public IReadOnlyList<RecommendationCard> BuildAlertsForGrow(GrowRun grow, int? maxCount = null)
    {
        var latest = _repository.GetLatestMeasurement(grow.Id);
        var previous = latest is null ? null : _repository.GetPreviousMeasurement(grow.Id, latest.TakenAt, latest.Id);
        var measurements = _repository.GetMeasurementsForGrow(grow.Id);
        var lastSolutionChangeAt = measurements
            .Where(x => x.SolutionChange)
            .OrderByDescending(x => x.TakenAt)
            .Select(x => (DateTime?)x.TakenAt)
            .FirstOrDefault();

        if (latest is not null && grow.IrrigationType == IrrigationType.ActiveHydro && grow.Profile.IsHydro)
        {
            var deviations = _deviationAnalyzer.Analyze(grow, measurements);
            var treatmentRecommendations = _treatmentRecommender.Recommend(grow, deviations).Recommendations;
            var diagnosticAlerts = _recommendationEngine.BuildCardsFromDiagnostics(grow, deviations, treatmentRecommendations);
            return maxCount is > 0 ? diagnosticAlerts.Take(maxCount.Value).ToList() : diagnosticAlerts;
        }

        var alerts = _recommendationEngine.Evaluate(grow, latest, previous, lastSolutionChangeAt);
        return maxCount is > 0 ? alerts.Take(maxCount.Value).ToList() : alerts;
    }

    public static string ResolveStateToneFromDeviations(IEnumerable<GrowDeviation> deviations, bool homeAssistantConfigured)
    {
        if (deviations.Any(deviation => deviation.Severity == DeviationSeverity.Critical))
        {
            return "critical";
        }

        if (deviations.Any(deviation => deviation.Severity == DeviationSeverity.Warning))
        {
            return "attention";
        }

        return homeAssistantConfigured ? "healthy" : "neutral";
    }

    public static string ResolveStateTone(IEnumerable<RecommendationCard> alerts, bool homeAssistantConfigured)
    {
        if (alerts.Any(a => a.Severity == "danger"))
        {
            return "critical";
        }

        if (alerts.Any(a => a.Severity == "warning"))
        {
            return "attention";
        }

        return homeAssistantConfigured ? "healthy" : "neutral";
    }

    public static string ResolveStateLabel(string tone)
        => tone switch
        {
            "critical" => "kritisch",
            "attention" => "beobachten",
            "healthy" => "stabil",
            _ => "neutral"
        };
}
