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
    public void TargetVolume_TurnsIntoARunTime()
    {
        // 100 ml bei 46 ml/min sind 130,4 s — deutlich mehr als die 60-s-Grenze
        // einer Dosis. Genau deshalb hat der Kalibrierlauf eine eigene.
        Assert.Equal(130.4, DosingCalculator.SecondsForTarget(100, 46)!.Value, 1);
        Assert.True(DosingCalculator.SecondsForTarget(100, 46) > DosingGuard.AbsoluteMaxSeconds);
        Assert.True(DosingCalculator.SecondsForTarget(100, 46) < DosingGuard.MaxCalibrationSeconds);
    }

    [Fact]
    public void WithoutAKnownRate_ThereIsNoTargetTime()
    {
        // Beim allerersten Mal weiss niemand, wie lange 100 ml dauern — dann
        // laeuft es ueber die Zeit, nicht ueber die Menge.
        Assert.Null(DosingCalculator.SecondsForTarget(100, null));
        Assert.Null(DosingCalculator.SecondsForTarget(100, 0));
        Assert.Null(DosingCalculator.SecondsForTarget(0, 46));
    }

    [Fact]
    public void ALargerCalibrationVolume_IsMoreForgivingOfMisreading()
    {
        // Der eigentliche Grund fuer die Zielmenge: 1 ml Ablesefehler wiegt bei
        // 23 ml viermal so schwer wie bei 100 ml.
        var kleinFalsch = DosingCalculator.MlPerMinuteFrom(23 + 1, 30)!.Value;
        var kleinRichtig = DosingCalculator.MlPerMinuteFrom(23, 30)!.Value;
        var grossFalsch = DosingCalculator.MlPerMinuteFrom(100 + 1, 130.4)!.Value;
        var grossRichtig = DosingCalculator.MlPerMinuteFrom(100, 130.4)!.Value;

        var fehlerKlein = Math.Abs(kleinFalsch - kleinRichtig) / kleinRichtig;
        var fehlerGross = Math.Abs(grossFalsch - grossRichtig) / grossRichtig;

        Assert.True(fehlerGross < fehlerKlein / 3, $"{fehlerGross:P1} muesste deutlich unter {fehlerKlein:P1} liegen.");
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
    public void LearnedChange_IgnoresSimulatedDoses()
    {
        // Im Testbetrieb ist nichts geflossen. Jede Aenderung danach hat eine
        // andere Ursache — sonst stuende unter „gelernt" eine Zahl, hinter der
        // nie ein Tropfen war.
        var doses = new List<DoseEvent> { Dose(2, 6.4, 6.2), Dose(3, 6.3, 6.0), Dose(1, 6.2, 6.1) };
        doses.Add(new DoseEvent
        {
            Outcome = DoseOutcome.Done, DosedMl = 5, ValueBefore = 6.5, ValueAfter = 5.0, Simulated = true,
        });

        Assert.Equal(-0.1, DosingCalculator.LearnedChangePerMl(doses)!.Value, 3);
    }

    [Fact]
    public void OnlySimulatedDoses_TeachNothing()
    {
        var doses = new[]
        {
            new DoseEvent { Outcome = DoseOutcome.Done, DosedMl = 2, ValueBefore = 6.4, ValueAfter = 6.2, Simulated = true },
            new DoseEvent { Outcome = DoseOutcome.Done, DosedMl = 3, ValueBefore = 6.3, ValueAfter = 6.0, Simulated = true },
            new DoseEvent { Outcome = DoseOutcome.Done, DosedMl = 1, ValueBefore = 6.2, ValueAfter = 6.1, Simulated = true },
        };

        Assert.Null(DosingCalculator.LearnedChangePerMl(doses));
    }

    [Fact]
    public void LearnedChange_IgnoresRejectedRequests()
    {
        var doses = new List<DoseEvent> { Dose(2, 6.4, 6.2), Dose(3, 6.3, 6.0), Dose(1, 6.2, 6.1) };
        doses.Add(new DoseEvent { Outcome = DoseOutcome.Rejected, DosedMl = 0, ValueBefore = 6.5, ValueAfter = 6.5 });

        Assert.Equal(-0.1, DosingCalculator.LearnedChangePerMl(doses)!.Value, 3);
    }

    // ---------- Volumen und Wasserwechsel ----------

    [Fact]
    public void AHalfEmptyReservoir_HalvesTheDose()
    {
        // Die gelernte Wirkung je ml stammt aus dem vollen Becken. In der
        // Haelfte Wasser wirkt dieselbe Menge fast doppelt — die Dosis muss
        // mitschrumpfen.
        Assert.Equal(0.5, DosingCalculator.VolumeFactor(12.5, 25), 2);
    }

    [Fact]
    public void AnOverfullReservoir_NeverScalesUp()
    {
        // Mehr Wasser macht die Dosis nur schwaecher — schwaecher ist die
        // sichere Richtung, hochskaliert wird nie.
        Assert.Equal(1.0, DosingCalculator.VolumeFactor(30, 25), 2);
    }

    [Fact]
    public void BelowThirtyPercent_TheFactorStopsShrinking()
    {
        // Bei so wenig Wasser stimmt meist etwas anderes nicht.
        Assert.Equal(0.3, DosingCalculator.VolumeFactor(2, 25), 2);
    }

    [Fact]
    public void WithoutAFillLevel_TheFactorIsOne()
    {
        Assert.Equal(1.0, DosingCalculator.VolumeFactor(null, 25), 2);
        Assert.Equal(1.0, DosingCalculator.VolumeFactor(12, null), 2);
        Assert.Equal(1.0, DosingCalculator.VolumeFactor(0, 25), 2);
    }

    [Fact]
    public void LearningIsCutAtTheLastWaterChange()
    {
        // Frisches Wasser puffert anders: Dosen aus dem alten Wasser wuerden
        // den Schnitt verwaessern. Vor dem Wechsel wirkte 1 ml −0,2; danach
        // −0,1 — gelernt werden darf nur das Danach.
        var wechsel = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);
        var doses = new[]
        {
            Alt(wechsel.AddDays(-3), 2, 6.4, 6.0),
            Alt(wechsel.AddDays(-2), 2, 6.4, 6.0),
            Alt(wechsel.AddDays(-1), 2, 6.4, 6.0),
            Alt(wechsel.AddDays(1), 2, 6.4, 6.2),
            Alt(wechsel.AddDays(2), 2, 6.4, 6.2),
            Alt(wechsel.AddDays(3), 2, 6.4, 6.2),
        };

        Assert.Equal(-0.1, DosingCalculator.LearnedChangePerMl(doses, wechsel)!.Value, 3);
        // Ohne Schnitt mischt sich beides.
        Assert.Equal(-0.15, DosingCalculator.LearnedChangePerMl(doses)!.Value, 3);
    }

    [Fact]
    public void TooFewDosesSinceTheChange_MeansNoClaim()
    {
        // Nach dem Wechsel erst zwei Dosen: lieber „keine Erfahrung" als eine
        // Zahl aus dem alten Wasser.
        var wechsel = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);
        var doses = new[]
        {
            Alt(wechsel.AddDays(-2), 2, 6.4, 6.0),
            Alt(wechsel.AddDays(-1), 2, 6.4, 6.0),
            Alt(wechsel.AddDays(1), 2, 6.4, 6.2),
            Alt(wechsel.AddDays(2), 2, 6.4, 6.2),
        };

        Assert.Null(DosingCalculator.LearnedChangePerMl(doses, wechsel));
    }

    private static DoseEvent Alt(DateTime at, double ml, double before, double after) => new()
    {
        OccurredAtUtc = at, Outcome = DoseOutcome.Done, DosedMl = ml, ValueBefore = before, ValueAfter = after,
    };

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
