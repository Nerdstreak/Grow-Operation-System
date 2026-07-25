using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

public sealed class WatchdogServiceTests
{
    private static readonly DateTime Now = new(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);

    private static WatchdogInput Healthy(
        bool configured = true,
        int sensors = 4,
        int snapshotAgeMinutes = 2,
        int haAgeMinutes = 2,
        int? readingAgeMinutes = 3,
        string? haError = null) => new(
            configured,
            sensors,
            Now.AddMinutes(-snapshotAgeMinutes),
            Now.AddMinutes(-haAgeMinutes),
            readingAgeMinutes is { } age ? Now.AddMinutes(-age) : null,
            haError);

    [Fact]
    public void EverythingFresh_IsHealthy_AndSaysHowOldTheDataIs()
    {
        var verdict = WatchdogService.Evaluate(Healthy(readingAgeMinutes: 3), Now);

        Assert.Equal(WatchdogService.Ok, verdict.Code);
        Assert.False(verdict.IsProblem);
        Assert.Contains("3 Minuten", verdict.Detail);
    }

    [Fact]
    public void WithoutHomeAssistant_NothingToWatch()
    {
        var verdict = WatchdogService.Evaluate(Healthy(configured: false), Now);

        Assert.Equal(WatchdogService.Idle, verdict.Code);
        Assert.False(verdict.IsProblem);
    }

    [Fact]
    public void WithoutMappedSensors_NothingToWatch()
    {
        var verdict = WatchdogService.Evaluate(Healthy(sensors: 0), Now);

        Assert.Equal(WatchdogService.Idle, verdict.Code);
        Assert.False(verdict.IsProblem);
    }

    [Fact]
    public void StalledWorker_IsReportedFirst()
    {
        // Worker stopped: everything else is unknowable, so this must win over the others.
        var verdict = WatchdogService.Evaluate(Healthy(snapshotAgeMinutes: 40, haAgeMinutes: 40, readingAgeMinutes: 40), Now);

        Assert.Equal(WatchdogService.WorkerStalled, verdict.Code);
        Assert.True(verdict.IsProblem);
    }

    [Fact]
    public void NeverRanWorker_CountsAsStalled()
        => Assert.Equal(
            WatchdogService.WorkerStalled,
            WatchdogService.Evaluate(new WatchdogInput(true, 3, null, null, null, null), Now).Code);

    [Fact]
    public void HomeAssistantSilent_IsReported_WithReason()
    {
        var verdict = WatchdogService.Evaluate(Healthy(haAgeMinutes: 30, haError: "HttpRequestException"), Now);

        Assert.Equal(WatchdogService.HaUnreachable, verdict.Code);
        Assert.True(verdict.IsProblem);
        Assert.Contains("HttpRequestException", verdict.Detail);
    }

    [Fact]
    public void ConnectionUpButNoFreshReadings_IsReported()
    {
        var verdict = WatchdogService.Evaluate(Healthy(readingAgeMinutes: 45), Now);

        Assert.Equal(WatchdogService.NoData, verdict.Code);
        Assert.True(verdict.IsProblem);
    }

    [Fact]
    public void ShortHiccup_IsNotAProblem()
    {
        // One missed 5-minute round must not raise an alarm.
        var verdict = WatchdogService.Evaluate(Healthy(snapshotAgeMinutes: 7, haAgeMinutes: 7, readingAgeMinutes: 8), Now);

        Assert.Equal(WatchdogService.Ok, verdict.Code);
        Assert.False(verdict.IsProblem);
    }
}
