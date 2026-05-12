using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services.Knowledge;
using GrowDiary.Web.Services.Knowledge.Schema;

namespace GrowDiary.Web.Services;

public sealed class RiskEventSopRecommender
{
    private const string EmergencyPowerRecoverySopId = "emergency-power-recovery";

    private readonly KnowledgeBaseLoader _knowledgeBase;
    private readonly GrowRepository _repository;

    public RiskEventSopRecommender(KnowledgeBaseLoader knowledgeBase, GrowRepository repository)
    {
        _knowledgeBase = knowledgeBase;
        _repository = repository;
    }

    public IReadOnlyList<RiskEventSopRecommendationDto> Recommend(RiskEvent riskEvent)
    {
        var sopId = GetMappedSopId(riskEvent);
        if (sopId is null)
        {
            return Array.Empty<RiskEventSopRecommendationDto>();
        }

        var sop = FindSop(sopId);
        if (sop is null)
        {
            return Array.Empty<RiskEventSopRecommendationDto>();
        }

        var activeInstance = riskEvent.GrowId.HasValue
            ? _repository.GetActiveSopInstancesByGrow(riskEvent.GrowId.Value)
                .FirstOrDefault(instance => string.Equals(instance.SopId, sop.Id, StringComparison.OrdinalIgnoreCase))
            : null;

        return
        [
            new RiskEventSopRecommendationDto(
                RiskEventId: riskEvent.Id,
                RiskEventType: riskEvent.EventType.ToString(),
                Severity: riskEvent.Severity.ToString(),
                SopId: sop.Id,
                SopName: sop.Name,
                Reason: BuildReason(riskEvent, sop),
                Confidence: GetConfidence(riskEvent.Severity),
                AlreadyActive: activeInstance is not null,
                ActiveSopInstanceId: activeInstance?.Id)
        ];
    }

    public SopDefinition? FindSop(string sopId)
        => _knowledgeBase.Sops.FirstOrDefault(sop => string.Equals(sop.Id, sopId, StringComparison.OrdinalIgnoreCase));

    private static string? GetMappedSopId(RiskEvent riskEvent)
        => riskEvent.EventType switch
        {
            RiskEventType.PowerOutage => EmergencyPowerRecoverySopId,
            RiskEventType.UpsOnBattery => EmergencyPowerRecoverySopId,
            RiskEventType.PumpOffline => EmergencyPowerRecoverySopId,
            RiskEventType.CriticalDo => EmergencyPowerRecoverySopId,
            RiskEventType.HomeAssistantUnavailable when riskEvent.Severity == RiskEventSeverity.Critical => EmergencyPowerRecoverySopId,
            _ => null
        };

    private static string GetConfidence(RiskEventSeverity severity)
        => severity switch
        {
            RiskEventSeverity.Critical => "High",
            RiskEventSeverity.Warning => "Medium",
            _ => "Low"
        };

    private static string BuildReason(RiskEvent riskEvent, SopDefinition sop)
        => $"{riskEvent.EventType} kann mit SOP '{sop.Name}' bearbeitet werden.";
}
