using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Die Riegel, die aus dem echten Becken kommen — nicht aus dem Code.
/// </summary>
/// <remarks>
/// Drei Regeln der Praxis: in stehendes Wasser dosiert niemand (das Konzentrat
/// bleibt an einer Stelle), die Mischpause gehört dem Becken und nicht der
/// Pumpe, und solange eine Düngung unvollständig ist, wird nichts anderes
/// dosiert.
/// </remarks>
public sealed class DosingRealWorldGuardTests
{
    private static readonly DateTime Jetzt = new(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);

    private static DosingPump Pump(bool simulation = false, bool autoOff = true) => new()
    {
        Id = 1,
        TentId = 1,
        Name = "pH Minus",
        Purpose = DosingPurpose.PhDown,
        HaEntityId = "switch.ph_minus",
        MlPerMinute = 46,
        MaxSingleDoseMl = 5,
        MinIntervalMinutes = 18,
        MaxDosesPerDay = 6,
        MaxMlPerDay = 25,
        AutomationEnabled = true,
        HasHomeAssistantAutoOff = autoOff,
        SimulationMode = simulation,
    };

    private static DosingContext Context(
        bool? circulation = null,
        bool tentPending = false,
        DateTime? lastTentDose = null)
        => new(
            Reading: 6.4, ReadingAge: TimeSpan.FromMinutes(2),
            ProbeCalibratedAtUtc: Jetzt.AddDays(-2), ProbeCalibrationOverdue: false,
            DosesToday: [], WaterLevelOk: null,
            LastTentDoseUtc: lastTentDose,
            TentHasPendingDose: tentPending,
            CirculationOn: circulation);

    // ---------- A1: Umwaelzung ----------

    [Fact]
    public void AStoppedCirculationPump_BlocksEvenAManualDose()
    {
        // Ohne Umwaelzung verteilt sich nichts: ein Topf bekommt das Konzentrat
        // ab, dort verbrennen Wurzeln.
        var decision = DosingGuard.Evaluate(Pump(), 3, Context(circulation: false), Jetzt);

        Assert.False(decision.Allowed);
        Assert.Contains("Umwälzpumpe", decision.Reason);
    }

    [Fact]
    public void UnknownCirculation_DoesNotBlockAManualDose()
    {
        // Kein Sensor gemappt: wer von Hand drueckt, steht daneben und hoert
        // die Pumpe. Blocken wuerde jede Anlage ohne Sensor lahmlegen.
        Assert.True(DosingGuard.Evaluate(Pump(), 3, Context(circulation: null), Jetzt).Allowed);
    }

    [Fact]
    public void TheAutomation_RequiresConfirmedCirculation()
    {
        // Unbeaufsichtigt reicht „unbekannt" nicht: eine stehende Umwaelzpumpe
        // ist oft genau der Grund, warum die Werte driften, die die Automatik
        // korrigieren will.
        Assert.False(DosingGuard.EvaluateAutomatic(Pump(), 3, Context(circulation: null), Jetzt).Allowed);
        Assert.False(DosingGuard.EvaluateAutomatic(Pump(), 3, Context(circulation: false), Jetzt).Allowed);
        Assert.True(DosingGuard.EvaluateAutomatic(Pump(), 3, Context(circulation: true), Jetzt).Allowed);
    }

    [Fact]
    public void InSimulation_TheAutomationSkipsTheCirculationCheck()
    {
        // Im Testbetrieb fliesst nichts — dort darf ohne Sensor durchgespielt
        // werden, sonst liesse sich Stufe 3 nie ohne Hardware ansehen.
        Assert.True(DosingGuard.EvaluateAutomatic(Pump(simulation: true), 3, Context(circulation: null), Jetzt).Allowed);
    }

    // ---------- A2: Mischpause gehoert dem Becken ----------

    [Fact]
    public void ADoseFromAnotherPump_StartsTheMixingPauseForEveryone()
    {
        // Vor 5 Minuten hat irgendeine Pumpe dieses Zelts dosiert. Der Messwert
        // zeigt noch die Schliere — egal, wer als Naechstes dosieren will.
        var decision = DosingGuard.Evaluate(Pump(), 3, Context(lastTentDose: Jetzt.AddMinutes(-5)), Jetzt);

        Assert.False(decision.Allowed);
        Assert.Contains("mischen", decision.Reason);
    }

    [Fact]
    public void AfterTheTentWideMixingPause_DosingIsAllowedAgain()
    {
        Assert.True(DosingGuard.Evaluate(Pump(), 3, Context(lastTentDose: Jetzt.AddMinutes(-20)), Jetzt).Allowed);
    }

    // ---------- A3: unvollstaendige Duengung sperrt das Zelt ----------

    [Fact]
    public void AnOutstandingSecondHalf_BlocksEveryPumpOfTheTent()
    {
        // A ist gegeben, B wartet noch. Eine pH-Dosis dazwischen korrigierte
        // einen Zustand, den B gleich wieder verschiebt.
        var decision = DosingGuard.Evaluate(Pump(), 3, Context(tentPending: true), Jetzt);

        Assert.False(decision.Allowed);
        Assert.Contains("zweite Dünger-Hälfte", decision.Reason);
    }

    [Fact]
    public void TheAutomationRespectsTheSameHold()
    {
        Assert.False(DosingGuard.EvaluateAutomatic(Pump(), 3, Context(circulation: true, tentPending: true), Jetzt).Allowed);
    }
}
