using System.Globalization;
using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

public sealed class DeviationRiskEventSyncService
{
    private const string DedupePrefix = "deviation:grow:";

    private readonly GrowRepository _repository;
    private readonly DeviationAnalyzerService _deviationAnalyzer;
    private readonly TreatmentRecommender _treatmentRecommender;

    public DeviationRiskEventSyncService(
        GrowRepository repository,
        DeviationAnalyzerService deviationAnalyzer,
        TreatmentRecommender treatmentRecommender)
    {
        _repository = repository;
        _deviationAnalyzer = deviationAnalyzer;
        _treatmentRecommender = treatmentRecommender;
    }

    public int SyncActiveGrowDeviations()
    {
        var changed = 0;
        foreach (var grow in _repository.GetActiveGrows())
        {
            changed += SyncGrow(grow);
        }

        return changed;
    }

    private int SyncGrow(GrowRun grow)
    {
        var measurements = _repository.GetMeasurementsForGrow(grow.Id);
        var deviations = _deviationAnalyzer
            .Analyze(grow, measurements)
            .Where(IsActionable)
            .GroupBy(deviation => deviation.StableKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(deviation => deviation.Severity).First())
            .ToList();
        var recommendations = _treatmentRecommender.Recommend(grow, deviations).Recommendations
            .GroupBy(recommendation => recommendation.DeviationStableKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var changed = 0;
        foreach (var deviation in deviations)
        {
            recommendations.TryGetValue(deviation.StableKey, out var recommendation);
            var beforeCount = _repository.GetRiskEvents().Count;
            _repository.CreateRiskEvent(ToRiskEvent(grow, deviation, recommendation));
            if (_repository.GetRiskEvents().Count != beforeCount)
            {
                changed++;
            }
        }

        changed += ResolveStaleDeviationRisks(grow.Id, deviations.Select(deviation => DedupeKey(grow.Id, deviation.StableKey)).ToHashSet(StringComparer.OrdinalIgnoreCase));
        return changed;
    }

    private int ResolveStaleDeviationRisks(int growId, ISet<string> currentDedupeKeys)
    {
        var stale = _repository.GetRiskEventsByGrow(growId)
            .Where(risk => risk.Source == RiskEventSource.Deviation)
            .Where(risk => risk.Status is RiskEventStatus.Open or RiskEventStatus.Acknowledged)
            .Where(risk => !string.IsNullOrWhiteSpace(risk.DedupeKey))
            .Where(risk => risk.DedupeKey!.StartsWith($"{DedupePrefix}{growId}:", StringComparison.OrdinalIgnoreCase))
            .Where(risk => !currentDedupeKeys.Contains(risk.DedupeKey!))
            .ToList();

        foreach (var risk in stale)
        {
            _repository.ResolveRiskEvent(risk.Id, DateTime.UtcNow, "Deviation aktuell nicht mehr erkannt.");
        }

        return stale.Count;
    }

    private static bool IsActionable(GrowDeviation deviation)
        => deviation.Severity is DeviationSeverity.Critical or DeviationSeverity.Warning;

    private static RiskEvent ToRiskEvent(GrowRun grow, GrowDeviation deviation, TreatmentRecommendationDto? recommendation)
    {
        var now = DateTime.UtcNow;
        var action = recommendation?.TreatmentName ?? recommendation?.SopTitle ?? deviation.RecommendationHint ?? deviation.Recommendation;
        var description = string.IsNullOrWhiteSpace(action)
            ? deviation.Message
            : $"{deviation.Message} Handlung: {action}";

        return new RiskEvent
        {
            EventType = ToRiskEventType(deviation),
            Severity = deviation.Severity == DeviationSeverity.Critical ? RiskEventSeverity.Critical : RiskEventSeverity.Warning,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Deviation,
            Title = $"{deviation.Metric}: {ToSeverityLabel(deviation.Severity)}",
            Description = description,
            TentId = grow.TentId,
            GrowId = grow.Id,
            StartedAtUtc = deviation.FirstDetectedAtUtc ?? now,
            LastSeenAtUtc = deviation.LastDetectedAtUtc ?? now,
            DedupeKey = DedupeKey(grow.Id, deviation.StableKey),
            RawValue = FormatRawValue(deviation),
            Notes = $"Grow: {grow.Name}"
        };
    }

    private static RiskEventType ToRiskEventType(GrowDeviation deviation)
        => deviation.Metric switch
        {
            DeviationMetric.DissolvedOxygen when deviation.Severity == DeviationSeverity.Critical => RiskEventType.CriticalDo,
            DeviationMetric.Ppfd => RiskEventType.LightMismatch,
            _ => RiskEventType.Other
        };

    private static string DedupeKey(int growId, string stableKey)
        => $"{DedupePrefix}{growId}:{stableKey}";

    private static string FormatRawValue(GrowDeviation deviation)
    {
        var actual = deviation.ActualValue?.ToString("0.##", CultureInfo.InvariantCulture) ?? "-";
        var target = deviation.TargetMin.HasValue && deviation.TargetMax.HasValue
            ? $"{deviation.TargetMin.Value.ToString("0.##", CultureInfo.InvariantCulture)}-{deviation.TargetMax.Value.ToString("0.##", CultureInfo.InvariantCulture)}"
            : deviation.TargetMin.HasValue
                ? $">={deviation.TargetMin.Value.ToString("0.##", CultureInfo.InvariantCulture)}"
                : deviation.TargetMax.HasValue
                    ? $"<={deviation.TargetMax.Value.ToString("0.##", CultureInfo.InvariantCulture)}"
                    : "-";
        return $"actual={actual};target={target};unit={deviation.Unit ?? ""}";
    }

    private static string ToSeverityLabel(DeviationSeverity severity)
        => severity == DeviationSeverity.Critical ? "kritische Abweichung" : "Abweichung prüfen";
}
