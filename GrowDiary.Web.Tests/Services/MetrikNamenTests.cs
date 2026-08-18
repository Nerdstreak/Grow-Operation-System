using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Kein Entwickler-Bezeichner im Titel einer Aufgabe.
/// </summary>
/// <remarks>
/// <para>Der Titel wurde aus dem Enum-Namen gebaut. Auf der Aufgabenseite stand
/// deshalb „Ec: Abweichung prüfen" — auf genau der Seite, die man mit dem
/// Telefon im Zelt aufhat. Gefunden im Handy-Audit vom 18.08.2026.</para>
///
/// <para>Der Test fällt, sobald eine neue Messgröße dazukommt, ohne dass jemand
/// ihr einen deutschen Namen gibt: dann fällt sie in den Rückfall und trägt
/// wieder ihren Enum-Namen. Das ist der Sinn — der Fehler soll hier auffallen
/// und nicht beim Nutzer.</para>
/// </remarks>
public class MetrikNamenTests
{
    [Fact]
    public void JedeMessgroesseHatEinenLesbarenNamen()
    {
        var ohneNamen = Enum.GetValues<DeviationMetric>()
            .Where(metrik => DeviationRiskEventSyncService.ToMetricLabel(metrik) == metrik.ToString())
            .ToList();

        Assert.True(
            ohneNamen.Count == 0,
            "Diese Messgrößen würden mit ihrem Entwickler-Namen im Titel stehen: "
                + string.Join(", ", ohneNamen));
    }

    [Fact]
    public void SchreibweiseIstFachlichRichtig()
    {
        // pH ist nicht PH, EC ist nicht Ec — die Gross- und Kleinschreibung
        // gehoert zur Groesse selbst, nicht zum Geschmack.
        Assert.Equal("pH", DeviationRiskEventSyncService.ToMetricLabel(DeviationMetric.Ph));
        Assert.Equal("EC", DeviationRiskEventSyncService.ToMetricLabel(DeviationMetric.Ec));
        Assert.Equal("CO₂", DeviationRiskEventSyncService.ToMetricLabel(DeviationMetric.Co2));
        Assert.Equal("Wassertemperatur", DeviationRiskEventSyncService.ToMetricLabel(DeviationMetric.WaterTemp));
    }
}
