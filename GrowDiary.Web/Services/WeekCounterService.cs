using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

public sealed record GrowWeekInfo(
    GrowCounterState State,
    int? VegWeek,
    int? FlowerWeek,
    int? AutoflowerWeek,
    int? DaysGerminating,
    int? DaysRooting,
    string Label
);

public sealed class WeekCounterService
{
    public GrowWeekInfo Calculate(GrowRun grow)
    {
        var today = DateTime.Today;
        var isAutoflower = grow.SeedType == SeedType.Autoflower;

        // Startpunkt ermitteln
        DateTime? growStart = grow.GerminatedAt?.Date
            ?? (grow.CloneIsRooted || grow.RootedAt.HasValue
                ? (grow.RootedAt?.Date ?? grow.StartDate)
                : (DateTime?)null);

        // Warten auf Keimung
        if (grow.StartMaterial == StartMaterial.Seed
            && grow.GerminatedAt == null)
        {
            var days = (today - grow.StartDate.Date).Days;
            return new GrowWeekInfo(
                GrowCounterState.WaitingForGermination,
                null, null, null,
                days, null,
                $"Keimt seit {days} Tag{(days == 1 ? "" : "en")}");
        }

        // Warten auf Bewurzelung
        if (grow.StartMaterial == StartMaterial.Clone
            && !grow.CloneIsRooted
            && grow.RootedAt == null)
        {
            var days = (today - grow.StartDate.Date).Days;
            return new GrowWeekInfo(
                GrowCounterState.WaitingForRooting,
                null, null, null,
                null, days,
                $"Bewurzelung seit {days} Tag{(days == 1 ? "" : "en")}");
        }

        // Kein Startpunkt
        if (growStart == null)
            return new GrowWeekInfo(GrowCounterState.NoData,
                null, null, null, null, null, "Noch kein Start");

        // Einstieg mid-grow: bereits vergangene Tage einberechnen
        if (isAutoflower)
        {
            var extraDays = grow.AutoflowerDaysSinceGermination ?? 0;
            var totalDays = (today - growStart.Value).Days + extraDays;
            var week = totalDays / 7 + 1;
            return new GrowWeekInfo(
                GrowCounterState.Autoflowering,
                null, null, week, null, null,
                $"Woche {week} (Autoflower)");
        }

        // Für Photoperiod: DaysAlreadyInPhase zurückrechnen (nur wenn kein FlipDate gesetzt)
        if (grow.DaysAlreadyInPhase is > 0 && !grow.FlipDate.HasValue)
        {
            growStart = growStart.Value.AddDays(-grow.DaysAlreadyInPhase.Value);
        }

        // Photoperiod mit Flip
        if (grow.FlipDate.HasValue)
        {
            var vegDays = (grow.FlipDate.Value.Date - growStart.Value).Days;
            var vegWeek = vegDays / 7 + 1;
            var flowerDays = (today - grow.FlipDate.Value.Date).Days;
            var flowerWeek = flowerDays / 7 + 1;
            return new GrowWeekInfo(
                GrowCounterState.Flowering,
                vegWeek, flowerWeek, null, null, null,
                $"Blüte Woche {flowerWeek}");
        }

        // Photoperiod Veg
        {
            var vegDays = (today - growStart.Value).Days;
            var vegWeek = vegDays / 7 + 1;
            return new GrowWeekInfo(
                GrowCounterState.Vegetating,
                vegWeek, null, null, null, null,
                $"Veg Woche {vegWeek}");
        }
    }
}
