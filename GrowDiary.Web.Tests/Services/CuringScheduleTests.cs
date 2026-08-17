using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Der Lüft-Rhythmus beim Aushärten.
/// </summary>
/// <remarks>
/// Diese Rechnung entscheidet, wann jemand vor dem Schrank steht und ein Glas
/// öffnet. Sie darf weder zu oft rufen (dann hört man auf hinzusehen) noch zu
/// selten (dann schimmelt es). Die Zahlen sind belegt, nicht geschätzt — die
/// Quellen stehen an <see cref="CuringSchedule"/>.
/// </remarks>
public sealed class CuringScheduleTests
{
    private static readonly DateTime Eingeglast = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    private static CuringJar Glas(bool mitRegler = false, DateTime? fertig = null) => new()
    {
        Id = 1,
        GrowId = 1,
        Label = "Glas 1",
        FilledAtUtc = Eingeglast,
        HasHumidityPack = mitRegler,
        FinishedAtUtc = fertig,
    };

    [Fact]
    public void TheDayOfFillingIsDayOne()
    {
        // Vor dem Glas zaehlt man „Tag 1", nicht „Tag 0".
        var duty = CuringSchedule.Evaluate(Glas(), lastBurpUtc: null, Eingeglast);

        Assert.Equal(1, duty.DayInCure);
    }

    [Fact]
    public void TheFirstWeekAsksForDailyBurping()
    {
        var duty = CuringSchedule.Evaluate(Glas(), lastBurpUtc: null, Eingeglast.AddDays(2));

        Assert.Equal(1, duty.IntervalDays);
        Assert.Equal(5, duty.BurpMinutesMin);
        Assert.Equal(10, duty.BurpMinutesMax);
        Assert.Contains("Woche 1", duty.Text);
    }

    [Fact]
    public void TheSecondWeekStretchesToEveryTwoToThreeDays()
    {
        var duty = CuringSchedule.Evaluate(Glas(), lastBurpUtc: null, Eingeglast.AddDays(9));

        Assert.Equal(2, duty.IntervalDays);
        Assert.Equal(2, duty.BurpMinutesMin);
        Assert.Equal(3, duty.BurpMinutesMax);
    }

    [Fact]
    public void WeeksThreeAndFourAskOnlyOnceAWeek()
    {
        var duty = CuringSchedule.Evaluate(Glas(), lastBurpUtc: null, Eingeglast.AddDays(20));

        Assert.Equal(7, duty.IntervalDays);
        Assert.Equal(1, duty.BurpMinutesMin);
    }

    [Fact]
    public void FromDayThirtyTheHygrometerDecides()
    {
        // Ab hier waere ein Kalendertermin Scheingenauigkeit — deshalb gibt es
        // keinen, und der Text sagt, wonach man stattdessen geht.
        var duty = CuringSchedule.Evaluate(Glas(), lastBurpUtc: null, Eingeglast.AddDays(35));

        Assert.Null(duty.NextDueUtc);
        Assert.Equal(CuringDueLevel.Ok, duty.Level);
        Assert.Contains("Hygrometer", duty.Text);
        Assert.Contains("58", duty.Text);
        Assert.Contains("62", duty.Text);
    }

    [Fact]
    public void AJarNeverOpenedCountsFromTheDayItWasFilled()
    {
        // Ohne diese Regel haette ein nie geoeffnetes Glas nie einen Termin —
        // ausgerechnet das Glas, das am dringendsten einen braucht.
        var duty = CuringSchedule.Evaluate(Glas(), lastBurpUtc: null, Eingeglast.AddDays(3));

        Assert.Equal(CuringDueLevel.Overdue, duty.Level);
        Assert.Equal(Eingeglast.AddDays(1), duty.NextDueUtc);
    }

    [Fact]
    public void BurpingTodayClearsTheDuty()
    {
        var jetzt = Eingeglast.AddDays(3);
        var duty = CuringSchedule.Evaluate(Glas(), lastBurpUtc: jetzt, jetzt);

        Assert.Equal(CuringDueLevel.Ok, duty.Level);
    }

    [Fact]
    public void OneDayPastTheDateIsDueTwoIsOverdue()
    {
        var glas = Glas();
        var gelueftet = Eingeglast.AddDays(2);

        var faellig = CuringSchedule.Evaluate(glas, gelueftet, gelueftet.AddDays(1));
        var ueberfaellig = CuringSchedule.Evaluate(glas, gelueftet, gelueftet.AddDays(2));

        Assert.Equal(CuringDueLevel.Due, faellig.Level);
        Assert.Equal(CuringDueLevel.Overdue, ueberfaellig.Level);
    }

    [Fact]
    public void AHumidityPackStretchesTheRhythmButDoesNotAbolishIt()
    {
        // Ein Regler tauscht Feuchte, keine Luft — die erste Woche bleibt eine
        // Aufgabe, nur eine seltenere.
        var ohne = CuringSchedule.Evaluate(Glas(), null, Eingeglast.AddDays(2));
        var mit = CuringSchedule.Evaluate(Glas(mitRegler: true), null, Eingeglast.AddDays(2));

        Assert.Equal(2, mit.IntervalDays);
        Assert.True(mit.IntervalDays > ohne.IntervalDays);
        Assert.NotNull(mit.NextDueUtc);
        Assert.Contains("Feuchtigkeitsregler", mit.Text);
    }

    [Fact]
    public void AFinishedJarHasNoMoreDuties()
    {
        var fertig = Eingeglast.AddDays(40);
        var duty = CuringSchedule.Evaluate(Glas(fertig: fertig), null, fertig.AddDays(5));

        Assert.Equal(CuringDueLevel.Finished, duty.Level);
        Assert.Null(duty.NextDueUtc);
        // Die Tage zaehlen bis zum Abschluss, nicht bis heute — sonst wuerde ein
        // laengst fertiges Glas immer weiter „aushaerten". 41, nicht 40: der
        // Einglastag ist Tag 1 (siehe TheDayOfFillingIsDayOne).
        Assert.Equal(41, duty.DayInCure);
    }

    [Fact]
    public void EveryDutyNamesItsSource()
    {
        // Ohne Quelle ist eine Empfehlung eine Behauptung. Gilt hier wie bei der
        // Wasser-Ampel und den Feedcharts.
        foreach (var tag in new[] { 1, 5, 10, 20, 35 })
        {
            var duty = CuringSchedule.Evaluate(Glas(), null, Eingeglast.AddDays(tag));
            Assert.False(string.IsNullOrWhiteSpace(duty.Source), $"Tag {tag} ohne Quelle");
        }
    }
}
