using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Zweikomponenten-Dünger: das Verhältnis muss stimmen, und A und B dürfen sich
/// nicht konzentriert begegnen.
/// </summary>
/// <remarks>
/// Konzentriert fällt das Calcium aus A mit den Sulfaten und Phosphaten aus B
/// als Gips aus. Was ausgeflockt ist, erreicht die Pflanze nie — und der
/// naheliegende Schluss aus einem EC, der trotz Dünger nicht steigt, ist
/// „zu wenig gegeben".
/// </remarks>
public sealed class PartnerDosingTests
{
    private static readonly DateTime Jetzt = new(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);

    private static DosingPump Pump(int id = 1, int? partnerId = 2, double ratio = 1, int delay = 5, int tentId = 1)
        => new()
        {
            Id = id,
            TentId = tentId,
            Name = $"Pumpe {id}",
            PartnerPumpId = partnerId,
            PartnerRatio = ratio,
            PartnerDelayMinutes = delay,
        };

    // ---------- Menge ----------

    [Fact]
    public void AtOneToOne_ThePartnerGetsTheSameAmount()
    {
        Assert.Equal(4.0, PartnerDosing.PartnerMl(Pump(ratio: 1), 4.0));
    }

    [Fact]
    public void TheRatioIsApplied()
    {
        // Nicht jeder Zweikomponenten-Duenger ist 1:1.
        Assert.Equal(2.0, PartnerDosing.PartnerMl(Pump(ratio: 0.5), 4.0));
        Assert.Equal(6.0, PartnerDosing.PartnerMl(Pump(ratio: 1.5), 4.0));
    }

    [Fact]
    public void WithoutAPartner_NothingIsPlanned()
    {
        Assert.Null(PartnerDosing.PartnerMl(Pump(partnerId: null), 4.0));
    }

    [Fact]
    public void AnUnusableRatio_PlansNothing()
    {
        // Lieber gar keine zweite Dosis als eine geratene: bei falschem
        // Verhaeltnis stimmt das ganze Naehrstoffprofil nicht mehr.
        Assert.Null(PartnerDosing.PartnerMl(Pump(ratio: 0), 4.0));
        Assert.Null(PartnerDosing.PartnerMl(Pump(ratio: -1), 4.0));
    }

    [Fact]
    public void WithoutAnAmount_NothingIsPlanned()
    {
        Assert.Null(PartnerDosing.PartnerMl(Pump(), 0));
    }

    // ---------- Zeit ----------

    [Fact]
    public void ThePartnerRunsAfterTheSeparationTime()
    {
        Assert.Equal(Jetzt.AddMinutes(5), PartnerDosing.PartnerDueAt(Pump(delay: 5), Jetzt));
    }

    [Fact]
    public void AZeroSeparationTime_IsRaisedToTheMinimum()
    {
        // Null Minuten waeren keine Trennung, sondern zwei Pumpen, die praktisch
        // zusammen laufen — genau der Fall, den das hier verhindern soll.
        Assert.Equal(Jetzt.AddMinutes(PartnerDosing.MinDelayMinutes), PartnerDosing.PartnerDueAt(Pump(delay: 0), Jetzt));
    }

    // ---------- Riegel ----------

    [Fact]
    public void WhileAHalfIsOutstanding_NeitherPumpMayStart()
    {
        // Sonst gaebe eine zweite Anforderung A ein zweites Mal, waehrend das
        // erste B noch wartet.
        Assert.True(PartnerDosing.IsBlockedByPending(new[] { new PendingDose { PumpId = 2, Ml = 4 } }));
        Assert.False(PartnerDosing.IsBlockedByPending(Array.Empty<PendingDose>()));
    }

    // ---------- Einrichtung ----------

    [Fact]
    public void APumpWithoutAPartner_IsAlwaysValid()
    {
        Assert.Null(PartnerDosing.Validate(Pump(partnerId: null), null));
    }

    [Fact]
    public void AMissingPartner_IsRejected()
    {
        Assert.Contains("existiert nicht", PartnerDosing.Validate(Pump(), null)!);
    }

    [Fact]
    public void APumpCannotBeItsOwnPartner()
    {
        var pump = Pump(id: 1, partnerId: 1);

        Assert.Contains("eigener Partner", PartnerDosing.Validate(pump, pump)!);
    }

    [Fact]
    public void APartnerInAnotherTent_IsRejected()
    {
        // Zwei Becken, ein Paar: B liefe woanders hin, und im ersten Becken
        // staende A allein.
        var a = Pump(id: 1, partnerId: 2, tentId: 1);
        var b = Pump(id: 2, partnerId: 1, tentId: 9);

        Assert.Contains("selben Zelt", PartnerDosing.Validate(a, b)!);
    }

    [Fact]
    public void AnUnusableRatio_IsRejected()
    {
        Assert.Contains("über null", PartnerDosing.Validate(Pump(ratio: 0), Pump(id: 2, partnerId: 1))!);
    }

    [Fact]
    public void ASeparationTimeBelowTheMinimum_IsRejected()
    {
        Assert.Contains("Trennzeit", PartnerDosing.Validate(Pump(delay: 0), Pump(id: 2, partnerId: 1))!);
    }

    [Fact]
    public void AProperPair_Passes()
    {
        Assert.Null(PartnerDosing.Validate(Pump(id: 1, partnerId: 2), Pump(id: 2, partnerId: 1)));
    }
}
