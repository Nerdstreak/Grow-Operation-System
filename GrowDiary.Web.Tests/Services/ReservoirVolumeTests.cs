using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Zentimeter in Liter — aus zwei gemessenen Punkten.
/// </summary>
/// <remarks>
/// Der Grund für zwei Punkte statt einem: ein eTape beginnt erst ein Stück über
/// der Unterkante zu messen. Mit nur dem Voll-Punkt liefe die Gerade durch den
/// Ursprung und wäre unten am stärksten daneben — genau dort, wo der Füllstand
/// zählt.
/// </remarks>
public sealed class ReservoirVolumeTests
{
    // Der Fall aus dem echten Becken: leer 5 cm, voll 38 cm, 100 L eingefüllt.
    private const double Leer = 5;
    private const double Voll = 38;
    private const double Liter = 100;

    [Fact]
    public void TheTwoMeasuredPointsMapToEmptyAndFull()
    {
        Assert.Equal(0, ReservoirVolume.Liters(Leer, Leer, Voll, Liter));
        Assert.Equal(100, ReservoirVolume.Liters(Voll, Leer, Voll, Liter));
    }

    [Fact]
    public void HalfwayUpTheTapeIsHalfTheVolume()
    {
        // Mitte zwischen 5 und 38 cm ist 21,5 cm.
        Assert.Equal(50, ReservoirVolume.Liters(21.5, Leer, Voll, Liter)!.Value, 1);
    }

    [Fact]
    public void IgnoringTheOffsetWouldBeWrongExactlyWhereItMatters()
    {
        // Der Grund fuer den zweiten Punkt: bei 10 cm sind es in Wahrheit 15 L.
        // Eine Gerade durch den Ursprung (10/38) behauptete 26 L — also fast
        // das Doppelte, und das im unteren Bereich, wo es zaehlt.
        var richtig = ReservoirVolume.Liters(10, Leer, Voll, Liter)!.Value;
        var ohneVersatz = ReservoirVolume.Liters(10, 0, Voll, Liter)!.Value;

        Assert.Equal(15.2, richtig, 1);
        Assert.True(ohneVersatz > richtig * 1.5);
    }

    [Fact]
    public void BelowTheZeroPointIsSimplyEmpty()
    {
        // Negative Liter gibt es nicht.
        Assert.Equal(0, ReservoirVolume.Liters(3, Leer, Voll, Liter));
    }

    [Fact]
    public void AboveFullKeepsCounting()
    {
        // Ein uebervolles Becken gibt es — „mehr als voll" ist ehrlicher als
        // „genau voll".
        Assert.True(ReservoirVolume.Liters(40, Leer, Voll, Liter) > 100);
    }

    [Fact]
    public void WithoutACalibrationThereIsNoAnswer()
    {
        Assert.Null(ReservoirVolume.Liters(20, null, Voll, Liter));
        Assert.Null(ReservoirVolume.Liters(20, Leer, null, Liter));
        Assert.Null(ReservoirVolume.Liters(20, Leer, Voll, null));
        Assert.False(ReservoirVolume.IsCalibrated(null, Voll, Liter));
    }

    [Fact]
    public void IdenticalPointsAreNotACalibration()
    {
        // Gleiche Punkte ergeben keine Gerade — und ein Volumen von null ist
        // ein Tippfehler, keine Angabe.
        Assert.Null(ReservoirVolume.Liters(20, 10, 10, Liter));
        Assert.Null(ReservoirVolume.Liters(20, Leer, Voll, 0));
    }

    [Fact]
    public void TheFractionFeedsTheDosingFactor()
    {
        // Genau der Wert, mit dem die Dosis skaliert: halb voll = halbe Menge.
        Assert.Equal(0.5, ReservoirVolume.Fraction(21.5, Leer, Voll, Liter)!.Value, 2);
        Assert.Equal(1.0, ReservoirVolume.Fraction(Voll, Leer, Voll, Liter)!.Value, 2);
    }
}
