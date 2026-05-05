using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

public sealed class GrowAlertService
{
    private readonly GrowRepository _repository;
    private readonly RecommendationEngine _recommendationEngine;

    public GrowAlertService(GrowRepository repository, RecommendationEngine recommendationEngine)
    {
        _repository = repository;
        _recommendationEngine = recommendationEngine;
    }

    public IReadOnlyList<RecommendationCard> BuildAlertsForGrow(GrowRun grow, int? maxCount = null)
    {
        var latest = _repository.GetLatestMeasurement(grow.Id);
        var previous = latest is null ? null : _repository.GetPreviousMeasurement(grow.Id, latest.TakenAt, latest.Id);
        var lastSolutionChangeAt = _repository.GetMeasurementsForGrow(grow.Id)
            .Where(x => x.SolutionChange)
            .OrderByDescending(x => x.TakenAt)
            .Select(x => (DateTime?)x.TakenAt)
            .FirstOrDefault();

        var alerts = _recommendationEngine.Evaluate(grow, latest, previous, lastSolutionChangeAt);
        return maxCount is > 0 ? alerts.Take(maxCount.Value).ToList() : alerts;
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
