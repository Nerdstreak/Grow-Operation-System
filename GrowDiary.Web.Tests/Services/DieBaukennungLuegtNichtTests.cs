using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Die Baukennung meldet niemals Gleichheit, wo sie nichts weiß.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> <c>Bauzeit</c> stand auf der Liste der
/// Klassen, die kein Test je anfasst. Beim Hinsehen widersprachen sich
/// Kommentar und Code: „Dann lieber eine, die bei jedem Start anders ist, als
/// eine erfundene Konstante: so fällt eine Prüfung auf, statt still
/// durchzugehen" — und darunter <c>return 0</c>. Also genau die Konstante.</para>
///
/// <para><b>Warum das schwer wiegt.</b> Diese Kennung ist die einzige Prüfung,
/// die belegt, dass der <i>laufende</i> Stand der <i>gebaute</i> ist. Sie steht
/// in <c>CLAUDE.md</c>, weil einmal eine halbe Stunde gegen eine App aus einer
/// früheren Sitzung gemessen wurde. Liefern zwei Seiten bei einem Lesefehler
/// beide <c>00000000</c>, dann sind sie <b>gleich</b> — und die Prüfung meldet
/// Erfolg für zwei verschiedene Stände. Eine Prüfung, die im Fehlerfall grün
/// wird, ist schlechter als keine.</para>
/// </remarks>
public sealed class DieBaukennungLuegtNichtTests : IDisposable
{
    private readonly string _wurzel;

    public DieBaukennungLuegtNichtTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), "Baukennung_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_wurzel);
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    /// <summary>Eine echte Programmdatei liefert ihre Kennung.</summary>
    /// <remarks>
    /// Mengenwächter für alles darunter: liest die Klasse eine gewöhnliche
    /// .NET-Datei überhaupt, dann sagen die Fehlerfälle etwas aus.
    /// </remarks>
    [Fact]
    public void EineEchteAssembly_LiefertIhreKennung()
    {
        var eigene = typeof(Bauzeit).Assembly.Location;
        Assert.True(File.Exists(eigene), $"Die eigene Assembly liegt nicht unter „{eigene}\".");

        var kennung = Bauzeit.KennungFuer(eigene);

        Assert.True(kennung.Length == 8 && kennung.All(Uri.IsHexDigit),
            $"„{kennung}\" sind keine acht Hexziffern.");
        Assert.True(kennung != "00000000",
            "Die eigene Assembly liefert die Ausfall-Kennung — dann liest die Klasse gar nichts, "
            + "und jede Messung gegen die laufende App belegt nichts.");
    }

    /// <summary>Zweimal dieselbe Datei: dieselbe Kennung.</summary>
    /// <remarks>
    /// Die Gegenrichtung. Wäre die Kennung <i>immer</i> zufällig, verglichen
    /// sich auch zwei gleiche Stände als verschieden — und die Prüfung wäre
    /// unbrauchbar statt still.
    /// </remarks>
    [Fact]
    public void DieselbeDatei_LiefertZweimalDasGleiche()
    {
        var eigene = typeof(Bauzeit).Assembly.Location;

        Assert.Equal(Bauzeit.KennungFuer(eigene), Bauzeit.KennungFuer(eigene));
    }

    /// <summary>
    /// Zwei <b>unlesbare</b> Stände gelten nie als derselbe.
    /// </summary>
    /// <remarks>
    /// Der eigentliche Befund. Bis zum 02.09.2026 gab jeder Fehlerfall
    /// <c>0</c> zurück; zwei verschiedene, beide unlesbare Stände verglichen
    /// sich damit als <b>gleich</b>, und die Prüfung „ist der laufende Stand
    /// der gebaute" ging still durch.
    /// </remarks>
    [Theory]
    [InlineData("gibtesnicht.dll")]
    [InlineData("leer.dll")]
    [InlineData("kein-pe.dll")]
    [InlineData("abgeschnitten.dll")]
    public void EinUnlesbarerStand_GiltNieAlsDerselbe(string name)
    {
        var pfad = Datei(name);

        var einmal = Bauzeit.KennungFuer(pfad);
        var nochmal = Bauzeit.KennungFuer(pfad);

        Assert.True(einmal.Length == 8 && einmal.All(Uri.IsHexDigit),
            $"„{einmal}\" sind keine acht Hexziffern — die Kennung muss vergleichbar bleiben.");

        Assert.True(einmal != nochmal,
            $"„{name}\" ist nicht lesbar, und trotzdem kommt zweimal dieselbe Kennung „{einmal}\" "
            + "heraus. Zwei verschiedene Staende, beide unlesbar, gelten damit als DERSELBE — "
            + "und genau die Pruefung, die belegen soll, dass ich den gebauten Stand messe, "
            + "meldet dann Erfolg fuer einen fremden.");
    }

    /// <summary>Und eine unlesbare gleicht nie einer echten.</summary>
    [Fact]
    public void EinUnlesbarerStand_GleichtNieDerEchtenAssembly()
    {
        var echt = Bauzeit.KennungFuer(typeof(Bauzeit).Assembly.Location);
        var kaputt = Bauzeit.KennungFuer(Datei("kein-pe.dll"));

        Assert.True(echt != kaputt,
            "Eine unlesbare Datei liefert dieselbe Kennung wie die echte Assembly.");
    }

    // ------------------------------------------------------------------ Hilfe

    private string Datei(string name)
    {
        var pfad = Path.Combine(_wurzel, name);
        switch (name)
        {
            case "gibtesnicht.dll":
                break;
            case "leer.dll":
                File.WriteAllBytes(pfad, []);
                break;
            case "kein-pe.dll":
                // Gross genug fuer den Kopf, aber ohne PE-Signatur.
                File.WriteAllBytes(pfad, Enumerable.Repeat((byte)0x41, 512).ToArray());
                break;
            case "abgeschnitten.dll":
                // MZ-Kopf mit einem Zeiger, der ins Leere zeigt.
                var roh = new byte[0x40];
                roh[0] = (byte)'M';
                roh[1] = (byte)'Z';
                BitConverter.GetBytes(0x0000_1000).CopyTo(roh, 0x3C);
                File.WriteAllBytes(pfad, roh);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, "Unbekannter Fall.");
        }

        return pfad;
    }
}
