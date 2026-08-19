using System.Globalization;

namespace GrowDiary.Web.Services;

/// <summary>
/// Die Sprache dieser App ist Deutsch — auch bei Zahlen.
///
/// <para><b>Der Fehler.</b> Kein Ort setzte eine Kultur. Damit formatierte
/// jedes <c>$"{wert:0.0}"</c> mit der Kultur der Umgebung: auf einem deutschen
/// Windows-Rechner „6,5“, im Container ohne <c>LANG</c> „6.5“. Der Container ist
/// das, was der Nutzer laufen hat. In <b>80 Nutzertexten</b> im Backend stand
/// dort also der englische Dezimalpunkt in einem deutschen Satz —
/// „SOP-Schwelle 6.5 mg/l“, „Anmischen auf 5.8-6.2“.</para>
///
/// <para><b>Wie es aufgefallen ist.</b> Gar nicht — durch einen Test, der
/// zufaellig gegen „6,5“ prueft und deshalb auf CI rot wurde. Auf dem
/// Entwicklungsrechner lief er zwei Monate gruen. Dieselbe Familie wie die
/// UTC-Falle: was auf dem eigenen Rechner richtig aussieht, muss es dort, wo
/// es laeuft, nicht sein.</para>
///
/// <para><b>Warum hier und nicht an 80 Stellen.</b> Eine Wahrheit je Zahl. 80
/// Aufrufe mit angehaengtem <c>CultureInfo</c> waeren 80 Gelegenheiten, einen
/// zu vergessen — und der 81. Text waere wieder falsch. Die Kultur wird einmal
/// gesetzt, und <see cref="GrowDiary.Web.Tests"/> haelt das fest.</para>
///
/// <para><b>Warum das gefahrlos ist.</b> Geprueft wurde beides:
/// <list type="bullet">
/// <item>Jede Zahl, die AUS Text gelesen wird (<c>double.TryParse</c> in
/// <c>LightStateNormalizer</c>, <c>PhenoRepository</c>,
/// <c>GrowWorkflowApiController</c>), pinnt bereits ausdruecklich
/// <see cref="CultureInfo.InvariantCulture"/>.</item>
/// <item>Jede Zahl, die ALS Text gespeichert wird (nur eine: das Pheno-Gewicht
/// in <c>AppSettings</c>), ebenso.</item>
/// <item>JSON ist nach Norm kulturunabhaengig — <c>System.Text.Json</c>
/// schreibt und liest immer mit Punkt, unabhaengig von dieser Einstellung.</item>
/// </list>
/// Es kippt also nur, was der Mensch liest.</para>
/// </summary>
public static class Deutsch
{
    /// <summary>Die Kultur, in der diese App rechnet und schreibt.</summary>
    public const string Kennung = "de-DE";

    /// <summary>
    /// Wurde die Kultur wirklich wirksam?
    /// </summary>
    /// <remarks>
    /// <para><b>Warum das nachgesehen wird.</b> Laeuft .NET im Invariant-Modus
    /// (<c>DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1</c> oder ein Basis-Image
    /// ohne ICU, etwa Alpine), gibt <c>new CultureInfo("de-DE")</c> <b>keinen
    /// Fehler</b> — es liefert die invariante Kultur zurueck, die sich brav
    /// „de-DE" nennt und trotzdem mit Punkt schreibt. Die Einstellung taete
    /// dann nichts, und niemand wuesste es.</para>
    /// <para>Deshalb prueft sie ihre eigene Wirkung an einer Zahl, statt sich
    /// auf den Rueckgabewert zu verlassen. Das Basis-Image dieses Add-ons
    /// (<c>mcr.microsoft.com/dotnet/aspnet:8.0</c>, Debian) bringt ICU mit —
    /// das soll so bleiben, und wenn es jemand aendert, sagt es das.</para>
    /// </remarks>
    public static bool IstWirksam => 6.5.ToString("0.0") == "6,5";

    /// <summary>Einmal beim Start setzen — gilt fuer jeden Thread danach.</summary>
    /// <remarks>
    /// Muss die erste Anweisung in <c>Program.cs</c> sein: Threads, die vorher
    /// entstehen, erben die alte Einstellung. Hintergrunddienste
    /// (<c>AlertWatchWorker</c> und die anderen Worker) starten erst nach
    /// <c>app.Run()</c> und sind damit abgedeckt.
    /// </remarks>
    public static void Setzen()
    {
        var kultur = new CultureInfo(Kennung);
        CultureInfo.DefaultThreadCurrentCulture = kultur;
        CultureInfo.DefaultThreadCurrentUICulture = kultur;
        CultureInfo.CurrentCulture = kultur;
        CultureInfo.CurrentUICulture = kultur;
    }
}
