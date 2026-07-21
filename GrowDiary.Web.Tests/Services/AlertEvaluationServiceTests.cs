using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

public sealed class AlertEvaluationServiceTests
{
    private static readonly DateTime Now = new(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);

    private static TentAlertRule Rule(double? min, double? max, string? lastState = null, DateTime? lastNotified = null, int cooldown = 30) => new()
    {
        Id = 1,
        TentId = 1,
        MetricKey = "reservoir-ph",
        MinValue = min,
        MaxValue = max,
        NotifyService = "notify.mobile_app_test",
        Enabled = true,
        CooldownMinutes = cooldown,
        LastState = lastState,
        LastNotifiedUtc = lastNotified,
    };

    [Fact]
    public void InRange_DoesNotNotify()
    {
        var decision = AlertEvaluationService.Decide(Rule(5.5, 6.5), 6.0, Now);

        Assert.Equal(AlertEvaluationService.InRange, decision.NewState);
        Assert.False(decision.SendBreach);
        Assert.False(decision.SendRecovery);
    }

    [Fact]
    public void FirstBreachBelow_Notifies()
    {
        var decision = AlertEvaluationService.Decide(Rule(5.5, 6.5, lastState: AlertEvaluationService.InRange), 5.0, Now);

        Assert.Equal(AlertEvaluationService.Below, decision.NewState);
        Assert.True(decision.SendBreach);
    }

    [Fact]
    public void FirstBreachAbove_Notifies()
    {
        var decision = AlertEvaluationService.Decide(Rule(5.5, 6.5, lastState: AlertEvaluationService.InRange), 7.0, Now);

        Assert.Equal(AlertEvaluationService.Above, decision.NewState);
        Assert.True(decision.SendBreach);
    }

    [Fact]
    public void ContinuousBreach_DoesNotRepeat()
    {
        // Already in the "Below" state and notified recently -> no repeat.
        var decision = AlertEvaluationService.Decide(
            Rule(5.5, 6.5, lastState: AlertEvaluationService.Below, lastNotified: Now.AddMinutes(-5)), 5.0, Now);

        Assert.Equal(AlertEvaluationService.Below, decision.NewState);
        Assert.False(decision.SendBreach);
    }

    [Fact]
    public void Flapping_WithinCooldown_DoesNotNotify()
    {
        // Recovered to in-range, notified 10 min ago, cooldown 30 -> re-breach stays quiet.
        var decision = AlertEvaluationService.Decide(
            Rule(5.5, 6.5, lastState: AlertEvaluationService.InRange, lastNotified: Now.AddMinutes(-10), cooldown: 30), 5.0, Now);

        Assert.Equal(AlertEvaluationService.Below, decision.NewState);
        Assert.False(decision.SendBreach);
    }

    [Fact]
    public void ReBreach_AfterCooldown_Notifies()
    {
        var decision = AlertEvaluationService.Decide(
            Rule(5.5, 6.5, lastState: AlertEvaluationService.InRange, lastNotified: Now.AddMinutes(-40), cooldown: 30), 5.0, Now);

        Assert.True(decision.SendBreach);
    }

    [Fact]
    public void ReturnToRange_AfterBreach_SendsRecovery()
    {
        var decision = AlertEvaluationService.Decide(
            Rule(5.5, 6.5, lastState: AlertEvaluationService.Above), 6.0, Now);

        Assert.Equal(AlertEvaluationService.InRange, decision.NewState);
        Assert.False(decision.SendBreach);
        Assert.True(decision.SendRecovery);
    }

    [Fact]
    public void StayingInRange_DoesNotSendRecovery()
    {
        var decision = AlertEvaluationService.Decide(
            Rule(5.5, 6.5, lastState: AlertEvaluationService.InRange), 6.0, Now);

        Assert.False(decision.SendRecovery);
    }

    [Fact]
    public void OnlyMaxBound_LowValueStaysInRange()
    {
        var decision = AlertEvaluationService.Decide(Rule(null, 6.5, lastState: AlertEvaluationService.InRange), 3.0, Now);

        Assert.Equal(AlertEvaluationService.InRange, decision.NewState);
        Assert.False(decision.SendBreach);
    }

    [Fact]
    public void OnlyMinBound_HighValueStaysInRange()
    {
        var decision = AlertEvaluationService.Decide(Rule(5.5, null, lastState: AlertEvaluationService.InRange), 9.0, Now);

        Assert.Equal(AlertEvaluationService.InRange, decision.NewState);
        Assert.False(decision.SendBreach);
    }
}
