using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Wer im Dosiertakt wann drankommt — und wer aussetzt.
/// </summary>
/// <remarks>
/// <para><b>Warum das eine eigene Klasse ist (02.09.2026).</b> Beide Regeln
/// standen als Kommentar mitten in <see cref="DosingWorker"/>, in einer Methode,
/// die eine Datenbank, Home Assistant und vier weitere Dienste braucht. Damit
/// waren sie praktisch nicht prüfbar: der Takt hatte 4,8 % Zeilen- und
/// <b>0 % Zweigabdeckung</b>.</para>
///
/// <para>Die <i>Entscheidung</i> „darf ich dosieren" ist gut geprüft — sie steht
/// in <see cref="DosingGuard"/>, mit 82 Fällen in fünf Dateien. Ungeprüft war
/// die Reihenfolge drumherum. Ein Kommentar ist keine Zusage; hier sind es zwei
/// reine Funktionen, wie <see cref="AcTest.ZeitplanErlaubt"/> und
/// <see cref="Kalibrierpunkte.SteilheitProzent"/>.</para>
/// </remarks>
public static class Dosierreihenfolge
{
    /// <summary>Erst Dünger, dann pH.</summary>
    /// <remarks>
    /// <para><b>Warum.</b> Dünger verschiebt den pH von selbst. Wer erst Säure
    /// gibt und danach Dünger, korrigiert etwas, das sich gleich wieder ändert —
    /// und gibt beim nächsten Takt noch einmal Säure. In einem RDWC-Becken ohne
    /// Puffer ist das der Weg zu einem pH-Sturz.</para>
    ///
    /// <para><b>Stabil.</b> Innerhalb einer Gruppe bleibt die Reihenfolge, wie
    /// sie kam (<c>OrderBy</c> in LINQ sortiert stabil). Das ist kein Detail:
    /// weil je Zelt und Takt nur <i>eine</i> Dosis fällt
    /// (<see cref="DarfDosieren"/>), entscheidet die Reihenfolge, welche von
    /// zwei Düngerpumpen überhaupt je zum Zug kommt. Wechselte sie, wäre es
    /// Zufall.</para>
    /// </remarks>
    public static IReadOnlyList<DosingPump> Reihenfolge(IEnumerable<DosingPump> pumpen)
        => pumpen.OrderBy(pumpe => IstPh(pumpe) ? 1 : 0).ToList();

    /// <summary>Darf diese Pumpe in diesem Takt noch dosieren?</summary>
    /// <remarks>
    /// <para><b>Nur eine Dosis je Zelt und Takt.</b> Nach einer Dosis ist der
    /// Messwert der übrigen Pumpen desselben Zelts veraltet: die Lösung ist noch
    /// nicht durchmischt. Wer darauf hin ein zweites Mal dosiert, dosiert auf
    /// einen Zustand, den es nicht mehr gibt.</para>
    ///
    /// <para>Die Mischpause in <see cref="DosingGuard"/> lehnte das ohnehin ab —
    /// aber mit einem Kontext von <i>vor</i> der Dosis wüsste sie das nicht.
    /// Deshalb hält der Takt selbst an, statt sich darauf zu verlassen.</para>
    /// </remarks>
    /// <param name="pumpe">Die Pumpe, die als nächste an der Reihe wäre.</param>
    /// <param name="zelteMitDosis">Zelte, in denen in diesem Takt schon dosiert wurde.</param>
    public static bool DarfDosieren(DosingPump pumpe, IReadOnlySet<int> zelteMitDosis)
        => pumpe.AutomationEnabled && !zelteMitDosis.Contains(pumpe.TentId);

    /// <summary>Darf die zweite Hälfte eines Zweikomponenten-Düngers jetzt raus?</summary>
    /// <remarks>
    /// <para><b>Der Fall.</b> Ein A/B-Dünger wird getrennt gegeben: A steht schon
    /// im Becken, B kommt nach der Trennzeit. B ist keine neue Entscheidung,
    /// sondern die Vollendung einer schon getroffenen — die Mischpause wird
    /// deshalb nicht gefragt, sie hätte gerade erst A gesehen.</para>
    ///
    /// <para><b>Aber nicht in stehendes Wasser.</b> Ist die Umwälzung
    /// <i>bestätigt</i> aus, bleibt B liegen und der nächste Takt versucht es
    /// wieder. Konzentriertes B an einer Stelle im Becken steht sonst direkt an
    /// den Wurzeln.</para>
    ///
    /// <para><b>Unbekannt lässt durch.</b> Die meisten Anlagen haben keinen
    /// Umwälz-Sensor. Würde <c>null</c> wie <c>false</c> behandelt, bliebe B bei
    /// jedem solchen Aufbau für immer liegen — und im Becken stünde A ohne B,
    /// was schlimmer ist als B in ruhigem Wasser.</para>
    /// </remarks>
    /// <param name="simulationsbetrieb">
    /// Die Pumpe schaltet nichts Echtes; dann gibt es auch keine Umwälzung zu
    /// prüfen.
    /// </param>
    /// <param name="umwaelzungLaeuft">
    /// <c>true</c> läuft, <c>false</c> steht bestätigt, <c>null</c> unbekannt
    /// (kein Sensor oder Home Assistant nicht erreichbar).
    /// </param>
    public static bool ZweiteHaelfteJetzt(bool simulationsbetrieb, bool? umwaelzungLaeuft)
        => simulationsbetrieb || umwaelzungLaeuft != false;

    /// <summary>
    /// Zählt diese Pumpe als pH-Pumpe?
    /// </summary>
    /// <remarks>
    /// <c>Custom</c> ist weder Dünger noch pH und wird nur von Hand ausgelöst —
    /// er bleibt deshalb in der vorderen Gruppe. Am Ergebnis ändert das nichts;
    /// an der Bedeutung der Regel schon.
    /// </remarks>
    private static bool IstPh(DosingPump pumpe)
        => pumpe.Purpose is DosingPurpose.PhDown or DosingPurpose.PhUp;
}
