using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.ViewModels.Live;

namespace GrowDiary.Web.Tests;

public sealed class GrowAlertServiceTests
{
    [Fact]
    public void ResolveStateTone_GibtCriticalBeiDanger()
    {
        var alerts = new[]
        {
            new RecommendationCard { Severity = "info" },
            new RecommendationCard { Severity = "danger" }
        };

        var tone = GrowAlertService.ResolveStateTone(alerts, homeAssistantConfigured: true);

        Assert.Equal("critical", tone);
    }

    [Fact]
    public void ResolveStateTone_GibtHealthyOhneWarnungenUndMitHa()
    {
        var alerts = new[]
        {
            new RecommendationCard { Severity = "success" }
        };

        var tone = GrowAlertService.ResolveStateTone(alerts, homeAssistantConfigured: true);

        Assert.Equal("healthy", tone);
    }

    [Fact]
    public void ResolveStateToneFromDeviations_GibtCriticalBeiCriticalDeviation()
    {
        var deviations = new[]
        {
            new GrowDeviation { Severity = DeviationSeverity.Critical }
        };

        var tone = GrowAlertService.ResolveStateToneFromDeviations(deviations, homeAssistantConfigured: true);

        Assert.Equal("critical", tone);
    }

    [Fact]
    public void ResolveStateToneFromDeviations_GibtWarningBeiWarningDeviation()
    {
        var deviations = new[]
        {
            new GrowDeviation { Severity = DeviationSeverity.Warning }
        };

        var tone = GrowAlertService.ResolveStateToneFromDeviations(deviations, homeAssistantConfigured: true);

        Assert.Equal("attention", tone);
    }

    [Fact]
    public void ResolveStateToneFromDeviations_GibtHealthyOhneDeviations()
    {
        var tone = GrowAlertService.ResolveStateToneFromDeviations(Array.Empty<GrowDeviation>(), homeAssistantConfigured: true);

        Assert.Equal("healthy", tone);
    }

    [Theory]
    [InlineData("critical", "kritisch")]
    [InlineData("attention", "beobachten")]
    [InlineData("healthy", "stabil")]
    [InlineData("neutral", "neutral")]
    public void ResolveStateLabel_MapptBekannteTones(string tone, string expectedLabel)
    {
        var label = GrowAlertService.ResolveStateLabel(tone);

        Assert.Equal(expectedLabel, label);
    }

    [Fact]
    public void ToPayload_UebernimmtMetricCardFelder()
    {
        var metric = new MetricCard
        {
            Key = "air-temp",
            Label = "Lufttemperatur",
            Value = "24.1",
            Unit = "C",
            Tone = "ok",
            Hint = "im Zielbereich"
        };

        var payload = metric.ToPayload();

        Assert.Equal(metric.Key, payload.Key);
        Assert.Equal(metric.Label, payload.Label);
        Assert.Equal(metric.Value, payload.Value);
        Assert.Equal(metric.Unit, payload.Unit);
        Assert.Equal(metric.Tone, payload.Tone);
        Assert.Equal(metric.Hint, payload.Hint);
    }
}
