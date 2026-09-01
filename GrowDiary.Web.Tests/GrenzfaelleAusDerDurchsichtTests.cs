using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests;

/// <summary>
/// Vier Grenzfälle aus der Gesamtdurchsicht vom 01.09.2026.
/// </summary>
/// <remarks>
/// <para>Alle vier haben dieselbe Form: eine Rechnung, die für den erwarteten
/// Bereich stimmt und am Rand etwas Falsches liefert — ohne Fehlermeldung, ohne
/// dass jemand es sieht. Genau das, was die Regel „Grenzfälle: 0, negativ,
/// null, leer" in <c>CLAUDE.md</c> meint.</para>
/// </remarks>
public sealed class GrenzfaelleAusDerDurchsichtTests
{
    /// <summary>
    /// Der Median über Uhrzeiten kippt nicht, wenn die Flanken um Mitternacht liegen.
    /// </summary>
    /// <remarks>
    /// <para><b>Der Fund.</b> <c>MedianTime</c> sortiert Minuten seit
    /// Mitternacht. Liegen die Flanken um 00:00, mischt das Werte nahe 0 mit
    /// Werten nahe 1440 — und der Median landet bei 12:00.</para>
    ///
    /// <para>Ein Blüte-Zelt mit 12/12 und Licht aus um Mitternacht ist der
    /// Normalfall, nicht die Ausnahme: die Flanken kommen aus dem Poll-Takt des
    /// Snapshot-Workers und streuen um ein, zwei Minuten. Der gelernte Zyklus
    /// war danach um <b>zwölf Stunden</b> verschoben, und alles, was daran
    /// hängt, urteilte falsch.</para>
    /// </remarks>
    [Theory]
    // Aus-Flanken um Mitternacht: 23:58, 23:59, 00:01, 00:02 -> 00:00.
    [InlineData(new[] { 23 * 60 + 58, 23 * 60 + 59, 1, 2 }, 0)]
    // Dasselbe ungerade: drei Flanken.
    [InlineData(new[] { 23 * 60 + 59, 0, 1 }, 0)]
    // Und der gewöhnliche Fall bleibt, wie er war.
    [InlineData(new[] { 12 * 60 - 1, 12 * 60, 12 * 60 + 1 }, 12 * 60)]
    [InlineData(new[] { 6 * 60, 6 * 60 + 2 }, 6 * 60 + 1)]
    public void MedianUeberUhrzeiten_KipptNichtUmMitternacht(int[] minuten, int erwartet)
    {
        var basis = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var zeitpunkte = minuten.Select(m => basis.AddMinutes(m)).ToList();

        var median = LightCycleLearner.MedianZeit(zeitpunkte, TimeSpan.Zero);

        Assert.True(median == TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(erwartet)),
            $"Median {median} statt {TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(erwartet))}. "
            + "Flanken um Mitternacht mischen Werte nahe 0 mit Werten nahe 1440 — der gelernte "
            + "Zyklus ist danach um zwoelf Stunden verschoben.");
    }

    /// <summary>
    /// Ein Verbrauch, der auf 0 einbricht, wird erkannt.
    /// </summary>
    /// <remarks>
    /// <para><b>Der Fund.</b> Der Filter liess Tage mit <c>TopOffLiters == 0</c>
    /// fallen, statt sie als 0 in die Reihe zu nehmen. Der <b>vollständige</b>
    /// Einbruch verschwand damit aus der Rechnung — während ein blosser
    /// Rückgang auf die Hälfte gemeldet wurde.</para>
    ///
    /// <para>Der schlimmere Fall war der stille: eine Pflanze, die drei Tage
    /// gar nichts mehr trinkt, hat ein Wurzelproblem.</para>
    /// </remarks>
    [Fact]
    public void VerbrauchAufNull_WirdGemeldet()
    {
        var jetzt = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Local);
        var messungen = new List<Measurement>();
        // Vier Tage je 4 L, dann drei Tage gar nichts.
        double?[] liter = [4, 4, 4, 4, 0, 0, 0];
        for (var i = 0; i < liter.Length; i += 1)
        {
            messungen.Add(new Measurement
            {
                TakenAt = jetzt.AddDays(-(liter.Length - 1 - i)),
                TopOffLiters = liter[i],
                ReservoirPh = 6.0,
                ReservoirEc = 1.0,
            });
        }

        var befunde = TrendWatchService.Evaluate(messungen, null, jetzt);

        Assert.Contains(befunde, b => b.Code == "trend.consumption.drop");
    }

    /// <summary>
    /// Ein Erntedatum in der Zukunft öffnet kein Trocknungsfenster.
    /// </summary>
    /// <remarks>
    /// <para><b>Der Fund.</b> <c>DayFor</c> hatte keine untere Schranke: bei
    /// einem Erntedatum in der Zukunft kamen 0 oder negative Trocknungstage
    /// heraus, und das Fenster galt trotzdem als offen.</para>
    ///
    /// <para><b>Was das kostet.</b> Der Dashboard-Composer schaltet im
    /// Trocknungsfenster auf Trocknungsziele um — die Reservoir-Alarme des noch
    /// laufenden Grows sind damit stillgelegt. Ein Vertipper um ein Jahr beim
    /// Erntedatum genügt.</para>
    ///
    /// <para>Geprüft wird die Schranke selbst, ohne Datenbank: die Entscheidung
    /// ist eine Rechnung, und die lässt sich einzeln festnageln.</para>
    /// </remarks>
    [Theory]
    [InlineData(0, 1)]      // heute geerntet -> Tag 1
    [InlineData(-3, 4)]     // vor drei Tagen -> Tag 4
    [InlineData(1, null)]   // morgen -> gar kein Fenster
    [InlineData(365, null)] // um ein Jahr vertippt -> gar kein Fenster
    public void ErntedatumInDerZukunft_OeffnetKeinFenster(int tageAbHeute, int? erwartet)
    {
        var heute = new DateTime(2026, 5, 20);
        var ernte = heute.AddDays(tageAbHeute);

        var tag = DryingWindow.TrocknungsTag(heute, ernte);

        Assert.True(tag == erwartet,
            $"Erntedatum {tageAbHeute:+#;-#;0} Tage von heute ergibt Trocknungstag {tag?.ToString() ?? "null"}, "
            + $"erwartet {erwartet?.ToString() ?? "null"}. Ein Fenster, das ein Datum aus der Zukunft "
            + "oeffnet, legt die Reservoir-Alarme des laufenden Grows stumm.");
    }
}
