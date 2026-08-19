using System.Globalization;
using System.Runtime.CompilerServices;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests;

/// <summary>
/// Die Tests laufen in derselben Kultur wie die App.
/// </summary>
/// <remarks>
/// <para>Und zwar durch <b>denselben Aufruf</b>, nicht durch eine zweite
/// Einstellung mit demselben Wert: <see cref="Deutsch.Setzen"/> ist eine
/// Wahrheit, eine Kopie waere die zweite.</para>
///
/// <para><b>Warum das noetig ist.</b> Ohne diesen Aufruf erben die Tests die
/// Kultur des Rechners. Auf einem deutschen Windows-Rechner sind sie damit
/// gruen, auf CI (invariant) rot — oder umgekehrt still falsch. Genau so ist
/// der Fehler entstanden, den <see cref="Deutsch"/> beschreibt: zwei Monate
/// gruen auf dem Entwicklungsrechner, waehrend der Nutzer im Container
/// „SOP-Schwelle 6.5 mg/l" las.</para>
/// </remarks>
internal static class TestKultur
{
    [ModuleInitializer]
    internal static void Init() => Deutsch.Setzen();
}

/// <summary>
/// Zahlen in Nutzertexten stehen deutsch da — mit Komma.
///
/// <para><b>Was diese Klasse NICHT beweist.</b> Dass die App das im Betrieb
/// auch tut. Der Aufruf steht in <c>Program.cs</c>, und der laeuft in keinem
/// Test — es gibt keinen Integrations-Aufbau (kein
/// <c>WebApplicationFactory</c>). Diese Klasse haelt zwei Dinge fest: dass die
/// Formatierung tatsaechlich an der Kultur haengt (also dass die Einstellung
/// ueberhaupt etwas bewirkt) und dass niemand den Aufruf aus <c>Program.cs</c>
/// entfernt. Die Probe am laufenden Stand macht
/// <c>e2e/deutsche-zahlen.spec.ts</c>.</para>
/// </summary>
public sealed class DeutscheZahlenTests
{
    [Fact]
    public void Die_Tests_laufen_deutsch()
    {
        // Sonst prüft alles darunter in einer anderen Sprache als die App.
        Assert.Equal(Deutsch.Kennung, CultureInfo.CurrentCulture.Name);
        Assert.Equal("6,5", $"{6.5:0.0}");
    }

    [Fact]
    public void Ohne_die_Einstellung_stuende_dort_ein_Punkt()
    {
        // <b>Der Beweis, dass die Einstellung beisst.</b> Ohne ihn liesse sich
        // nicht unterscheiden, ob Deutsch.Setzen etwas bewirkt oder ob der
        // Rechner ohnehin deutsch ist — und genau diese Verwechslung hat den
        // Fehler zwei Monate am Leben gehalten.
        var vorher = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.Equal("6.5", $"{6.5:0.0}");
        }
        finally
        {
            CultureInfo.CurrentCulture = vorher;
        }

        // Und danach wieder deutsch — sonst faerbt dieser Test die anderen ein.
        Assert.Equal("6,5", $"{6.5:0.0}");
    }

    [Fact]
    public void Program_setzt_die_Kultur_als_erste_Anweisung()
    {
        // Schwache Prüfung, und das steht hier ausdrücklich: sie liest Quelltext.
        // Kommentare werden deshalb ausgeschlossen — eine Erwähnung ist keine
        // Verwendung, und in Program.cs steht der Name auch im Kommentar darüber.
        var datei = Path.Combine(Projektwurzel(), "GrowDiary.Web", "Program.cs");
        var zeilen = File.ReadAllLines(datei)
            .Select(z => z.Trim())
            .Where(z => z.Length > 0 && !z.StartsWith("//", StringComparison.Ordinal))
            .ToList();

        Assert.True(zeilen.Count > 50, "Program.cs wurde nicht gelesen — der Test prüft nichts.");

        var ruf = zeilen.FindIndex(z => z.StartsWith("Deutsch.Setzen()", StringComparison.Ordinal));
        var bau = zeilen.FindIndex(z => z.Contains("WebApplication.CreateBuilder", StringComparison.Ordinal));

        Assert.True(ruf >= 0,
            "Program.cs ruft Deutsch.Setzen() nicht auf. Ohne den Aufruf formatiert die App mit der "
            + "Kultur der Umgebung — im Container also mit englischem Dezimalpunkt in deutschen Sätzen.");
        Assert.True(ruf < bau,
            $"Deutsch.Setzen() steht erst in Zeile {ruf}, nach CreateBuilder in {bau}. Threads, die "
            + "vorher entstehen, erben die alte Kultur.");
    }

    [Theory]
    // Ein Wert je Messgröße, die in einem Nutzertext vorkommt. Der Punkt ist
    // nicht die einzelne Zahl, sondern dass hier ueberhaupt formatiert wird.
    [InlineData(6.5, "0.0", "6,5")]
    [InlineData(5.85, "0.00", "5,85")]
    [InlineData(1234.5, "0.0", "1234,5")]
    [InlineData(0.9, "0.00", "0,90")]
    public void Messwerte_werden_mit_Komma_geschrieben(double wert, string muster, string erwartet)
        => Assert.Equal(erwartet, wert.ToString(muster));

    private static string Projektwurzel()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "GrowDiary.Web"))) return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Projektwurzel nicht gefunden.");
    }
}
