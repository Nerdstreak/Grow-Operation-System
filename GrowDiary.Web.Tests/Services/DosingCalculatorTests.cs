using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Am Ende dieser Rechnung drückt eine Pumpe Säure in ein Becken mit lebenden
/// Pflanzen. Deshalb hier mehr Nachkommastellen an Sorgfalt als sonst.
/// </summary>
public sealed class DosingCalculatorTests
{
    [Fact]
    public void SecondsFor_TurnsMillilitresIntoRuntime()
    {
        // 46 ml/min ⇒ 3,5 ml brauchen 4,57 s.
        Assert.Equal(4.57, DosingCalculator.SecondsFor(3.5, 46), 2);
    }

    [Fact]
    public void SecondsAndMl_AreEachOthersInverse()
    {
        var seconds = DosingCalculator.SecondsFor(3.5, 46);

        Assert.Equal(3.5, DosingCalculator.MlFor(seconds, 46), 2);
    }

    [Fact]
    public void WithoutAFlowRate_ThereIsNoRuntime()
    {
        // Nicht kalibriert heisst: Milliliter sind keine Laufzeit. Lieber 0 als
        // eine angenommene Foerdermenge.
        Assert.Equal(0, DosingCalculator.SecondsFor(3.5, 0));
        Assert.Equal(0, DosingCalculator.SecondsFor(3.5, -1));
    }

    [Fact]
    public void Calibration_ExtrapolatesToTheMinute()
    {
        // 23 ml in 30 s ⇒ 46 ml/min.
        Assert.Equal(46, DosingCalculator.MlPerMinuteFrom(23, 30));
    }

    [Fact]
    public void Calibration_RefusesNonsense()
    {
        Assert.Null(DosingCalculator.MlPerMinuteFrom(0, 30));
        Assert.Null(DosingCalculator.MlPerMinuteFrom(23, 0));
    }

    // ---------- Lernen ----------

    private static DoseEvent Dose(double ml, double before, double after) => new()
    {
        Outcome = DoseOutcome.Done, DosedMl = ml, ValueBefore = before, ValueAfter = after,
    };

    [Fact]
    public void LearnedChange_NeedsThreeDosesBeforeItClaimsAnything()
    {
        var zwei = new[] { Dose(3.5, 6.4, 6.05), Dose(3.0, 6.3, 6.0) };

        Assert.Null(DosingCalculator.LearnedChangePerMl(zwei));
        Assert.NotNull(DosingCalculator.LearnedChangePerMl(zwei.Append(Dose(2.0, 6.25, 6.05))));
    }

    [Fact]
    public void LearnedChange_IsTheAverageEffectPerMillilitre()
    {
        // Jeweils −0,1 pH je ml.
        var doses = new[] { Dose(2, 6.4, 6.2), Dose(3, 6.3, 6.0), Dose(1, 6.2, 6.1) };

        Assert.Equal(-0.1, DosingCalculator.LearnedChangePerMl(doses)!.Value, 3);
    }

    [Fact]
    public void LearnedChange_IgnoresDosesWithoutAnAfterValue()
    {
        // Ohne Wert danach ist die Wirkung unbekannt — so eine Zeile darf den
        // Schnitt nicht verwaessern.
        var doses = new List<DoseEvent> { Dose(2, 6.4, 6.2), Dose(3, 6.3, 6.0), Dose(1, 6.2, 6.1) };
        doses.Add(new DoseEvent { Outcome = DoseOutcome.Done, DosedMl = 5, ValueBefore = 6.5, ValueAfter = null });

        Assert.Equal(-0.1, DosingCalculator.LearnedChangePerMl(doses)!.Value, 3);
    }

    [Fact]
    public void LearnedChange_IgnoresRejectedRequests()
    {
        var doses = new List<DoseEvent> { Dose(2, 6.4, 6.2), Dose(3, 6.3, 6.0), Dose(1, 6.2, 6.1) };
        doses.Add(new DoseEvent { Outcome = DoseOutcome.Rejected, DosedMl = 0, ValueBefore = 6.5, ValueAfter = 6.5 });

        Assert.Equal(-0.1, DosingCalculator.LearnedChangePerMl(doses)!.Value, 3);
    }

    // ---------- Menge bis zum Ziel ----------

    [Fact]
    public void WithoutExperience_NoAmountIsClaimed()
    {
        // Aus der Konzentration allein laesst sich die Menge nicht ausrechnen.
        Assert.Null(DosingCalculator.MlToReach(6.42, 6.05, null));
    }

    [Fact]
    public void OnlyHalfTheWay_IsDosedAtOnce()
    {
        // 0,37 pH bei −0,11 pH/ml waeren 3,36 ml fuer die ganze Strecke.
        // Gegeben wird die Haelfte: nach unten ist der pH schnell, zurueck nicht.
        var ml = DosingCalculator.MlToReach(6.42, 6.05, -0.11);

        Assert.Equal(1.68, ml!.Value, 2);
    }

    [Fact]
    public void APumpThatWorksTheWrongWay_DosesNothing()
    {
        // Eine Saeure kann einen zu NIEDRIGEN pH nicht heben.
        Assert.Null(DosingCalculator.MlToReach(5.6, 6.05, -0.11));
    }

    [Fact]
    public void ARaisingPump_WorksTheOtherDirection()
    {
        // pH-Plus: positive Wirkung je ml, Ist unter Ziel.
        var ml = DosingCalculator.MlToReach(5.6, 6.0, 0.1);

        Assert.Equal(2.0, ml!.Value, 2);
    }

    [Fact]
    public void AtTheTarget_NothingIsDosed()
    {
        Assert.Null(DosingCalculator.MlToReach(6.05, 6.05, -0.11));
    }
}
