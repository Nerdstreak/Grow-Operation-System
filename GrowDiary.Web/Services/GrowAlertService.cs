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
        var legacyAlerts = _recommendationEngine.Evaluate(grow, latest, previous, lastSolutionChangeAt);

        if (latest is not null && grow.IrrigationType == IrrigationType.ActiveHydro && grow.Profile.IsHydro)
        {
            var deviations = _deviationAnalyzer.Analyze(grow, measurements);
            var treatmentRecommendations = _treatmentRecommender.Recommend(grow, deviations).Recommendations;
            var diagnosticAlerts = _recommendationEngine.BuildCardsFromDiagnostics(grow, deviations, treatmentRecommendations);
            var mergedAlerts = MergeDiagnosticAndLegacyAlerts(diagnosticAlerts, legacyAlerts);
            return ApplyMaxCount(mergedAlerts, maxCount);
        }

        return ApplyMaxCount(legacyAlerts, maxCount);
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

    private static IReadOnlyList<RecommendationCard> ApplyMaxCount(IReadOnlyList<RecommendationCard> alerts, int? maxCount)
        => maxCount is > 0 ? alerts.Take(maxCount.Value).ToList() : alerts;

    private static IReadOnlyList<RecommendationCard> MergeDiagnosticAndLegacyAlerts(
        IReadOnlyList<RecommendationCard> diagnosticAlerts,
        IReadOnlyList<RecommendationCard> legacyAlerts)
    {
        var legacyHasWarning = legacyAlerts.Any(IsWarningOrDanger);
        var merged = diagnosticAlerts
            .Where(alert => !(legacyHasWarning && alert.Severity == "success"))
            .ToList();
        var seenTitles = new HashSet<string>(merged.Select(alert => NormalizeTitle(alert.Title)), StringComparer.OrdinalIgnoreCase);
        var diagnosticProblemKeys = new HashSet<string>(
            merged.Where(IsWarningOrDanger).Select(alert => GetProblemKey(alert.Title)),
            StringComparer.OrdinalIgnoreCase);
        var hasDiagnosticSuccess = merged.Any(alert => alert.Severity == "success");

        foreach (var legacyAlert in legacyAlerts)
        {
            var normalizedTitle = NormalizeTitle(legacyAlert.Title);
            if (!seenTitles.Add(normalizedTitle))
            {
                continue;
            }

            if (legacyAlert.Severity == "success" && hasDiagnosticSuccess)
            {
                continue;
            }

            if (IsWarningOrDanger(legacyAlert) && diagnosticProblemKeys.Contains(GetProblemKey(legacyAlert.Title)))
            {
                continue;
            }

            merged.Add(legacyAlert);
            if (legacyAlert.Severity == "success")
            {
                hasDiagnosticSuccess = true;
            }
        }

        return merged;
    }

    private static bool IsWarningOrDanger(RecommendationCard alert)
        => alert.Severity is "danger" or "warning";

    private static string GetProblemKey(string title)
    {
        var normalized = NormalizeTitle(title);
        if (normalized.Contains("loesungswechsel") || normalized.Contains("wasserwechsel") || normalized.Contains("wechsel"))
        {
            return "solution-change";
        }

        if (normalized.Contains("wassertemperatur") || normalized.Contains("wassertemp") || normalized.Contains("watertemp"))
        {
            return "water-temp";
        }

        if (normalized.Contains("reservoirph") || normalized.Contains("ph"))
        {
            return "ph";
        }

        if (normalized.Contains("reservoirect") || normalized.Contains("ec"))
        {
            return "ec";
        }

        if (normalized.Contains("sauerstoff") || normalized.Contains("dissolvedoxygen") || normalized.Contains("do"))
        {
            return "do";
        }

        if (normalized.Contains("orp"))
        {
            return "orp";
        }

        if (normalized.Contains("ppfd") || normalized.Contains("licht"))
        {
            return "ppfd";
        }

        if (normalized.Contains("co2"))
        {
            return "co2";
        }

        return normalized;
    }

    private static string NormalizeTitle(string title)
        => new(title
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
}
