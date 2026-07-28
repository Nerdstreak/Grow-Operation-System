using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Die erfundenen Werte für den Entwicklungsrechner. Was hier geprüft wird,
/// ist nicht die Physik, sondern zweierlei: dass die Werte plausibel bleiben,
/// und dass Kurve und Kachel dieselbe Quelle haben — sonst endet der Verlauf
/// woanders, als die Kachel steht.
/// </summary>
public sealed class DemoDataTests
{
    private static readonly DateTime Now = new(2026, 7, 28, 14, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void EveryKnownMetric_HasAValue()
    {
        foreach (var key in DemoData.MetricKeys)
        {
            Assert.NotNull(DemoData.ValueFor(key, Now));
        }
    }

    [Fact]
    public void AnUnknownMetric_HasNone()
    {
        Assert.Null(DemoData.ValueFor("gibt-es-nicht", Now));
    }

    [Fact]
    public void TheSameMomentAlwaysGivesTheSameValue()
    {
        // Sonst passten zwei Abrufe kurz hintereinander nicht zusammen, und
        // nach einem Neustart spränge die Kurve.
        Assert.Equal(DemoData.ValueFor("temperature", Now), DemoData.ValueFor("temperature", Now));
    }

    [Fact]
    public void ValuesMoveOverTime()
    {
        // Ein starrer Wert waere zum Pruefen wertlos: keine Kurve, kein Trend.
        var jetzt = DemoData.ValueFor("temperature", Now)!.Value;
        var spaeter = DemoData.ValueFor("temperature", Now.AddHours(6))!.Value;

        Assert.NotEqual(jetzt, spaeter);
    }

    [Theory]
    [InlineData("temperature", 18, 30)]
    [InlineData("humidity", 40, 80)]
    [InlineData("reservoir-ph", 5.4, 6.6)]
    [InlineData("reservoir-ec", 1.2, 2.2)]
    [InlineData("reservoir-temp", 17, 23)]
    [InlineData("orp", 250, 450)]
    [InlineData("dissolved-oxygen", 6, 9.5)]
    public void ValuesStayPlausible_AcrossAFullDay(string key, double min, double max)
    {
        // Ueber 24 Stunden in Viertelstundenschritten: die Drift darf nicht ins
        // Unmoegliche laufen, sonst stuenden auf dem Entwicklungsrechner
        // Werte, die es in keinem Zelt gibt.
        var start = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc);
        for (var minuten = 0; minuten < 24 * 60; minuten += 15)
        {
            var wert = DemoData.ValueFor(key, start.AddMinutes(minuten))!.Value;
            Assert.InRange(wert, min, max);
        }
    }

    [Fact]
    public void PhDriftsUpwards_SoThereIsSomethingToCorrect()
    {
        // Absicht: dadurch gibt es auf dem Entwicklungsrechner eine echte
        // Abweichung, gegen die sich die Dosierung pruefen laesst.
        var start = new DateTime(2026, 7, 28, 6, 0, 0, DateTimeKind.Utc);

        Assert.True(DemoData.ValueFor("reservoir-ph", start.AddHours(12))
                  > DemoData.ValueFor("reservoir-ph", start));
    }

    [Fact]
    public void TheHistoryEndsWhereTheTileStands()
    {
        // Kurve und Kachel kommen aus demselben Generator. Liefe das
        // auseinander, zeigte der Verlauf etwas anderes als der Messwert
        // darueber — und niemand wuesste, welchem zu glauben ist.
        var verlauf = DemoData.SeedHistory(tentId: 1, Now).ToList();
        var letzterPh = verlauf.Where(r => r.MetricKey == "reservoir-ph").MaxBy(r => r.CapturedAtUtc)!;

        Assert.Equal(DemoData.ValueFor("reservoir-ph", letzterPh.CapturedAtUtc), letzterPh.Value);
    }

    [Fact]
    public void TheHistoryCoversTheAdvertisedWindow()
    {
        var verlauf = DemoData.SeedHistory(tentId: 1, Now).ToList();
        var aeltester = verlauf.Min(r => r.CapturedAtUtc);

        Assert.True(Now - aeltester <= TimeSpan.FromHours(DemoData.HistoryHours));
        Assert.True(Now - aeltester >= TimeSpan.FromHours(DemoData.HistoryHours - 1));
        Assert.All(verlauf, reading => Assert.Equal(1, reading.TentId));
    }

    [Fact]
    public void StatesCoverEveryMetric_PlusTheLight()
    {
        var states = DemoData.StatesFor(Now);

        foreach (var key in DemoData.MetricKeys)
        {
            Assert.True(states.ContainsKey(key), $"Messwert {key} fehlt.");
            Assert.NotNull(states[key].NumericValue);
        }

        Assert.Contains(states.Values, state => state.State is "on" or "off");
    }

    [Fact]
    public void EveryDemoEntity_IsRecognisableAsOne()
    {
        // Man muss auf den ersten Blick sehen, dass nichts davon echt ist.
        var entities = DemoData.Entities(Now);

        Assert.All(entities, entity => Assert.Contains(DemoData.EntityPrefix, entity.EntityId));
        Assert.All(entities, entity => Assert.StartsWith("Demo", entity.FriendlyName));
    }

    [Fact]
    public void ThereAreSwitchableEntities_SoPumpsCanBeMapped()
    {
        var schalter = DemoData.Entities(Now).Where(entity => entity.Domain == "switch").ToList();

        Assert.True(schalter.Count >= 2, "Ohne schaltbare Entitaeten laesst sich keine Pumpe zuordnen.");
    }

    [Fact]
    public void TheDemoConnectionCountsAsConfigured()
    {
        // Sonst hielten Watchdog, Live und Dosierung den Rechner fuer
        // unkonfiguriert und blockierten, bevor die Werte gefragt waeren.
        Assert.True(DemoData.Settings().IsConfigured);
    }

    [Fact]
    public void LightFollowsAnEighteenSixCycle()
    {
        Assert.False(DemoData.LightOn(new DateTime(2026, 7, 28, 3, 0, 0, DateTimeKind.Utc)));
        Assert.True(DemoData.LightOn(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc)));
    }
}
