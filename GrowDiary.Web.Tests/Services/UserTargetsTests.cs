using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Welcher Zielbereich gilt.
/// </summary>
/// <remarks>
/// Der Fall, der das ausgelöst hat: eingetragen 5,60–5,90, gemessen 5,99. Die
/// Kachel sagte „zu niedrig" (Ziel 6,00–6,10 aus dem Wissen), der Alarm sagte
/// „zu hoch" (über 5,90). Wer der Kachel folgte, hätte pH angehoben; wer dem
/// eigenen Wert folgte, gesenkt. Genau entgegengesetzt.
/// </remarks>
public sealed class UserTargetsTests
{
    private static TentAlertRule Rule(string key, double? min, double? max, bool enabled = true) => new()
    {
        TentId = 1, MetricKey = key, MinValue = min, MaxValue = max, Enabled = enabled, NotifyService = "",
    };

    private static HydroTargetValues Wissen() => new(
        PhMin: 6.0, PhMax: 6.1,
        EcMin: 0.6, EcMax: 0.8,
        OrpMin: 300, OrpMax: 400,
        WaterTempDayC: 21, WaterTempNightC: 19,
        VpdMin: 0.7, VpdMax: 0.9,
        PpfdMin: 400, PpfdMax: 600,
        Co2Min: 700, Co2Max: 900);

    // ---------- Was der Nutzer gesetzt hat ----------

    [Fact]
    public void WithoutRules_TheUserHasSetNothing()
    {
        Assert.Null(UserTargets.For("reservoir-ph", null));
        Assert.Null(UserTargets.For("reservoir-ph", []));
        Assert.False(UserTargets.IsUserSet("reservoir-ph", []));
    }

    [Fact]
    public void AnEnteredRange_IsFound()
    {
        var eigene = UserTargets.For("reservoir-ph", [Rule("reservoir-ph", 5.6, 5.9)]);

        Assert.Equal((5.6, 5.9), eigene);
    }

    [Fact]
    public void HalfARange_IsAllowed()
    {
        // Wer nur „nicht ueber 6,2" will, bekommt genau das.
        var eigene = UserTargets.For("reservoir-ph", [Rule("reservoir-ph", null, 6.2)]);

        Assert.Equal((null, 6.2), eigene);
    }

    [Fact]
    public void ASwitchedOffRule_DoesNotCount()
    {
        // Aus heisst aus, nicht „gilt heimlich weiter".
        Assert.Null(UserTargets.For("reservoir-ph", [Rule("reservoir-ph", 5.6, 5.9, enabled: false)]));
    }

    [Fact]
    public void AnEmptyRule_DoesNotCount()
    {
        // Eine Zeile ohne Zahlen ist keine Vorgabe.
        Assert.Null(UserTargets.For("reservoir-ph", [Rule("reservoir-ph", null, null)]));
    }

    [Fact]
    public void RulesForOtherMetrics_AreNotConfused()
    {
        var rules = new[] { Rule("reservoir-ec", 1.2, 1.6) };

        Assert.Null(UserTargets.For("reservoir-ph", rules));
        Assert.NotNull(UserTargets.For("reservoir-ec", rules));
    }

    // ---------- Ueberlagerung ----------

    [Fact]
    public void TheUsersValueBeatsTheKnowledge()
    {
        var result = UserTargets.Overlay(Wissen(), [Rule("reservoir-ph", 5.6, 5.9)]);

        Assert.Equal(5.6, result.PhMin);
        Assert.Equal(5.9, result.PhMax);
    }

    [Fact]
    public void WhatTheUserDidNotTouch_StaysWithTheKnowledge()
    {
        // Wichtig fuer die Phasenstaffelung: ein eigener pH darf nicht die
        // uebrigen Werte der Phase mitreissen.
        var result = UserTargets.Overlay(Wissen(), [Rule("reservoir-ph", 5.6, 5.9)]);

        Assert.Equal(0.6, result.EcMin);
        Assert.Equal(0.8, result.EcMax);
        Assert.Equal(0.7, result.VpdMin);
        Assert.Equal(300, result.OrpMin);
    }

    [Fact]
    public void OnlyTheSetHalfIsOverwritten()
    {
        var result = UserTargets.Overlay(Wissen(), [Rule("reservoir-ph", null, 6.4)]);

        Assert.Equal(6.0, result.PhMin);   // aus dem Wissen
        Assert.Equal(6.4, result.PhMax);   // vom Nutzer
    }

    [Fact]
    public void WithoutRules_TheKnowledgeIsUnchanged()
    {
        Assert.Equal(Wissen(), UserTargets.Overlay(Wissen(), null));
        Assert.Equal(Wissen(), UserTargets.Overlay(Wissen(), []));
    }

    [Fact]
    public void WaterTemperature_MapsOntoTheDayNightPair()
    {
        // Im Wissen steht ein Tag/Nacht-Paar, der Nutzer traegt eine Spanne ein.
        var result = UserTargets.Overlay(Wissen(), [Rule("reservoir-temp", 18, 20)]);

        Assert.Equal(18, result.WaterTempNightC);
        Assert.Equal(20, result.WaterTempDayC);
    }

    [Theory]
    [InlineData("reservoir-ec")]
    [InlineData("orp")]
    [InlineData("vpd")]
    [InlineData("ppfd")]
    [InlineData("co2")]
    public void EveryOverridableMetric_ActuallyOverrides(string key)
    {
        var vorher = UserTargets.Overlay(Wissen(), []);
        var nachher = UserTargets.Overlay(Wissen(), [Rule(key, 1.11, 2.22)]);

        Assert.NotEqual(vorher, nachher);
    }

    [Fact]
    public void TheReportedCase_NowHasOneAnswer()
    {
        // 5,99 bei eingetragenen 5,60–5,90: zu hoch. Und zwar ueberall, statt
        // „zu niedrig" auf der Kachel und „zu hoch" im Alarm.
        var result = UserTargets.Overlay(Wissen(), [Rule("reservoir-ph", 5.6, 5.9)]);

        Assert.True(5.99 > result.PhMax, "5,99 muss ueber der eigenen Obergrenze liegen.");
        Assert.False(5.99 < result.PhMin, "5,99 darf nicht gleichzeitig als zu niedrig gelten.");
    }
}
