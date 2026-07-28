using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Ist im Zelt gerade Tag? Daran hängen die Urteile über PPFD, CO₂ und VPD.
/// </summary>
public sealed class LightClockTests
{
    private static LightSchedule Plan(string an, string aus)
        => new() { TentId = 1, LightsOnTime = an, LightsOffTime = aus };

    [Fact]
    public void ADayScheduleKnowsDayAndNight()
    {
        var plan = Plan("08:00", "20:00");

        Assert.Equal(LightsNow.On, LightClock.FromSchedule(plan, new TimeOnly(12, 0)));
        Assert.Equal(LightsNow.Off, LightClock.FromSchedule(plan, new TimeOnly(3, 0)));
        // Randlagen: die An-Minute zählt zum Tag, die Aus-Minute zur Nacht.
        Assert.Equal(LightsNow.On, LightClock.FromSchedule(plan, new TimeOnly(8, 0)));
        Assert.Equal(LightsNow.Off, LightClock.FromSchedule(plan, new TimeOnly(20, 0)));
    }

    [Fact]
    public void AnOvernightScheduleIsARealPlan()
    {
        // Licht über Nacht ist gaengige Praxis: die Lampenwaerme faellt in die
        // kalten Stunden. 20:00–08:00 muss also funktionieren, nicht als Fehler
        // gelten.
        var plan = Plan("20:00", "08:00");

        Assert.Equal(LightsNow.On, LightClock.FromSchedule(plan, new TimeOnly(23, 0)));
        Assert.Equal(LightsNow.On, LightClock.FromSchedule(plan, new TimeOnly(3, 0)));
        Assert.Equal(LightsNow.Off, LightClock.FromSchedule(plan, new TimeOnly(12, 0)));
    }

    [Fact]
    public void EqualTimesAreATypoNotAPlan()
    {
        Assert.Equal(LightsNow.Unknown, LightClock.FromSchedule(Plan("08:00", "08:00"), new TimeOnly(12, 0)));
    }

    [Fact]
    public void UnparsableTimesStayUnknown()
    {
        Assert.Equal(LightsNow.Unknown, LightClock.FromSchedule(Plan("acht", "20:00"), new TimeOnly(12, 0)));
    }

    [Fact]
    public void TheSensorBeatsTheSchedule()
    {
        // Der Sensor sieht auch den Ausfall: Plan sagt „an", Lampe ist aus —
        // dann zaehlt die Lampe.
        var sensorAus = new HomeAssistantState { State = "off" };

        Assert.Equal(LightsNow.Off, LightClock.Resolve(sensorAus, Plan("00:00", "23:59"), DateTime.UtcNow));
    }

    [Fact]
    public void WithoutAnySource_ItStaysUnknown()
    {
        // Unbekannt heisst: alles verhaelt sich wie bisher. Lieber ein
        // unnoetiges Nacht-Urteil als ein unterdruecktes Tag-Urteil.
        Assert.Equal(LightsNow.Unknown, LightClock.Resolve(null, null, DateTime.UtcNow));
    }

    [Fact]
    public void OnlyDaytimeMetricsAreGated()
    {
        Assert.True(LightClock.IsDaytimeOnly("ppfd"));
        Assert.True(LightClock.IsDaytimeOnly("co2"));
        Assert.True(LightClock.IsDaytimeOnly("vpd"));
        // Temperatur und Reservoir gelten rund um die Uhr: eine ausgefallene
        // Heizung oder ein kippender pH warten nicht auf Licht an.
        Assert.False(LightClock.IsDaytimeOnly("temperature"));
        Assert.False(LightClock.IsDaytimeOnly("reservoir-ph"));
    }
}
