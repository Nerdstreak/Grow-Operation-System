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
/// <para><b>Was daran gefährlich ist.</b> Diese Zeichenkette entscheidet die
/// <b>Zustandsampel</b> auf der Live-Seite; sie ist das Einzige, was von den
/// Empfehlungen beim Nutzer ankommt. Ein Tippfehler (<c>"Danger"</c>) oder ein
/// neuer Wert (<c>"urgent"</c>) macht aus einem kritischen Befund still ein
/// „stabil". Grün, weil niemand hinsieht — der teuerste Fehler, den eine Ampel
/// machen kann.</para>
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
