using System.Globalization;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Evaluates a tent's alert rules against its current live sensor values and pushes a
/// Home Assistant notification when a threshold is crossed. Edge-triggered with a cooldown
/// so a value that stays out of range (or flaps around the limit) does not spam the user.
/// </summary>
public sealed class AlertEvaluationService
{
    private readonly AlertRuleRepository _rules;
    private readonly NotificationService _notifications;
    private readonly ILogger<AlertEvaluationService> _logger;

    public AlertEvaluationService(
        AlertRuleRepository rules,
        NotificationService notifications,
        ILogger<AlertEvaluationService> logger)
    {
        _rules = rules;
        _notifications = notifications;
        _logger = logger;
    }

    public const string InRange = "InRange";
    public const string Below = "Below";
    public const string Above = "Above";

    public readonly record struct AlertDecision(string NewState, bool SendBreach, bool SendRecovery);

    /// <summary>
    /// Pure decision logic (no side effects) so it can be unit-tested exhaustively.
    /// Given a rule, the current value and the time, it returns the new persisted state and
    /// whether a breach or recovery notification should be sent.
    /// </summary>
    public static AlertDecision Decide(TentAlertRule rule, double value, DateTime nowUtc)
    {
        var breach =
            rule.MinValue is { } min && value < min ? Below :
            rule.MaxValue is { } max && value > max ? Above :
            InRange;

        if (breach == InRange)
        {
            var recovered = rule.LastState is Below or Above;
            return new AlertDecision(InRange, SendBreach: false, SendRecovery: recovered);
        }

        var changed = !string.Equals(rule.LastState, breach, StringComparison.Ordinal);
        var cooledDown = rule.LastNotifiedUtc is null
            || (nowUtc - rule.LastNotifiedUtc.Value) >= TimeSpan.FromMinutes(Math.Max(1, rule.CooldownMinutes));

        return new AlertDecision(breach, SendBreach: changed && cooledDown, SendRecovery: false);
    }

    public async Task EvaluateAsync(
        Tent tent,
        IReadOnlyDictionary<string, HomeAssistantState> states,
        CancellationToken cancellationToken = default)
    {
        var rules = _rules.GetEnabledForTent(tent.Id);
        if (rules.Count == 0)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        foreach (var rule in rules)
        {
            if (!states.TryGetValue(rule.MetricKey, out var state) || state.NumericValue is not { } value)
            {
                continue;
            }

            var decision = Decide(rule, value, nowUtc);

            try
            {
                if (decision.SendBreach)
                {
                    var sent = await _notifications.SendAsync(
                        NotificationCategory.Threshold, BuildTitle(tent), BuildBreachMessage(rule, value, decision.NewState), cancellationToken);
                    if (sent)
                    {
                        _rules.UpdateState(rule.Id, decision.NewState, nowUtc);
                        _logger.LogInformation("Alarm gesendet: Zelt {TentId}, {MetricKey} = {Value}.", tent.Id, rule.MetricKey, value);
                    }
                    // Not sent (quiet hours, notifications unconfigured, or HA unreachable):
                    // keep the previous state so the breach is retried on the next poll and
                    // fires once sending becomes possible, instead of being swallowed silently.
                }
                else if (decision.SendRecovery)
                {
                    await _notifications.SendAsync(
                        NotificationCategory.Threshold, BuildTitle(tent), BuildRecoveryMessage(rule, value), cancellationToken);
                    _rules.UpdateState(rule.Id, decision.NewState, rule.LastNotifiedUtc);
                }
                else if (!string.Equals(rule.LastState, decision.NewState, StringComparison.Ordinal))
                {
                    _rules.UpdateState(rule.Id, decision.NewState, rule.LastNotifiedUtc);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Alarm-Auswertung fehlgeschlagen: Zelt {TentId}, {MetricKey}.", tent.Id, rule.MetricKey);
            }
        }
    }

    private static string BuildTitle(Tent tent) => $"🌱 Grow OS · {tent.Name}";

    private static string BuildBreachMessage(TentAlertRule rule, double value, string breach)
    {
        var (label, unit) = MetricDisplay(rule.MetricKey);
        var direction = breach == Below ? "unter" : "über";
        var limit = breach == Below ? rule.MinValue : rule.MaxValue;
        var limitText = limit is { } l ? $" (Grenze {Format(l)}{unit})" : string.Empty;
        return $"{label} {direction} Zielbereich: {Format(value)}{unit}{limitText}.";
    }

    private static string BuildRecoveryMessage(TentAlertRule rule, double value)
    {
        var (label, unit) = MetricDisplay(rule.MetricKey);
        return $"{label} wieder im Zielbereich: {Format(value)}{unit}.";
    }

    private static string Format(double value)
    {
        var rounded = Math.Round(value, 2);
        return rounded.ToString(rounded == Math.Truncate(rounded) ? "0.##" : "0.##", CultureInfo.InvariantCulture);
    }

    public static (string Label, string Unit) MetricDisplay(string metricKey) => metricKey switch
    {
        "reservoir-ph" => ("pH", ""),
        "reservoir-ec" => ("EC", " mS/cm"),
        "reservoir-temp" => ("Wassertemp.", " °C"),
        "reservoir-level" => ("Wasserstand", " L"),
        "reservoir-level-cm" => ("Wasserstand", " cm"),
        "orp" => ("ORP", " mV"),
        "dissolved-oxygen" => ("DO", " mg/L"),
        "temperature" => ("Lufttemp.", " °C"),
        "humidity" => ("Luftfeuchte", " %"),
        "vpd" => ("VPD", " kPa"),
        "co2" => ("CO₂", " ppm"),
        "ppfd" => ("PPFD", " µmol/m²/s"),
        _ => (metricKey, ""),
    };
}
