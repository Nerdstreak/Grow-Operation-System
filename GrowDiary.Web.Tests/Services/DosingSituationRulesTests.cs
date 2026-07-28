using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Welcher Messwert gilt und welches Ziel — die zwei Entscheidungen, auf denen
/// jeder Vorschlag steht.
/// </summary>
public sealed class DosingSituationRulesTests
{
    private static readonly DateTime Jetzt = new(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);

    // ---------- Welcher Messwert ----------

    [Fact]
    public void WithoutAnyValue_ThereIsNothingToDoseAgainst()
    {
        var (wert, alter, herkunft) = DosingSituationRules.PickReading(null, null, null, null, Jetzt);

        Assert.Null(wert);
        Assert.Null(alter);
        Assert.Equal(ReadingSource.None, herkunft);
    }

    [Fact]
    public void AFreshSensorValue_BeatsAnOldHandEntry()
    {
        // Der eigentliche Grund fuer diese Regel: gegen eine drei Tage alte
        // Handmessung zu rechnen, waehrend der Sensor seit Minuten etwas anderes
        // sagt, ergibt einen Vorschlag, der genauso aussieht wie ein guter.
        var (wert, alter, herkunft) = DosingSituationRules.PickReading(
            6.42, Jetzt.AddMinutes(-4),
            5.90, Jetzt.AddDays(-3),
            Jetzt);

        Assert.Equal(6.42, wert);
        Assert.Equal(4, alter!.Value.TotalMinutes, 1);
        Assert.Equal(ReadingSource.Sensor, herkunft);
    }

    [Fact]
    public void AFreshHandEntry_BeatsAnOlderSensorValue()
    {
        // Wer eben von Hand gemessen hat, hat meist genau deshalb gemessen, weil
        // er dem Sensor nicht traute.
        var (wert, _, herkunft) = DosingSituationRules.PickReading(
            6.42, Jetzt.AddHours(-2),
            5.90, Jetzt.AddMinutes(-5),
            Jetzt);

        Assert.Equal(5.90, wert);
        Assert.Equal(ReadingSource.Manual, herkunft);
    }

    [Fact]
    public void WithOnlyOneSource_ThatOneCounts()
    {
        Assert.Equal(ReadingSource.Sensor,
            DosingSituationRules.PickReading(6.4, Jetzt.AddMinutes(-1), null, null, Jetzt).From);
        Assert.Equal(ReadingSource.Manual,
            DosingSituationRules.PickReading(null, null, 6.4, Jetzt.AddMinutes(-1), Jetzt).From);
    }

    [Fact]
    public void AValueWithoutATimestamp_DoesNotCount()
    {
        // Ohne Zeitpunkt laesst sich das Alter nicht bestimmen — und genau am
        // Alter bricht die Automatik ab. Ein Wert ohne Zeit waere ein Wert ohne
        // Ablaufdatum.
        Assert.Equal(ReadingSource.None,
            DosingSituationRules.PickReading(6.4, null, null, null, Jetzt).From);
    }

    [Fact]
    public void AValueFromTheFuture_IsNotTreatedAsNegativelyOld()
    {
        // Verstellte Uhr oder verrutschte Zeitzone: ein negatives Alter waere
        // immer juenger als jede Grenze und wuerde sie damit aushebeln.
        var (_, alter, _) = DosingSituationRules.PickReading(6.4, Jetzt.AddMinutes(30), null, null, Jetzt);

        Assert.Equal(TimeSpan.Zero, alter);
    }

    // ---------- Welches Ziel ----------

    [Fact]
    public void TheUsersOwnLimits_WinOverTheProfile()
    {
        var (ziel, herkunft) = DosingSituationRules.PickTarget((5.6, 5.9), (6.0, 6.1));

        Assert.Equal(5.75, ziel!.Value, 3);
        Assert.Equal(TargetSource.User, herkunft);
    }

    [Fact]
    public void WithoutOwnLimits_TheProfileDecides()
    {
        // Der Fall, den es vorher gar nicht gab: wer nichts eingetragen hat, bekam
        // ueberhaupt keinen Vorschlag, obwohl das Phasen-Profil einen Sollwert kennt.
        var (ziel, herkunft) = DosingSituationRules.PickTarget(null, (6.0, 6.1));

        Assert.Equal(6.05, ziel!.Value, 3);
        Assert.Equal(TargetSource.Profile, herkunft);
    }

    [Fact]
    public void AHalfLimit_IsNoTarget()
    {
        // „nicht ueber 6,2" sagt nichts darueber, worauf dosiert werden soll —
        // dann faellt es auf das Profil zurueck.
        var (ziel, herkunft) = DosingSituationRules.PickTarget((null, 6.2), (6.0, 6.1));

        Assert.Equal(6.05, ziel!.Value, 3);
        Assert.Equal(TargetSource.Profile, herkunft);
    }

    [Fact]
    public void WithNeither_ThereIsNoTarget()
    {
        var (ziel, herkunft) = DosingSituationRules.PickTarget(null, null);

        Assert.Null(ziel);
        Assert.Equal(TargetSource.None, herkunft);
    }

    [Fact]
    public void DosingAimsAtTheMiddleOfTheBand_NotItsEdge()
    {
        // Wer auf die Grenze dosiert, steht nach der naechsten Drift sofort
        // wieder draussen.
        var (ziel, _) = DosingSituationRules.PickTarget(null, (5.8, 6.2));

        Assert.Equal(6.0, ziel!.Value, 3);
    }
}
