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

    /// <summary>
    /// Was in einem echten Zelt überhaupt vorkommen kann — je Messgröße.
    /// </summary>
    /// <remarks>
    /// <b>Plausibilitätsgrenzen, keine Kurvenmaße.</b> Sie sagen „das gibt es
    /// in keinem Zelt", nicht „die Kurve läuft heute genau hier". Wer sie eng
    /// an die Kurve legt, muss sie bei jeder Änderung nachziehen und prüft
    /// am Ende nur noch sich selbst.
    /// </remarks>
    private static readonly Dictionary<string, (double Min, double Max)> Moeglich = new()
    {
        ["temperature"] = (15, 35),
        ["humidity"] = (30, 90),
        ["co2"] = (300, 1600),
        ["ppfd"] = (0, 1200),               // 0 ist richtig: nachts ist es dunkel
        ["reservoir-ph"] = (4.5, 7.5),
        ["reservoir-ec"] = (0.4, 3.0),
        ["reservoir-temp"] = (14, 28),      // die obere Spitze ist der Kühlerausfall
        ["reservoir-level-cm"] = (5, 60),
        ["orp"] = (200, 600),
        ["dissolved-oxygen"] = (4, 10),     // die untere Spitze ist derselbe Ausfall
    };

    /// <summary>
    /// Keine Kurve verlässt über die ganzen 42 Tage das Mögliche.
    /// </summary>
    /// <remarks>
    /// <para><b>Der Anlass.</b> Die erste Fassung prüfte <b>einen festen
    /// Kalendertag</b> (28.07.2026) über 24 Stunden. Die Demokurve ist aber an
    /// <c>DateTime.Today</c> verankert: mit jedem echten Tag wandert dieser
    /// Punkt weiter durch den Sägezahn. Am 20.08. war der Test grün, am 23.08.
    /// rot — bei einem EC von 1,10 gegen eine untere Schranke von 1,20. Der
    /// Code hatte sich nicht geändert, nur das Datum.</para>
    ///
    /// <para><b>Und er prüfte zu wenig.</b> Sieben von zehn Kurven, und drei
    /// der sieben Schranken waren gegen den echten Verlauf falsch — nur lag
    /// der eine geprüfte Tag zufällig günstig. Deshalb geht die Prüfung jetzt
    /// über die <b>Grundmenge</b> <see cref="Demoverlauf.Schluessel"/> und über
    /// das ganze Fenster, das die App zeigt.</para>
    /// </remarks>
    [Fact]
    public void JedeKurveBleibtImMoeglichen()
    {
        Assert.True(Demoverlauf.Schluessel.Length >= 8,
            "Die Grundmenge ist leer oder geschrumpft — dann liefe diese Prüfung "
            + "null Mal durch und wäre grundlos grün.");

        var heute = DateTime.Now;
        var befunde = new List<string>();

        foreach (var key in Demoverlauf.Schluessel)
        {
            Assert.True(Moeglich.ContainsKey(key),
                $"Für „{key}\" gibt es keine Plausibilitätsgrenze. Neue Kurve ohne "
                + "Schranke: entweder eine eintragen oder die Kurve wieder entfernen.");

            var (min, max) = Moeglich[key];

            // Stündlich über die ganzen 42 Tage — genau das Fenster, das die
            // App anzeigt. Ein einzelner Tag beweist nichts über den Rest.
            for (var stunde = 0; stunde < Demoverlauf.TageRueckwaerts * 24; stunde++)
            {
                var zeitpunkt = heute.AddHours(-stunde);
                if (Demoverlauf.Wert(key, zeitpunkt) is not { } wert) continue;
                if (wert < min || wert > max)
                {
                    befunde.Add($"{key} = {wert:0.##} am {zeitpunkt:dd.MM. HH}h "
                        + $"(möglich wären {min:0.##} bis {max:0.##})");
                    break;   // ein Beleg je Kurve reicht
                }
            }
        }

        Assert.True(befunde.Count == 0,
            "Diese Testdaten gibt es in keinem Zelt:\n" + string.Join("\n", befunde));
    }

    /// <summary>Beisst die Prüfung? Eine unmögliche Schranke muss auffallen.</summary>
    [Fact]
    public void Die_Pruefung_wuerde_einen_unmoeglichen_Wert_finden()
    {
        // Der EC-Verlauf liegt über die 42 Tage bei etwa 1,02 bis 1,24. Gegen
        // eine Schranke, die erst bei 2,0 beginnt, MUSS er auffallen — sonst
        // prüft die Schleife oben nichts.
        var heute = DateTime.Now;
        var getroffen = false;

        for (var stunde = 0; stunde < Demoverlauf.TageRueckwaerts * 24; stunde++)
        {
            if (Demoverlauf.Wert("reservoir-ec", heute.AddHours(-stunde)) is { } wert && wert < 2.0)
            {
                getroffen = true;
                break;
            }
        }

        Assert.True(getroffen,
            "Kein EC-Wert unter 2,0 gefunden — dann liest die Prüfung oben nicht, "
            + "was sie zu lesen glaubt.");
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
