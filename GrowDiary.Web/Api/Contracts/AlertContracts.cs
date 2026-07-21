namespace GrowDiary.Web.Api.Contracts;

public sealed record AlertRuleDto(
    string MetricKey,
    double? MinValue,
    double? MaxValue,
    string NotifyService,
    bool Enabled,
    int CooldownMinutes);

public sealed record TentAlertRulesDto(int TentId, IReadOnlyList<AlertRuleDto> Rules);

public sealed record SaveTentAlertRulesRequest(IReadOnlyList<AlertRuleDto> Rules);

public sealed record AlertTestRequest(string NotifyService);
