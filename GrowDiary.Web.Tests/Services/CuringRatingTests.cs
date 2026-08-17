using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Die Feuchte-Ampel fürs Glas.
/// </summary>
/// <remarks>
/// Gebaut wie die Wasser-Ampel: jede Stufe sagt, was los ist, was zu tun ist,
/// und woher die Schwelle stammt. Ein Wert ohne Handlung ist eine Zahl, die man
/// wegklickt.
/// </remarks>
public sealed class CuringRatingTests
{
    [Theory]
    [InlineData(58)]
    [InlineData(60)]
    [InlineData(62)]
    public void TheWindowIsFiftyEightToSixtyTwo(double feuchte)
    {
        Assert.Equal(CuringHumidityLevel.Good, CuringRating.Rate(feuchte).Level);
    }

    [Fact]
    public void SixtyFiveIsMoldTerritory()
    {
        var urteil = CuringRating.Rate(67);

        Assert.Equal(CuringHumidityLevel.MoldRisk, urteil.Level);
        // Die Handlung muss konkret sein: „zu feucht" allein rettet kein Glas.
        Assert.Contains("ausbreiten", urteil.Action);
    }

    [Fact]
    public void JustAboveTheWindowIsNotYetAnEmergency()
    {
        // Zwischen 62 und 65 ist es zu feucht, aber nicht akut — der Unterschied
        // muss erhalten bleiben, sonst wird jede Meldung gleich laut.
        Assert.Equal(CuringHumidityLevel.Damp, CuringRating.Rate(63.5).Level);
    }

    [Fact]
    public void TooDryIsNamedAsTheIrreversibleOne()
    {
        var urteil = CuringRating.Rate(48);

        Assert.Equal(CuringHumidityLevel.TooDry, urteil.Level);
        // Zu feucht kann man reparieren, zu trocken nicht. Wer das nicht sagt,
        // laesst den Eindruck entstehen, ein Regler mache alles wieder gut.
        Assert.Contains("nicht wieder", urteil.Action);
    }

    [Fact]
    public void EveryVerdictCarriesItsSource()
    {
        foreach (var feuchte in new double[] { 40, 56, 60, 64, 70 })
        {
            Assert.False(string.IsNullOrWhiteSpace(CuringRating.Rate(feuchte).Source), $"{feuchte} % ohne Quelle");
        }
    }

    [Fact]
    public void TheUpperBoundAgreesWithTheMoldGuard()
    {
        // Zwei Stellen im Code nennen dieselbe Grenze fuers Glas. Laufen sie
        // auseinander, widerspricht die App sich selbst — genau das soll dieser
        // Test verhindern.
        Assert.Equal(CuringSchedule.TargetHumidityMax, MoldGuard.MaxHumidityPercent(GrowStage.Cure));
    }

    [Fact]
    public void NumbersAreWrittenTheGermanWay()
    {
        // 61,5 — nicht 61.5. Der Rest der App schreibt so, und ein Punkt liest
        // sich hier wie ein Tausendertrenner.
        Assert.Contains("61,5", CuringRating.Rate(61.5).Summary);
    }
}
