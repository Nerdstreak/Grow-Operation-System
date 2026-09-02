namespace GrowDiary.Web.Models;

/// <summary>
/// Die vier Schweregrade einer <see cref="RecommendationCard"/> — und nur diese.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> Die Schwere war eine freie
/// Zeichenkette, an neun Stellen roh hingeschrieben. Gelesen wurde sie von
/// <c>GrowAlertService.ResolveStateTone</c>, das auf <c>"danger"</c> und
/// <c>"warning"</c> vergleicht — und alles andere als gesund durchgehen
/// lässt.</para>
///
/// <para><b>Was daran gefährlich ist.</b> Diese Zeichenkette entscheidet
/// <c>GrowAlertService.ResolveStateTone</c>. Ein Tippfehler (<c>"Danger"</c>)
/// oder ein neuer Wert (<c>"urgent"</c>) fällt dort als „gesund" durch — der
/// teuerste Fehler, den eine Ampel machen kann.</para>
///
/// <para><b>Richtigstellung (02.09.2026, vom Prüfer gefunden).</b> Beim Anlegen
/// dieser Klasse stand hier, die Ampel sei „das Einzige, was von den
/// Empfehlungen beim Nutzer ankommt". <b>Das stimmt nicht.</b> Belegt wurde es
/// damals mit einem Blick ins JSON — Zahlen erheben ist keine Prüfung. Wer die
/// Seite ansieht, findet: <c>/api/live/tents/1</c> meldet
/// <c>stateTone: "attention"</c>, und auf <c>/zelte/1</c> steht „Stabil /
/// 100 %". Die angezeigte Ampel wird im Browser gerechnet
/// (<c>live-model.ts</c>, eigene Schwellen 55/82). <c>stateTone</c> und
/// <c>stateLabel</c> kommen in der Oberfläche nur in der Typdeklaration vor —
/// keine Seite liest sie. Siehe
/// <c>ZweiAmpelnFuerDasselbeZeltTests</c>.</para>
///
/// <para>Gehalten wird das von
/// <c>JedeKartenschwereErreichtDieAmpelTests</c>: kein Erzeuger schreibt einen
/// Wert, den die Ampel nicht kennt.</para>
/// </remarks>
public static class Kartenschwere
{
    /// <summary>Zur Kenntnis. Ampel bleibt, wie sie ist.</summary>
    public const string Hinweis = "info";

    /// <summary>Alles in Ordnung — ausdrücklich, nicht bloss stumm.</summary>
    public const string Gut = "success";

    /// <summary>Ansehen. Ampel wird „beobachten".</summary>
    public const string Warnung = "warning";

    /// <summary>Jetzt handeln. Ampel wird „kritisch".</summary>
    public const string Gefahr = "danger";

    /// <summary>Alle vier — die Grundmenge für jede Zählung.</summary>
    public static readonly IReadOnlyList<string> Alle = [Hinweis, Gut, Warnung, Gefahr];
}
