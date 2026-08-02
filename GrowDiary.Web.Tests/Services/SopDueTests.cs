using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Der Fälligkeits-Wächter: die App liest ihre eigenen Zeitpläne endlich selbst.
/// </summary>
/// <remarks>
/// Die Rhythmen standen seit jeher in den Abläufen (Wasserwechsel: alle 7,
/// Warnung nach 8, kritisch nach 10) und niemand las sie. Diese Tests sichern
/// den Kern: WOVON das „zuletzt gemacht" abgeleitet wird — denn eine falsche
/// Quelle erinnerte entweder nie oder staendig.
/// </remarks>
public sealed class SopDueTests
{
    private static Measurement Messung(int tageHer, bool wechsel = false) => new()
    {
        TakenAt = DateTime.Now.AddDays(-tageHer),
        SolutionChange = wechsel,
        ReservoirPh = 6.0,
    };

    [Fact]
    public void TheWaterChangeCountsFromTheSolutionChangeMark()
    {
        // Vor 3 Tagen gewechselt (markiert), gestern nur gemessen: fuer den
        // Wasserwechsel zaehlt der Wechsel, nicht die juengste Messung.
        var grow = new GrowRun { StartDate = DateTime.Today.AddDays(-30) };
        var messungen = new List<Measurement> { Messung(1), Messung(3, wechsel: true) };

        var zuletzt = SopDueService.ZuletztGemacht("weekly-water-change", grow, messungen,
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal(DateTime.Today.AddDays(-3), zuletzt.Date);
    }

    [Fact]
    public void ACompletedInstanceCountsWhenItIsNewer()
    {
        // Wer die SOP in der App abhakt, hat den Wechsel gemacht — auch ohne
        // markierte Messung danach.
        var grow = new GrowRun { StartDate = DateTime.Today.AddDays(-30) };
        var messungen = new List<Measurement> { Messung(9, wechsel: true) };
        var instanzen = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase)
        {
            ["weekly-water-change"] = DateTime.Now.AddDays(-2),
        };

        var zuletzt = SopDueService.ZuletztGemacht("weekly-water-change", grow, messungen, instanzen);

        Assert.Equal(DateTime.Today.AddDays(-2), zuletzt.Date);
    }

    [Fact]
    public void AFreshGrowIsNotOverdueOnDayOne()
    {
        // Ohne jeden Beleg zaehlt der Start des Grows — sonst begruesste die
        // App jeden neuen Lauf mit „Wasserwechsel 30 Tage ueberfaellig".
        var grow = new GrowRun { StartDate = DateTime.Today.AddDays(-2) };

        var zuletzt = SopDueService.ZuletztGemacht("weekly-water-change", grow, [],
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal(DateTime.Today.AddDays(-2), zuletzt.Date);
    }

    [Fact]
    public void TheDailyRoutineCountsAnyMeasurement()
    {
        var grow = new GrowRun { StartDate = DateTime.Today.AddDays(-30) };
        var messungen = new List<Measurement> { Messung(1) };

        var zuletzt = SopDueService.ZuletztGemacht("daily-measurement-routine", grow, messungen,
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal(DateTime.Today.AddDays(-1), zuletzt.Date);
    }
}
