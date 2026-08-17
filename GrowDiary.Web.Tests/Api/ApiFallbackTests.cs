namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// Ein unbekannter /api/-Pfad muss 404 sagen — nicht die Startseite ausliefern.
/// </summary>
/// <remarks>
/// <para>Der SPA-Fallback beantwortete JEDE unbekannte Route mit index.html und
/// Status 200, auch <c>/api/…</c>. Die Folgen sind still und teuer: ein
/// Tippfehler im Client-Pfad sieht nach Erfolg aus, ein DELETE auf einen
/// Endpunkt, den es gar nicht gibt, meldet 200 und loescht nichts, und wer JSON
/// erwartet, bekommt HTML und eine Parse-Meldung, die nichts mit der Ursache zu
/// tun hat.</para>
///
/// <para>Gefunden ist das genau so: beim Aufraeumen von Testdaten meldeten drei
/// DELETE-Aufrufe 200 — und die Eintraege standen danach immer noch da.</para>
///
/// <para>Der Test liest den Quelltext, weil es hier keinen Host-Test gibt. Er
/// prueft das, was der Kommentar ueber dem Fallback immer schon behauptet hat.</para>
/// </remarks>
public sealed class ApiFallbackTests
{
    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "GrowDiary.slnx")))
        {
            dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("Projektwurzel nicht gefunden.");
        }
        return dir;
    }

    [Fact]
    public void TheSpaFallbackRefusesApiPaths()
    {
        var quelltext = File.ReadAllText(Path.Combine(FindProjectRoot(), "GrowDiary.Web", "Program.cs"));

        var fallbackAb = quelltext.IndexOf("app.MapFallback(", StringComparison.Ordinal);
        Assert.True(fallbackAb > 0, "app.MapFallback wurde nicht gefunden — wurde das Routing umgebaut?");

        var fallback = quelltext[fallbackAb..];
        var indexAb = fallback.IndexOf("index.html", StringComparison.Ordinal);
        Assert.True(indexAb > 0, "Der Fallback liefert keine index.html mehr — Test anpassen.");

        // Die Weiche muss VOR dem Ausliefern der Startseite stehen; danach waere
        // sie wirkungslos.
        var weiche = fallback.IndexOf("StartsWithSegments(\"/api\"", StringComparison.Ordinal);
        Assert.True(
            weiche > 0 && weiche < indexAb,
            "Der SPA-Fallback muss /api/-Pfade vor der index.html abfangen und 404 liefern.");

        Assert.Contains("Status404NotFound", fallback[..indexAb], StringComparison.Ordinal);
    }
}
