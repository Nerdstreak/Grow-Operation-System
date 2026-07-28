using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Ist dieses Zelt gerade ein Trockenraum — und der wievielte Tag ist es?
/// </summary>
/// <remarks>
/// Eine Frage, zwei Abnehmer: die Kacheln (Trocknungs-Klimaziele) und die
/// Alarme (Reservoir-Regeln pausieren — die Sonden liegen trocken und messen
/// Unsinn). Deshalb EINE Stelle; zwei Kopien wuerden irgendwann verschieden
/// antworten.
///
/// Trocknung liegt vor, wenn kein Grow mehr laeuft, der letzte in den
/// vergangenen drei Wochen geerntet wurde und noch kein Trockengewicht
/// eingetragen ist. Das Gewicht ist der natuerliche Abschluss: gewogen wird
/// nach dem Trocknen.
/// </remarks>
public static class DryingWindow
{
    /// <summary>Der Trocknungs-Tag (1 = Erntetag + 1), oder null wenn keine Trocknung.</summary>
    public static int? DayFor(GrowRepository? grows, HarvestRepository? harvests, int tentId, DateTime today)
    {
        if (grows is null || harvests is null) return null;
        if (grows.GetActiveGrowsForTent(tentId).Count > 0) return null;

        var geerntet = grows.GetAllGrows()
            .Where(grow => grow.TentId == tentId
                && grow.Status == GrowStatus.Completed
                && grow.EndDate is { } ende
                && (today.Date - ende.Date).TotalDays <= MoldGuard.DryingWindowDays)
            .OrderByDescending(grow => grow.EndDate)
            .FirstOrDefault();
        if (geerntet is null) return null;

        var ernte = harvests.GetForGrow(geerntet.Id);
        if (ernte is null || ernte.DryWeightG is not null) return null;

        return (int)(today.Date - geerntet.EndDate!.Value.Date).TotalDays + 1;
    }

    /// <summary>
    /// Messgroessen, die waehrend der Trocknung nichts mehr bedeuten: das
    /// Reservoir ist abgelassen, die Sonden liegen trocken und melden Unsinn.
    /// </summary>
    public static readonly string[] ReservoirKeys =
    [
        "reservoir-ph", "reservoir-ec", "reservoir-temp", "orp",
        "dissolved-oxygen", "reservoir-level", "reservoir-level-cm",
    ];

    public static bool IsReservoirKey(string metricKey)
        => ReservoirKeys.Contains(metricKey, StringComparer.OrdinalIgnoreCase);
}
