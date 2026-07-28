using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Wann die Wirkung einer Dosis gezählt werden darf.
/// </summary>
/// <remarks>
/// Der Schritt, ohne den keine Pumpe je etwas lernt: bei jeder Dosis wurde der
/// Wert davor festgehalten, der danach von niemandem. Und der Zeitpunkt ist der
/// heikle Teil — zu früh misst man eine Schliere, zu spät die Pflanzen beim
/// Trinken.
/// </remarks>
public sealed class DosingFollowUpTests
{
    private static readonly DateTime Dosiert = new(2026, 7, 28, 10, 0, 0, DateTimeKind.Utc);
    private const int Mischzeit = 18;

    private static DoseEvent Dose(double ml = 3.0, double? vorher = 6.4, double? nachher = null, bool simuliert = false)
        => new()
        {
            Id = 1,
            OccurredAtUtc = Dosiert,
            Outcome = DoseOutcome.Done,
            RequestedMl = ml,
            DosedMl = ml,
            ValueBefore = vorher,
            ValueAfter = nachher,
            Simulated = simuliert,
        };

    [Fact]
    public void BeforeMixingIsDone_NothingIsRecorded()
    {
        // 17 von 18 Minuten: der Messwert zeigt noch eine Schliere, nicht das Becken.
        Assert.False(DosingFollowUp.IsReadyForEffect(Dose(), Mischzeit, Dosiert.AddMinutes(17)));
    }

    [Fact]
    public void OnceMixed_TheEffectIsRecorded()
    {
        Assert.True(DosingFollowUp.IsReadyForEffect(Dose(), Mischzeit, Dosiert.AddMinutes(18)));
        Assert.True(DosingFollowUp.IsReadyForEffect(Dose(), Mischzeit, Dosiert.AddMinutes(25)));
    }

    [Fact]
    public void AfterTheWindow_NothingIsRecordedAnymore()
    {
        // Nach zwei Mischzeiten haben Pflanzen getrunken, es wurde nachgefuellt,
        // vielleicht lief eine zweite Dosis. Die Aenderung hat dann andere
        // Ursachen — eingetragen waere daraus eine gelernte Luege.
        Assert.False(DosingFollowUp.IsReadyForEffect(Dose(), Mischzeit, Dosiert.AddMinutes(37)));
        Assert.True(DosingFollowUp.WindowHasClosed(Dose(), Mischzeit, Dosiert.AddMinutes(37)));
    }

    [Fact]
    public void TheWindowIsStillOpenWhileItIsUsable()
    {
        Assert.False(DosingFollowUp.WindowHasClosed(Dose(), Mischzeit, Dosiert.AddMinutes(30)));
    }

    [Fact]
    public void ADoseThatAlreadyHasItsEffect_IsNotTouchedAgain()
    {
        Assert.False(DosingFollowUp.IsReadyForEffect(Dose(nachher: 6.1), Mischzeit, Dosiert.AddMinutes(20)));
    }

    [Fact]
    public void ASimulatedDose_HasNoEffectToRecord()
    {
        // Im Testbetrieb ist nichts geflossen. Was sich danach aendert, hat eine
        // andere Ursache.
        Assert.False(DosingFollowUp.IsReadyForEffect(Dose(simuliert: true), Mischzeit, Dosiert.AddMinutes(20)));
    }

    [Fact]
    public void ARejectedRequest_HasNoEffectToRecord()
    {
        var abgelehnt = Dose();
        abgelehnt.Outcome = DoseOutcome.Rejected;
        abgelehnt.DosedMl = 0;

        Assert.False(DosingFollowUp.IsReadyForEffect(abgelehnt, Mischzeit, Dosiert.AddMinutes(20)));
    }

    [Fact]
    public void WithoutAValueBefore_ThereIsNothingToCompare()
    {
        // Die gelernte Wirkung ist die Differenz. Ohne den Wert davor gibt es
        // keine.
        Assert.False(DosingFollowUp.IsReadyForEffect(Dose(vorher: null), Mischzeit, Dosiert.AddMinutes(20)));
    }

    [Fact]
    public void AZeroMixingTime_DoesNotOpenTheWindowInstantly()
    {
        // Eine auf 0 gesetzte Mischzeit wuerde sonst sofort messen — und jede
        // Dosis mit ihrer eigenen Schliere lernen.
        Assert.False(DosingFollowUp.IsReadyForEffect(Dose(), 0, Dosiert.AddSeconds(30)));
        Assert.True(DosingFollowUp.IsReadyForEffect(Dose(), 0, Dosiert.AddMinutes(1)));
    }
}
